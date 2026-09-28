using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Infrastructure.Integracao.WhatsApp;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Eventos;

/// <summary>
/// Fatia S5 no banco de testes: a montagem do fim de semana e o disparo num fluxo só (CA-10), a mensagem por convocado com
/// data, hora, local, mapa e a pergunta (CA-11), a resposta pelo botão do WhatsApp chegando ao painel em tempo real (CA-12),
/// os irmãos em jogos diferentes (CA-13), o cancelamento e o adiamento (CA-14), o desligamento (CA-3), a primeira resposta
/// que vale (P03), o lembrete e o "sem resposta" (P19), a alteração, os alertas de agenda, a janela de disparo, os sinais
/// da montagem e o escopo do professor. O relógio começa no sábado, 26/09/2026, 12:00 de Brasília; os jogos são no fim de
/// semana seguinte (03 e 04/10), no Ginásio Moringão.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class EventosBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private const string CpfMaria = "529.982.247-25";
    private const string Mapa = "https://www.google.com/maps/search/?api=1&query=Rua%20Tiet%C3%AA%2C%201000%2C%20Centro%2C%20Londrina%2FPR";
    private static readonly Guid PessoaGestor = Guid.NewGuid();
    private static readonly DateOnly Sabado = new(2026, 10, 3);
    private static readonly DateOnly Domingo = new(2026, 10, 4);
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    /// <summary>
    /// Carlos (com login de professor) dá aula na Sub-11 e na Sub-13. Pedro (Sub-11) e Ana (Sub-13) são irmãos: a Maria é
    /// responsável pelos dois, e o João também pelo Pedro. Bruno (Rita) e Carla (Lia) são da Sub-11.
    /// </summary>
    private sealed record Base(Guid Escolinha, Guid Local, Guid Carlos, Guid PessoaCarlos, Guid Sub11, Guid Sub13,
        Guid Pedro, Guid Ana, Guid Bruno, Guid Carla, Guid Maria, Guid Joao, Guid Rita, Guid Lia);

    // ── Montagem ──

    private Task<T> Como<T>(Guid escolinha, Guid pessoa, string nome, PapelAtuacao papel, Func<IServiceProvider, Task<T>> acao)
    {
        _app.Usuario.Id = pessoa;
        _app.Usuario.Nome = nome;
        return _app.NaEscolinhaAsync(escolinha, [papel], acao);
    }

    private Task<T> Gestor<T>(Guid escolinha, Func<IServiceProvider, Task<T>> acao) =>
        Como(escolinha, PessoaGestor, "Gestor dos testes", PapelAtuacao.Gestor, acao);

    private Task<T> Carlos<T>(Base b, Func<IServiceProvider, Task<T>> acao) =>
        Como(b.Escolinha, b.PessoaCarlos, "Carlos Treinador", PapelAtuacao.Professor, acao);

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    private async Task<Guid> EscolinhaAsync()
    {
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao
            {
                Nome = "Bola na Rede", NomeModalidade = "Futsal", GestorNome = "Gestor Bola na Rede", GestorEmail = "gestor@bolanarede.com"
            }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return Assert.IsType<EscolinhaCriada>(resultado.Dados).EscolinhaId;
    }

    private async Task CreditarAsync(Guid escolinha, int quantidade)
    {
        var r = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = escolinha, Quantidade = quantidade, Motivo = "Teste" }));
        Assert.True(r.Sucesso, r.Mensagem);
    }

    /// <summary>Professor com login: a ficha ligada à pessoa, como a ativação do convite deixa (S2).</summary>
    private async Task<(Guid Professor, Guid Pessoa)> ProfessorComLoginAsync(Guid escolinha, string nome, string telefone)
    {
        var professor = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = nome, Telefone = telefone })));
        var pessoa = Guid.NewGuid();
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        db.Pessoas.Add(new Pessoa { Pessoa_ID = pessoa, Nome = nome });
        db.VinculosAtuacao.Add(new VinculoAtuacao { Escolinha_ID = escolinha, Pessoa_ID = pessoa, Papel = PapelAtuacao.Professor });
        (await db.Professores.IgnoreQueryFilters().SingleAsync(p => p.Professor_ID == professor)).Pessoa_ID = pessoa;
        await db.SaveChangesAsync();
        return (professor, pessoa);
    }

    private async Task<Guid> TurmaAsync(Guid escolinha, string nome, Guid local, Guid professor, DayOfWeek dia)
    {
        var requisicao = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        requisicao.Nome = nome;
        requisicao.LocalId = local;
        requisicao.ProfessorId = professor;
        requisicao.Mensalidade = 150m;
        requisicao.Horarios = [new HorarioRequisicao { DiaSemana = dia, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }];
        return Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(requisicao)));
    }

    private async Task<Guid> AlunoAsync(Guid escolinha, Guid turma, string nome, string responsavel, string? cpf, string telefone,
        string? segundoResponsavel = null, string? segundoTelefone = null, bool recebeNotificacao = true)
    {
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = nome;
        r.DataNascimento = new DateOnly(2015, 4, 12);
        r.TurmaId = turma;
        r.Responsaveis[0].Nome = responsavel;
        r.Responsaveis[0].Cpf = cpf;
        r.Responsaveis[0].Telefone = telefone;
        r.Responsaveis[0].RecebeNotificacao = recebeNotificacao;
        if (segundoResponsavel is not null)
        {
            r.Responsaveis[1].Nome = segundoResponsavel;
            r.Responsaveis[1].Telefone = segundoTelefone;
        }
        return Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r)));
    }

    private async Task<Base> MontarAsync()
    {
        var escolinha = await EscolinhaAsync();
        var local = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao
        {
            Nome = "Ginásio Moringão", Logradouro = "Rua Tietê", Numero = "1000", Bairro = "Centro", Cidade = "Londrina", Uf = "PR"
        })));
        var (carlos, pessoaCarlos) = await ProfessorComLoginAsync(escolinha, "Carlos Treinador", "(43) 99999-0000");
        var sub11 = await TurmaAsync(escolinha, "Sub-11", local, carlos, DayOfWeek.Tuesday);
        var sub13 = await TurmaAsync(escolinha, "Sub-13", local, carlos, DayOfWeek.Wednesday);

        var pedro = await AlunoAsync(escolinha, sub11, "Pedro Lima", "Maria Lima", CpfMaria, "43988887777", "João Lima", "43977776666");
        var ana = await AlunoAsync(escolinha, sub13, "Ana Lima", "Maria Lima", CpfMaria, "43988887777");
        var bruno = await AlunoAsync(escolinha, sub11, "Bruno Alves", "Rita Alves", null, "43955554444");
        var carla = await AlunoAsync(escolinha, sub11, "Carla Costa", "Lia Costa", null, "43933332222");

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var responsaveis = await db.Responsaveis.IgnoreQueryFilters().Where(r => r.Escolinha_ID == escolinha)
            .ToDictionaryAsync(r => r.Nome, r => r.Responsavel_ID);
        return new Base(escolinha, local, carlos, pessoaCarlos, sub11, sub13, pedro, ana, bruno, carla,
            responsaveis["Maria Lima"], responsaveis["João Lima"], responsaveis["Rita Alves"], responsaveis["Lia Costa"]);
    }

    private static EventoRequisicao NovoJogo(Base b, Guid[] turmas, DateOnly dia, TimeOnly hora, string? adversario) =>
        new() { Turmas = [.. turmas], Data = dia, Hora = hora, DuracaoMinutos = 60, LocalId = b.Local, Adversario = adversario };

    private async Task<Guid> JogoAsync(Base b, Guid[] turmas, DateOnly dia, TimeOnly hora, string? adversario) =>
        Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().CriarAsync(NovoJogo(b, turmas, dia, hora, adversario))));

    /// <summary>A escolha dos convocados de um jogo (rascunho da montagem).</summary>
    private Task<ResultadoOperacao> SelecionarAsync(Guid escolinha, Guid jogo, params Guid[] alunos) =>
        Gestor(escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().SalvarSelecaoAsync([jogo], alunos.Select(a => (jogo, a)).ToList()));

    private async Task ConvocarAsync(Guid escolinha, Guid jogo, params Guid[] alunos)
    {
        var selecionou = await SelecionarAsync(escolinha, jogo, alunos);
        Assert.True(selecionou.Sucesso, selecionou.Mensagem);
        var disparou = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync([jogo]));
        Assert.True(disparou.Sucesso, disparou.Mensagem);
    }

    private Task<ResultadoOperacao> RegistrarPorForaAsync(Guid escolinha, Guid convocacao, bool vai) =>
        Gestor(escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().RegistrarPorForaAsync(convocacao, vai));

    private async Task<SemanaConvocacoes> SemanaAsync(Guid escolinha, DateOnly dia) =>
        (await Gestor(escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().SemanaAsync(dia)))!;

    private async Task<PainelEvento> PainelAsync(Guid escolinha, Guid jogo) =>
        (await Gestor(escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().PainelAsync(jogo)))!;

    private async Task<LinhaConvocado> ConvocadoAsync(Guid escolinha, Guid jogo, Guid aluno) =>
        (await PainelAsync(escolinha, jogo)).Convocados.Single(c => c.AlunoId == aluno);

    private Task<ResumoRotinaConvocacoes> RotinaAsync(Guid escolinha) =>
        _app.NaEscolinhaAsync(escolinha, [], sp => sp.GetRequiredService<RotinaConvocacoes>().ExecutarAsync());

    private Task<SituacaoMensagem?> EnviarAsync(Guid escolinha, Guid mensagem) =>
        _app.NaEscolinhaAsync(escolinha, [], sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(mensagem));

    private static async Task<List<Convocacao>> ConvocacoesAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Convocacoes.IgnoreQueryFilters().Include(c => c.Historico).Where(c => c.Escolinha_ID == escolinha).ToListAsync();
    }

    private static async Task<List<Mensagem>> MensagensAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Mensagens.IgnoreQueryFilters().Include(m => m.Respostas)
            .Where(m => m.Escolinha_ID == escolinha).OrderBy(m => m.CriadaEm).ThenBy(m => m.DestinatarioNome).ToListAsync();
    }

    /// <summary>A família toca no botão da mensagem no WhatsApp: chega o webhook assinado, e o job o processa.</summary>
    private async Task BotaoAsync(Mensagem mensagem, bool vai, string idResposta)
    {
        var corpo = ArmazemSimuladorWhatsApp.MontarResposta(mensagem.Telefone!.TrimStart('+'), idResposta, vai ? "Vai" : "Não vai",
            PayloadBotao.Montar(mensagem.Mensagem_ID, vai ? 0 : 1), _app.GatewayWhatsApp.IdDe(mensagem.Mensagem_ID), _app.Tempo.GetUtcNow());
        Assert.Equal(ResultadoRecebimentoWebhook.Aceito, await _app.SemContextoAsync(sp => sp.GetRequiredService<ProcessadorWebhooksWhatsApp>()
            .ReceberAsync(corpo, AssinaturaWebhookWhatsApp.Montar(corpo, _app.WhatsApp.SegredoAppEfetivo), "127.0.0.1")));
        await _app.SemContextoAsync(async sp =>
        {
            await sp.GetRequiredService<ProcessadorWebhooksWhatsApp>().ProcessarAsync(_app.Mensageria.Webhooks[^1]);
            return 0;
        });
    }

    /// <summary>Uma mensalidade emitida e vencida, gravada direto (o caminho da Asaas tem os testes dele, no S4).</summary>
    private static async Task MensalidadeVencidaAsync(Guid escolinha, Guid aluno, Guid pagador, DateOnly vencimento)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var matricula = await db.Matriculas.IgnoreQueryFilters().SingleAsync(m => m.Aluno_ID == aluno);
        var cobranca = Cobranca.Prevista(matricula.Matricula_ID, aluno, matricula.Turma_ID, vencimento, vencimento, 150m, primeira: false);
        cobranca.Escolinha_ID = escolinha;
        cobranca.PrepararEmissao(pagador, "Pagador", 2m, 1m, vencimento.AddDays(-10));
        cobranca.RegistrarEmissao("pay_teste_vencida", null, Guid.NewGuid(), vencimento.AddDays(-10).ToDateTime(TimeOnly.MinValue));
        Assert.True(cobranca.Vencer(new DateOnly(2026, 9, 26)));
        db.Cobrancas.Add(cobranca);
        await db.SaveChangesAsync();
    }

    // ── Convocação de fim de semana (spec §11) ──

    /// <summary>CA-10, CA-11, CA-13 e CA-19: o critério de pronto do S5, pelo professor.</summary>
    [FatoComBanco]
    public async Task O_professor_monta_quatro_jogos_com_doze_convocados_cada_e_dispara_tudo_num_fluxo_so()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 100);
        // Doze em cada turma: a base tem Pedro, Bruno e Carla na Sub-11 e a Ana na Sub-13.
        (string Aluno, string Responsavel)[] sub11 =
        [
            ("Davi Rocha", "Tânia Rocha"), ("Enzo Martins", "Paulo Martins"), ("Felipe Souza", "Cláudia Souza"),
            ("Gabriel Nunes", "Renata Nunes"), ("Heitor Ramos", "Sílvia Ramos"), ("Igor Teixeira", "Marcos Teixeira"),
            ("Júlia Freitas", "Patrícia Freitas"), ("Kauã Mendes", "Roberto Mendes"), ("Laura Pires", "Vanessa Pires")
        ];
        (string Aluno, string Responsavel)[] sub13 =
        [
            ("Mateus Duarte", "Fábio Duarte"), ("Nicolas Barros", "Adriana Barros"), ("Otávio Cardoso", "Luciana Cardoso"),
            ("Rafael Moreira", "Sandro Moreira"), ("Samuel Viana", "Débora Viana"), ("Thiago Castro", "Eliane Castro"),
            ("Vitor Araújo", "Gustavo Araújo"), ("Yasmin Correia", "Helena Correia"), ("Lucas Batista", "Márcia Batista"),
            ("Miguel Farias", "Juliana Farias"), ("Bianca Moura", "Rodrigo Moura")
        ];
        var numero = 0;
        foreach (var (turma, familias) in new[] { (b.Sub11, sub11), (b.Sub13, sub13) })
        foreach (var (aluno, responsavel) in familias)
            await AlunoAsync(b.Escolinha, turma, aluno, responsavel, null, $"439911100{++numero:D2}");

        // Os quatro jogos do fim de semana, montados pelo professor (o professor do jogo é ele).
        var jogos = new List<Guid>();
        foreach (var (turma, dia, hora, adversario) in new[]
                 {
                     (b.Sub11, Sabado, 9, "Tigre FC"), (b.Sub13, Sabado, 14, "Leões da Vila"),
                     (b.Sub11, Domingo, 9, "Estrela do Norte"), (b.Sub13, Domingo, 14, "União Cambé")
                 })
        {
            var criou = await Carlos(b, sp => sp.GetRequiredService<ServicoEventos>()
                .CriarAsync(NovoJogo(b, [turma], dia, new TimeOnly(hora, 0), adversario)));
            jogos.Add(Id(criou));
        }

        // Uma tela só: os jogos da semana, cada um com os doze da turma para escolher.
        var semana = (await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().SemanaAsync(Sabado)))!;
        Assert.Equal((new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4)), (semana.Inicio, semana.Fim));
        Assert.Equal(jogos, semana.Eventos.Select(e => e.Id));
        Assert.All(semana.Eventos, e => Assert.Equal((true, 12, 0), (e.PodeConvocar, e.Alunos.Count, e.Contagem.Selecionadas)));

        // A escolha é rascunho: nenhuma mensagem sai antes do disparo.
        var todos = semana.Eventos.SelectMany(e => e.Alunos.Select(a => (e.Id, a.AlunoId))).ToList();
        var salvou = await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().SalvarSelecaoAsync(jogos, todos));
        Assert.Equal("Escolha dos convocados salva.", salvou.Mensagem);
        Assert.Empty(await MensagensAsync(b.Escolinha));
        Assert.Equal(48, (await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().SemanaAsync(Sabado)))!.ParaDisparar);

        // CA-10: o disparo de tudo, num fluxo só e numa gravação só; as telas abertas dos quatro jogos são avisadas.
        _app.TempoReal.Limpar();
        var disparou = await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync(jogos));
        Assert.Equal("48 convocação(ões) disparada(s) em 4 jogo(s).", disparou.Mensagem);
        var convocacoes = await ConvocacoesAsync(b.Escolinha);
        Assert.Equal(48, convocacoes.Count);
        Assert.All(convocacoes, c => Assert.Equal(SituacaoConvocacao.Enviada, c.Situacao));
        Assert.Equal(jogos.Order(), _app.TempoReal.Avisos.Where(a => a.Escolinha == b.Escolinha).Select(a => a.Evento).Distinct().Order());

        // CA-11: uma mensagem por convocado a cada responsável que recebe notificação (o Pedro tem dois: 50 no total), com
        // data, hora, local com link de mapa e a pergunta; todas aos responsáveis (R4, CA-19), despachadas depois da gravação.
        var mensagens = await MensagensAsync(b.Escolinha);
        Assert.Equal(50, mensagens.Count);
        Assert.Equal(50, mensagens.Select(m => (m.Origem_ID, m.Destinatario_ID)).Distinct().Count());
        Assert.Equal(convocacoes.Select(c => c.Convocacao_ID).Order(), mensagens.Select(m => m.Origem_ID).Distinct().Order());
        Assert.All(mensagens, m =>
        {
            Assert.Equal((GatilhoMensagem.ConvocacaoCriada, CanalMensagem.WhatsApp, TipoDestinatario.Responsavel), (m.Gatilho, m.Canal, m.TipoDestinatario));
            Assert.EndsWith($", no local Ginásio Moringão. Como chegar: {Mapa}. Toque em Vai ou Não vai para confirmar a presença.", m.Conteudo);
        });
        Assert.Equal(mensagens.Select(m => m.Mensagem_ID).Order(), _app.Mensageria.Envios.Select(e => e.MensagemId).Order());

        // CA-13: a Maria recebe as convocações dos dois filhos, cada uma com o nome do filho, o jogo, o dia e a hora.
        Assert.Equal(
        [
            $"Olá, Maria! Bola na Rede convocou Ana para o jogo da Sub-13 contra Leões da Vila, sábado, 03/10 às 14:00, no local Ginásio Moringão. Como chegar: {Mapa}. Toque em Vai ou Não vai para confirmar a presença.",
            $"Olá, Maria! Bola na Rede convocou Ana para o jogo da Sub-13 contra União Cambé, domingo, 04/10 às 14:00, no local Ginásio Moringão. Como chegar: {Mapa}. Toque em Vai ou Não vai para confirmar a presença.",
            $"Olá, Maria! Bola na Rede convocou Pedro para o jogo da Sub-11 contra Estrela do Norte, domingo, 04/10 às 09:00, no local Ginásio Moringão. Como chegar: {Mapa}. Toque em Vai ou Não vai para confirmar a presença.",
            $"Olá, Maria! Bola na Rede convocou Pedro para o jogo da Sub-11 contra Tigre FC, sábado, 03/10 às 09:00, no local Ginásio Moringão. Como chegar: {Mapa}. Toque em Vai ou Não vai para confirmar a presença."
        ], mensagens.Where(m => m.Destinatario_ID == b.Maria).Select(m => m.Conteudo).Order(StringComparer.Ordinal));
        Assert.Equal(2, mensagens.Count(m => m.Destinatario_ID == b.Maria && m.Aluno_ID == b.Ana));
        Assert.Equal(2, mensagens.Count(m => m.Destinatario_ID == b.Joao && m.Aluno_ID == b.Pedro));

        // O painel de cada jogo: doze aguardando resposta, contra o mínimo de confirmados da turma.
        semana = (await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().SemanaAsync(Sabado)))!;
        Assert.Equal(0, semana.ParaDisparar);
        Assert.All(semana.Eventos, e => Assert.Equal((12, 7), (e.Contagem.Aguardando, e.MinimoConfirmados)));
        var painel = (await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().PainelAsync(jogos[0])))!;
        Assert.Equal(("Jogo da Sub-11 contra Tigre FC", "Carlos Treinador", Mapa), (painel.Titulo, painel.Professor, painel.LinkMapa));
        Assert.Equal(12, painel.Convocados.Count);
        Assert.All(painel.Convocados, c => Assert.True(c.PodeRegistrar));
    }

    /// <summary>CA-12, P03 e o registro por fora (§6).</summary>
    [FatoComBanco]
    public async Task A_resposta_pelo_botao_do_whatsapp_chega_ao_painel_na_hora_e_vale_a_primeira_das_duas_familias()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 10);
        var jogo = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        await ConvocarAsync(b.Escolinha, jogo, b.Pedro, b.Bruno);
        var mensagens = await MensagensAsync(b.Escolinha);
        var maria = mensagens.Single(m => m.Destinatario_ID == b.Maria);
        var joao = mensagens.Single(m => m.Destinatario_ID == b.Joao);
        var rita = mensagens.Single(m => m.Destinatario_ID == b.Rita);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, maria.Mensagem_ID));
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, joao.Mensagem_ID));
        var pedido = Assert.Single(_app.GatewayWhatsApp.Enviados, p => p.MensagemId == maria.Mensagem_ID);
        Assert.Equal(("5543988887777", "convocacao_v1"), (pedido.Telefone, pedido.Template));
        Assert.Equal(["Vai", "Não vai"], pedido.Botoes);
        Assert.Equal(["Maria", "Bola na Rede", "Pedro", "jogo da Sub-11 contra Tigre FC", "sábado, 03/10 às 09:00", "Ginásio Moringão", Mapa],
            pedido.Parametros);

        // CA-12: a Maria toca em "Vai" e o painel muda sem ninguém atualizar a tela (o tempo real avisa a tela aberta,
        // que busca o painel de novo).
        _app.TempoReal.Limpar();
        _app.Tempo.Advance(TimeSpan.FromMinutes(5));
        await BotaoAsync(maria, vai: true, "wamid.RESPOSTA1");
        Assert.Contains((b.Escolinha, jogo), _app.TempoReal.Avisos);
        var painel = await PainelAsync(b.Escolinha, jogo);
        var pedro = painel.Convocados.Single(c => c.AlunoId == b.Pedro);
        Assert.Equal((SituacaoConvocacao.Confirmada, OrigemResposta.WhatsApp, "Maria Lima"), (pedro.Situacao, pedro.Origem, pedro.RespondidoPor));
        Assert.Equal((1, 1), (painel.Contagem.Confirmadas, painel.Contagem.Aguardando));
        var respostaMaria = Assert.Single((await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == maria.Mensagem_ID).Respostas);
        Assert.Equal((true, "Convocação confirmada pela resposta de Maria Lima."), (respostaMaria.Tratada, respostaMaria.Observacao));

        // P03: o João responde depois, e diferente. Vale a primeira; a outra fica à vista de quem convocou.
        _app.Tempo.Advance(TimeSpan.FromMinutes(5));
        await BotaoAsync(joao, vai: false, "wamid.RESPOSTA2");
        pedro = await ConvocadoAsync(b.Escolinha, jogo, b.Pedro);
        Assert.Equal((SituacaoConvocacao.Confirmada, "Não vai · João Lima, 26/09 12:10"), (pedro.Situacao, pedro.OutraResposta));
        var respostaJoao = Assert.Single((await MensagensAsync(b.Escolinha)).Single(m => m.Mensagem_ID == joao.Mensagem_ID).Respostas);
        Assert.Contains("vale a primeira", respostaJoao.Observacao);

        // O professor fala com a família e registra por fora: sobrescreve qualquer estado, com autoria (§6).
        _app.Tempo.Advance(TimeSpan.FromMinutes(5));
        var registrou = await Carlos(b, sp => sp.GetRequiredService<ServicoConvocacoes>().RegistrarPorForaAsync(pedro.ConvocacaoId, false));
        Assert.Equal("Pedro Lima: não vai (registrado por fora).", registrou.Mensagem);
        pedro = await ConvocadoAsync(b.Escolinha, jogo, b.Pedro);
        Assert.Equal((SituacaoConvocacao.Recusada, OrigemResposta.RegistroManual, "Carlos Treinador", (string?)null),
            (pedro.Situacao, pedro.Origem, pedro.RespondidoPor, pedro.OutraResposta));

        // A convocação do Bruno ainda estava na fila (a Rita não recebeu): com a resposta registrada, ela não sai mais.
        var bruno = await ConvocadoAsync(b.Escolinha, jogo, b.Bruno);
        Assert.True((await RegistrarPorForaAsync(b.Escolinha, bruno.ConvocacaoId, true)).Sucesso);
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, rita.Mensagem_ID));
        Assert.DoesNotContain(_app.GatewayWhatsApp.Enviados, p => p.MensagemId == rita.Mensagem_ID);

        // O histórico da convocação conta tudo, na ordem.
        var historico = (await ConvocacoesAsync(b.Escolinha)).Single(c => c.Aluno_ID == b.Pedro).Historico;
        Assert.Equal(
            [
                TipoHistoricoConvocacao.Selecionada, TipoHistoricoConvocacao.Enviada, TipoHistoricoConvocacao.Resposta,
                TipoHistoricoConvocacao.RespostaPosterior, TipoHistoricoConvocacao.RegistroManual
            ],
            historico.OrderBy(h => h.Em).ThenBy(h => h.Tipo).Select(h => h.Tipo));
    }

    /// <summary>CA-14: cancelar e adiar avisam todos os convocados, e as convocações passam a canceladas.</summary>
    [FatoComBanco]
    public async Task Cancelar_o_jogo_avisa_cada_familia_uma_vez_e_as_convocacoes_passam_a_canceladas()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 20);
        // O festival reúne as duas turmas: a Maria tem os dois filhos no mesmo jogo.
        var festival = await JogoAsync(b, [b.Sub11, b.Sub13], Sabado, new TimeOnly(9, 0), "Tigre FC");
        await ConvocarAsync(b.Escolinha, festival, b.Pedro, b.Ana, b.Bruno);
        var convocacaoRita = (await MensagensAsync(b.Escolinha)).Single(m => m.Destinatario_ID == b.Rita);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, convocacaoRita.Mensagem_ID));
        Assert.True((await SelecionarAsync(b.Escolinha, festival, b.Carla)).Sucesso); // escolhida, ainda não disparada

        var cancelou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().EncerrarAsync(
            new EncerrarEventoRequisicao { Id = festival, Motivo = "Quadra alagada pela chuva" }));
        Assert.Equal("Jogo cancelado. 3 convocação(ões) cancelada(s); 3 aviso(s) às famílias na fila.", cancelou.Mensagem);

        // As convocações disparadas passam a canceladas, com o motivo; a escolhida e não disparada some.
        var convocacoes = await ConvocacoesAsync(b.Escolinha);
        Assert.Equal(new[] { b.Ana, b.Bruno, b.Pedro }.Order(), convocacoes.Select(c => c.Aluno_ID).Order());
        Assert.All(convocacoes, c => Assert.Equal((SituacaoConvocacao.Cancelada, "Jogo cancelado: Quadra alagada pela chuva"),
            (c.Situacao, c.MotivoCancelamento)));

        // Cada família recebe um aviso só, com os filhos dela.
        var mensagens = await MensagensAsync(b.Escolinha);
        var avisos = mensagens.Where(m => m.Gatilho == GatilhoMensagem.EventoCancelado).ToList();
        Assert.Equal(new[] { b.Joao, b.Maria, b.Rita }.Order(), avisos.Select(m => m.Destinatario_ID).Order());
        var avisoMaria = avisos.Single(m => m.Destinatario_ID == b.Maria);
        Assert.Equal("Atenção, Maria: Bola na Rede informa que o jogo da Sub-11 e Sub-13 contra Tigre FC, marcado para sábado, 03/10 às 09:00, foi " +
                     "cancelado. Motivo: Quadra alagada pela chuva. Este aviso é sobre Ana e Pedro. Qualquer dúvida, fale com a equipe da escolinha.", avisoMaria.Conteudo);
        Assert.EndsWith("Este aviso é sobre Pedro. Qualquer dúvida, fale com a equipe da escolinha.", avisos.Single(m => m.Destinatario_ID == b.Joao).Conteudo);
        Assert.EndsWith("Este aviso é sobre Bruno. Qualquer dúvida, fale com a equipe da escolinha.", avisos.Single(m => m.Destinatario_ID == b.Rita).Conteudo);

        // A convocação que ainda estava na fila não sai; a que já saiu fica no log como estava.
        var convocacoesNaFila = mensagens.Where(m => m.Gatilho == GatilhoMensagem.ConvocacaoCriada && m.Mensagem_ID != convocacaoRita.Mensagem_ID).ToList();
        Assert.Equal(3, convocacoesNaFila.Count);
        Assert.All(convocacoesNaFila, m => Assert.Equal(SituacaoMensagem.Suprimida, m.Situacao));
        Assert.Equal(SituacaoMensagem.Enviada, mensagens.Single(m => m.Mensagem_ID == convocacaoRita.Mensagem_ID).Situacao);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, avisoMaria.Mensagem_ID));

        // O painel mostra o jogo cancelado, sem ações; o jogo encerrado não muda mais.
        var painel = await PainelAsync(b.Escolinha, festival);
        Assert.Equal((SituacaoEvento.Cancelado, "Quadra alagada pela chuva", 3, false),
            (painel.Situacao, painel.Motivo, painel.Contagem.Canceladas, painel.PodeAlterar));
        Assert.All(painel.Convocados, c => Assert.False(c.PodeRegistrar));
        Assert.False((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().EncerrarAsync(
            new EncerrarEventoRequisicao { Id = festival, Motivo = "De novo" }))).Sucesso);

        // Adiar avisa do mesmo jeito, com a promessa da nova data.
        var domingo = await JogoAsync(b, [b.Sub11], Domingo, new TimeOnly(9, 0), "Estrela do Norte");
        await ConvocarAsync(b.Escolinha, domingo, b.Bruno);
        var adiou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().EncerrarAsync(
            new EncerrarEventoRequisicao { Id = domingo, Adiar = true, Motivo = "O adversário pediu outra data" }));
        Assert.Equal("Jogo adiado. 1 convocação(ões) cancelada(s); 1 aviso(s) às famílias na fila.", adiou.Mensagem);
        Assert.Equal("Atenção, Rita: Bola na Rede informa que o jogo da Sub-11 contra Estrela do Norte, marcado para domingo, 04/10 às 09:00, foi adiado " +
                     "(a nova data será avisada). Motivo: O adversário pediu outra data. Este aviso é sobre Bruno. Qualquer dúvida, fale com a equipe da escolinha.",
            (await MensagensAsync(b.Escolinha)).Single(m => m.Gatilho == GatilhoMensagem.EventoCancelado && m.Origem_ID == domingo).Conteudo);
        Assert.Equal(SituacaoEvento.Adiado, (await PainelAsync(b.Escolinha, domingo)).Situacao);
    }

    /// <summary>CA-3: o desligado sai das convocações dos próximos jogos e fica no histórico do jogo que passou.</summary>
    [FatoComBanco]
    public async Task Desligar_o_aluno_tira_as_convocacoes_dos_proximos_jogos_e_guarda_a_do_jogo_que_passou()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 20);
        var amistoso = await JogoAsync(b, [b.Sub11], new DateOnly(2026, 9, 27), new TimeOnly(10, 0), "Tigre FC"); // amanhã
        await ConvocarAsync(b.Escolinha, amistoso, b.Pedro, b.Carla);
        var sabado = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Leões da Vila");
        await ConvocarAsync(b.Escolinha, sabado, b.Pedro, b.Carla);
        var domingo = await JogoAsync(b, [b.Sub11], Domingo, new TimeOnly(9, 0), "Estrela do Norte");
        Assert.True((await SelecionarAsync(b.Escolinha, domingo, b.Pedro)).Sucesso); // escolhido, ainda não disparado
        var pedroNoAmistoso = (await ConvocacoesAsync(b.Escolinha)).Single(c => c.Evento_ID == amistoso && c.Aluno_ID == b.Pedro);
        Assert.True((await RegistrarPorForaAsync(b.Escolinha, pedroNoAmistoso.Convocacao_ID, true)).Sucesso);

        // Segunda, 28/09: o amistoso passou (a rotina fecha quem não respondeu), e o Pedro sai da escolinha.
        _app.Tempo.Advance(TimeSpan.FromDays(2));
        Assert.Equal(new ResumoRotinaConvocacoes(0, 1), await RotinaAsync(b.Escolinha));
        var desligou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(new MudarSituacaoRequisicao
        {
            AlunoId = b.Pedro, Para = SituacaoAluno.Desistente, Data = new DateOnly(2026, 9, 28), Motivo = "Mudou de cidade"
        }));
        Assert.True(desligou.Sucesso, desligou.Mensagem);
        Assert.EndsWith(" 2 convocação(ões) de jogo retirada(s).", desligou.Mensagem);

        var doPedro = (await ConvocacoesAsync(b.Escolinha)).Where(c => c.Aluno_ID == b.Pedro).ToList();
        Assert.Equal(SituacaoConvocacao.Confirmada, doPedro.Single(c => c.Evento_ID == amistoso).Situacao); // o período em que esteve ativo
        var noSabado = doPedro.Single(c => c.Evento_ID == sabado);
        Assert.Equal((SituacaoConvocacao.Cancelada, "Aluno desligado."), (noSabado.Situacao, noSabado.MotivoCancelamento));
        Assert.DoesNotContain(doPedro, c => c.Evento_ID == domingo);
        var daFila = (await MensagensAsync(b.Escolinha)).Where(m => m.Origem_ID == noSabado.Convocacao_ID).ToList();
        Assert.Equal(2, daFila.Count); // Maria e João
        Assert.All(daFila, m => Assert.Equal(SituacaoMensagem.Suprimida, m.Situacao));

        // Some da lista de convocação: no domingo nem aparece; no sábado, só a convocação cancelada, sem caixa para marcar.
        var semana = await SemanaAsync(b.Escolinha, Sabado);
        Assert.DoesNotContain(semana.Eventos.Single(e => e.Id == domingo).Alunos, a => a.AlunoId == b.Pedro);
        var naLista = semana.Eventos.Single(e => e.Id == sabado).Alunos.Single(a => a.AlunoId == b.Pedro);
        Assert.Equal((SituacaoConvocacao.Cancelada, false, false), (naLista.Situacao, naLista.Escolhido, naLista.Editavel));

        // A matrícula encerrada tira das convocações por aquela turma depois do último dia.
        Guid matriculaCarla;
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            matriculaCarla = (await db.Matriculas.IgnoreQueryFilters().SingleAsync(m => m.Aluno_ID == b.Carla)).Matricula_ID;
        var encerrou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMatriculas>().EncerrarAsync(
            new EncerrarMatriculaRequisicao { MatriculaId = matriculaCarla, Data = new DateOnly(2026, 9, 28), Motivo = "Foi para o vôlei" }));
        Assert.Contains(" 1 convocação(ões) de jogo retirada(s).", encerrou.Mensagem);
        var daCarla = (await ConvocacoesAsync(b.Escolinha)).Where(c => c.Aluno_ID == b.Carla).ToList();
        Assert.Equal(SituacaoConvocacao.SemResposta, daCarla.Single(c => c.Evento_ID == amistoso).Situacao);
        Assert.Equal((SituacaoConvocacao.Cancelada, "Matrícula encerrada em 28/09/2026."),
            (daCarla.Single(c => c.Evento_ID == sabado).Situacao, daCarla.Single(c => c.Evento_ID == sabado).MotivoCancelamento));
    }

    // ── Lembrete, alteração e sinais ──

    /// <summary>BL-M07-11 e P19.</summary>
    [FatoComBanco]
    public async Task O_lembrete_sai_uma_vez_a_quem_nao_respondeu_e_no_inicio_do_jogo_a_convocacao_fica_sem_resposta()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 20);
        var jogo = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        await ConvocarAsync(b.Escolinha, jogo, b.Pedro, b.Bruno, b.Carla);
        Assert.True((await RegistrarPorForaAsync(b.Escolinha, (await ConvocadoAsync(b.Escolinha, jogo, b.Pedro)).ConvocacaoId, true)).Sucesso);
        var convocacaoLia = (await MensagensAsync(b.Escolinha)).Single(m => m.Destinatario_ID == b.Lia);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, convocacaoLia.Mensagem_ID));

        // Antes da hora do lembrete (24 horas antes do jogo, pelo parâmetro), nada.
        Assert.Equal(new ResumoRotinaConvocacoes(0, 0), await RotinaAsync(b.Escolinha));

        // Sexta, 02/10, 09:15: o lembrete sai aos responsáveis de quem não respondeu (Bruno e Carla), uma vez só.
        _app.Tempo.Advance(new DateTime(2026, 10, 2, 9, 15, 0) - new DateTime(2026, 9, 26, 12, 0, 0));
        Assert.Equal(new ResumoRotinaConvocacoes(2, 0), await RotinaAsync(b.Escolinha));
        Assert.Equal(new ResumoRotinaConvocacoes(0, 0), await RotinaAsync(b.Escolinha));
        var lembretes = (await MensagensAsync(b.Escolinha)).Where(m => m.Gatilho == GatilhoMensagem.ConvocacaoSemResposta).ToList();
        Assert.Equal(new[] { b.Lia, b.Rita }.Order(), lembretes.Select(m => m.Destinatario_ID).Order());
        var lembreteRita = lembretes.Single(m => m.Destinatario_ID == b.Rita);
        Assert.Equal("Olá, Rita! Bola na Rede ainda aguarda a resposta da convocação de Bruno para o jogo da Sub-11 contra Tigre FC, " +
                     "sábado, 03/10 às 09:00. Toque em Vai ou Não vai para confirmar a presença.", lembreteRita.Conteudo);
        Assert.NotNull((await ConvocadoAsync(b.Escolinha, jogo, b.Carla)).LembreteEm);

        // A Rita responde pelo botão do lembrete: vale como resposta da convocação.
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, lembreteRita.Mensagem_ID));
        await BotaoAsync(lembreteRita, vai: true, "wamid.LEMBRETE1");
        Assert.Equal(SituacaoConvocacao.Confirmada, (await ConvocadoAsync(b.Escolinha, jogo, b.Bruno)).Situacao);

        // O Diego entra depois da hora do lembrete: acabou de receber a convocação, não recebe lembrete em seguida.
        var diego = await AlunoAsync(b.Escolinha, b.Sub11, "Diego Prado", "Sônia Prado", null, "43922221111");
        await ConvocarAsync(b.Escolinha, jogo, diego);
        Assert.Equal(new ResumoRotinaConvocacoes(0, 0), await RotinaAsync(b.Escolinha));

        // Sábado, 09:00: o jogo começou; quem não respondeu fica "sem resposta" (P19).
        _app.Tempo.Advance(new DateTime(2026, 10, 3, 9, 0, 0) - new DateTime(2026, 10, 2, 9, 15, 0));
        Assert.Equal(new ResumoRotinaConvocacoes(0, 2), await RotinaAsync(b.Escolinha));
        var painel = await PainelAsync(b.Escolinha, jogo);
        Assert.Equal(SituacaoConvocacao.SemResposta, painel.Convocados.Single(c => c.AlunoId == b.Carla).Situacao);
        Assert.Equal(SituacaoConvocacao.SemResposta, painel.Convocados.Single(c => c.AlunoId == diego).Situacao);
        Assert.Equal((2, 2, 0), (painel.Contagem.Confirmadas, painel.Contagem.SemResposta, painel.Contagem.Aguardando));

        // A resposta que chega depois do início fica à vista, sem mudar o estado; o lembrete que ficou na fila não sai mais.
        _app.Tempo.Advance(TimeSpan.FromMinutes(10));
        await BotaoAsync(convocacaoLia, vai: true, "wamid.TARDIA1");
        var carla = await ConvocadoAsync(b.Escolinha, jogo, b.Carla);
        Assert.Equal((SituacaoConvocacao.SemResposta, "Vai · Lia Costa, 03/10 09:10 (depois do início)"), (carla.Situacao, carla.OutraResposta));
        Assert.Equal(SituacaoMensagem.Suprimida, await EnviarAsync(b.Escolinha, lembretes.Single(m => m.Destinatario_ID == b.Lia).Mensagem_ID));
    }

    /// <summary>BL-M07-13: mudar o quando ou o onde avisa as famílias; o resto só fica registrado.</summary>
    [FatoComBanco]
    public async Task Mudar_o_horario_avisa_as_familias_dos_convocados_e_o_aviso_anterior_que_estava_na_fila_nao_sai()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 20);
        var jogo = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        await ConvocarAsync(b.Escolinha, jogo, b.Pedro, b.Bruno);
        Assert.True((await RegistrarPorForaAsync(b.Escolinha, (await ConvocadoAsync(b.Escolinha, jogo, b.Bruno)).ConvocacaoId, true)).Sucesso);

        const string logistica = "Chegar 30 minutos antes, com o uniforme azul.";
        Task<ResultadoOperacao> AlterarAsync(TimeOnly hora)
        {
            var r = NovoJogo(b, [], Sabado, hora, "Tigre FC");
            r.Id = jogo;
            r.Logistica = logistica;
            return Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().AlterarAsync(r));
        }

        // Só a logística mudou: fica registrado, e ninguém recebe mensagem por isso.
        Assert.Equal("Jogo alterado.", (await AlterarAsync(new TimeOnly(9, 0))).Mensagem);
        Assert.DoesNotContain(await MensagensAsync(b.Escolinha), m => m.Gatilho == GatilhoMensagem.EventoAlterado);

        // O horário mudou: cada família dos convocados é avisada uma vez, e as respostas continuam valendo.
        _app.Tempo.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("Jogo alterado. 3 aviso(s) às famílias na fila.", (await AlterarAsync(new TimeOnly(10, 30))).Mensagem);
        var primeiros = (await MensagensAsync(b.Escolinha)).Where(m => m.Gatilho == GatilhoMensagem.EventoAlterado).ToList();
        Assert.Equal(new[] { b.Joao, b.Maria, b.Rita }.Order(), primeiros.Select(m => m.Destinatario_ID).Order());
        Assert.Equal($"Atenção, Rita: Bola na Rede informa que o jogo da Sub-11 contra Tigre FC mudou. Novo horário: sábado, 03/10 às 10:30, " +
                     $"no local Ginásio Moringão. Como chegar: {Mapa}. Este aviso é sobre Bruno. Qualquer dúvida, fale com a equipe da escolinha.", primeiros.Single(m => m.Destinatario_ID == b.Rita).Conteudo);
        Assert.Equal(SituacaoConvocacao.Confirmada, (await ConvocadoAsync(b.Escolinha, jogo, b.Bruno)).Situacao);

        // Mudou de novo antes de o aviso sair: o anterior, com o horário velho, é recolhido, e só o novo sai.
        _app.Tempo.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal("Jogo alterado. 3 aviso(s) às famílias na fila.", (await AlterarAsync(new TimeOnly(11, 0))).Mensagem);
        var avisos = (await MensagensAsync(b.Escolinha)).Where(m => m.Gatilho == GatilhoMensagem.EventoAlterado).ToList();
        Assert.Equal(6, avisos.Count);
        Assert.All(avisos.Where(m => primeiros.Any(p => p.Mensagem_ID == m.Mensagem_ID)), m => Assert.Equal(SituacaoMensagem.Suprimida, m.Situacao));
        var novoDaRita = avisos.Single(m => m.Destinatario_ID == b.Rita && m.Situacao == SituacaoMensagem.Pendente);
        Assert.Contains("Novo horário: sábado, 03/10 às 11:00", novoDaRita.Conteudo);
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, novoDaRita.Mensagem_ID));

        // O registro guarda quem mudou o quê, campo a campo, com o antes e o depois.
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var registros = await db.RegistrosAlteracao.IgnoreQueryFilters().Where(r => r.Registro_ID == jogo).OrderBy(r => r.Em).ToListAsync();
        Assert.Equal(["Logística", "Quando", "Quando"], registros.Select(r => r.Campo));
        Assert.Equal(((string?)null, logistica), (registros[0].ValorAnterior, registros[0].ValorNovo));
        Assert.Equal(("sábado, 03/10 às 10:30 · 60 min", "sábado, 03/10 às 11:00 · 60 min", "Gestor dos testes"),
            (registros[2].ValorAnterior, registros[2].ValorNovo, registros[2].AutorNome));
    }

    /// <summary>BL-M07-03 e BL-M07-06: os alertas sinalizam e pedem confirmação, não bloqueiam (R9).</summary>
    [FatoComBanco]
    public async Task Os_alertas_de_agenda_pedem_confirmacao_e_o_aluno_em_dois_jogos_no_mesmo_horario_aparece_sinalizado()
    {
        var b = await MontarAsync();
        var primeiro = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        Assert.True((await SelecionarAsync(b.Escolinha, primeiro, b.Pedro)).Sucesso);

        // Outro jogo às 09:30, no mesmo ginásio e com o mesmo professor: a gravação para e mostra os dois alertas.
        var requisicao = NovoJogo(b, [b.Sub11], Sabado, new TimeOnly(9, 30), "Leões da Vila");
        var pediu = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().CriarAsync(requisicao));
        Assert.False(pediu.Sucesso);
        Assert.Equal(("Há 2 alertas de agenda. Confira e confirme para salvar mesmo assim.", nameof(EventoRequisicao.ConfirmarAlertas)),
            (pediu.Mensagem, pediu.Campo));
        Assert.Equal(
            [
                (TipoConflito.ProfessorOcupado, "Carlos Treinador já tem o jogo da Sub-11 contra Tigre FC de 03/10 09:00 a 10:00."),
                (TipoConflito.LocalOcupado, "Ginásio Moringão já recebe o jogo da Sub-11 contra Tigre FC de 03/10 09:00 a 10:00.")
            ],
            Assert.IsType<AlertasEvento>(pediu.Dados).Alertas.Select(a => (a.Tipo, a.Descricao)));
        Assert.Single((await SemanaAsync(b.Escolinha, Sabado)).Eventos);

        // Quem salva viu os alertas e decide seguir: fica registrado.
        requisicao.ConfirmarAlertas = true;
        var segundo = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoEventos>().CriarAsync(requisicao)));
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var registro = await db.RegistrosAlteracao.IgnoreQueryFilters()
                .SingleAsync(r => r.Registro_ID == segundo && r.Campo == "Alertas de agenda confirmados");
            Assert.Equal("Gestor dos testes", registro.AutorNome);
            Assert.Contains("Ginásio Moringão já recebe o jogo da Sub-11 contra Tigre FC", registro.ValorNovo);
        }

        // Na montagem, o Pedro aparece no segundo jogo com o sinal do outro jogo no horário, e pode ser escolhido mesmo assim.
        var semana = await SemanaAsync(b.Escolinha, Sabado);
        var noSegundo = semana.Eventos.Single(e => e.Id == segundo);
        Assert.Equal(["Também no jogo contra Tigre FC, 09:00–10:00"], noSegundo.Alunos.Single(a => a.AlunoId == b.Pedro).Conflitos);
        Assert.Empty(noSegundo.Alunos.Single(a => a.AlunoId == b.Bruno).Conflitos);
        Assert.Equal(2, noSegundo.Alertas.Count);
        Assert.True((await SelecionarAsync(b.Escolinha, segundo, b.Pedro)).Sucesso);
        Assert.Equal(2, (await ConvocacoesAsync(b.Escolinha)).Count(c => c.Aluno_ID == b.Pedro));
        Assert.Equal(["Também no jogo contra Leões da Vila, 09:30–10:30"],
            (await SemanaAsync(b.Escolinha, Sabado)).Eventos.Single(e => e.Id == primeiro).Alunos.Single(a => a.AlunoId == b.Pedro).Conflitos);
    }

    /// <summary>RF-CON-003: a convocação feita fora da janela sai no começo dela.</summary>
    [FatoComBanco]
    public async Task Com_a_janela_de_disparo_a_convocacao_feita_fora_do_horario_espera_o_inicio_da_janela()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 5);
        var alterou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(new AlterarParametrosRequisicao
        {
            Valores = { [ChavesParametro.ConvocacaoModoDisparo] = "Janela", [ChavesParametro.ConvocacaoJanelaDisparo] = "14:00-20:00" }
        }));
        Assert.True(alterou.Sucesso, alterou.Mensagem);
        var jogo = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        Assert.True((await SelecionarAsync(b.Escolinha, jogo, b.Bruno)).Sucesso);

        var disparou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync([jogo]));
        Assert.Equal("1 convocação(ões) disparada(s) em 1 jogo(s). Fora da janela de disparo: as mensagens saem às 14:00 de 26/09.",
            disparou.Mensagem);
        var mensagem = Assert.Single(await MensagensAsync(b.Escolinha));
        Assert.Equal(new DateTime(2026, 9, 26, 14, 0, 0), mensagem.AgendadaPara);

        // O job que roda antes da hora só reagenda; às 14:00 a mensagem sai.
        Assert.Equal(SituacaoMensagem.Pendente, await EnviarAsync(b.Escolinha, mensagem.Mensagem_ID));
        Assert.Contains(_app.Mensageria.Envios, e => e.MensagemId == mensagem.Mensagem_ID && e.Quando == new DateTime(2026, 9, 26, 14, 0, 0));
        Assert.Empty(_app.GatewayWhatsApp.Enviados);
        _app.Tempo.Advance(TimeSpan.FromHours(2));
        Assert.Equal(SituacaoMensagem.Enviada, await EnviarAsync(b.Escolinha, mensagem.Mensagem_ID));
    }

    /// <summary>Disparada na janela de silêncio, a convocação espera o fim dela, e quem dispara fica sabendo quando sai.</summary>
    [FatoComBanco]
    public async Task Disparar_na_janela_de_silencio_diz_quando_as_mensagens_saem()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 5);
        var jogo = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        Assert.True((await SelecionarAsync(b.Escolinha, jogo, b.Bruno)).Sucesso);
        _app.Tempo.Advance(TimeSpan.FromHours(10)); // sábado, 22:00

        var disparou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync([jogo]));
        Assert.Equal("1 convocação(ões) disparada(s) em 1 jogo(s). Na janela de silêncio: as mensagens saem às 07:00 de 27/09.",
            disparou.Mensagem);
        var mensagem = Assert.Single(await MensagensAsync(b.Escolinha));
        Assert.Equal(SituacaoMensagem.Pendente, await EnviarAsync(b.Escolinha, mensagem.Mensagem_ID));
        Assert.Contains(_app.Mensageria.Envios, e => e.MensagemId == mensagem.Mensagem_ID && e.Quando == new DateTime(2026, 9, 27, 7, 0, 0));
    }

    /// <summary>RF-CON-016 e RF-CON-017: sinalizar, não bloquear (R9); o bloqueio só com o parâmetro ligado.</summary>
    [FatoComBanco]
    public async Task A_montagem_sinaliza_mensalidade_atrasada_e_quem_nao_recebe_mensagem_e_so_bloqueia_se_a_escolinha_ligar()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 5);
        var davi = await AlunoAsync(b.Escolinha, b.Sub11, "Davi Rocha", "Tânia Rocha", null, "43911112222", recebeNotificacao: false);
        await MensalidadeVencidaAsync(b.Escolinha, b.Bruno, b.Rita, new DateOnly(2026, 9, 10)); // 16 dias de atraso
        var jogo = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");

        var alunos = (await SemanaAsync(b.Escolinha, Sabado)).Eventos.Single().Alunos;
        var bruno = alunos.Single(a => a.AlunoId == b.Bruno);
        Assert.Equal((true, false), (bruno.Inadimplente, bruno.Bloqueado));
        Assert.False(alunos.Single(a => a.AlunoId == b.Pedro).Inadimplente);
        Assert.Equal((true, false), (alunos.Single(a => a.AlunoId == davi).SemCelular, alunos.Single(a => a.AlunoId == b.Pedro).SemCelular));

        // Sinalizado, o Bruno entra na convocação. O Davi não tem quem receba a mensagem: o disparo avisa para registrar por fora.
        Assert.Equal("Escolha dos convocados salva.", (await SelecionarAsync(b.Escolinha, jogo, b.Bruno, davi)).Mensagem);
        var disparou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync([jogo]));
        Assert.Equal("2 convocação(ões) disparada(s) em 1 jogo(s). 1 convocado(s) sem responsável que receba mensagem: registre a resposta por fora.",
            disparou.Mensagem);
        Assert.Equal([b.Rita], (await MensagensAsync(b.Escolinha)).Select(m => m.Destinatario_ID));

        // A escolinha liga o bloqueio: quem está atrasado não entra, e a tela diz por quê.
        var ligou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(new AlterarParametrosRequisicao
        {
            Valores = { [ChavesParametro.ConvocacaoBloquearInadimplente] = "true" }
        }));
        Assert.True(ligou.Sucesso, ligou.Mensagem);
        var domingo = await JogoAsync(b, [b.Sub11], Domingo, new TimeOnly(9, 0), "Estrela do Norte");
        Assert.True((await SemanaAsync(b.Escolinha, Sabado)).Eventos.Single(e => e.Id == domingo).Alunos.Single(a => a.AlunoId == b.Bruno).Bloqueado);
        var barrado = await SelecionarAsync(b.Escolinha, domingo, b.Bruno, b.Pedro);
        Assert.Equal("Escolha dos convocados salva. Bruno Alves está com mensalidade em atraso, e a escolinha bloqueia a convocação de inadimplente.",
            barrado.Mensagem);
        Assert.Equal([b.Pedro], (await ConvocacoesAsync(b.Escolinha)).Where(c => c.Evento_ID == domingo).Select(c => c.Aluno_ID));
    }

    // ── Escopo ──

    /// <summary>BL-M07-18: o professor monta e acompanha só os jogos das turmas dele.</summary>
    [FatoComBanco]
    public async Task O_professor_monta_e_acompanha_so_os_jogos_das_proprias_turmas()
    {
        var b = await MontarAsync();
        await CreditarAsync(b.Escolinha, 10);
        var (paula, pessoaPaula) = await ProfessorComLoginAsync(b.Escolinha, "Paula Souza", "(43) 98888-1111");
        var marista = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao
        {
            Nome = "Quadra do Colégio Marista", Logradouro = "Rua Pernambuco", Numero = "1000", Cidade = "Londrina", Uf = "PR"
        })));
        var sub15 = await TurmaAsync(b.Escolinha, "Sub-15", marista, paula, DayOfWeek.Friday);
        var lucas = await AlunoAsync(b.Escolinha, sub15, "Lucas Prado", "Sônia Prado", null, "43922223333");
        var doCarlos = await JogoAsync(b, [b.Sub11], Sabado, new TimeOnly(9, 0), "Tigre FC");
        await ConvocarAsync(b.Escolinha, doCarlos, b.Bruno);
        Task<T> ComoPaula<T>(Func<IServiceProvider, Task<T>> acao) => Como(b.Escolinha, pessoaPaula, "Paula Souza", PapelAtuacao.Professor, acao);

        var formulario = await ComoPaula(sp => sp.GetRequiredService<ServicoEventos>().FormularioAsync());
        Assert.Equal(["Sub-15"], formulario.Turmas.Select(t => t.Nome));
        Assert.False(formulario.PodeEscolherProfessor);

        // Não monta jogo da turma de outro professor.
        var alheio = await ComoPaula(sp => sp.GetRequiredService<ServicoEventos>()
            .CriarAsync(NovoJogo(b, [b.Sub11], Domingo, new TimeOnly(10, 0), null)));
        Assert.Equal(("Escolha turmas ativas que você acompanha.", nameof(EventoRequisicao.Turmas)), (alheio.Mensagem, alheio.Campo));

        // Monta o da Sub-15 como ela mesma, ainda que o pedido traga outro professor; sem local escolhido, vale o da turma.
        var requisicao = NovoJogo(b, [sub15], Domingo, new TimeOnly(10, 0), "Grêmio Maringá");
        requisicao.ProfessorId = b.Carlos;
        requisicao.LocalId = null;
        var proprio = Id(await ComoPaula(sp => sp.GetRequiredService<ServicoEventos>().CriarAsync(requisicao)));
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var evento = await db.Eventos.IgnoreQueryFilters().SingleAsync(e => e.Evento_ID == proprio);
            Assert.Equal((paula, marista), (evento.Professor_ID, evento.Local_ID));
        }

        // Vê só o jogo dela; o do Carlos, nem pelo endereço direto.
        Assert.Equal([proprio], (await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().SemanaAsync(Sabado)))!.Eventos.Select(e => e.Id));
        Assert.Null(await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().PainelAsync(doCarlos)));
        Assert.Equal("Jogo não encontrado.", (await ComoPaula(sp => sp.GetRequiredService<ServicoEventos>().EncerrarAsync(
            new EncerrarEventoRequisicao { Id = doCarlos, Motivo = "Sem quadra" }))).Mensagem);
        var convocacaoBruno = (await ConvocacoesAsync(b.Escolinha)).Single(c => c.Aluno_ID == b.Bruno).Convocacao_ID;
        Assert.Equal("Só a gestão e o professor do jogo registram a resposta.",
            (await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().RegistrarPorForaAsync(convocacaoBruno, true))).Mensagem);

        // No jogo dela, convoca só quem é da turma dela (o Bruno, de outra turma, não entra) e registra a resposta.
        var salvou = await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>()
            .SalvarSelecaoAsync([proprio], [(proprio, lucas), (proprio, b.Bruno)]));
        Assert.True(salvou.Sucesso, salvou.Mensagem);
        Assert.Equal("1 convocação(ões) disparada(s) em 1 jogo(s).",
            (await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().DispararAsync([proprio]))).Mensagem);
        var linha = Assert.Single((await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().PainelAsync(proprio)))!.Convocados);
        Assert.Equal((lucas, true), (linha.AlunoId, linha.PodeRegistrar));
        Assert.True((await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().RegistrarPorForaAsync(linha.ConvocacaoId, true))).Sucesso);
        linha = Assert.Single((await ComoPaula(sp => sp.GetRequiredService<ServicoConvocacoes>().PainelAsync(proprio)))!.Convocados);
        Assert.Equal((SituacaoConvocacao.Confirmada, OrigemResposta.RegistroManual, "Paula Souza"), (linha.Situacao, linha.Origem, linha.RespondidoPor));

        // A gestão vê os dois.
        Assert.Equal([doCarlos, proprio], (await SemanaAsync(b.Escolinha, Sabado)).Eventos.Select(e => e.Id));
    }
}
