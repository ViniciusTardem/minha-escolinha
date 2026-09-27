using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Plataforma;

/// <summary>
/// Compra de pacotes de créditos pelo Stripe da LDNA, no banco de testes: a compra nasce do gestor, o pagamento só vale
/// quando o Stripe confirma, os créditos entram uma vez só (evento repetido, fora de ordem ou em dobro não duplica) e
/// cada escolinha só enxerga as próprias compras.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class ComprasCreditosBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    [FatoComBanco]
    public async Task Gestor_compra_e_a_sessao_leva_valor_em_centavos_escolinha_e_compra()
    {
        var (escolinha, pacote) = await PrepararAsync();

        var resultado = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().IniciarCompraAsync(pacote));

        Assert.True(resultado.Sucesso, resultado.Mensagem);
        Assert.StartsWith("https://checkout.stripe.com/", Assert.IsType<string>(resultado.Dados));
        var pedido = Assert.Single(_app.GatewayStripe.Pedidos);
        Assert.Equal((5000L, escolinha), (pedido.ValorCentavos, pedido.EscolinhaId));
        Assert.Equal($"https://localhost:7186/Creditos/Compra/{pedido.CompraId}", pedido.UrlSucesso);

        var compra = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().ObterCompraAsync(pedido.CompraId));
        Assert.NotNull(compra);
        Assert.Equal(SituacaoCompraCreditos.AguardandoPagamento, compra.Situacao);
        Assert.Equal(("Básico", 500, 50m), (compra.NomePacote, compra.Creditos, compra.Valor));
    }

    [FatoComBanco]
    public async Task Pacote_fora_de_venda_nao_e_vendido()
    {
        var (escolinha, pacote) = await PrepararAsync(ativo: false);

        var resultado = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().IniciarCompraAsync(pacote));

        Assert.False(resultado.Sucesso);
        Assert.Empty(_app.GatewayStripe.Pedidos);
    }

    [FatoComBanco]
    public async Task Stripe_fora_do_ar_encerra_a_compra_como_falha_sem_cobrar()
    {
        var (escolinha, pacote) = await PrepararAsync();
        _app.GatewayStripe.FalhaAoCriar = new FalhaIntegracaoException(NaturezaFalha.Retentavel, "Stripe inacessível.");

        var resultado = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().IniciarCompraAsync(pacote));

        Assert.False(resultado.Sucesso);
        Assert.Contains("não respondeu", resultado.Mensagem);
        var tela = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().ObterTelaAsync());
        Assert.Equal(SituacaoCompraCreditos.Falhou, Assert.Single(tela.Compras).Situacao);
    }

    [FatoComBanco]
    public async Task Pagamento_confirmado_pelo_webhook_lanca_os_creditos_uma_vez_so()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var compraId = await IniciarAsync(escolinha, pacote);
        var sessao = _app.GatewayStripe.SessaoDa(compraId);
        _app.GatewayStripe.Pagar(sessao);

        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await ReceberAsync(Evento("evt_1", EventosStripe.SessaoConcluida, sessao, escolinha, compraId)));
        await ProcessarAgendadosAsync();

        Assert.Equal(500, await SaldoAsync(escolinha));
        var compra = await CompraAsync(escolinha, compraId);
        Assert.Equal((SituacaoCompraCreditos.Paga, "cartão"), (compra.Situacao, compra.RotuloMeio));

        // O mesmo evento de novo: gravado uma vez só, nada é agendado.
        Assert.Equal(ResultadoRecebimentoWebhook.Duplicado, await ReceberAsync(Evento("evt_1", EventosStripe.SessaoConcluida, sessao, escolinha, compraId)));
        Assert.Empty(_app.Agendador.Webhooks);

        // Outro evento sobre a mesma compra (fora de ordem): processa, mas não credita de novo.
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito,
            await ReceberAsync(Evento("evt_2", EventosStripe.PagamentoAssincronoConfirmado, sessao, escolinha, compraId)));
        await ProcessarAgendadosAsync();
        Assert.Equal(500, await SaldoAsync(escolinha));

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(2, await db.WebhooksRecebidos.CountAsync(w => w.Situacao == SituacaoWebhook.Processado));
        var movimento = await db.MovimentosCredito.IgnoreQueryFilters().SingleAsync(m => m.Escolinha_ID == escolinha);
        Assert.Equal((TipoMovimentoCredito.Concessao, 500), (movimento.Tipo, movimento.Quantidade));
        Assert.Contains("Básico", movimento.Motivo);
    }

    [FatoComBanco]
    public async Task Conferencias_ao_mesmo_tempo_creditam_uma_vez_so()
    {
        // Webhook e volta do pagamento chegam juntos em quase todo pagamento com cartão.
        var (escolinha, pacote) = await PrepararAsync();
        var compraId = await IniciarAsync(escolinha, pacote);
        _app.GatewayStripe.Pagar(_app.GatewayStripe.SessaoDa(compraId));

        var resultados = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => ConferirAsync(escolinha, compraId))));

        Assert.All(resultados, r => Assert.Equal(SituacaoCompraCreditos.Paga, r));
        Assert.Equal(500, await SaldoAsync(escolinha));
    }

    [FatoComBanco]
    public async Task Boleto_emitido_aguarda_e_credita_quando_o_banco_confirma()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var compraId = await IniciarAsync(escolinha, pacote);
        var sessao = _app.GatewayStripe.SessaoDa(compraId);

        _app.GatewayStripe.EmitirBoletoOuPix(sessao, "boleto");
        Assert.Equal(SituacaoCompraCreditos.AguardandoConfirmacao, await ConferirAsync(escolinha, compraId));
        Assert.Equal(0, await SaldoAsync(escolinha));

        _app.GatewayStripe.Pagar(sessao, "boleto");
        Assert.Equal(SituacaoCompraCreditos.Paga, await ConferirAsync(escolinha, compraId));
        Assert.Equal(500, await SaldoAsync(escolinha));
    }

    [FatoComBanco]
    public async Task Sessao_expirada_e_boleto_nao_pago_nao_lancam_creditos()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var expira = await IniciarAsync(escolinha, pacote);
        var naoPaga = await IniciarAsync(escolinha, pacote);

        _app.GatewayStripe.Expirar(_app.GatewayStripe.SessaoDa(expira));
        _app.GatewayStripe.EmitirBoletoOuPix(_app.GatewayStripe.SessaoDa(naoPaga), "pix");
        _app.GatewayStripe.Recusar(_app.GatewayStripe.SessaoDa(naoPaga));

        Assert.Equal(SituacaoCompraCreditos.Expirada, await ConferirAsync(escolinha, expira));
        Assert.Equal(SituacaoCompraCreditos.Falhou, await ConferirAsync(escolinha, naoPaga));
        Assert.Equal(0, await SaldoAsync(escolinha));
    }

    [FatoComBanco]
    public async Task Assinatura_invalida_nao_grava_nem_agenda()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var compraId = await IniciarAsync(escolinha, pacote);
        var corpo = Evento("evt_falso", EventosStripe.SessaoConcluida, _app.GatewayStripe.SessaoDa(compraId), escolinha, compraId);

        var resultado = await _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorPagamentosStripe>()
            .ReceberWebhookAsync(corpo, AssinaturaWebhookStripe.Cabecalho(corpo, _app.Tempo.GetUtcNow().ToUnixTimeSeconds(), "whsec_outro"), "10.0.0.1"));

        Assert.Equal(ResultadoRecebimentoWebhook.AssinaturaInvalida, resultado);
        Assert.Empty(_app.Agendador.Webhooks);
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.False(await db.WebhooksRecebidos.AnyAsync());
    }

    [FatoComBanco]
    public async Task Evento_de_outro_uso_da_conta_fica_registrado_e_ignorado()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var compraId = await IniciarAsync(escolinha, pacote);
        var sessao = _app.GatewayStripe.SessaoDa(compraId);
        _app.GatewayStripe.Pagar(sessao);

        Assert.Equal(ResultadoRecebimentoWebhook.Aceito,
            await ReceberAsync(Evento("evt_outro", EventosStripe.SessaoConcluida, sessao, escolinha, compraId, nosso: false)));
        await ProcessarAgendadosAsync();

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var webhook = await db.WebhooksRecebidos.SingleAsync();
        Assert.Equal(SituacaoWebhook.Ignorado, webhook.Situacao);
        Assert.Equal(0, _app.GatewayStripe.Consultas);
        Assert.Equal(SituacaoCompraCreditos.AguardandoPagamento, (await CompraAsync(escolinha, compraId)).Situacao);
    }

    [FatoComBanco]
    public async Task Cada_escolinha_so_enxerga_as_proprias_compras()
    {
        var (escolinhaA, pacote) = await PrepararAsync();
        var escolinhaB = await CriarEscolinhaAsync("Escolinha B", "gestor.b@escolinha.com");
        var compraDeA = await IniciarAsync(escolinhaA, pacote);

        var vistaPorB = await ComoGestorAsync(escolinhaB, sp => sp.GetRequiredService<ServicoCompraCreditos>().ObterCompraAsync(compraDeA));
        var telaDeB = await ComoGestorAsync(escolinhaB, sp => sp.GetRequiredService<ServicoCompraCreditos>().ObterTelaAsync());

        Assert.Null(vistaPorB);
        Assert.Empty(telaDeB.Compras);
        Assert.Single(telaDeB.Pacotes);
    }

    [FatoComBanco]
    public async Task Varredura_manda_conferir_so_as_compras_sem_desfecho()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var paga = await IniciarAsync(escolinha, pacote);
        var pendente = await IniciarAsync(escolinha, pacote);
        _app.GatewayStripe.Pagar(_app.GatewayStripe.SessaoDa(paga));
        await ConferirAsync(escolinha, paga);

        var total = await _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorPagamentosStripe>().VarrerPendentesAsync());

        Assert.Equal(1, total);
        Assert.Equal((escolinha, pendente), Assert.Single(_app.Agendador.Conferencias));
    }

    [FatoComBanco]
    public async Task Sessao_que_nao_existe_mais_no_Stripe_encerra_a_compra()
    {
        var (escolinha, pacote) = await PrepararAsync();
        var compraId = await IniciarAsync(escolinha, pacote);
        _app.GatewayStripe.Sessoes.Clear();

        await _app.SemContextoAsync(async sp =>
        {
            await sp.GetRequiredService<ProcessadorPagamentosStripe>().ConferirCompraPorJobAsync(escolinha, compraId);
            return true;
        });

        Assert.Equal(SituacaoCompraCreditos.Falhou, (await CompraAsync(escolinha, compraId)).Situacao);
    }

    [FatoComBanco]
    public async Task So_a_equipe_da_plataforma_mantem_os_pacotes()
    {
        var escolinha = await CriarEscolinhaAsync("Escolinha A", "gestor.a@escolinha.com");
        var novo = new PacoteRequisicao { Nome = "Básico", Creditos = 500, Preco = 50m };

        var peloGestor = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoPacotesCreditos>().SalvarAsync(novo));
        var semPreco = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPacotesCreditos>()
            .SalvarAsync(new PacoteRequisicao { Nome = "Grátis", Creditos = 10, Preco = 0m }));
        var pelaPlataforma = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPacotesCreditos>().SalvarAsync(novo));

        Assert.False(peloGestor.Sucesso);
        Assert.Equal((false, nameof(PacoteRequisicao.Preco)), (semPreco.Sucesso, semPreco.Campo));
        Assert.True(pelaPlataforma.Sucesso, pelaPlataforma.Mensagem);
    }

    // ── Apoio ──

    private async Task<(Guid Escolinha, Guid Pacote)> PrepararAsync(bool ativo = true)
    {
        var escolinha = await CriarEscolinhaAsync("Escolinha A", "gestor.a@escolinha.com");
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPacotesCreditos>()
            .SalvarAsync(new PacoteRequisicao { Nome = "Básico", Creditos = 500, Preco = 50m, Ativo = ativo }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return (escolinha, Assert.IsType<Guid>(resultado.Dados));
    }

    private async Task<Guid> CriarEscolinhaAsync(string nome, string email)
    {
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = nome, Cidade = "Londrina", Uf = "PR", NomeModalidade = "Futsal", GestorNome = $"Gestor {nome}", GestorEmail = email }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return Assert.IsType<EscolinhaCriada>(resultado.Dados).EscolinhaId;
    }

    private async Task<Guid> IniciarAsync(Guid escolinha, Guid pacote)
    {
        var resultado = await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().IniciarCompraAsync(pacote));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return _app.GatewayStripe.Pedidos[^1].CompraId;
    }

    private Task<T> ComoGestorAsync<T>(Guid escolinhaId, Func<IServiceProvider, Task<T>> acao) =>
        _app.NaEscolinhaAsync(escolinhaId, [PapelAtuacao.Gestor], acao);

    private Task<ResultadoRecebimentoWebhook> ReceberAsync(string corpo) =>
        _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorPagamentosStripe>().ReceberWebhookAsync(corpo,
            AssinaturaWebhookStripe.Cabecalho(corpo, _app.Tempo.GetUtcNow().ToUnixTimeSeconds(), _app.Stripe.SegredoWebhook), "127.0.0.1"));

    /// <summary>Roda os webhooks agendados como o Hangfire rodaria: um escopo novo, sem contexto de escolinha.</summary>
    private async Task ProcessarAgendadosAsync()
    {
        var agendados = _app.Agendador.Webhooks.ToList();
        _app.Agendador.Webhooks.Clear();
        foreach (var id in agendados)
            await _app.SemContextoAsync(async sp =>
            {
                await sp.GetRequiredService<ProcessadorPagamentosStripe>().ProcessarWebhookAsync(id);
                return true;
            });
    }

    private Task<SituacaoCompraCreditos?> ConferirAsync(Guid escolinha, Guid compraId) =>
        _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorPagamentosStripe>().ConferirCompraAsync(escolinha, compraId));

    private async Task<int> SaldoAsync(Guid escolinha) =>
        (await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCreditos>().ObterAsync(1)))!.Saldo;

    private async Task<CompraResposta> CompraAsync(Guid escolinha, Guid compraId) =>
        (await ComoGestorAsync(escolinha, sp => sp.GetRequiredService<ServicoCompraCreditos>().ObterCompraAsync(compraId)))!;

    private static string Evento(string id, string tipo, string sessaoId, Guid escolinha, Guid compra, bool nosso = true) =>
        JsonSerializer.Serialize(new
        {
            id,
            @object = "event",
            type = tipo,
            data = new
            {
                @object = new
                {
                    id = sessaoId,
                    @object = "checkout.session",
                    metadata = nosso
                        ? new Dictionary<string, string>
                        {
                            [MetadadosStripe.Origem] = MetadadosStripe.ValorOrigem,
                            [MetadadosStripe.Escolinha] = escolinha.ToString(),
                            [MetadadosStripe.Compra] = compra.ToString()
                        }
                        : new Dictionary<string, string> { ["pedido"] = "loja-123" }
                }
            }
        });
}
