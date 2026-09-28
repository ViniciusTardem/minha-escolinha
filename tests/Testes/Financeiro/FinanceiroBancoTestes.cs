using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Financeiro;

/// <summary>
/// Fatia S4 no banco de testes, com a Asaas falsa: a mensalidade emitida sozinha, a régua, a conciliação pelo webhook e
/// pela revalidação do envio, as divergências, o desligamento e o acesso só do gestor (critério de pronto do S4, CA-2,
/// CA-7, CA-8, CA-9 e o consumidor <c>financeiro.*</c> do CA-18). Os padrões: vencimento dia 10, emissão 10 dias antes,
/// lembrete 3 dias antes e no dia, cobranças 1, 5 e 10 dias depois, multa de 2 % e juros de 1 % ao mês.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class FinanceiroBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync()
    {
        _app.Usuario.Nome = "Gestão dos testes";
        return BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private sealed record Base(Guid Escolinha, Guid Turma, Guid Pedro, Guid Bruno, string Senha);

    // ── Montagem ──

    private Task<T> Gestor<T>(Guid escolinha, Func<IServiceProvider, Task<T>> acao) =>
        _app.NaEscolinhaAsync(escolinha, [PapelAtuacao.Gestor], acao);

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    /// <summary>Leva o relógio para a hora de Brasília indicada (UTC−3).</summary>
    private void Em(int mes, int dia, int hora = 12, int minuto = 0) =>
        _app.Tempo.SetUtcNow(new DateTimeOffset(2026, mes, dia, hora, minuto, 0, TimeSpan.Zero).AddHours(3));

    private async Task<Guid> AlunoAsync(Guid escolinha, Guid turma, string nome, string responsavel, string? cpf, string telefone)
    {
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = nome;
        r.DataNascimento = new DateOnly(2016, 4, 12);
        r.TurmaId = turma;
        r.Responsaveis[0].Nome = responsavel;
        r.Responsaveis[0].Cpf = cpf;
        r.Responsaveis[0].Telefone = telefone;
        return Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r)));
    }

    /// <summary>O relógio começa em 26/09/2026, 12:00 de Brasília (e o relógio falso não volta).</summary>
    private async Task<Base> MontarAsync(string nome = "Bola na Rede", string email = "gestao@bolanarede.com")
    {
        var criada = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = nome, NomeModalidade = "Futsal", GestorNome = "Gestão", GestorEmail = email }));
        Assert.True(criada.Sucesso, criada.Mensagem);
        var dados = Assert.IsType<EscolinhaCriada>(criada.Dados);
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            _app.Usuario.Id = (await db.Credenciais.SingleAsync(c => c.Login == email)).Pessoa_ID;

        var escolinha = dados.EscolinhaId;
        var local = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao { Nome = "Quadra Central" })));
        var professor = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var turma = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        turma.Nome = "Sub-9";
        turma.LocalId = local;
        turma.ProfessorId = professor;
        turma.Mensalidade = 150m;
        turma.Horarios = [new HorarioRequisicao { DiaSemana = DayOfWeek.Tuesday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }];
        var turmaId = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(turma)));

        var pedro = await AlunoAsync(escolinha, turmaId, "Pedro Lima", "Maria Lima", "529.982.247-25", "43988887777");
        var bruno = await AlunoAsync(escolinha, turmaId, "Bruno Alves", "Rita Alves", null, "43955554444");

        await VincularContaAsync(escolinha, dados.SenhaTemporaria!, "$aact_hmlg_000contaUm0001");
        return new Base(escolinha, turmaId, pedro, bruno, dados.SenhaTemporaria!);
    }

    private async Task VincularContaAsync(Guid escolinha, string senha, string chave)
    {
        var r = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoContaAsaas>().VincularAsync(
            new VincularContaAsaasRequisicao { Ambiente = AmbienteAsaas.Sandbox, ChaveApi = chave, SenhaAtual = senha }));
        Assert.True(r.Sucesso, r.Mensagem);
    }

    private Task<PlanoCobrancas> ProjetarAsync(Guid escolinha) =>
        Gestor(escolinha, sp => sp.GetRequiredService<RotinaCobrancas>().ProjetarAsync());

    private Task<ResumoRotinaCobrancas> RotinaAsync(Guid escolinha) =>
        Gestor(escolinha, sp => sp.GetRequiredService<RotinaCobrancas>().ExecutarAsync());

    private Task<string> EmitirAsync(Guid escolinha, Guid cobranca) =>
        Gestor(escolinha, sp => sp.GetRequiredService<EmissaoCobrancas>().EmitirAsync(cobranca));

    private Task<ResultadoOperacao> ConfirmarAsync(Guid escolinha, Guid cobranca, decimal valor = 150m) =>
        Gestor(escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().ConfirmarAsync(new ConfirmarMensalidadeRequisicao { Id = cobranca, Valor = valor }));

    private static async Task<List<Cobranca>> CobrancasAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Cobrancas.IgnoreQueryFilters().Where(c => c.Escolinha_ID == escolinha).OrderBy(c => c.Competencia).ToListAsync();
    }

    private static async Task<Cobranca> CobrancaAsync(Guid escolinha, Guid aluno, int mes) =>
        (await CobrancasAsync(escolinha)).Single(c => c.Aluno_ID == aluno && c.Competencia == new DateOnly(2026, mes, 1));

    private static async Task<List<Mensagem>> MensagensDaCobrancaAsync(Guid escolinha, Guid cobranca)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Mensagens.IgnoreQueryFilters()
            .Where(m => m.Escolinha_ID == escolinha && m.TipoOrigem == EmissorGatilhos.OrigemCobranca && m.Origem_ID == cobranca)
            .OrderBy(m => m.CriadaEm).ToListAsync();
    }

    /// <summary>Até a emissão de outubro do Pedro: confirmada em 26/09 e emitida pela rotina de 30/09 (06:30).</summary>
    private async Task<(Base Base, Guid Outubro)> EmitidaAsync()
    {
        var b = await MontarAsync();
        await ProjetarAsync(b.Escolinha);
        var outubro = (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Cobranca_ID;
        Assert.True((await ConfirmarAsync(b.Escolinha, outubro)).Sucesso);

        Em(9, 30, 6, 30);
        await RotinaAsync(b.Escolinha);
        Assert.Contains((b.Escolinha, outubro), _app.Financeiro.Emissoes);
        Assert.StartsWith("Emitida", await EmitirAsync(b.Escolinha, outubro));
        return (b, outubro);
    }

    private async Task<string> TokenAsync(Guid escolinha) =>
        (await Gestor(escolinha, sp => sp.GetRequiredService<ServicoAcessoAsaas>().TokenWebhookAsync()))!;

    /// <summary>O webhook como a Asaas manda (só o que o sistema lê).</summary>
    private static string Evento(string tipo, GatewayAsaasFalso.Cobranca c, string? id = null) => System.Text.Json.JsonSerializer.Serialize(new
    {
        id = id ?? $"evt_{Guid.NewGuid():N}",
        @event = tipo,
        payment = new
        {
            @object = "payment",
            id = c.Id,
            status = c.Status,
            value = c.Valor,
            originalValue = c.ValorOriginal,
            billingType = c.Meio ?? "UNDEFINED",
            dueDate = c.Pedido.Vencimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            clientPaymentDate = c.PagaEm?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            externalReference = c.Pedido.CobrancaId.ToString(),
            deleted = c.Excluida
        }
    });

    /// <summary>A Asaas avisa (webhook com o token da escolinha) e o job processa.</summary>
    private async Task<ResultadoRecebimentoWebhook> AvisoAsync(Guid escolinha, string corpo, string? token = null)
    {
        var antes = _app.Financeiro.Webhooks.Count;
        var resultado = await _app.SemContextoAsync(async sp => await sp.GetRequiredService<ProcessadorWebhooksAsaas>()
            .ReceberAsync(escolinha, corpo, token ?? await TokenAsync(escolinha), "127.0.0.1"));
        foreach (var webhook in _app.Financeiro.Webhooks.Skip(antes).ToList())
            await _app.SemContextoAsync(async sp =>
            {
                await sp.GetRequiredService<ProcessadorWebhooksAsaas>().ProcessarAsync(webhook);
                return true;
            });
        return resultado;
    }

    // ── Critério de pronto: emitida sozinha, lembrete antes do vencimento (BL-M06-01, 02, 04) ──

    [FatoComBanco]
    public async Task A_mensalidade_do_mes_e_emitida_sozinha_na_data_e_o_lembrete_sai_antes_do_vencimento()
    {
        var b = await MontarAsync();
        var plano = await ProjetarAsync(b.Escolinha);
        Assert.Equal(2, plano.Criar.Count); // outubro de Pedro e de Bruno; setembro já venceu
        var outubro = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.True(outubro.AguardandoConfirmacao);
        Assert.Contains(b.Escolinha, _app.Financeiro.Projecoes); // o cadastro do aluno pediu a projeção

        var confirmada = await ConfirmarAsync(b.Escolinha, outubro.Cobranca_ID);
        Assert.Contains("data de emissão", confirmada.Mensagem);
        Assert.Empty(_app.Financeiro.Emissoes); // 26/09: a emissão é 10 dias antes do dia 10

        Em(9, 30, 6, 30);
        var rotina = await RotinaAsync(b.Escolinha);
        Assert.Equal(0, rotina.Criadas); // o horizonte ainda acaba em 31/10
        Assert.Equal(1, rotina.ParaEmitir); // o outubro de Bruno ainda espera confirmação
        Assert.StartsWith("Emitida", await EmitirAsync(b.Escolinha, outubro.Cobranca_ID));

        outubro = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(SituacaoCobranca.Emitida, outubro.Situacao);
        var naAsaas = _app.GatewayAsaas.Da(outubro.Cobranca_ID);
        Assert.Equal(outubro.IdAsaas, naAsaas.Id);
        Assert.Equal(150m, naAsaas.Pedido.Valor);
        Assert.Equal(new DateOnly(2026, 10, 10), naAsaas.Pedido.Vencimento);
        Assert.Equal((2m, 1m), (naAsaas.Pedido.MultaPercentual, naAsaas.Pedido.JurosPercentualMes));
        var cliente = Assert.Single(_app.GatewayAsaas.Clientes);
        Assert.Equal(("Maria Lima", "52998224725"), (cliente.Pedido.Nome, cliente.Pedido.Cpf));

        Em(10, 7, 6, 30);
        var lembretes = await RotinaAsync(b.Escolinha);
        Assert.Equal((2, 1), (lembretes.Criadas, lembretes.Avisos)); // novembro de Pedro e de Bruno nasce no horizonte de outubro
        Assert.False((await CobrancaAsync(b.Escolinha, b.Pedro, 11)).PrimeiraDaMatricula);
        var lembrete = Assert.Single(await MensagensDaCobrancaAsync(b.Escolinha, outubro.Cobranca_ID));
        Assert.Equal((GatilhoMensagem.MensalidadeAVencer, "A3", "Maria Lima"), (lembrete.Gatilho, lembrete.Etapa, lembrete.DestinatarioNome));
        Assert.Contains("Bola na Rede informa: a mensalidade de Pedro (R$ 150,00) vence em 10/10/2026", lembrete.Conteudo);
        Assert.Contains(naAsaas.Pagamento().UrlFatura!, lembrete.Conteudo);
        Assert.Equal(0, (await RotinaAsync(b.Escolinha)).Avisos); // a mesma rotina de novo não repete

        Em(10, 10, 6, 30);
        await RotinaAsync(b.Escolinha);
        Assert.Equal(["A3", "D0"], (await MensagensDaCobrancaAsync(b.Escolinha, outubro.Cobranca_ID)).Select(m => m.Etapa));
    }

    // ── CA-7: pago na Asaas → paga sem ação humana, e a régua para na hora ──

    [FatoComBanco]
    public async Task Pagamento_confirmado_pela_asaas_concilia_sem_acao_humana_para_a_regua_e_agradece()
    {
        var (b, outubro) = await EmitidaAsync();
        Em(10, 7, 6, 30);
        await RotinaAsync(b.Escolinha); // o lembrete A3 fica na fila (janela de silêncio até 07:00)

        Em(10, 8, 9);
        var pago = _app.GatewayAsaas.Pagar(outubro, new DateOnly(2026, 10, 8));
        var corpo = Evento(EventoAsaas.PagamentoRecebido, pago, "evt_pago_1");
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await AvisoAsync(b.Escolinha, corpo));

        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(SituacaoCobranca.Paga, cobranca.Situacao);
        Assert.Equal((new DateOnly(2026, 10, 8), MeioPagamento.Pix), (cobranca.PagaEm!.Value, cobranca.Meio!.Value));
        var mensagens = await MensagensDaCobrancaAsync(b.Escolinha, outubro);
        Assert.Equal(SituacaoMensagem.Suprimida, mensagens.Single(m => m.Etapa == "A3").Situacao);
        Assert.Equal(ConciliacaoCobrancas.MotivoReguaParou, mensagens.Single(m => m.Etapa == "A3").Motivo);
        Assert.Single(mensagens, m => m.Gatilho == GatilhoMensagem.PagamentoConfirmado);

        // O mesmo evento de novo não tem efeito; outro evento do mesmo pagamento não agradece duas vezes.
        Assert.Equal(ResultadoRecebimentoWebhook.Duplicado, await AvisoAsync(b.Escolinha, corpo));
        await AvisoAsync(b.Escolinha, Evento(EventoAsaas.PagamentoConfirmado, pago));
        Assert.Single(await MensagensDaCobrancaAsync(b.Escolinha, outubro), m => m.Gatilho == GatilhoMensagem.PagamentoConfirmado);

        Em(10, 11, 6, 30);
        await RotinaAsync(b.Escolinha);
        Assert.Equal(SituacaoCobranca.Paga, (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Situacao);
        Assert.DoesNotContain(await MensagensDaCobrancaAsync(b.Escolinha, outubro), m => m.Gatilho == GatilhoMensagem.MensalidadeVencida);
    }

    // ── CA-8: quem pagou nunca recebe cobrança, nem com o aviso da Asaas atrasado ──

    [FatoComBanco]
    public async Task Quem_pagou_nunca_recebe_a_cobranca_mesmo_com_o_aviso_da_asaas_atrasado()
    {
        var (b, outubro) = await EmitidaAsync();
        _app.GatewayAsaas.Pagar(outubro, new DateOnly(2026, 10, 10)); // pagou no dia, e o webhook se perdeu

        Em(10, 11, 6, 30);
        await RotinaAsync(b.Escolinha);
        var cobrancaVencida = Assert.Single(await MensagensDaCobrancaAsync(b.Escolinha, outubro),
            m => m.Gatilho == GatilhoMensagem.MensalidadeVencida && m.Canal == CanalMensagem.WhatsApp);

        Em(10, 11, 9);
        await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(cobrancaVencida.Mensagem_ID));

        var mensagem = (await MensagensDaCobrancaAsync(b.Escolinha, outubro)).Single(m => m.Mensagem_ID == cobrancaVencida.Mensagem_ID);
        Assert.Equal(SituacaoMensagem.Suprimida, mensagem.Situacao);
        Assert.Contains("já registra o pagamento", mensagem.Motivo);
        Assert.DoesNotContain(_app.GatewayWhatsApp.Enviados, p => p.MensagemId == cobrancaVencida.Mensagem_ID);
        Assert.Contains((b.Escolinha, outubro), _app.Financeiro.Conferencias);

        await Gestor(b.Escolinha, sp => sp.GetRequiredService<VarreduraCobrancas>().ConferirAsync(outubro));
        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(SituacaoCobranca.Paga, cobranca.Situacao); // pagou no prazo: não é "paga em atraso"
    }

    // ── CA-9: parcial ou divergente fica sinalizado, sem conciliar ──

    [FatoComBanco]
    public async Task Pagamento_com_valor_diferente_fica_sinalizado_e_para_a_regua_ate_a_gestao_resolver()
    {
        var (b, outubro) = await EmitidaAsync();
        Em(10, 9);
        var parcial = _app.GatewayAsaas.Da(outubro);
        parcial.Status = StatusAsaas.Recebida;
        parcial.Meio = "BOLETO";
        parcial.PagaEm = new DateOnly(2026, 10, 9);
        parcial.Valor = 75m;
        await AvisoAsync(b.Escolinha, Evento(EventoAsaas.PagamentoRecebido, parcial));

        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(SituacaoCobranca.Emitida, cobranca.Situacao);
        Assert.Equal(SinalCobranca.ValorDivergente, cobranca.Sinal);
        Assert.Equal(75m, cobranca.ValorPago);

        Em(10, 11, 6, 30);
        await RotinaAsync(b.Escolinha);
        cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(SituacaoCobranca.Emitida, cobranca.Situacao); // não vence sozinha
        Assert.DoesNotContain(await MensagensDaCobrancaAsync(b.Escolinha, outubro), m => m.Gatilho == GatilhoMensagem.MensalidadeVencida);

        var fila = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().PainelAsync(new FiltroMensalidades { SoDivergencias = true }));
        Assert.Equal(outubro, Assert.Single(fila!.Linhas).Id);

        var resolvida = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().ResolverAsync(new ResolverDivergenciaRequisicao
        {
            Id = outubro, Resolucao = ResolucaoDivergencia.AceitarComoPaga, Explicacao = "Metade agora, metade na próxima: combinado com a família."
        }));
        Assert.True(resolvida.Sucesso, resolvida.Mensagem);
        cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.Paga, SinalCobranca.Nenhum, 75m), (cobranca.Situacao, cobranca.Sinal, cobranca.ValorPago!.Value));
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.True(await db.RegistrosAlteracao.IgnoreQueryFilters()
            .AnyAsync(r => r.Registro_ID == outubro && r.Campo == "Divergência" && r.AutorNome == "Gestão dos testes"));
    }

    [FatoComBanco]
    public async Task Estorno_de_mensalidade_paga_so_sinaliza_e_a_gestao_cancela_pela_fila_sem_mexer_na_asaas()
    {
        var (b, outubro) = await EmitidaAsync();
        Em(10, 8, 9);
        var pago = _app.GatewayAsaas.Pagar(outubro, new DateOnly(2026, 10, 8), meio: "CREDIT_CARD");
        await AvisoAsync(b.Escolinha, Evento(EventoAsaas.PagamentoRecebido, pago));

        Em(10, 9, 9);
        pago.Status = StatusAsaas.Estornada;
        await AvisoAsync(b.Escolinha, Evento(EventoAsaas.PagamentoEstornado, pago));
        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.Paga, SinalCobranca.Estorno), (cobranca.Situacao, cobranca.Sinal)); // nada muda sozinho

        Task<ResultadoOperacao> Resolver(ResolucaoDivergencia resolucao, string explicacao) =>
            Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().ResolverAsync(new ResolverDivergenciaRequisicao
            {
                Id = outubro, Resolucao = resolucao, Explicacao = explicacao
            }));

        Assert.False((await Resolver(ResolucaoDivergencia.AceitarComoPaga, "Não se aplica ao estorno.")).Sucesso);
        var cancelada = await Resolver(ResolucaoDivergencia.Cancelar, "A escolinha devolveu o valor: o aluno não começou em outubro.");
        Assert.True(cancelada.Sucesso, cancelada.Mensagem);

        cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.Cancelada, SinalCobranca.Nenhum), (cobranca.Situacao, cobranca.Sinal));
        Assert.Empty(_app.Financeiro.Exclusoes); // a Asaas não exclui pagamento recebido: a fatura estornada fica lá
    }

    [FatoComBanco]
    public async Task Pagamento_em_atraso_com_multa_e_juros_concilia_pelo_valor_original()
    {
        var (b, outubro) = await EmitidaAsync();
        Em(10, 15);
        var pago = _app.GatewayAsaas.Pagar(outubro, new DateOnly(2026, 10, 15), acrescimo: 3.25m);
        await AvisoAsync(b.Escolinha, Evento(EventoAsaas.PagamentoRecebido, pago));

        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.PagaEmAtraso, SinalCobranca.Nenhum, 153.25m), (cobranca.Situacao, cobranca.Sinal, cobranca.ValorPago!.Value));
    }

    // ── Emissão reprocessável e sem duplicar (spec §8.1) ──

    [FatoComBanco]
    public async Task A_emissao_repetida_reencontra_a_cobranca_na_asaas_em_vez_de_criar_outra()
    {
        var b = await MontarAsync();
        await ProjetarAsync(b.Escolinha);
        var outubro = (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Cobranca_ID;
        await ConfirmarAsync(b.Escolinha, outubro);

        Em(9, 30, 6, 30);
        _app.GatewayAsaas.PerderRespostaDaProximaCriacao = true;
        var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() => EmitirAsync(b.Escolinha, outubro));
        Assert.True(falha.Retentavel);
        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.Prevista, 1), (cobranca.Situacao, cobranca.TentativasEmissao));

        Assert.StartsWith("Emitida", await EmitirAsync(b.Escolinha, outubro));
        var naAsaas = Assert.Single(_app.GatewayAsaas.Cobrancas);
        Assert.Equal(naAsaas.Id, (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).IdAsaas);
        Assert.Single(_app.GatewayAsaas.Clientes);
        Assert.Equal("Nada a emitir.", await EmitirAsync(b.Escolinha, outubro));
    }

    [FatoComBanco]
    public async Task A_recusa_da_asaas_nao_se_repete_sozinha_e_volta_com_a_correcao_ou_com_a_conta_nova()
    {
        var b = await MontarAsync();
        await ProjetarAsync(b.Escolinha);
        var outubro = (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Cobranca_ID;
        await ConfirmarAsync(b.Escolinha, outubro);

        Em(9, 30, 6, 30);
        _app.GatewayAsaas.Falha = new FalhaIntegracaoException(NaturezaFalha.Definitiva, "O e-mail do cliente é inválido.");
        Assert.StartsWith("Recusada pela Asaas", await EmitirAsync(b.Escolinha, outubro));
        _app.Financeiro.Emissoes.Clear();
        await RotinaAsync(b.Escolinha);
        Assert.Empty(_app.Financeiro.Emissoes); // o erro definitivo não se repete sozinho (regra de ouro 8)

        var linha = (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().ParaAcaoAsync(outubro)))!.Linha;
        Assert.True(linha.RecusadaPelaAsaas);
        Assert.Equal("Recusada pela Asaas: O e-mail do cliente é inválido.", linha.Impedimento);

        // A gestão corrigiu o dado do pagador e manda de novo.
        var deNovo = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().TentarDeNovoAsync(outubro));
        Assert.True(deNovo.Sucesso, deNovo.Mensagem);
        Assert.Contains((b.Escolinha, outubro), _app.Financeiro.Emissoes);

        // Recusada de novo, agora pela chave revogada: a conta vinculada outra vez libera a emissão.
        _app.GatewayAsaas.Falha = new FalhaIntegracaoException(NaturezaFalha.Definitiva, "Chave de API inválida.");
        Assert.StartsWith("Recusada pela Asaas", await EmitirAsync(b.Escolinha, outubro));
        _app.Financeiro.Projecoes.Clear();
        await VincularContaAsync(b.Escolinha, b.Senha, "$aact_hmlg_000contaDois002");
        Assert.False((await CobrancaAsync(b.Escolinha, b.Pedro, 10)).ErroEmissaoDefinitivo);
        Assert.Contains(b.Escolinha, _app.Financeiro.Projecoes); // a emissão sai sem esperar a rotina da madrugada
        Assert.StartsWith("Emitida", await EmitirAsync(b.Escolinha, outubro));
    }

    [FatoComBanco]
    public async Task Sem_pagador_com_cpf_nao_emite_e_a_tela_diz_o_porque()
    {
        var b = await MontarAsync();
        await ProjetarAsync(b.Escolinha);
        var outubro = (await CobrancaAsync(b.Escolinha, b.Bruno, 10)).Cobranca_ID;
        await ConfirmarAsync(b.Escolinha, outubro);

        Em(9, 30, 6, 30);
        Assert.Equal(EmissaoCobrancas.MotivoSemPagador, await EmitirAsync(b.Escolinha, outubro));
        Assert.Equal(SituacaoCobranca.Prevista, (await CobrancaAsync(b.Escolinha, b.Bruno, 10)).Situacao);
        Assert.Empty(_app.GatewayAsaas.Cobrancas);

        var linha = (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>()
            .PainelAsync(new FiltroMensalidades { Competencia = new DateOnly(2026, 10, 1) })))!.Linhas.Single(l => l.Id == outubro);
        Assert.Equal("Pagador sem CPF válido", linha.Impedimento);
        Assert.Equal(1, (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().ResumoAsync()))!.Impedidas);
    }

    // ── CA-2: o desligamento encerra a recorrência; o débito em aberto fica sinalizado ──

    [FatoComBanco]
    public async Task Desligar_encerra_a_recorrencia_e_o_debito_em_aberto_fica_sinalizado()
    {
        var (b, outubro) = await EmitidaAsync();
        Em(10, 1, 6, 30);
        await RotinaAsync(b.Escolinha);
        var novembro = await CobrancaAsync(b.Escolinha, b.Pedro, 11);
        Assert.Equal(SituacaoCobranca.Prevista, novembro.Situacao);

        Em(10, 15);
        var desligado = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(new MudarSituacaoRequisicao
        {
            AlunoId = b.Pedro, Para = SituacaoAluno.Desistente, Data = new DateOnly(2026, 10, 15), Motivo = "Mudou de cidade"
        }));
        Assert.True(desligado.Sucesso, desligado.Mensagem);
        Assert.Contains("1 mensalidade(s) prevista(s) cancelada(s)", desligado.Mensagem);

        novembro = await CobrancaAsync(b.Escolinha, b.Pedro, 11);
        Assert.Equal(SituacaoCobranca.Cancelada, novembro.Situacao);
        Assert.Equal("Recorrência encerrada: desligamento (Mudou de cidade)", novembro.Motivo);
        Assert.Equal(SituacaoCobranca.Emitida, (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Situacao); // nunca cancelada sozinha

        await ProjetarAsync(b.Escolinha);
        Assert.Equal(2, (await CobrancasAsync(b.Escolinha)).Count(c => c.Aluno_ID == b.Pedro)); // nada volta
        var linha = (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().DoAlunoAsync(b.Pedro)))!.Single(l => l.Id == outubro);
        Assert.True(linha.DebitoRemanescente);
    }

    // ── Ações da gestão: recebido por fora, cancelar (P18, BL-M06-03) ──

    [FatoComBanco]
    public async Task Recebido_por_fora_para_a_regua_agradece_e_avisa_a_asaas_por_job()
    {
        var (b, outubro) = await EmitidaAsync();
        Em(10, 7, 6, 30);
        await RotinaAsync(b.Escolinha);

        Em(10, 9, 10);
        var r = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().ReceberPorForaAsync(new ReceberPorForaRequisicao
        {
            Id = outubro, Data = new DateOnly(2026, 10, 9), Meio = MeioPagamento.Dinheiro, Observacao = "Pago na secretaria, recibo 12"
        }));
        Assert.True(r.Sucesso, r.Mensagem);

        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.Paga, MeioPagamento.Dinheiro), (cobranca.Situacao, cobranca.Meio!.Value));
        var mensagens = await MensagensDaCobrancaAsync(b.Escolinha, outubro);
        Assert.Equal(SituacaoMensagem.Suprimida, mensagens.Single(m => m.Etapa == "A3").Situacao);
        Assert.Single(mensagens, m => m.Gatilho == GatilhoMensagem.PagamentoConfirmado);

        Assert.Contains((b.Escolinha, outubro), _app.Financeiro.RecebimentosEmDinheiro);
        await Gestor(b.Escolinha, sp => sp.GetRequiredService<OperacoesAsaas>().ReceberEmDinheiroAsync(outubro));
        Assert.Equal((cobranca.IdAsaas!, new DateOnly(2026, 10, 9), 150m), Assert.Single(_app.GatewayAsaas.RecebidasEmDinheiro));
    }

    [FatoComBanco]
    public async Task Cancelar_a_emitida_exclui_a_fatura_na_asaas_por_job_e_fica_no_historico()
    {
        var (b, outubro) = await EmitidaAsync();
        var r = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMensalidades>().EncerrarAsync(new EncerrarMensalidadeRequisicao
        {
            Id = outubro, Para = SituacaoCobranca.Cancelada, Motivo = "Cobrança em dobro"
        }));
        Assert.True(r.Sucesso, r.Mensagem);
        Assert.Equal(SituacaoCobranca.Cancelada, (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Situacao);

        Assert.Contains((b.Escolinha, outubro), _app.Financeiro.Exclusoes);
        await Gestor(b.Escolinha, sp => sp.GetRequiredService<OperacoesAsaas>().ExcluirAsync(outubro));
        Assert.Single(_app.GatewayAsaas.Excluidas);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var registro = await db.RegistrosAlteracao.IgnoreQueryFilters().SingleAsync(x => x.Registro_ID == outubro && x.Campo == "Situação");
        Assert.Equal(("Emitida", "Cobrança em dobro"), (registro.ValorAnterior, registro.Motivo));
    }

    // ── Webhook: só com o token da escolinha, e o evento repetido não tem segundo efeito ──

    [FatoComBanco]
    public async Task O_webhook_so_aceita_o_token_da_escolinha_e_cobranca_de_outra_escolinha_e_ignorada()
    {
        var (a, outubro) = await EmitidaAsync();
        var pago = _app.GatewayAsaas.Pagar(outubro, new DateOnly(2026, 10, 1));
        var corpo = Evento(EventoAsaas.PagamentoRecebido, pago);

        Assert.Equal(ResultadoRecebimentoWebhook.AssinaturaInvalida, await AvisoAsync(a.Escolinha, corpo, "token-errado"));
        Assert.Equal(ResultadoRecebimentoWebhook.AssinaturaInvalida, await AvisoAsync(Guid.NewGuid(), corpo, "qualquer"));
        Assert.Equal(SituacaoCobranca.Emitida, (await CobrancaAsync(a.Escolinha, a.Pedro, 10)).Situacao);

        // Escolinha B, com a conta e o token dela: o aviso da cobrança de A no endereço de B não acha nada.
        var b = await MontarAsync("Escolinha B", "gestao@escolinhab.com");
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await AvisoAsync(b.Escolinha, corpo));
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var webhook = await db.WebhooksRecebidos.Where(w => w.Escolinha_ID == b.Escolinha).SingleAsync();
            Assert.Equal(SituacaoWebhook.Ignorado, webhook.Situacao);
        }
        Assert.Equal(SituacaoCobranca.Emitida, (await CobrancaAsync(a.Escolinha, a.Pedro, 10)).Situacao);
    }

    // ── BL-M06-24: só o gestor vê o financeiro, em qualquer caminho ──

    [FatoComBanco]
    public async Task O_professor_nao_ve_nem_mexe_no_financeiro()
    {
        var (b, outubro) = await EmitidaAsync();
        var professor = await _app.NaEscolinhaAsync(b.Escolinha, [PapelAtuacao.Professor], async sp =>
        {
            var servico = sp.GetRequiredService<ServicoMensalidades>();
            return (await servico.PainelAsync(new FiltroMensalidades()), await servico.DoAlunoAsync(b.Pedro), await servico.ResumoAsync(),
                await servico.EncerrarAsync(new EncerrarMensalidadeRequisicao { Id = outubro, Para = SituacaoCobranca.Isenta, Motivo = "x" }));
        });
        Assert.Null(professor.Item1);
        Assert.Null(professor.Item2);
        Assert.Null(professor.Item3);
        Assert.False(professor.Item4.Sucesso);
        Assert.Equal(SituacaoCobranca.Emitida, (await CobrancaAsync(b.Escolinha, b.Pedro, 10)).Situacao);
    }

    // ── Troca de conta: o que ficou na conta anterior sai da régua ──

    [FatoComBanco]
    public async Task Trocar_a_conta_tira_da_regua_o_que_ficou_na_conta_anterior()
    {
        var (b, outubro) = await EmitidaAsync();
        await VincularContaAsync(b.Escolinha, b.Senha, "$aact_hmlg_000contaDois002");
        var cobranca = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(SinalCobranca.ContaAnterior, cobranca.Sinal);

        Em(10, 7, 6, 30);
        await RotinaAsync(b.Escolinha);
        Assert.Empty(await MensagensDaCobrancaAsync(b.Escolinha, outubro));
    }

    // ── CA-18: consumidor financeiro.* (vale na hora; multa e juros congelados na emissão) ──

    [FatoComBanco]
    public async Task Mudar_o_dia_do_vencimento_vale_na_projecao_seguinte_e_a_multa_fica_a_da_emissao()
    {
        var b = await MontarAsync();
        var alterar = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(
            new AlterarParametrosRequisicao { Valores = { [ChavesParametro.FinanceiroDiaVencimento] = "15" } }));
        Assert.True(alterar.Sucesso, alterar.Mensagem);

        await ProjetarAsync(b.Escolinha);
        var outubro = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal(new DateOnly(2026, 10, 15), outubro.Vencimento);
        await ConfirmarAsync(b.Escolinha, outubro.Cobranca_ID);

        Em(10, 5, 6, 30);
        await RotinaAsync(b.Escolinha);
        await EmitirAsync(b.Escolinha, outubro.Cobranca_ID);
        await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(
            new AlterarParametrosRequisicao { Valores = { [ChavesParametro.FinanceiroMulta] = "5" } }));

        outubro = await CobrancaAsync(b.Escolinha, b.Pedro, 10);
        Assert.Equal((SituacaoCobranca.Emitida, 2m), (outubro.Situacao, outubro.MultaPercentual!.Value));
        Assert.Equal(2m, _app.GatewayAsaas.Da(outubro.Cobranca_ID).Pedido.MultaPercentual);
    }
}
