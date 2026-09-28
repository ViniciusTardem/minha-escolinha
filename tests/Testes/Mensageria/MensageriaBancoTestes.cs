using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Portal;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Infrastructure.Integracao.WhatsApp;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Mensageria;

/// <summary>
/// Fatia S2 no banco de testes: o motor único (outbox, crédito, janela de silêncio, retenção, revalidação da origem),
/// o retorno do WhatsApp (entrega e resposta), os convites, a ativação pelo link, a verificação dos vínculos e o portal.
/// O relógio está em sábado, 26/09/2026, 12:00 de Brasília; os treinos da Sub-9 são terça e quinta às 18:00.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class MensageriaBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private const string CpfMaria = "529.982.247-25";
    private const string SenhaMaria = "maria2026a";
    private static readonly Guid PessoaGestor = Guid.NewGuid();
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync()
    {
        _app.Usuario.Id = PessoaGestor;
        _app.Usuario.Nome = "Gestor dos testes";
        return BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private sealed record Base(Guid Escolinha, Guid Turma, Guid Professor, Guid Pedro, Guid Ana, Guid Bruno, Guid Maria, Guid Rita);

    // ── Montagem ──

    private Task<T> Gestor<T>(Guid escolinha, Func<IServiceProvider, Task<T>> acao) =>
        _app.NaEscolinhaAsync(escolinha, [PapelAtuacao.Gestor], acao);

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    private async Task<Guid> EscolinhaAsync(string nome, string email)
    {
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = nome, NomeModalidade = "Futsal", GestorNome = $"Gestor {nome}", GestorEmail = email }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return Assert.IsType<EscolinhaCriada>(resultado.Dados).EscolinhaId;
    }

    private async Task CreditarAsync(Guid escolinha, int quantidade)
    {
        var r = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = escolinha, Quantidade = quantidade, Motivo = "Teste" }));
        Assert.True(r.Sucesso, r.Mensagem);
    }

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

    private async Task<Base> MontarAsync(string nome = "Bola na Rede", string email = "gestor@bolanarede.com")
    {
        var escolinha = await EscolinhaAsync(nome, email);
        var local = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao { Nome = "Quadra Central" })));
        var professor = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var requisicao = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        requisicao.Nome = "Sub-9";
        requisicao.LocalId = local;
        requisicao.ProfessorId = professor;
        requisicao.Mensalidade = 150m;
        requisicao.Horarios =
        [
            new HorarioRequisicao { DiaSemana = DayOfWeek.Tuesday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) },
            new HorarioRequisicao { DiaSemana = DayOfWeek.Thursday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }
        ];
        var turma = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(requisicao)));

        var pedro = await AlunoAsync(escolinha, turma, "Pedro Lima", "Maria Lima", CpfMaria, "43988887777");
        var ana = await AlunoAsync(escolinha, turma, "Ana Lima", "Maria Lima", CpfMaria, "43988887777");
        var bruno = await AlunoAsync(escolinha, turma, "Bruno Alves", "Rita Alves", null, "43955554444");

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var maria = (await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Escolinha_ID == escolinha && r.Nome == "Maria Lima")).Responsavel_ID;
        var rita = (await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Escolinha_ID == escolinha && r.Nome == "Rita Alves")).Responsavel_ID;
        return new Base(escolinha, turma, professor, pedro, ana, bruno, maria, rita);
    }

    private async Task<Guid> SessaoAsync(Guid escolinha, Guid turma, DateOnly dia) =>
        (await Gestor(escolinha, sp => sp.GetRequiredService<ServicoAgenda>().ConsultarAsync(new FiltroAgenda { Id = turma, De = dia, Ate = dia })))
        .Itens.Single().SessaoId;

    private Task<ResultadoOperacao> CancelarAsync(Guid escolinha, Guid sessao, string motivo = "Quadra interditada") =>
        Gestor(escolinha, sp => sp.GetRequiredService<ServicoSessoes>().CancelarAsync(new CancelarSessaoRequisicao { SessaoId = sessao, Motivo = motivo }));

    private static async Task<List<Mensagem>> MensagensAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Mensagens.IgnoreQueryFilters().Include(m => m.Eventos).Include(m => m.Respostas).AsSplitQuery()
            .Where(m => m.Escolinha_ID == escolinha).OrderBy(m => m.CriadaEm).ThenBy(m => m.DestinatarioNome).ToListAsync();
    }

    private static async Task<int> SaldoAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return (await db.ContasCreditos.IgnoreQueryFilters().SingleAsync(c => c.Escolinha_ID == escolinha)).Saldo;
    }

    private Task<SituacaoMensagem?> EnviarAsync(Guid escolinha, Guid mensagem) =>
        _app.NaEscolinhaAsync(escolinha, [], sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(mensagem));

    /// <summary>O link do convite na mensagem que ainda está na fila (o teste faz o papel de quem recebeu no celular).</summary>
    private static string TokenDo(Mensagem mensagem) => Regex.Match(mensagem.Conteudo, "/Convite/(?<t>[A-Za-z0-9_-]+)").Groups["t"].Value;

    private async Task<string> ConvidarAsync(Guid escolinha, TipoConvite tipo, Guid ficha)
    {
        var convidou = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoConvites>().ConvidarAsync(tipo, ficha));
        var conviteId = Id(convidou);
        return TokenDo((await MensagensAsync(escolinha)).Single(m => m.Gatilho == GatilhoMensagem.ConviteAcesso && m.Origem_ID == conviteId));
    }

    private Task<ResultadoOperacao> AtivarAsync(string token, string cpf, string senha, string? email = null) =>
        _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>().AtivarAsync(token,
            new AtivacaoRequisicao { Cpf = cpf, Senha = senha, ConfirmacaoSenha = senha, Email = email }));

    private async Task<Guid> PessoaDaMariaAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return (await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Escolinha_ID == escolinha && r.Nome == "Maria Lima")).Pessoa_ID!.Value;
    }

    private Task<T> ComoMaria<T>(Guid escolinha, Guid pessoa, Func<IServiceProvider, Task<T>> acao)
    {
        _app.Usuario.Id = pessoa;
        _app.Usuario.Nome = "Maria Lima";
        return _app.NaEscolinhaAsync(escolinha, [PapelAtuacao.Responsavel], acao);
    }

    // ── Motor: gatilho, destinatários, crédito, janela, retenção ──

    [FatoComBanco]
    public async Task Cancelar_o_treino_avisa_cada_familia_uma_vez_com_os_filhos_dela_e_despacha_depois_da_gravacao()
    {
        var b = await MontarAsync();
        // Em pausa não treina: a família do Bruno não recebe o aviso.
        Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(
            new MudarSituacaoRequisicao { AlunoId = b.Bruno, Para = SituacaoAluno.Inativo, Data = new DateOnly(2026, 9, 26) }))).Sucesso);

        var cancelou = await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        Assert.True(cancelou.Sucesso, cancelou.Mensagem);
        Assert.Contains("1 aviso(s)", cancelou.Mensagem);

        var mensagem = Assert.Single(await MensagensAsync(b.Escolinha));
        Assert.Equal((GatilhoMensagem.TreinoCancelado, CanalMensagem.WhatsApp, TipoDestinatario.Responsavel, b.Maria),
            (mensagem.Gatilho, mensagem.Canal, mensagem.TipoDestinatario, mensagem.Destinatario_ID));
        Assert.Equal("+5543988887777", mensagem.Telefone);
        Assert.Contains("Este aviso é sobre Ana e Pedro.", mensagem.Conteudo);
        Assert.Contains("Quadra interditada", mensagem.Conteudo);
        Assert.True(mensagem.Essencial);
        Assert.False(mensagem.Urgente); // o treino é daqui a mais de 24 horas
        Assert.Contains(_app.Mensageria.Envios, e => e.MensagemId == mensagem.Mensagem_ID && e.Quando is null);
    }

    [FatoComBanco]
    public async Task O_envio_debita_um_credito_e_guarda_o_id_do_whatsapp_no_indice_global()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 5);
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        var maria = (await MensagensAsync(b.Escolinha)).Single(m => m.Destinatario_ID == b.Maria);

        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, maria.Mensagem_ID));

        var pedido = Assert.Single(_app.GatewayWhatsApp.Enviados, p => p.MensagemId == maria.Mensagem_ID);
        Assert.Equal(("5543988887777", "treino_cancelado_v1"), (pedido.Telefone, pedido.Template));
        Assert.Equal("Maria", pedido.Parametros[0]);
        Assert.Equal("Ana e Pedro", pedido.Parametros[5]);
        Assert.Equal(4, await SaldoAsync(b.Escolinha)); // só a da Maria saiu; a da Rita continua na fila, sem gastar crédito
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var enviada = await db.Mensagens.IgnoreQueryFilters().SingleAsync(m => m.Mensagem_ID == maria.Mensagem_ID);
        Assert.Equal(1, enviada.CreditosConsumidos);
        Assert.Equal(_app.GatewayWhatsApp.IdDe(maria.Mensagem_ID), enviada.IdExterno);
        var rastreio = await db.RastreiosWhatsApp.SingleAsync(r => r.Mensagem_ID == maria.Mensagem_ID);
        Assert.Equal(("554388887777", b.Escolinha), (rastreio.ChaveTelefone, rastreio.Escolinha_ID));
        Assert.Contains(await db.MovimentosCredito.IgnoreQueryFilters().Where(m => m.Escolinha_ID == b.Escolinha).ToListAsync(),
            m => m is { Tipo: TipoMovimentoCredito.Consumo, Quantidade: -1 });
    }

    [FatoComBanco]
    public async Task Sem_credito_a_essencial_fica_retida_a_comum_e_suprimida_e_a_retida_sai_quando_o_saldo_volta()
    {
        var b = await MontarAsync();
        Assert.Equal(0, await SaldoAsync(b.Escolinha));
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        await ConvidarAsync(b.Escolinha, TipoConvite.Professor, b.Professor);
        var mensagens = await MensagensAsync(b.Escolinha);
        var aviso = mensagens.First(m => m.Gatilho == GatilhoMensagem.TreinoCancelado);
        var convite = mensagens.Single(m => m.Gatilho == GatilhoMensagem.ConviteAcesso);

        Assert.Equal(SituacaoMensagem.Retida, await EnviarAsync(b.Escolinha, aviso.Mensagem_ID));
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, convite.Mensagem_ID));
        Assert.Empty(_app.GatewayWhatsApp.Enviados);

        // Nada some em silêncio: a retida volta sozinha quando há crédito (varredura).
        await CreditarAsync(b.Escolinha, 10);
        var (liberadas, enfileiradas) = await _app.NaEscolinhaAsync(b.Escolinha, [], sp => sp.GetRequiredService<VarreduraMensagens>().VarrerAsync());
        Assert.True(liberadas >= 1);
        Assert.True(enfileiradas >= 1);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, aviso.Mensagem_ID));
        Assert.Contains((await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == aviso.Mensagem_ID).Eventos,
            e => e.Tipo == TipoEventoMensagem.Liberada);
    }

    [FatoComBanco]
    public async Task Na_janela_de_silencio_a_mensagem_espera_o_fim_e_a_urgente_das_proximas_24_horas_sai()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 10);
        _app.Tempo.Advance(TimeSpan.FromHours(58.5)); // segunda, 28/09, 22:30

        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29))); // amanhã 18:00: urgente
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 10, 1))); // quinta: pode esperar
        var mensagens = (await MensagensAsync(b.Escolinha)).Where(m => m.Destinatario_ID == b.Maria).ToList();
        var urgente = mensagens.Single(m => m.Urgente);
        var comum = mensagens.Single(m => !m.Urgente);

        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, urgente.Mensagem_ID));
        Assert.Equal(SituacaoMensagem.Pendente, await EnviarAsync(b.Escolinha, comum.Mensagem_ID));
        var agendada = (await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == comum.Mensagem_ID);
        Assert.Equal(new DateTime(2026, 9, 29, 7, 0, 0), agendada.AgendadaPara);
        Assert.Contains(_app.Mensageria.Envios, e => e.MensagemId == comum.Mensagem_ID && e.Quando == new DateTime(2026, 9, 29, 7, 0, 0));

        _app.Tempo.Advance(TimeSpan.FromHours(8.5)); // terça, 07:00
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, comum.Mensagem_ID));
    }

    [FatoComBanco]
    public async Task Falha_retentavel_tenta_de_novo_sem_cobrar_duas_vezes_e_a_definitiva_estorna_o_credito()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 3);
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        var mensagens = await MensagensAsync(b.Escolinha);
        var maria = mensagens.Single(m => m.Destinatario_ID == b.Maria);
        var rita = mensagens.Single(m => m.Destinatario_ID == b.Rita);

        _app.GatewayWhatsApp.Falha = new FalhaIntegracaoException(NaturezaFalha.Retentavel, "WhatsApp 500: fora do ar", 500);
        await Assert.ThrowsAsync<FalhaIntegracaoException>(() => EnviarAsync(b.Escolinha, maria.Mensagem_ID));
        Assert.Equal(2, await SaldoAsync(b.Escolinha));
        _app.GatewayWhatsApp.Falha = null;
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, maria.Mensagem_ID));
        Assert.Equal(2, await SaldoAsync(b.Escolinha));

        _app.GatewayWhatsApp.Falha = new FalhaIntegracaoException(NaturezaFalha.Definitiva, "WhatsApp 400 (erro 131026): número sem WhatsApp", 400);
        Assert.Equal(SituacaoMensagem.Falhou, await EnviarAsync(b.Escolinha, rita.Mensagem_ID));
        Assert.Equal(2, await SaldoAsync(b.Escolinha));
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Contains(await db.MovimentosCredito.IgnoreQueryFilters().Where(m => m.Escolinha_ID == b.Escolinha).ToListAsync(),
            m => m is { Tipo: TipoMovimentoCredito.Estorno, Quantidade: 1 });
    }

    [FatoComBanco]
    public async Task Fora_de_producao_a_api_so_manda_template_aprovado_e_para_numero_de_teste()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 10);
        _app.WhatsApp.Modo = OpcoesWhatsApp.ModoApi;
        _app.WhatsApp.NumerosTeste = ["+5543988887777"];
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 10, 1)));
        var mensagens = await MensagensAsync(b.Escolinha);

        // Sem o template aprovado na Meta, a API recusaria: nem tenta.
        var primeira = mensagens.First(m => m.Destinatario_ID == b.Maria);
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, primeira.Mensagem_ID));

        await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoTemplates>().ReconciliarAsync());
        var template = (await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoTemplates>().ListarAsync()))
            .Single(t => t.Gatilho == GatilhoMensagem.TreinoCancelado && t.Tom == TomMensagem.Cordial);
        var edicao = (await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoTemplates>().ParaEdicaoAsync(template.Id)))!;
        edicao.Situacao = SituacaoAprovacaoTemplate.Aprovado;
        Assert.True((await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoTemplates>().SalvarAsync(edicao))).Sucesso);

        var segundaMaria = mensagens.Last(m => m.Destinatario_ID == b.Maria);
        var rita = mensagens.First(m => m.Destinatario_ID == b.Rita);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, segundaMaria.Mensagem_ID));
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, rita.Mensagem_ID));
        Assert.Contains("números de teste", (await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == rita.Mensagem_ID).Motivo);
        Assert.Equal(9, await SaldoAsync(b.Escolinha));
    }

    // ── Retorno do WhatsApp ──

    [FatoComBanco]
    public async Task O_webhook_avanca_a_entrega_e_a_resposta_solta_acha_a_familia_pelo_celular_sem_o_nono_digito()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 5);
        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        var maria = (await MensagensAsync(b.Escolinha)).Single(m => m.Destinatario_ID == b.Maria);
        await EnviarAsync(b.Escolinha, maria.Mensagem_ID);
        var simulada = new ArmazemSimuladorWhatsApp.MensagemSimulada { Id = _app.GatewayWhatsApp.IdDe(maria.Mensagem_ID), Telefone = "5543988887777" };
        var agora = _app.Tempo.GetUtcNow();

        async Task<ResultadoRecebimentoWebhook> ReceberAsync(string corpo, string? assinatura = null)
        {
            var resultado = await _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorWebhooksWhatsApp>()
                .ReceberAsync(corpo, assinatura ?? AssinaturaWebhookWhatsApp.Montar(corpo, _app.WhatsApp.SegredoAppEfetivo), "127.0.0.1"));
            if (resultado == ResultadoRecebimentoWebhook.Aceito)
                await _app.SemContextoAsync(async sp =>
                {
                    await sp.GetRequiredService<ProcessadorWebhooksWhatsApp>().ProcessarAsync(_app.Mensageria.Webhooks[^1]);
                    return 0;
                });
            return resultado;
        }

        var lida = ArmazemSimuladorWhatsApp.MontarStatus(simulada, "read", agora.AddMinutes(2));
        var entregue = ArmazemSimuladorWhatsApp.MontarStatus(simulada, "delivered", agora.AddMinutes(1));
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await ReceberAsync(lida));
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await ReceberAsync(entregue)); // chegou atrasado: não desfaz a leitura
        Assert.Equal(ResultadoRecebimentoWebhook.Duplicado, await ReceberAsync(lida));
        Assert.Equal(ResultadoRecebimentoWebhook.AssinaturaInvalida, await ReceberAsync(lida + " ", "sha256=00"));

        var texto = ArmazemSimuladorWhatsApp.MontarResposta("5543988887777", "wamid.IN1", "Obrigada pelo aviso!", null, null, agora.AddMinutes(3));
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await ReceberAsync(texto));

        var atualizada = (await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == maria.Mensagem_ID);
        Assert.Equal(SituacaoMensagem.Lida, atualizada.Situacao);
        var resposta = Assert.Single(atualizada.Respostas);
        Assert.Equal("Obrigada pelo aviso!", resposta.Texto);
        Assert.Contains("não espera resposta", resposta.Observacao);

        // Retorno de uma mensagem que não é nossa: fica registrado e ignorado.
        var alheia = new ArmazemSimuladorWhatsApp.MensagemSimulada { Id = "wamid.DEOUTRO", Telefone = "5511999998888" };
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await ReceberAsync(ArmazemSimuladorWhatsApp.MontarStatus(alheia, "delivered", agora)));
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var idAlheia = _app.Mensageria.Webhooks[^1];
        var alheiaGravada = await db.WebhooksRecebidos.SingleAsync(w => w.WebhookRecebido_ID == idAlheia);
        Assert.Equal(SituacaoWebhook.Ignorado, alheiaGravada.Situacao);
        Assert.Contains("sem mensagem nossa", alheiaGravada.Observacao);
    }

    /// <summary>
    /// BL-M09-07: o botão de um gatilho que espera resposta volta ao módulo de origem (a convocação do S5 será o 1º
    /// receptor); o texto livre e o botão de outra mensagem ficam só no log.
    /// </summary>
    [FatoComBanco]
    public async Task O_botao_volta_ao_modulo_de_origem_e_o_texto_ou_o_botao_de_outra_mensagem_ficam_so_no_log()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 1);
        var receptor = new ReceptorDeTeste();
        // O que se testa é o caminho do botão: a mensagem é montada à mão, com uma origem sem validador (a convocação de
        // verdade, com o receptor dela, está em EventosBancoTestes).
        var mensagemId = await _app.NaEscolinhaAsync(b.Escolinha, [], async sp =>
        {
            var mensagem = Mensagem.Nova(CatalogoGatilhos.Obter(GatilhoMensagem.ConvocacaoCriada), CanalMensagem.WhatsApp, "Teste",
                Guid.NewGuid(), TipoDestinatario.Responsavel, b.Maria, "Maria Lima", "+5543988887777", null, b.Pedro, "convocacao_v1",
                "[]", "Convocação de Pedro", false, null, sp.GetRequiredService<IRelogioBrasil>().Agora());
            var repositorio = sp.GetRequiredService<IMensageriaRepositorio>();
            repositorio.AdicionarMensagem(mensagem);
            await repositorio.SalvarAsync();
            return mensagem.Mensagem_ID;
        });
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, mensagemId));

        Task<string> ResponderAsync(string texto, string? payload, string idExterno) =>
            _app.NaEscolinhaAsync(b.Escolinha, [], sp =>
                new ServicoRetornoMensagens(sp.GetRequiredService<IMensageriaRepositorio>(), [receptor], sp.GetRequiredService<IRelogioBrasil>())
                    .RegistrarRespostaAsync(mensagemId, CanalMensagem.WhatsApp, texto, payload, idExterno, sp.GetRequiredService<IRelogioBrasil>().Agora()));

        Assert.Equal("Resposta tratada.", await ResponderAsync("Vai", PayloadBotao.Montar(mensagemId, 0), "wamid.B1"));
        Assert.Equal("Resposta registrada.", await ResponderAsync("Não vai", PayloadBotao.Montar(Guid.NewGuid(), 1), "wamid.B2"));
        Assert.Equal("Resposta registrada.", await ResponderAsync("Talvez", null, "wamid.B3"));
        Assert.Equal("Resposta já registrada.", await ResponderAsync("Vai", PayloadBotao.Montar(mensagemId, 0), "wamid.B1"));

        var mensagemGravada = (await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == mensagemId);
        Assert.Equal(SituacaoMensagem.Lida, mensagemGravada.Situacao); // quem respondeu leu
        var respostas = mensagemGravada.Respostas.OrderBy(r => r.IdExterno).ToList();
        Assert.Equal(3, respostas.Count);
        Assert.True(respostas[0].Tratada);
        Assert.Equal("Confirmou: Vai", respostas[0].Observacao);
        Assert.All(respostas.Skip(1), r => Assert.False(r.Tratada));
        Assert.All(respostas.Skip(1), r => Assert.Contains("Texto livre", r.Observacao));
        Assert.Equal([PayloadBotao.Montar(mensagemId, 0)], receptor.Recebidos);
    }

    private sealed class ReceptorDeTeste : IReceptorResposta
    {
        public List<string?> Recebidos { get; } = [];
        public GatilhoMensagem Gatilho => GatilhoMensagem.ConvocacaoCriada;

        public Task<string?> ReceberAsync(Mensagem mensagem, RespostaMensagem resposta, CancellationToken ct = default)
        {
            Recebidos.Add(resposta.Payload);
            return Task.FromResult<string?>($"Confirmou: {resposta.Texto}");
        }
    }

    // ── Convites ──

    [FatoComBanco]
    public async Task Convite_em_lote_manda_um_por_pessoa_e_explica_quem_ficou_de_fora()
    {
        var b = await MontarAsync();
        var semCelular = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Juliana Sem Celular" })));

        var lote = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvites>().ConvidarLoteAsync(
            [(TipoConvite.Responsavel, b.Maria), (TipoConvite.Responsavel, b.Rita), (TipoConvite.Professor, b.Professor), (TipoConvite.Professor, semCelular)]));
        Assert.True(lote.Sucesso, lote.Mensagem);
        Assert.Contains("3 convite(s)", lote.Mensagem);
        Assert.Contains("Juliana Sem Celular não tem celular", lote.Mensagem);

        var convites = (await MensagensAsync(b.Escolinha)).Where(m => m.Gatilho == GatilhoMensagem.ConviteAcesso).ToList();
        Assert.Equal(3, convites.Count);
        Assert.All(convites, m => Assert.Equal(CanalMensagem.WhatsApp, m.Canal));
        Assert.Contains(convites, m => m.TipoDestinatario == TipoDestinatario.Professor);

        var candidatos = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvites>().CandidatosAsync());
        Assert.Equal(SituacaoAcesso.Convidado, candidatos.Single(c => c.FichaId == b.Maria).Situacao);
        Assert.Equal(SituacaoAcesso.SemAcesso, candidatos.Single(c => c.FichaId == semCelular).Situacao);
    }

    [FatoComBanco]
    public async Task A_mensagem_de_um_convite_substituido_nao_sai_e_o_link_enviado_fica_mascarado_no_banco()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 5);
        var primeiroToken = await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria);
        var segundoToken = await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria); // reenvio: link novo
        Assert.NotEqual(primeiroToken, segundoToken);
        var convites = (await MensagensAsync(b.Escolinha)).Where(m => m.Gatilho == GatilhoMensagem.ConviteAcesso).ToList();
        var primeira = convites.Single(m => TokenDo(m) == primeiroToken);
        var segunda = convites.Single(m => TokenDo(m) == segundoToken);

        // Regra de ouro 5: o reenvio suprime na hora a mensagem do convite anterior que ainda estava na fila.
        Assert.Equal(SituacaoMensagem.Suprimida, primeira.Situacao);
        Assert.Contains("substituído", primeira.Motivo);
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, primeira.Mensagem_ID));
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, segunda.Mensagem_ID));
        Assert.Contains(segundoToken, _app.GatewayWhatsApp.Enviados.Single().Parametros[2]);

        var guardada = (await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == segunda.Mensagem_ID);
        Assert.DoesNotContain(segundoToken, guardada.Conteudo);
        Assert.DoesNotContain(segundoToken, guardada.Variaveis);
        Assert.Contains("/Convite/••••••", guardada.Conteudo);

        var (estado, _) = await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>().EstadoAsync(primeiroToken));
        Assert.Equal(EstadoConvite.Cancelado, estado);
    }

    [FatoComBanco]
    public async Task Cancelar_o_convite_suprime_na_hora_a_mensagem_que_ainda_nao_saiu()
    {
        var b = await MontarAsync();
        var conviteId = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvites>().ConvidarAsync(TipoConvite.Responsavel, b.Maria)));

        Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvites>().CancelarAsync(conviteId, null))).Sucesso);

        var mensagem = (await MensagensAsync(b.Escolinha)).Single(m => m.Origem_ID == conviteId);
        Assert.Equal(SituacaoMensagem.Suprimida, mensagem.Situacao);
        Assert.Contains("cancelado", mensagem.Motivo);
        Assert.Contains(mensagem.Eventos, e => e.Tipo == TipoEventoMensagem.Suprimida);
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, mensagem.Mensagem_ID));
        Assert.Empty(_app.GatewayWhatsApp.Enviados);
    }

    // ── Ativação, verificação e portal ──

    [FatoComBanco]
    public async Task A_ativacao_confere_o_cpf_cria_o_acesso_entra_pelo_cpf_e_pede_a_confirmacao_dos_vinculos()
    {
        var b = await MontarAsync();
        var token = await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria);
        var ativacao = (IServiceProvider sp) => sp.GetRequiredService<ServicoAtivacaoConvite>();

        Assert.Equal(EstadoConvite.Valido, (await _app.SemContextoAsync(sp => ativacao(sp).EstadoAsync(token))).Estado);
        Assert.Equal(EstadoConvite.Invalido, (await _app.SemContextoAsync(sp => ativacao(sp).EstadoAsync("inventado"))).Estado);

        var errado = await _app.SemContextoAsync(sp => ativacao(sp).IdentificarAsync(token, new IdentificacaoRequisicao { Cpf = "111.444.777-35" }));
        Assert.False(errado.Sucesso);
        Assert.Contains("Restam 4", errado.Mensagem);

        var certo = await _app.SemContextoAsync(sp => ativacao(sp).IdentificarAsync(token, new IdentificacaoRequisicao { Cpf = CpfMaria }));
        var identificacao = Assert.IsType<IdentificacaoConvite>(certo.Dados);
        Assert.Equal(("Maria Lima", "Bola na Rede", false), (identificacao.Nome, identificacao.Escolinha, identificacao.ContaExistente));

        Assert.False((await AtivarAsync(token, CpfMaria, "curta")).Sucesso);
        var ativou = await AtivarAsync(token, CpfMaria, SenhaMaria);
        Assert.True(ativou.Sucesso, ativou.Mensagem);
        Assert.Equal(EstadoConvite.JaUsado, (await _app.SemContextoAsync(sp => ativacao(sp).EstadoAsync(token))).Estado);

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var pessoa = await db.Pessoas.Include(p => p.Credencial).SingleAsync(p => p.Cpf == "52998224725");
            Assert.Equal("52998224725", pessoa.Credencial!.Login);
            Assert.True(await db.VinculosAtuacao.IgnoreQueryFilters()
                .AnyAsync(v => v.Pessoa_ID == pessoa.Pessoa_ID && v.Escolinha_ID == b.Escolinha && v.Papel == PapelAtuacao.Responsavel));
            Assert.Equal(pessoa.Pessoa_ID, (await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Responsavel_ID == b.Maria)).Pessoa_ID);
            var vinculos = await db.VinculosResponsavel.IgnoreQueryFilters().Where(v => v.Responsavel_ID == b.Maria).ToListAsync();
            Assert.Equal(2, vinculos.Count);
            Assert.All(vinculos, v => Assert.Equal(VerificacaoVinculo.Pendente, v.Verificacao));
        }

        var login = await _app.SemContextoAsync(sp => sp.GetRequiredService<IServicoAutenticacao>()
            .AutenticarAsync(new LoginRequisicao { Login = CpfMaria, Senha = SenhaMaria }));
        Assert.True(login.Sucesso, login.Mensagem);
    }

    [FatoComBanco]
    public async Task Cinco_cpfs_errados_cancelam_o_convite()
    {
        var b = await MontarAsync();
        var token = await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria);
        for (var i = 0; i < ConviteAcesso.MaxTentativasIdentificacao; i++)
            await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>()
                .IdentificarAsync(token, new IdentificacaoRequisicao { Cpf = "111.444.777-35" }));

        Assert.Equal(EstadoConvite.Cancelado,
            (await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>().EstadoAsync(token))).Estado);
        Assert.False((await AtivarAsync(token, CpfMaria, SenhaMaria)).Sucesso);
    }

    [FatoComBanco]
    public async Task O_portal_so_mostra_o_aluno_depois_da_confirmacao_e_o_vinculo_novo_ja_nasce_confirmado()
    {
        var b = await MontarAsync();
        Assert.True((await AtivarAsync(await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria), CpfMaria, SenhaMaria)).Sucesso);
        var pessoa = await PessoaDaMariaAsync(b.Escolinha);

        var antes = await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<ServicoPortal>().InicioAsync());
        Assert.Empty(antes!.Filhos);
        Assert.Equal(2, antes.VinculosAConfirmar);
        Assert.False(await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<IAutorizacao>()
            .PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, b.Pedro))));

        _app.Usuario.Id = PessoaGestor;
        var fila = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().PendentesAsync());
        Assert.Equal(["Ana Lima", "Pedro Lima"], fila.Select(v => v.Aluno).Order());
        Assert.Equal("52998224725", fila[0].Cpf);
        var vinculoPedro = fila.Single(v => v.AlunoId == b.Pedro).VinculoId;
        var vinculoAna = fila.Single(v => v.AlunoId == b.Ana).VinculoId;
        Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().ConfirmarAsync(vinculoPedro))).Sucesso);
        Assert.False((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().RecusarAsync(vinculoAna, " "))).Sucesso);
        Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>()
            .RecusarAsync(vinculoAna, "A Ana é da outra Maria"))).Sucesso);

        var depois = await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<ServicoPortal>().InicioAsync());
        Assert.Equal(["Pedro Lima"], depois!.Filhos.Select(f => f.Nome));
        Assert.Equal(0, depois.VinculosAConfirmar);
        Assert.True(await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<IAutorizacao>()
            .PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, b.Pedro))));
        Assert.False(await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<IAutorizacao>()
            .PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Aluno, b.Ana))));

        // O gestor vincula de propósito quem já tem acesso: o vínculo nasce confirmado por ele.
        _app.Usuario.Id = PessoaGestor;
        _app.Usuario.Nome = "Gestor dos testes";
        var caio = await AlunoAsync(b.Escolinha, b.Turma, "Caio Lima", "Maria Lima", CpfMaria, "43988887777");
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(VerificacaoVinculo.Verificado,
            (await db.VinculosResponsavel.IgnoreQueryFilters().SingleAsync(v => v.Aluno_ID == caio)).Verificacao);
    }

    [FatoComBanco]
    public async Task Quem_ja_tem_conta_ativa_outra_escolinha_com_a_propria_senha()
    {
        var a = await MontarAsync();
        Assert.True((await AtivarAsync(await ConvidarAsync(a.Escolinha, TipoConvite.Responsavel, a.Maria), CpfMaria, SenhaMaria)).Sucesso);

        var b = await MontarAsync("Escolinha B", "gestor@b.com");
        var token = await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria);
        var identificacao = Assert.IsType<IdentificacaoConvite>((await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>()
            .IdentificarAsync(token, new IdentificacaoRequisicao { Cpf = CpfMaria }))).Dados);
        Assert.True(identificacao.ContaExistente);

        var errada = await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>()
            .AtivarAsync(token, new AtivacaoRequisicao { Cpf = CpfMaria, Senha = "outraSenha9" }));
        Assert.False(errada.Sucesso);
        var certa = await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>()
            .AtivarAsync(token, new AtivacaoRequisicao { Cpf = CpfMaria, Senha = SenhaMaria }));
        Assert.True(certa.Sucesso, certa.Mensagem);

        var login = await _app.SemContextoAsync(sp => sp.GetRequiredService<IServicoAutenticacao>()
            .AutenticarAsync(new LoginRequisicao { Login = "52998224725", Senha = SenhaMaria }));
        Assert.Equal(2, Assert.IsType<CredencialAutenticada>(login.Dados).Escolinhas.Count);
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(1, await db.Pessoas.CountAsync(p => p.Cpf == "52998224725"));
    }

    [FatoComBanco]
    public async Task O_aviso_chega_no_portal_de_quem_ativou_e_abrir_marca_como_lido()
    {
        var b = await MontarAsync();
        Assert.True((await AtivarAsync(await ConvidarAsync(b.Escolinha, TipoConvite.Responsavel, b.Maria), CpfMaria, SenhaMaria)).Sucesso);
        var pessoa = await PessoaDaMariaAsync(b.Escolinha);
        foreach (var v in await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().PendentesAsync()))
            Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().ConfirmarAsync(v.VinculoId))).Sucesso);

        await CancelarAsync(b.Escolinha, await SessaoAsync(b.Escolinha, b.Turma, new DateOnly(2026, 9, 29)));
        var mensagens = (await MensagensAsync(b.Escolinha)).Where(m => m.Gatilho == GatilhoMensagem.TreinoCancelado).ToList();
        // WhatsApp para as duas famílias (o piso) e o aviso no portal só para quem ativou (modo parcial).
        Assert.Equal(2, mensagens.Count(m => m.Canal == CanalMensagem.WhatsApp));
        var portal = Assert.Single(mensagens, m => m.Canal == CanalMensagem.Portal);
        Assert.Equal((b.Maria, pessoa), (portal.Destinatario_ID, portal.Pessoa_ID!.Value));

        Assert.Equal(1, await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<ServicoMensagens>().AvisosNaoLidosAsync()));
        var avisos = await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<ServicoMensagens>().AvisosDoPortalAsync());
        Assert.True(Assert.Single(avisos).NovoAgora);
        Assert.False(Assert.Single(await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<ServicoMensagens>().AvisosDoPortalAsync())).NovoAgora);
        Assert.Equal(0, await ComoMaria(b.Escolinha, pessoa, sp => sp.GetRequiredService<ServicoMensagens>().AvisosNaoLidosAsync()));
        Assert.Equal(SituacaoMensagem.Lida, (await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == portal.Mensagem_ID).Situacao);
    }

    [FatoComBanco]
    public async Task A_escolinha_B_nao_ve_as_mensagens_nem_os_convites_da_A()
    {
        var a = await MontarAsync();
        await ConvidarAsync(a.Escolinha, TipoConvite.Responsavel, a.Maria);
        await CancelarAsync(a.Escolinha, await SessaoAsync(a.Escolinha, a.Turma, new DateOnly(2026, 9, 29)));
        var escolinhaB = await EscolinhaAsync("Escolinha B", "gestor@b.com");

        Assert.Equal(3, (await Gestor(a.Escolinha, sp => sp.GetRequiredService<ServicoMensagens>().ListarAsync(new FiltroMensagens(), 1))).Total);
        Assert.Equal(0, (await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoMensagens>().ListarAsync(new FiltroMensagens(), 1))).Total);
        Assert.Empty(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoConvites>().CandidatosAsync()));
        var qualquer = (await MensagensAsync(a.Escolinha))[0].Mensagem_ID;
        Assert.Null(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoMensagens>().DetalheAsync(qualquer)));
    }
}
