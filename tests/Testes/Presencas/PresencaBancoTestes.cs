using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Presencas;

/// <summary>
/// Fatia S3 no banco de testes: a chamada (fila do aparelho, a quadra prevalece), o alerta de faltas com a janela
/// anti-duplicidade e o que a ficha mostra (CA-3, CA-4, CA-5, CA-6, CA-18 e o lado servidor do CA-21).
/// A turma Sub-9 treina terça e quinta às 18:00; outubro de 2026 começa numa quinta.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class PresencaBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private readonly AmbienteAplicacao _app = new();
    private long _sequencia = 1_000;

    public Task InitializeAsync()
    {
        _app.Usuario.Id = Guid.NewGuid();
        _app.Usuario.Nome = "Gestão dos testes";
        return BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private sealed record Base(Guid Escolinha, Guid Turma, Guid Pedro, Guid Ana, Guid Bruno);

    // ── Montagem ──

    private Task<T> Gestor<T>(Guid escolinha, Func<IServiceProvider, Task<T>> acao) =>
        _app.NaEscolinhaAsync(escolinha, [PapelAtuacao.Gestor], acao);

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    /// <summary>Leva o relógio para a hora de Brasília indicada (UTC−3).</summary>
    private void Em(int mes, int dia, int hora = 19, int minuto = 0) =>
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

    private async Task<Base> MontarAsync()
    {
        var criada = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = "Bola na Rede", NomeModalidade = "Futsal", GestorNome = "Gestão", GestorEmail = "gestao@bolanarede.com" }));
        Assert.True(criada.Sucesso, criada.Mensagem);
        var escolinha = Assert.IsType<EscolinhaCriada>(criada.Dados).EscolinhaId;

        var local = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao { Nome = "Quadra Central" })));
        var professor = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var turma = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        turma.Nome = "Sub-9";
        turma.LocalId = local;
        turma.ProfessorId = professor;
        turma.Mensalidade = 150m;
        turma.Horarios =
        [
            new HorarioRequisicao { DiaSemana = DayOfWeek.Tuesday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) },
            new HorarioRequisicao { DiaSemana = DayOfWeek.Thursday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) }
        ];
        var turmaId = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(turma)));

        var pedro = await AlunoAsync(escolinha, turmaId, "Pedro Lima", "Maria Lima", "529.982.247-25", "43988887777");
        var ana = await AlunoAsync(escolinha, turmaId, "Ana Lima", "Maria Lima", "529.982.247-25", "43988887777");
        var bruno = await AlunoAsync(escolinha, turmaId, "Bruno Alves", "Rita Alves", null, "43955554444");
        return new Base(escolinha, turmaId, pedro, ana, bruno);
    }

    private async Task<Guid> SessaoAsync(Base b, int mes, int dia) =>
        (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>()
            .ConsultarAsync(new FiltroAgenda { Id = b.Turma, De = new DateOnly(2026, mes, dia), Ate = new DateOnly(2026, mes, dia) })))
        .Itens.Single().SessaoId;

    private MarcacaoRequisicao Marcacao(Guid sessao, Guid aluno, SituacaoPresenca situacao, string aparelho = "aparelho-quadra",
        long? sequencia = null, string? motivo = null, bool saude = false) => new()
    {
        Id = Guid.NewGuid(),
        SessaoId = sessao,
        AlunoId = aluno,
        Situacao = situacao,
        Justificativa = motivo,
        Saude = saude,
        Dispositivo = aparelho,
        Sequencia = sequencia ?? ++_sequencia,
        MarcadaEm = _app.Tempo.GetUtcNow().UtcDateTime
    };

    private Task<ResultadoSincronizacao> SincronizarAsync(Guid escolinha, params MarcacaoRequisicao[] marcacoes) =>
        Gestor(escolinha, sp => sp.GetRequiredService<ServicoChamada>().SincronizarAsync(new LoteMarcacoes { Marcacoes = [.. marcacoes] }));

    private async Task<int> FaltaAsync(Base b, int mes, int dia, Guid aluno)
    {
        Em(mes, dia);
        var r = await SincronizarAsync(b.Escolinha, Marcacao(await SessaoAsync(b, mes, dia), aluno, SituacaoPresenca.Falta));
        Assert.Equal(ResultadoDaMarcacao.Aplicada, Assert.Single(r.Marcacoes).Resultado);
        return r.Alertas;
    }

    private static async Task<List<AlertaFaltas>> AlertasAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.AlertasFaltas.IgnoreQueryFilters().Where(a => a.Escolinha_ID == escolinha).OrderBy(a => a.AvaliadoEm).ToListAsync();
    }

    private static async Task<List<Mensagem>> MensagensDeFaltasAsync(Guid escolinha)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Mensagens.IgnoreQueryFilters()
            .Where(m => m.Escolinha_ID == escolinha && m.Gatilho == GatilhoMensagem.FaltasAtingidas)
            .OrderBy(m => m.CriadaEm).ToListAsync();
    }

    // ── Alerta de faltas (CA-4, CA-5, CA-6) ──

    [FatoComBanco]
    public async Task A_terceira_falta_do_mes_avisa_os_responsaveis_e_a_seguinte_dentro_da_janela_fica_suprimida()
    {
        var b = await MontarAsync();

        Em(10, 1);
        var primeira = await SincronizarAsync(b.Escolinha,
            Marcacao(await SessaoAsync(b, 10, 1), b.Pedro, SituacaoPresenca.Falta),
            Marcacao(await SessaoAsync(b, 10, 1), b.Ana, SituacaoPresenca.Presente),
            Marcacao(await SessaoAsync(b, 10, 1), b.Bruno, SituacaoPresenca.Presente));
        Assert.All(primeira.Marcacoes, m => Assert.Equal(ResultadoDaMarcacao.Aplicada, m.Resultado));
        Assert.Equal(0, primeira.Alertas);
        Assert.Equal(0, await FaltaAsync(b, 10, 6, b.Pedro));

        // CA-4: a terceira falta do mês dispara, no registro dela.
        Assert.Equal(1, await FaltaAsync(b, 10, 8, b.Pedro));
        var aviso = Assert.Single(await MensagensDeFaltasAsync(b.Escolinha));
        Assert.Equal("Maria Lima", aviso.DestinatarioNome);
        Assert.Equal(CanalMensagem.WhatsApp, aviso.Canal);
        Assert.Equal(b.Pedro, aviso.Aluno_ID);
        Assert.Contains("Pedro teve 3 faltas em outubro nos treinos da turma Sub-9", aviso.Conteudo);
        Assert.Contains(aviso.Mensagem_ID, _app.Mensageria.Envios.Select(e => e.MensagemId));

        // CA-5: a quarta falta, cinco dias depois, fica dentro da janela de 15 dias: registrada e sem mensagem.
        Assert.Equal(0, await FaltaAsync(b, 10, 13, b.Pedro));
        var alertas = await AlertasAsync(b.Escolinha);
        Assert.Equal([SituacaoAlertaFaltas.Disparado, SituacaoAlertaFaltas.Suprimido], alertas.Select(a => a.Situacao));
        Assert.Contains("janela anti-duplicidade de 15 dia(s)", alertas[1].Motivo);
        Assert.Single(await MensagensDeFaltasAsync(b.Escolinha));

        // Passada a janela, a regra atingida de novo avisa de novo.
        Assert.Equal(1, await FaltaAsync(b, 10, 29, b.Pedro));
        Assert.Equal(2, (await MensagensDeFaltasAsync(b.Escolinha)).Count);

        // CA-6: a ficha mostra qual alerta saiu, quando e para quem; e o risco de evasão.
        var ficha = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoPresenca>().DoAlunoAsync(b.Pedro));
        Assert.NotNull(ficha);
        var naFicha = ficha.Alertas!.OrderBy(a => a.Em).ToList();
        Assert.Equal(3, naFicha.Count);
        Assert.Equal("3 faltas em outubro", naFicha[0].Descricao);
        Assert.Equal("Maria Lima", Assert.Single(naFicha[0].Destinos).Nome);
        Assert.Equal(new DateTime(2026, 10, 8, 19, 0, 0), naFicha[0].Em);
        Assert.Empty(naFicha[1].Destinos);
        Assert.True(ficha.Frequencia.EmRisco);
        Assert.Equal(0m, ficha.Frequencia.Percentual);

        var riscos = await Gestor(b.Escolinha, sp => sp.GetRequiredService<IRiscoEvasao>().AlunosEmRiscoAsync());
        Assert.Equal([b.Pedro], riscos.Select(r => r.AlunoId));
    }

    [FatoComBanco]
    public async Task Mudar_o_limite_de_faltas_vale_na_proxima_falta_sem_reiniciar()
    {
        var b = await MontarAsync();
        var alterou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(
            new AlterarParametrosRequisicao { Valores = { [ChavesParametro.PresencaLimiteFaltas] = "2" } }));
        Assert.True(alterou.Sucesso, alterou.Mensagem);

        Assert.Equal(0, await FaltaAsync(b, 10, 1, b.Pedro));
        Assert.Equal(1, await FaltaAsync(b, 10, 6, b.Pedro));
        Assert.Equal(2, (await AlertasAsync(b.Escolinha)).Single().Limite);
    }

    // ── A fila do aparelho e a quadra que prevalece (CA-21) ──

    [FatoComBanco]
    public async Task Sem_sinal_a_fila_chega_depois_uma_vez_so_e_a_marcacao_atrasada_nao_desfaz_a_correcao()
    {
        var b = await MontarAsync();
        Em(10, 1, 18, 5);
        var sessao = await SessaoAsync(b, 10, 1);
        MarcacaoRequisicao[] fila =
        [
            Marcacao(sessao, b.Pedro, SituacaoPresenca.Falta, "A", 100),
            Marcacao(sessao, b.Ana, SituacaoPresenca.Presente, "A", 101),
            Marcacao(sessao, b.Bruno, SituacaoPresenca.Presente, "A", 102)
        ];

        // O sinal volta às 21:00: a fila chega com a hora em que cada toque foi dado.
        Em(10, 1, 21);
        Assert.All((await SincronizarAsync(b.Escolinha, fila)).Marcacoes, m => Assert.Equal(ResultadoDaMarcacao.Aplicada, m.Resultado));
        var reenvio = await SincronizarAsync(b.Escolinha, fila);
        Assert.All(reenvio.Marcacoes, m => Assert.Equal(ResultadoDaMarcacao.Repetida, m.Resultado));

        // A gestão corrige a falta do Pedro de outro aparelho; a marcação atrasada do aparelho A não desfaz a correção.
        Assert.Equal(ResultadoDaMarcacao.Aplicada, (await SincronizarAsync(b.Escolinha,
            Marcacao(sessao, b.Pedro, SituacaoPresenca.Justificada, "painel", 5, "Avisou pelo telefone"))).Marcacoes[0].Resultado);
        var atrasada = await SincronizarAsync(b.Escolinha, Marcacao(sessao, b.Pedro, SituacaoPresenca.Presente, "A", 99));
        Assert.Equal(ResultadoDaMarcacao.Ignorada, atrasada.Marcacoes[0].Resultado);
        Assert.Equal(ResultadoDaMarcacao.Repetida, (await SincronizarAsync(b.Escolinha, fila[0])).Marcacoes[0].Resultado);

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var pedro = await db.Presencas.IgnoreQueryFilters().SingleAsync(p => p.SessaoTreino_ID == sessao && p.Aluno_ID == b.Pedro);
            Assert.Equal(SituacaoPresenca.Justificada, pedro.Situacao);
            Assert.Equal("Avisou pelo telefone", pedro.Justificativa);
            var ana = await db.Presencas.IgnoreQueryFilters().SingleAsync(p => p.SessaoTreino_ID == sessao && p.Aluno_ID == b.Ana);
            Assert.Equal(new DateTime(2026, 10, 1, 18, 5, 0), ana.MarcadaEm);
            Assert.Equal(SituacaoSessao.Realizada, (await db.Sessoes.IgnoreQueryFilters().SingleAsync(s => s.SessaoTreino_ID == sessao)).Situacao);
            var marcacoes = await db.MarcacoesPresenca.IgnoreQueryFilters().Where(m => m.SessaoTreino_ID == sessao).ToListAsync();
            Assert.Equal(5, marcacoes.Count);
            Assert.Single(marcacoes, m => m.Resultado == ResultadoMarcacao.Ignorada);
        }

        // Uma marcação nova do aparelho da quadra prevalece sobre o valor que o painel tinha.
        Assert.Equal(ResultadoDaMarcacao.Aplicada, (await SincronizarAsync(b.Escolinha,
            Marcacao(sessao, b.Pedro, SituacaoPresenca.Falta, "A", 150))).Marcacoes[0].Resultado);
        var chamada = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoChamada>().ChamadaAsync(sessao));
        Assert.Equal(SituacaoPresenca.Falta, chamada!.Alunos.Single(a => a.Id == b.Pedro).Situacao);
        Assert.Equal(3, chamada.Marcados);
    }

    [FatoComBanco]
    public async Task A_chamada_recusa_treino_cancelado_dia_futuro_aluno_fora_da_turma_e_quem_nao_tem_acesso()
    {
        var b = await MontarAsync();
        Em(10, 1);
        var hoje = await SessaoAsync(b, 10, 1);
        var futura = await SessaoAsync(b, 10, 6);
        var cancelada = await SessaoAsync(b, 10, 8);
        Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoSessoes>()
            .CancelarAsync(new CancelarSessaoRequisicao { SessaoId = cancelada, Motivo = "Quadra interditada" }))).Sucesso);

        var r = await SincronizarAsync(b.Escolinha,
            Marcacao(futura, b.Pedro, SituacaoPresenca.Presente),
            Marcacao(cancelada, b.Pedro, SituacaoPresenca.Presente),
            Marcacao(hoje, Guid.NewGuid(), SituacaoPresenca.Presente));
        Assert.All(r.Marcacoes, m => Assert.Equal(ResultadoDaMarcacao.Recusada, m.Resultado));
        Assert.Contains(r.Marcacoes, m => m.Mensagem!.Contains("no dia do treino"));
        Assert.Contains(r.Marcacoes, m => m.Mensagem!.Contains("cancelado"));
        Assert.Contains(r.Marcacoes, m => m.Mensagem!.Contains("não está na turma"));

        // Quem só lê a presença (o responsável) não marca.
        var responsavel = await _app.NaEscolinhaAsync(b.Escolinha, [PapelAtuacao.Responsavel], sp => sp.GetRequiredService<ServicoChamada>()
            .SincronizarAsync(new LoteMarcacoes { Marcacoes = [Marcacao(hoje, b.Pedro, SituacaoPresenca.Presente)] }));
        Assert.Equal(ResultadoDaMarcacao.Recusada, Assert.Single(responsavel.Marcacoes).Resultado);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.False(await db.Presencas.IgnoreQueryFilters().AnyAsync(p => p.Escolinha_ID == b.Escolinha));
    }

    [FatoComBanco]
    public async Task O_desligado_sai_da_chamada_a_partir_da_data_e_fica_na_chamada_de_antes()
    {
        // CA-3: o aluno desligado some da chamada e permanece no histórico do período em que esteve ativo.
        var b = await MontarAsync();
        Em(10, 1);
        var antes = await SessaoAsync(b, 10, 1);
        var depois = await SessaoAsync(b, 10, 6);

        Em(10, 5, 10);
        var desligou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(
            new MudarSituacaoRequisicao { AlunoId = b.Bruno, Para = SituacaoAluno.Desistente, Data = new DateOnly(2026, 10, 5), Motivo = "Mudou de cidade" }));
        Assert.True(desligou.Sucesso, desligou.Mensagem);

        Em(10, 6);
        var chamadaDepois = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoChamada>().ChamadaAsync(depois));
        Assert.DoesNotContain(chamadaDepois!.Alunos, a => a.Id == b.Bruno);
        var chamadaAntes = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoChamada>().ChamadaAsync(antes));
        Assert.Contains(chamadaAntes!.Alunos, a => a.Id == b.Bruno);

        var marcar = await SincronizarAsync(b.Escolinha, Marcacao(depois, b.Bruno, SituacaoPresenca.Presente),
            Marcacao(antes, b.Bruno, SituacaoPresenca.Presente));
        Assert.Equal([ResultadoDaMarcacao.Recusada, ResultadoDaMarcacao.Aplicada], marcar.Marcacoes.Select(m => m.Resultado));
    }

    // ── O motivo de saúde (BL-M05-13, R5) ──

    [FatoComBanco]
    public async Task O_motivo_de_saude_nao_vai_para_a_pagina_guardada_nem_para_o_log_e_so_quem_pode_ve()
    {
        var b = await MontarAsync();
        Em(10, 1);
        var sessao = await SessaoAsync(b, 10, 1);
        await SincronizarAsync(b.Escolinha, Marcacao(sessao, b.Pedro, SituacaoPresenca.Justificada, motivo: "Febre alta", saude: true),
            Marcacao(sessao, b.Ana, SituacaoPresenca.Justificada, motivo: "Viagem da família"));

        var chamada = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoChamada>().ChamadaAsync(sessao));
        var pedroNaChamada = chamada!.Alunos.Single(a => a.Id == b.Pedro);
        Assert.True(pedroNaChamada.JustificativaSaude);
        Assert.Null(pedroNaChamada.Justificativa);
        Assert.Equal("Viagem da família", chamada.Alunos.Single(a => a.Id == b.Ana).Justificativa);

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var log = await db.MarcacoesPresenca.IgnoreQueryFilters().SingleAsync(m => m.Aluno_ID == b.Pedro);
            Assert.True(log.JustificativaSaude);
            Assert.Null(log.Justificativa);
        }

        var gestao = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoPresenca>().DoAlunoAsync(b.Pedro));
        var presenca = Assert.Single(gestao!.Presencas);
        Assert.True(presenca.MotivoVisivel);
        Assert.Equal("Febre alta", presenca.Justificativa);

        // Quem não é do escopo do aluno não vê nada da presença dele.
        Assert.Null(await _app.NaEscolinhaAsync(b.Escolinha, [PapelAtuacao.Responsavel],
            sp => sp.GetRequiredService<ServicoPresenca>().DoAlunoAsync(b.Pedro)));
    }

    // ── Falta corrigida (regra de ouro 5) e a revalidação no envio ──

    [FatoComBanco]
    public async Task Corrigir_a_falta_antes_do_envio_recolhe_o_aviso_anula_o_alerta_e_libera_a_janela()
    {
        var b = await MontarAsync();
        await FaltaAsync(b, 10, 1, b.Pedro);
        await FaltaAsync(b, 10, 6, b.Pedro);
        Em(10, 8, 22);
        var sessao = await SessaoAsync(b, 10, 8);
        Assert.Equal(1, (await SincronizarAsync(b.Escolinha, Marcacao(sessao, b.Pedro, SituacaoPresenca.Falta, "A", 500))).Alertas);
        Assert.Equal(SituacaoMensagem.Pendente, Assert.Single(await MensagensDeFaltasAsync(b.Escolinha)).Situacao);

        // O professor viu que marcou errado e corrige antes de o aviso sair (a mensagem ainda estava na fila).
        Em(10, 8, 22, 10);
        await SincronizarAsync(b.Escolinha, Marcacao(sessao, b.Pedro, SituacaoPresenca.Presente, "A", 501));
        var recolhida = Assert.Single(await MensagensDeFaltasAsync(b.Escolinha));
        Assert.Equal(SituacaoMensagem.Suprimida, recolhida.Situacao);
        Assert.Contains("corrigida", recolhida.Motivo);
        Assert.Equal(SituacaoAlertaFaltas.Anulado, Assert.Single(await AlertasAsync(b.Escolinha)).Situacao);

        // O alerta anulado não segura a janela: a próxima falta que atinge a regra avisa.
        Assert.Equal(1, await FaltaAsync(b, 10, 13, b.Pedro));
    }

    [FatoComBanco]
    public async Task O_envio_revalida_o_alerta_e_nao_manda_se_a_falta_foi_corrigida()
    {
        var b = await MontarAsync();
        var conta = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().ConcederCreditosAsync(
            new ConcederCreditosRequisicao { EscolinhaId = b.Escolinha, Quantidade = 5, Motivo = "Teste" }));
        Assert.True(conta.Sucesso, conta.Mensagem);
        await FaltaAsync(b, 10, 1, b.Pedro);
        await FaltaAsync(b, 10, 6, b.Pedro);
        await FaltaAsync(b, 10, 8, b.Pedro);
        var aviso = Assert.Single(await MensagensDeFaltasAsync(b.Escolinha));

        // A falta muda por um caminho que não recolheu a fila (corrida com o envio): o motor revalida na hora de mandar.
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
            await db.Database.ExecuteSqlRawAsync("UPDATE Presenca SET Situacao = 10 WHERE Aluno_ID = {0} AND SessaoInicio = {1}",
                b.Pedro, new DateTime(2026, 10, 8, 18, 0, 0));

        var situacao = await _app.NaEscolinhaAsync(b.Escolinha, [], sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(aviso.Mensagem_ID));
        Assert.Equal(SituacaoMensagem.Suprimida, situacao);
        Assert.Contains("corrigida", Assert.Single(await MensagensDeFaltasAsync(b.Escolinha)).Motivo);
        Assert.Empty(_app.GatewayWhatsApp.Enviados);
    }
}
