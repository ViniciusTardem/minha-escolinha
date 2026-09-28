using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Portal;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Infrastructure.Integracao.WhatsApp;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Portal;

/// <summary>
/// Fatia S6 no banco de testes: o portal do responsável com dois filhos numa conta só (CA-15), o total do mês com o
/// detalhamento por filho (CA-16), o pagamento pela Asaas com o Pix copia e cola (BL-M10-06), a resposta à convocação pelo
/// portal com a mesma regra do WhatsApp (BL-M10-07, P03), as presenças, e nada do filho antes da confirmação do vínculo
/// (BL-M10-11). O relógio começa no sábado, 26/09/2026, 12:00 de Brasília; os jogos são no sábado seguinte, 03/10.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class PortalBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private const string CpfMaria = "529.982.247-25";
    private const string CpfJoao = "111.444.777-35";
    private readonly AmbienteAplicacao _app = new();
    private long _sequencia;

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    /// <summary>
    /// Pedro (Sub-11) e Ana (Sub-13) são filhos da Maria, que é a pagadora dos dois; o João também é responsável pelo Pedro,
    /// sem cuidar das mensalidades. Bruno (Sub-11) é da Rita. A escolinha tem a conta Asaas (Sandbox, falsa).
    /// </summary>
    private sealed record Base(Guid Escolinha, Guid PessoaGestor, Guid Local, Guid Sub11, Guid Sub13, Guid Pedro, Guid Ana, Guid Bruno,
        Guid Maria, Guid Joao);

    // ── Montagem ──

    private Task<T> Como<T>(Guid escolinha, Guid pessoa, string nome, PapelAtuacao papel, Func<IServiceProvider, Task<T>> acao)
    {
        _app.Usuario.Id = pessoa;
        _app.Usuario.Nome = nome;
        return _app.NaEscolinhaAsync(escolinha, [papel], acao);
    }

    private Task<T> Gestor<T>(Base b, Func<IServiceProvider, Task<T>> acao) => Como(b.Escolinha, b.PessoaGestor, "Gestão", PapelAtuacao.Gestor, acao);

    private Task<T> Familia<T>(Base b, Guid pessoa, string nome, Func<ServicoPortal, Task<T>> acao) =>
        Como(b.Escolinha, pessoa, nome, PapelAtuacao.Responsavel, sp => acao(sp.GetRequiredService<ServicoPortal>()));

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    /// <summary>Leva o relógio para a hora de Brasília indicada (UTC−3). O relógio falso não volta.</summary>
    private void Em(int mes, int dia, int hora = 12, int minuto = 0) =>
        _app.Tempo.SetUtcNow(new DateTimeOffset(2026, mes, dia, hora, minuto, 0, TimeSpan.Zero).AddHours(3));

    private async Task<Base> MontarAsync()
    {
        var criada = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = "Bola na Rede", NomeModalidade = "Futsal", GestorNome = "Gestão", GestorEmail = "gestao@bolanarede.com" }));
        Assert.True(criada.Sucesso, criada.Mensagem);
        var dados = Assert.IsType<EscolinhaCriada>(criada.Dados);
        Guid pessoaGestor;
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            pessoaGestor = (await db.Credenciais.SingleAsync(c => c.Login == "gestao@bolanarede.com")).Pessoa_ID;
        var b = new Base(dados.EscolinhaId, pessoaGestor, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);

        var local = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao
        {
            Nome = "Ginásio Moringão", Logradouro = "Rua Tietê", Numero = "1000", Bairro = "Centro", Cidade = "Londrina", Uf = "PR"
        })));
        var professor = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var sub11 = await TurmaAsync(b, "Sub-11", local, professor, DayOfWeek.Tuesday, 150m);
        var sub13 = await TurmaAsync(b, "Sub-13", local, professor, DayOfWeek.Wednesday, 140m);

        var pedro = await AlunoAsync(b, sub11, "Pedro Lima", "Maria Lima", CpfMaria, "43988887777", "João Lima", CpfJoao, "43977776666");
        var ana = await AlunoAsync(b, sub13, "Ana Lima", "Maria Lima", CpfMaria, "43988887777");
        var bruno = await AlunoAsync(b, sub11, "Bruno Alves", "Rita Alves", null, "43955554444");

        var vinculou = await Gestor(b, sp => sp.GetRequiredService<ServicoContaAsaas>().VincularAsync(new VincularContaAsaasRequisicao
        {
            Ambiente = AmbienteAsaas.Sandbox, ChaveApi = "$aact_hmlg_000contaUm0001", SenhaAtual = dados.SenhaTemporaria!
        }));
        Assert.True(vinculou.Sucesso, vinculou.Mensagem);

        await using var contexto = BancoDeTestes.NovoContextoSemEscolinha();
        var responsaveis = await contexto.Responsaveis.IgnoreQueryFilters().Where(r => r.Escolinha_ID == b.Escolinha)
            .ToDictionaryAsync(r => r.Nome, r => r.Responsavel_ID);
        return b with
        {
            Local = local, Sub11 = sub11, Sub13 = sub13, Pedro = pedro, Ana = ana, Bruno = bruno,
            Maria = responsaveis["Maria Lima"], Joao = responsaveis["João Lima"]
        };
    }

    private async Task<Guid> TurmaAsync(Base b, string nome, Guid local, Guid professor, DayOfWeek dia, decimal mensalidade)
    {
        var requisicao = await Gestor(b, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        requisicao.Nome = nome;
        requisicao.LocalId = local;
        requisicao.ProfessorId = professor;
        requisicao.Mensalidade = mensalidade;
        requisicao.Horarios = [new HorarioRequisicao { DiaSemana = dia, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }];
        return Id(await Gestor(b, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(requisicao)));
    }

    private async Task<Guid> AlunoAsync(Base b, Guid turma, string nome, string responsavel, string? cpf, string telefone,
        string? segundo = null, string? segundoCpf = null, string? segundoTelefone = null)
    {
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = nome;
        r.DataNascimento = new DateOnly(2015, 4, 12);
        r.TurmaId = turma;
        r.Responsaveis[0].Nome = responsavel;
        r.Responsaveis[0].Cpf = cpf;
        r.Responsaveis[0].Telefone = telefone;
        if (segundo is not null)
        {
            // O segundo recebe os avisos, mas não é o pagador nem recebe a cobrança.
            r.Responsaveis[1].Nome = segundo;
            r.Responsaveis[1].Cpf = segundoCpf;
            r.Responsaveis[1].Telefone = segundoTelefone;
        }
        return Id(await Gestor(b, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r)));
    }

    /// <summary>O convite pelo WhatsApp, a ativação pelo CPF e, se pedido, a confirmação dos vínculos pela gestão (S2).</summary>
    /// <returns>A pessoa com acesso ao portal.</returns>
    private async Task<Guid> AcessoAsync(Base b, Guid responsavel, string cpf, bool confirmar = true)
    {
        var conviteId = Id(await Gestor(b, sp => sp.GetRequiredService<ServicoConvites>().ConvidarAsync(TipoConvite.Responsavel, responsavel)));
        var convite = (await MensagensAsync(b)).Single(m => m.Gatilho == GatilhoMensagem.ConviteAcesso && m.Origem_ID == conviteId);
        var token = Regex.Match(convite.Conteudo, "/Convite/(?<t>[A-Za-z0-9_-]+)").Groups["t"].Value;
        var ativou = await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoAtivacaoConvite>().AtivarAsync(token,
            new AtivacaoRequisicao { Cpf = cpf, Senha = "familia2026a", ConfirmacaoSenha = "familia2026a" }));
        Assert.True(ativou.Sucesso, ativou.Mensagem);
        if (confirmar) await ConfirmarVinculosAsync(b, responsavel);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return (await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Responsavel_ID == responsavel)).Pessoa_ID!.Value;
    }

    private async Task ConfirmarVinculosAsync(Base b, Guid responsavel, Guid? soDoAluno = null)
    {
        var pendentes = await Gestor(b, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().PendentesAsync());
        foreach (var v in pendentes.Where(v => v.ResponsavelId == responsavel && (soDoAluno is null || v.AlunoId == soDoAluno)))
            Assert.True((await Gestor(b, sp => sp.GetRequiredService<ServicoVerificacaoVinculos>().ConfirmarAsync(v.VinculoId))).Sucesso);
    }

    private async Task<Guid> JogoAsync(Base b, Guid turma, int hora, string adversario) =>
        Id(await Gestor(b, sp => sp.GetRequiredService<ServicoEventos>().CriarAsync(new EventoRequisicao
        {
            Turmas = [turma], Data = new DateOnly(2026, 10, 3), Hora = new TimeOnly(hora, 0), DuracaoMinutos = 60, Adversario = adversario
        })));

    private async Task ConvocarAsync(Base b, Guid jogo, params Guid[] alunos)
    {
        Assert.True((await Gestor(b, sp => sp.GetRequiredService<ServicoConvocacoes>()
            .SalvarSelecaoAsync([jogo], alunos.Select(a => (jogo, a)).ToList()))).Sucesso);
        var disparou = await Gestor(b, sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync([jogo]));
        Assert.True(disparou.Sucesso, disparou.Mensagem);
    }

    /// <summary>As mensalidades de outubro do Pedro e da Ana: confirmadas e emitidas pela rotina de 30/09 (06:30), com o Pix.</summary>
    private async Task MensalidadesEmitidasAsync(Base b, bool buscarPix = true)
    {
        await Gestor(b, sp => sp.GetRequiredService<RotinaCobrancas>().ProjetarAsync());
        foreach (var aluno in new[] { b.Pedro, b.Ana })
        {
            var outubro = await CobrancaAsync(b, aluno);
            Assert.True((await Gestor(b, sp => sp.GetRequiredService<ServicoMensalidades>().ConfirmarAsync(
                new ConfirmarMensalidadeRequisicao { Id = outubro.Cobranca_ID, Valor = outubro.Valor }))).Sucesso);
        }

        Em(9, 30, 6, 30);
        await Gestor(b, sp => sp.GetRequiredService<RotinaCobrancas>().ExecutarAsync());
        foreach (var (_, cobranca) in _app.Financeiro.Emissoes.ToList())
            Assert.StartsWith("Emitida", await Gestor(b, sp => sp.GetRequiredService<EmissaoCobrancas>().EmitirAsync(cobranca)));
        if (!buscarPix) return;
        foreach (var (_, cobranca) in _app.Financeiro.BuscasDePix.ToList())
            Assert.Equal("Pix guardado.", await PixAsync(b, cobranca));
    }

    private Task<string> PixAsync(Base b, Guid cobranca) => Gestor(b, sp => sp.GetRequiredService<PixCobrancas>().AtualizarAsync(cobranca));

    private static async Task<Cobranca> CobrancaAsync(Base b, Guid aluno)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Cobrancas.IgnoreQueryFilters()
            .SingleAsync(c => c.Escolinha_ID == b.Escolinha && c.Aluno_ID == aluno && c.Competencia == new DateOnly(2026, 10, 1));
    }

    private static async Task<List<Mensagem>> MensagensAsync(Base b)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Mensagens.IgnoreQueryFilters().Where(m => m.Escolinha_ID == b.Escolinha).OrderBy(m => m.CriadaEm).ToListAsync();
    }

    private static async Task<Convocacao> ConvocacaoAsync(Base b, Guid aluno)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Convocacoes.IgnoreQueryFilters().SingleAsync(c => c.Escolinha_ID == b.Escolinha && c.Aluno_ID == aluno);
    }

    /// <summary>A Asaas avisa que a cobrança foi paga (webhook com o token da escolinha), e o job processa.</summary>
    private async Task PagarAsync(Base b, Guid cobrancaId)
    {
        var c = _app.GatewayAsaas.Pagar(cobrancaId, DateOnly.FromDateTime(_app.Tempo.GetLocalNow().DateTime));
        var corpo = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = $"evt_{Guid.NewGuid():N}",
            @event = "PAYMENT_RECEIVED",
            payment = new
            {
                @object = "payment", id = c.Id, status = c.Status, value = c.Valor, originalValue = c.ValorOriginal, billingType = c.Meio,
                dueDate = c.Pedido.Vencimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                clientPaymentDate = c.PagaEm?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                externalReference = c.Pedido.CobrancaId.ToString(), deleted = false
            }
        });
        var token = await Gestor(b, sp => sp.GetRequiredService<ServicoAcessoAsaas>().TokenWebhookAsync());
        var antes = _app.Financeiro.Webhooks.Count;
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorWebhooksAsaas>()
            .ReceberAsync(b.Escolinha, corpo, token, "127.0.0.1")));
        foreach (var webhook in _app.Financeiro.Webhooks.Skip(antes).ToList())
            await _app.SemContextoAsync(async sp =>
            {
                await sp.GetRequiredService<ProcessadorWebhooksAsaas>().ProcessarAsync(webhook);
                return 0;
            });
    }

    /// <summary>A família toca no botão da mensagem no WhatsApp (o webhook assinado chega e o job o processa).</summary>
    private async Task BotaoAsync(Base b, Mensagem mensagem, bool vai)
    {
        Assert.Equal(SituacaoMensagem.Enviada, await _app.NaEscolinhaAsync(b.Escolinha, [], sp =>
            sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(mensagem.Mensagem_ID)));
        var corpo = ArmazemSimuladorWhatsApp.MontarResposta(mensagem.Telefone!.TrimStart('+'), $"wamid.{Guid.NewGuid():N}", vai ? "Vai" : "Não vai",
            PayloadBotao.Montar(mensagem.Mensagem_ID, vai ? 0 : 1), _app.GatewayWhatsApp.IdDe(mensagem.Mensagem_ID), _app.Tempo.GetUtcNow());
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorWebhooksWhatsApp>()
            .ReceberAsync(corpo, AssinaturaWebhookWhatsApp.Montar(corpo, _app.WhatsApp.SegredoAppEfetivo), "127.0.0.1")));
        await _app.SemContextoAsync(async sp =>
        {
            await sp.GetRequiredService<ProcessadorWebhooksWhatsApp>().ProcessarAsync(_app.Mensageria.Webhooks[^1]);
            return 0;
        });
    }

    private async Task PresencaAsync(Base b, Guid aluno, int dia, SituacaoPresenca situacao)
    {
        var sessao = (await Gestor(b, sp => sp.GetRequiredService<ServicoAgenda>().ConsultarAsync(new FiltroAgenda
            { Id = b.Sub11, De = new DateOnly(2026, 9, dia), Ate = new DateOnly(2026, 9, dia) }))).Itens.Single().SessaoId;
        var r = await Gestor(b, sp => sp.GetRequiredService<ServicoChamada>().SincronizarAsync(new LoteMarcacoes
        {
            Marcacoes =
            [
                new MarcacaoRequisicao
                {
                    Id = Guid.NewGuid(), SessaoId = sessao, AlunoId = aluno, Situacao = situacao, Dispositivo = "aparelho-quadra",
                    Sequencia = ++_sequencia, MarcadaEm = _app.Tempo.GetUtcNow().UtcDateTime
                }
            ]
        }));
        Assert.Equal(ResultadoDaMarcacao.Aplicada, Assert.Single(r.Marcacoes).Resultado);
    }

    // ── Critério de pronto do S6 ──

    /// <summary>Critério de pronto do S6, CA-15 e CA-16: dois filhos numa conta só, a mensalidade paga e a convocação respondida.</summary>
    [FatoComBanco]
    public async Task O_responsavel_com_dois_filhos_ve_os_dois_numa_conta_paga_a_mensalidade_e_responde_a_convocacao()
    {
        var b = await MontarAsync();
        var maria = await AcessoAsync(b, b.Maria, CpfMaria);
        var jogoPedro = await JogoAsync(b, b.Sub11, 9, "Tigre FC");
        var jogoAna = await JogoAsync(b, b.Sub13, 14, "Leões da Vila");
        await ConvocarAsync(b, jogoPedro, b.Pedro, b.Bruno);
        await ConvocarAsync(b, jogoAna, b.Ana);
        Em(9, 29, 19);
        await PresencaAsync(b, b.Pedro, 29, SituacaoPresenca.Presente);
        await MensalidadesEmitidasAsync(b);
        Task<T> ComoMaria<T>(Func<ServicoPortal, Task<T>> acao) => Familia(b, maria, "Maria Lima", acao);

        // CA-15: uma conta só, os dois filhos, e o que espera a família.
        var inicio = (await ComoMaria(p => p.InicioAsync()))!;
        Assert.Equal(["Ana Lima", "Pedro Lima"], inicio.Filhos.Select(f => f.Nome));
        Assert.Equal((0, 2, 2, 290m), (inicio.VinculosAConfirmar, inicio.ConvocacoesAResponder, inicio.MensalidadesEmAberto, inicio.EmAberto));
        Assert.Equal((1, 0), (inicio.Frequencias[b.Pedro].Presencas, inicio.Frequencias[b.Ana].Marcadas));

        // BL-M10-02: a visão única de cada filho, sem trocar de tela (o jogo com a resposta e o que está em aberto).
        var resumoPedro = inicio.Resumos![b.Pedro];
        Assert.Equal(("Jogo contra Tigre FC", SituacaoConvocacao.Enviada, 150m),
            (resumoPedro.ProximoJogo, resumoPedro.Resposta!.Value, resumoPedro.EmAberto!.Value));
        Assert.Equal(("Jogo contra Leões da Vila", 140m), (inicio.Resumos[b.Ana].ProximoJogo, inicio.Resumos[b.Ana].EmAberto!.Value));

        // Os jogos: cada convocação diz de qual filho se trata; o Bruno, de outra família, não aparece.
        var jogos = (await ComoMaria(p => p.JogosAsync()))!;
        Assert.Equal([("Pedro Lima", "Jogo da Sub-11 contra Tigre FC"), ("Ana Lima", "Jogo da Sub-13 contra Leões da Vila")],
            jogos.AResponder.Select(c => (c.Aluno, c.Jogo)));
        Assert.All(jogos.AResponder, c => Assert.Equal(("Ginásio Moringão", true), (c.Local, c.PodeResponder)));
        Assert.StartsWith("https://www.google.com/maps/search/", jogos.AResponder[0].LinkMapa);

        // A Maria responde pelo portal que a Ana vai: o painel do jogo é avisado, e a mensagem que ainda estava na fila não sai.
        _app.TempoReal.Limpar();
        var convocacaoAna = jogos.AResponder.Single(c => c.AlunoId == b.Ana).ConvocacaoId;
        Assert.Equal("Ana: vai. A escolinha já vê a resposta.", (await ComoMaria(p => p.ResponderAsync(convocacaoAna, true))).Mensagem);
        var ana = await ConvocacaoAsync(b, b.Ana);
        Assert.Equal((SituacaoConvocacao.Confirmada, OrigemResposta.Portal, "Maria Lima", b.Maria),
            (ana.Situacao, ana.Origem!.Value, ana.RespondidoPorNome, ana.RespondidoPor_ID));
        Assert.Contains((b.Escolinha, jogoAna), _app.TempoReal.Avisos);
        var daAna = (await MensagensAsync(b)).Where(m => m.Origem_ID == ana.Convocacao_ID).ToList();
        Assert.Equal([CanalMensagem.WhatsApp, CanalMensagem.Portal], daAna.Select(m => m.Canal).Order());
        Assert.All(daAna, m => Assert.Equal(SituacaoMensagem.Suprimida, m.Situacao));
        jogos = (await ComoMaria(p => p.JogosAsync()))!;
        Assert.Equal([b.Pedro], jogos.AResponder.Select(c => c.AlunoId));
        Assert.Equal([(b.Ana, SituacaoConvocacao.Confirmada)], jogos.Respondidas.Select(c => (c.AlunoId, c.Situacao)));

        // CA-16: o total do mês (outubro: setembro não tem mensalidade) e o detalhamento por filho, com o Pix e a fatura.
        var pagamentos = (await ComoMaria(p => p.PagamentosAsync()))!;
        Assert.Equal(new DateOnly(2026, 10, 1), pagamentos.Mes);
        Assert.Equal([("Ana Lima", 140m, 0m, 140m), ("Pedro Lima", 150m, 0m, 150m)],
            pagamentos.PorFilho.Select(f => (f.Aluno, f.Total, f.Pago, f.APagar)));
        Assert.Equal((290m, 290m), (pagamentos.TotalDoMes, pagamentos.APagarNoMes));
        var pedroOutubro = await CobrancaAsync(b, b.Pedro);
        var emAbertoPedro = pagamentos.EmAberto.Single(m => m.AlunoId == b.Pedro);
        Assert.Equal((GatewayAsaasFalso.PixDe(pedroOutubro.IdAsaas!), pedroOutubro.UrlFatura), (emAbertoPedro.PixCopiaECola, emAbertoPedro.UrlFatura));

        // A Maria paga a do Pedro com o Pix: a Asaas avisa, e o portal mostra paga, sem Pix nem fatura.
        await PagarAsync(b, pedroOutubro.Cobranca_ID);
        pagamentos = (await ComoMaria(p => p.PagamentosAsync()))!;
        Assert.Equal([("Ana Lima", 140m, 0m, 140m), ("Pedro Lima", 150m, 150m, 0m)],
            pagamentos.PorFilho.Select(f => (f.Aluno, f.Total, f.Pago, f.APagar)));
        Assert.Equal([b.Ana], pagamentos.EmAberto.Select(m => m.AlunoId));
        var paga = Assert.Single(pagamentos.Pagas);
        Assert.Equal((b.Pedro, SituacaoCobranca.Paga, (string?)null, (string?)null), (paga.AlunoId, paga.Situacao, paga.PixCopiaECola, paga.UrlFatura));

        // As presenças do Pedro, só leitura.
        var presencas = (await ComoMaria(p => p.PresencasAsync(b.Pedro)))!;
        Assert.Equal([("Sub-11", SituacaoPresenca.Presente)], presencas.Presencas.Select(p => (p.Turma, p.Situacao)));

        // Nada do portal é só do portal (CA-17): as convocações e a cobrança também saíram pelo WhatsApp, aos responsáveis (CA-19).
        var mensagens = await MensagensAsync(b);
        Assert.Contains(mensagens, m => m.Gatilho == GatilhoMensagem.ConvocacaoCriada && m.Aluno_ID == b.Ana && m.Canal == CanalMensagem.WhatsApp);
        Assert.All(mensagens.Where(m => m.Aluno_ID is not null), m => Assert.Equal(TipoDestinatario.Responsavel, m.TipoDestinatario));
    }

    // ── Vínculo confirmado (BL-M10-11) ──

    [FatoComBanco]
    public async Task Nada_do_filho_aparece_no_portal_antes_de_a_escolinha_confirmar_o_vinculo()
    {
        var b = await MontarAsync();
        var maria = await AcessoAsync(b, b.Maria, CpfMaria, confirmar: false);
        var jogoPedro = await JogoAsync(b, b.Sub11, 9, "Tigre FC");
        var jogoAna = await JogoAsync(b, b.Sub13, 14, "Leões da Vila");
        await ConvocarAsync(b, jogoPedro, b.Pedro);
        await ConvocarAsync(b, jogoAna, b.Ana);
        var convocacaoAna = (await ConvocacaoAsync(b, b.Ana)).Convocacao_ID;
        Task<T> ComoMaria<T>(Func<ServicoPortal, Task<T>> acao) => Familia(b, maria, "Maria Lima", acao);

        var inicio = (await ComoMaria(p => p.InicioAsync()))!;
        Assert.Equal((0, 2, 0), (inicio.Filhos.Count, inicio.VinculosAConfirmar, inicio.ConvocacoesAResponder));
        var jogos = (await ComoMaria(p => p.JogosAsync()))!;
        Assert.Empty(jogos.AResponder.Concat(jogos.Respondidas).Concat(jogos.Encerradas));
        Assert.Empty((await ComoMaria(p => p.PagamentosAsync()))!.PorFilho);
        Assert.Equal("Convocação não encontrada.", (await ComoMaria(p => p.ResponderAsync(convocacaoAna, true))).Mensagem);
        Assert.Null(await ComoMaria(p => p.PresencasAsync(b.Ana)));

        // A gestão confirma só o vínculo com o Pedro: a Ana continua fora de tudo, até pelo endereço direto.
        await ConfirmarVinculosAsync(b, b.Maria, soDoAluno: b.Pedro);
        inicio = (await ComoMaria(p => p.InicioAsync()))!;
        Assert.Equal(["Pedro Lima"], inicio.Filhos.Select(f => f.Nome));
        Assert.Equal(1, inicio.VinculosAConfirmar);
        Assert.Equal([b.Pedro], (await ComoMaria(p => p.JogosAsync()))!.AResponder.Select(c => c.AlunoId));
        Assert.False((await ComoMaria(p => p.ResponderAsync(convocacaoAna, true))).Sucesso);
        Assert.Equal(SituacaoConvocacao.Enviada, (await ConvocacaoAsync(b, b.Ana)).Situacao);
        Assert.Null(await ComoMaria(p => p.PresencasAsync(b.Ana)));
        Assert.NotNull(await ComoMaria(p => p.PresencasAsync(b.Pedro)));

        // Quem não é responsável não usa o portal.
        Assert.Null(await Gestor(b, sp => sp.GetRequiredService<ServicoPortal>().InicioAsync()));
    }

    /// <summary>O segundo responsável, que não é o pagador nem recebe a cobrança, vê os jogos e não vê valores.</summary>
    [FatoComBanco]
    public async Task Quem_nao_cuida_das_mensalidades_ve_os_jogos_e_nao_os_valores()
    {
        var b = await MontarAsync();
        var joao = await AcessoAsync(b, b.Joao, CpfJoao);
        await ConvocarAsync(b, await JogoAsync(b, b.Sub11, 9, "Tigre FC"), b.Pedro);
        await MensalidadesEmitidasAsync(b);
        Task<T> ComoJoao<T>(Func<ServicoPortal, Task<T>> acao) => Familia(b, joao, "João Lima", acao);

        var inicio = (await ComoJoao(p => p.InicioAsync()))!;
        Assert.Equal(["Pedro Lima"], inicio.Filhos.Select(f => f.Nome));
        Assert.Equal((1, 0, 0m), (inicio.ConvocacoesAResponder, inicio.MensalidadesEmAberto, inicio.EmAberto));
        var pagamentos = (await ComoJoao(p => p.PagamentosAsync()))!;
        Assert.Empty(pagamentos.PorFilho);
        Assert.Empty(pagamentos.EmAberto);
        Assert.Equal(["Pedro Lima"], pagamentos.ComOutroResponsavel);

        // P50: a escolinha abre as mensalidades a todo responsável com vínculo confirmado.
        var abriu = await Gestor(b, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(new AlterarParametrosRequisicao
        {
            Valores = { [MinhaEscolinha.Domain.Configuracao.Parametros.ChavesParametro.FinanceiroPortalTodosResponsaveis] = "true" }
        }));
        Assert.True(abriu.Sucesso, abriu.Mensagem);
        pagamentos = (await ComoJoao(p => p.PagamentosAsync()))!;
        Assert.Empty(pagamentos.ComOutroResponsavel);
        Assert.NotEmpty(pagamentos.PorFilho);
    }

    // ── A resposta pelo portal (BL-M10-07) ──

    /// <summary>A mesma regra em qualquer canal: vale a primeira da família (P03), e quem respondeu muda a própria resposta (D87).</summary>
    [FatoComBanco]
    public async Task A_resposta_pelo_portal_segue_a_regra_da_resposta_pelo_whatsapp()
    {
        var b = await MontarAsync();
        await CreditarAsync(b, 10);
        var maria = await AcessoAsync(b, b.Maria, CpfMaria);
        var joao = await AcessoAsync(b, b.Joao, CpfJoao);
        await ConvocarAsync(b, await JogoAsync(b, b.Sub11, 9, "Tigre FC"), b.Pedro);
        var convocacao = (await ConvocacaoAsync(b, b.Pedro)).Convocacao_ID;

        // O João responde primeiro, pelo botão do WhatsApp: não vai.
        var paraJoao = (await MensagensAsync(b)).Single(m => m.Gatilho == GatilhoMensagem.ConvocacaoCriada && m.Destinatario_ID == b.Joao
            && m.Canal == CanalMensagem.WhatsApp);
        await BotaoAsync(b, paraJoao, vai: false);
        Assert.Equal(SituacaoConvocacao.Recusada, (await ConvocacaoAsync(b, b.Pedro)).Situacao);

        // A Maria responde depois, pelo portal, e diferente: vale a primeira, e a dela fica para a escolinha conferir.
        var outra = await Familia(b, maria, "Maria Lima", p => p.ResponderAsync(convocacao, true));
        Assert.Equal("Outro responsável já tinha respondido por Pedro. A sua resposta fica registrada para a escolinha conferir.", outra.Mensagem);
        var pedro = await ConvocacaoAsync(b, b.Pedro);
        Assert.Equal(SituacaoConvocacao.Recusada, pedro.Situacao);
        Assert.StartsWith("Vai · Maria Lima", pedro.OutraResposta);
        var noPortal = (await Familia(b, maria, "Maria Lima", p => p.JogosAsync()))!.Respondidas.Single();
        Assert.Equal(("João Lima", OrigemResposta.WhatsApp, false), (noPortal.RespondidoPor, noPortal.Origem!.Value, noPortal.PropriaResposta));
        Assert.True((await Familia(b, joao, "João Lima", p => p.JogosAsync()))!.Respondidas.Single().PropriaResposta);

        // O João muda de ideia pelo portal: a resposta dele vale, e a outra resposta some.
        Assert.Equal("Pedro: vai. A escolinha já vê a resposta.", (await Familia(b, joao, "João Lima", p => p.ResponderAsync(convocacao, true))).Mensagem);
        pedro = await ConvocacaoAsync(b, b.Pedro);
        Assert.Equal((SituacaoConvocacao.Confirmada, OrigemResposta.Portal, (string?)null), (pedro.Situacao, pedro.Origem!.Value, pedro.OutraResposta));
        Assert.Equal("Pedro: esta resposta já estava registrada.",
            (await Familia(b, joao, "João Lima", p => p.ResponderAsync(convocacao, true))).Mensagem);

        // A gestão registra por fora que o Pedro não vai: a família não muda mais a resposta, e a dela fica para conferir.
        Assert.True((await Gestor(b, sp => sp.GetRequiredService<ServicoConvocacoes>().RegistrarPorForaAsync(convocacao, false))).Sucesso);
        Assert.Equal("A escolinha já tinha registrado a resposta de Pedro. A sua fica registrada para a escolinha conferir.",
            (await Familia(b, joao, "João Lima", p => p.ResponderAsync(convocacao, true))).Mensagem);
        pedro = await ConvocacaoAsync(b, b.Pedro);
        Assert.Equal((SituacaoConvocacao.Recusada, OrigemResposta.RegistroManual), (pedro.Situacao, pedro.Origem!.Value));
        Assert.StartsWith("Vai · João Lima", pedro.OutraResposta);
        Assert.False((await Familia(b, joao, "João Lima", p => p.JogosAsync()))!.Respondidas.Single().PropriaResposta);

        // Depois do início do jogo, a resposta não muda mais pelo portal.
        Em(10, 3, 9, 5);
        Assert.Equal("Este jogo já começou, foi cancelado ou adiado: a resposta não muda mais por aqui.",
            (await Familia(b, maria, "Maria Lima", p => p.ResponderAsync(convocacao, false))).Mensagem);
    }

    // ── O Pix (BL-M10-06) ──

    [FatoComBanco]
    public async Task O_pix_e_buscado_depois_da_emissao_e_de_novo_na_rotina_quando_falta_ou_vence()
    {
        var b = await MontarAsync();
        await MensalidadesEmitidasAsync(b, buscarPix: false);
        var pedro = await CobrancaAsync(b, b.Pedro);
        var ana = await CobrancaAsync(b, b.Ana);
        // Cada emissão pede a busca do Pix num job à parte.
        Assert.Equal(new[] { pedro.Cobranca_ID, ana.Cobranca_ID }.Order(), _app.Financeiro.BuscasDePix.Select(p => p.CobrancaId).Order());

        // A conta sem chave Pix: nada é guardado, e o portal oferece a fatura.
        _app.GatewayAsaas.SemPix = true;
        Assert.Equal("A Asaas não tem Pix para esta cobrança.", await PixAsync(b, pedro.Cobranca_ID));
        Assert.Null((await CobrancaAsync(b, b.Pedro)).PixCopiaECola);

        // A rotina do dia seguinte pede de novo o que falta; com o Pix na Asaas, ele é guardado uma vez.
        _app.GatewayAsaas.SemPix = false;
        _app.GatewayAsaas.ValidadeDoPix = new DateTime(2026, 10, 2, 23, 59, 59);
        _app.Financeiro.BuscasDePix.Clear();
        Em(10, 1, 6, 30);
        await Gestor(b, sp => sp.GetRequiredService<RotinaCobrancas>().ExecutarAsync());
        Assert.Contains((b.Escolinha, pedro.Cobranca_ID), _app.Financeiro.BuscasDePix);
        Assert.Equal("Pix guardado.", await PixAsync(b, pedro.Cobranca_ID));
        Assert.Equal("O Pix já está guardado.", await PixAsync(b, pedro.Cobranca_ID));
        Assert.Equal(new DateTime(2026, 10, 2, 23, 59, 59), (await CobrancaAsync(b, b.Pedro)).PixExpiraEm);

        // Venceu: a rotina pede de novo.
        _app.Financeiro.BuscasDePix.Clear();
        Em(10, 3, 6, 30);
        await Gestor(b, sp => sp.GetRequiredService<RotinaCobrancas>().ExecutarAsync());
        Assert.Contains((b.Escolinha, pedro.Cobranca_ID), _app.Financeiro.BuscasDePix);

        // A mensalidade paga não busca mais nada.
        _app.GatewayAsaas.Pagar(ana.Cobranca_ID, new DateOnly(2026, 10, 3));
        await PagarAsync(b, ana.Cobranca_ID);
        Assert.Equal("Nada a buscar: a mensalidade não está em aberto na Asaas.", await PixAsync(b, ana.Cobranca_ID));
    }

    private async Task CreditarAsync(Base b, int quantidade)
    {
        var r = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = b.Escolinha, Quantidade = quantidade, Motivo = "Teste" }));
        Assert.True(r.Sucesso, r.Mensagem);
    }

    // ── Recuperação de senha (BL-M01-09) ──

    /// <summary>
    /// Quem entra só pelo CPF não tem e-mail para onde mandar o link: o pedido na tela de login não revela nada e não envia
    /// nada; a gestão gera o link na ficha (24 horas, uso único), a pessoa cria a senha nova e entra com ela.
    /// </summary>
    [FatoComBanco]
    public async Task A_gestao_gera_o_link_de_nova_senha_de_quem_entra_pelo_cpf_e_ele_vale_uma_vez()
    {
        var b = await MontarAsync();
        await AcessoAsync(b, b.Maria, CpfMaria);

        var pedido = await _app.SemContextoAsync(sp => sp.GetRequiredService<ServicoRecuperacaoSenha>()
            .PedirPorEmailAsync(new EsqueciSenhaRequisicao { Login = CpfMaria }));
        Assert.Equal(ServicoRecuperacaoSenha.RespostaPedido, pedido.Mensagem);
        Assert.Empty(_app.AgendadorEmail.Recuperacoes);

        var gerado = await Gestor(b, sp => sp.GetRequiredService<ServicoRecuperacaoSenha>().GerarLinkPelaGestaoAsync(TipoConvite.Responsavel, b.Maria));
        Assert.True(gerado.Sucesso, gerado.Mensagem);
        var link = Assert.IsType<LinkNovaSenha>(gerado.Dados);
        Assert.Equal(("Maria Lima", new DateTime(2026, 9, 27, 12, 0, 0)), (link.Nome, link.ExpiraEm));
        var token = link.Link[(link.Link.LastIndexOf('/') + 1)..];

        // Só o hash do segredo fica no banco; a geração fica no registro de alterações, com o autor.
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var guardada = await db.RecuperacoesSenha.SingleAsync();
            Assert.Equal((CanalRecuperacao.LinkDaGestao, "Gestão", b.Escolinha, (string?)null),
                (guardada.Canal, guardada.CriadaPorNome!, guardada.Escolinha_ID!.Value, guardada.TokenProtegido));
            Assert.NotEqual(token, guardada.TokenHash);
            Assert.True(await db.RegistrosAlteracao.IgnoreQueryFilters().AnyAsync(r => r.Registro_ID == b.Maria && r.Campo == "Link de nova senha"));
        }

        Task<T> Recuperacao<T>(Func<ServicoRecuperacaoSenha, Task<T>> acao) => _app.SemContextoAsync(sp => acao(sp.GetRequiredService<ServicoRecuperacaoSenha>()));
        Assert.Equal("Maria Lima", await Recuperacao(r => r.ValidarAsync(token)));
        var fraca = await Recuperacao(r => r.DefinirNovaSenhaAsync(token, new NovaSenhaRequisicao { NovaSenha = "123", ConfirmacaoSenha = "123" }));
        Assert.Equal((false, nameof(NovaSenhaRequisicao.NovaSenha)), (fraca.Sucesso, fraca.Campo));
        var definiu = await Recuperacao(r => r.DefinirNovaSenhaAsync(token, new NovaSenhaRequisicao { NovaSenha = "novaSenha2026", ConfirmacaoSenha = "novaSenha2026" }));
        Assert.True(definiu.Sucesso, definiu.Mensagem);

        var entrou = await _app.SemContextoAsync(sp => sp.GetRequiredService<IServicoAutenticacao>()
            .AutenticarAsync(new LoginRequisicao { Login = CpfMaria, Senha = "novaSenha2026" }));
        Assert.True(entrou.Sucesso, entrou.Mensagem);
        Assert.False(Assert.IsType<CredencialAutenticada>(entrou.Dados).TrocarSenha);

        // O link vale uma vez só.
        Assert.Null(await Recuperacao(r => r.ValidarAsync(token)));
        Assert.False((await Recuperacao(r => r.DefinirNovaSenhaAsync(token,
            new NovaSenhaRequisicao { NovaSenha = "outraSenha2026", ConfirmacaoSenha = "outraSenha2026" }))).Sucesso);

        // Quem ainda não ativou o acesso recebe o convite, não o link.
        var semAcesso = await Gestor(b, sp => sp.GetRequiredService<ServicoRecuperacaoSenha>().GerarLinkPelaGestaoAsync(TipoConvite.Responsavel, b.Joao));
        Assert.False(semAcesso.Sucesso);
    }

    /// <summary>
    /// Quem entra pelo e-mail pede o link na tela de login: o e-mail sai por job (com retentativa), com o link de 2 horas.
    /// Login que não existe recebe a mesma resposta, sem envio.
    /// </summary>
    [FatoComBanco]
    public async Task Quem_entra_pelo_email_recebe_o_link_por_email_e_ele_vence_em_duas_horas()
    {
        var b = await MontarAsync();
        Task<T> Recuperacao<T>(Func<ServicoRecuperacaoSenha, Task<T>> acao) => _app.SemContextoAsync(sp => acao(sp.GetRequiredService<ServicoRecuperacaoSenha>()));

        var ninguem = await Recuperacao(r => r.PedirPorEmailAsync(new EsqueciSenhaRequisicao { Login = "ninguem@bolanarede.com" }));
        Assert.Equal((true, ServicoRecuperacaoSenha.RespostaPedido), (ninguem.Sucesso, ninguem.Mensagem));
        Assert.Empty(_app.AgendadorEmail.Recuperacoes);

        var pedido = await Recuperacao(r => r.PedirPorEmailAsync(new EsqueciSenhaRequisicao { Login = " Gestao@BolaNaRede.com " }));
        Assert.Equal(ServicoRecuperacaoSenha.RespostaPedido, pedido.Mensagem);
        var id = Assert.Single(_app.AgendadorEmail.Recuperacoes);

        // O SMTP fora do ar: o job falha (o Hangfire tenta de novo) e nada muda; na segunda vez, o e-mail sai uma vez só.
        _app.EnvioEmail.FalharProximo = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Recuperacao(r => r.EnviarEmailAsync(id)));
        Assert.Equal("E-mail enviado.", await Recuperacao(r => r.EnviarEmailAsync(id)));
        Assert.Equal("O e-mail já saiu.", await Recuperacao(r => r.EnviarEmailAsync(id)));
        var email = Assert.Single(_app.EnvioEmail.Enviados);
        Assert.Equal("gestao@bolanarede.com", email.Para);
        var token = Regex.Match(email.Corpo, "/Conta/NovaSenha/(?<t>[A-Za-z0-9_-]+)").Groups["t"].Value;
        Assert.Equal("Gestão", await Recuperacao(r => r.ValidarAsync(token)));

        // O segredo cifrado sai do banco depois do envio; o link vence em 2 horas.
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            Assert.Null((await db.RecuperacoesSenha.SingleAsync()).TokenProtegido);
        _app.Tempo.Advance(TimeSpan.FromHours(2));
        Assert.Null(await Recuperacao(r => r.ValidarAsync(token)));
        Assert.False((await Recuperacao(r => r.DefinirNovaSenhaAsync(token,
            new NovaSenhaRequisicao { NovaSenha = "novaSenha2026", ConfirmacaoSenha = "novaSenha2026" }))).Sucesso);
    }
}
