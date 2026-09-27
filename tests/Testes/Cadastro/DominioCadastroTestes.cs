using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Testes.Cadastro;

/// <summary>A máquina do aluno (spec §6), a geração das sessões (BL-M04-04) e o alerta de conflito do professor (BL-M04-09).</summary>
public class MaquinaSituacaoAlunoTestes
{
    private static readonly DateOnly Matricula = new(2026, 3, 1);

    public static TheoryData<SituacaoAluno, SituacaoAluno, bool> Pares()
    {
        var permitidas = new HashSet<(SituacaoAluno, SituacaoAluno)>
        {
            (SituacaoAluno.Trial, SituacaoAluno.Ativo), (SituacaoAluno.Trial, SituacaoAluno.Desistente),
            (SituacaoAluno.Ativo, SituacaoAluno.Inativo), (SituacaoAluno.Inativo, SituacaoAluno.Ativo),
            (SituacaoAluno.Ativo, SituacaoAluno.Desistente), (SituacaoAluno.Desistente, SituacaoAluno.Ativo),
            (SituacaoAluno.Inativo, SituacaoAluno.Desistente) // atalho fora da §6, aprovado pelo responsável (P28)
        };
        var dados = new TheoryData<SituacaoAluno, SituacaoAluno, bool>();
        foreach (var de in Enum.GetValues<SituacaoAluno>())
        foreach (var para in Enum.GetValues<SituacaoAluno>())
            dados.Add(de, para, permitidas.Contains((de, para)));
        return dados;
    }

    [Theory]
    [MemberData(nameof(Pares))]
    public void So_as_transicoes_da_spec_e_o_atalho_aprovado_sao_permitidas(SituacaoAluno de, SituacaoAluno para, bool permitida) =>
        Assert.Equal(permitida, MaquinaSituacaoAluno.Permite(de, para));

    [Fact]
    public void Desligar_exige_motivo_e_data_depois_da_matricula()
    {
        var aluno = Aluno.Novo("Pedro", new DateOnly(2016, 5, 10), Matricula, null);

        Assert.Throws<ArgumentException>(() => aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 9, 1), " "));
        Assert.Throws<ArgumentException>(() => aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 2, 1), "Mudou de cidade"));

        aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 9, 1), "Mudou de cidade");
        Assert.Equal(SituacaoAluno.Desistente, aluno.Situacao);
        Assert.Equal(new DateOnly(2026, 9, 1), aluno.DataDesligamento);
        Assert.Equal("Mudou de cidade", aluno.MotivoDesligamento);
    }

    [Fact]
    public void Rematricula_volta_a_ativo_com_a_nova_data_de_matricula()
    {
        var aluno = Aluno.Novo("Pedro", new DateOnly(2016, 5, 10), Matricula, null);
        aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 6, 30), "Férias longas");

        Assert.Throws<ArgumentException>(() => aluno.MudarSituacao(SituacaoAluno.Ativo, new DateOnly(2026, 6, 1)));
        aluno.MudarSituacao(SituacaoAluno.Ativo, new DateOnly(2026, 8, 3));

        Assert.Equal(SituacaoAluno.Ativo, aluno.Situacao);
        Assert.Equal(new DateOnly(2026, 8, 3), aluno.DataMatricula);
        Assert.Null(aluno.DataDesligamento);
    }

    [Fact]
    public void Inativo_e_desligado_direto_mas_nao_antes_do_inicio_da_pausa()
    {
        var aluno = Aluno.Novo("Pedro", new DateOnly(2016, 5, 10), Matricula, null);
        aluno.MudarSituacao(SituacaoAluno.Inativo, new DateOnly(2026, 5, 1));

        Assert.Throws<ArgumentException>(() => aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 6, 1), " "));
        Assert.Throws<ArgumentException>(() => aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 4, 20), "Saiu"));

        aluno.MudarSituacao(SituacaoAluno.Desistente, new DateOnly(2026, 6, 1), "Saiu");
        Assert.Equal(SituacaoAluno.Desistente, aluno.Situacao);
        Assert.Equal(new DateOnly(2026, 6, 1), aluno.DataDesligamento);
    }

    [Fact]
    public void Trial_vencido_continua_em_trial_so_sinalizado()
    {
        var aluno = Aluno.Novo("Ana", new DateOnly(2017, 1, 20), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15));

        Assert.Equal(SituacaoAluno.Trial, aluno.Situacao);
        Assert.Equal(2, aluno.DiasParaFimDoTrial(new DateOnly(2026, 9, 13)));
        Assert.False(aluno.TrialVencido(new DateOnly(2026, 9, 15)));
        Assert.True(aluno.TrialVencido(new DateOnly(2026, 9, 16)));
        Assert.Equal(SituacaoAluno.Trial, aluno.Situacao);
    }

    [Theory]
    [InlineData("2016-05-10", "2026-05-09", 9)]
    [InlineData("2016-05-10", "2026-05-10", 10)]
    [InlineData("2016-02-29", "2026-02-28", 9)]
    public void Idade_conta_o_aniversario(string nascimento, string hoje, int idade) =>
        Assert.Equal(idade, Aluno.IdadeEm(DateOnly.Parse(nascimento), DateOnly.Parse(hoje)));
}

public class MatriculaEVinculoTestes
{
    [Fact]
    public void Matricula_encerrada_guarda_o_periodo_e_nao_termina_antes_de_comecar()
    {
        var matricula = new Matricula { DataInicio = new DateOnly(2026, 3, 1), Mensalidade = 150m };

        Assert.Throws<ArgumentException>(() => matricula.Encerrar(new DateOnly(2026, 2, 28), "Saiu"));
        matricula.Encerrar(new DateOnly(2026, 6, 30), "Mudou de turma");

        Assert.False(matricula.Ativa);
        Assert.True(matricula.VigenteEm(new DateOnly(2026, 6, 30)));
        Assert.False(matricula.VigenteEm(new DateOnly(2026, 7, 1)));
        Assert.Throws<InvalidOperationException>(() => matricula.Encerrar(new DateOnly(2026, 7, 1), "De novo"));
    }

    [Fact]
    public void Pagador_sempre_recebe_cobranca()
    {
        var vinculo = new VinculoAlunoResponsavel();
        vinculo.DefinirFinanceiro(pagador: true, recebeCobranca: false);

        Assert.True(vinculo.Pagador);
        Assert.True(vinculo.RecebeCobranca);
    }

    [Fact]
    public void O_consentimento_vigente_e_o_ultimo_registro_e_so_o_concedido_revoga()
    {
        var aluno = Guid.NewGuid();
        var recusado = new ConsentimentoAluno { Aluno_ID = aluno, Finalidade = FinalidadeConsentimento.UsoImagem, Concedido = false, RegistradoEm = new DateTime(2026, 3, 1) };
        var concedido = new ConsentimentoAluno { Aluno_ID = aluno, Finalidade = FinalidadeConsentimento.UsoImagem, Concedido = true, RegistradoEm = new DateTime(2026, 4, 1) };
        var registros = new[] { recusado, concedido };

        Assert.Equal(EstadoConsentimento.Concedido, ConsentimentoAluno.EstadoAtual(registros, FinalidadeConsentimento.UsoImagem));
        Assert.Equal(EstadoConsentimento.Pendente, ConsentimentoAluno.EstadoAtual(registros, FinalidadeConsentimento.TratamentoDados));
        Assert.Throws<InvalidOperationException>(() => recusado.Revogar(new DateTime(2026, 5, 1), null, "Gestor", null));

        concedido.Revogar(new DateTime(2026, 5, 1), null, "Gestor", "Pedido da mãe");
        Assert.Equal(EstadoConsentimento.Revogado, ConsentimentoAluno.EstadoAtual(registros, FinalidadeConsentimento.UsoImagem));
    }

    [Theory]
    [InlineData("150", 150)]
    [InlineData("150,5", 150.5)]
    [InlineData("150,50", 150.5)]
    [InlineData("1.500,00", 1500)]
    [InlineData("R$ 1.500,00", 1500)]
    [InlineData("150.50", 150.5)]
    [InlineData("1.500", 1500)]
    [InlineData("0", 0)]
    public void Moeda_le_o_que_o_gestor_digita(string texto, decimal esperado)
    {
        Assert.True(Moeda.TentarLer(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Moeda_recusa_o_que_nao_e_valor(string? texto) => Assert.False(Moeda.TentarLer(texto, out _));
}

public class GeradorSessoesTestes
{
    // Sábado, 26/09/2026, 12:00 em Brasília: o horizonte vai até 31/10.
    private static readonly DateTime Agora = new(2026, 9, 26, 12, 0, 0);
    private static readonly Guid Local = Guid.NewGuid();
    private static readonly Guid Professor = Guid.NewGuid();

    private static Turma TurmaTercaQuinta(DateOnly? inicio = null)
    {
        var turma = new Turma
        {
            Turma_ID = Guid.NewGuid(), Nome = "Sub-9", Local_ID = Local, Professor_ID = Professor,
            DataInicio = inicio ?? new DateOnly(2026, 9, 1), Situacao = SituacaoTurma.Ativa
        };
        turma.Horarios.Add(new HorarioTurma { HorarioTurma_ID = Guid.NewGuid(), DiaSemana = DayOfWeek.Tuesday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) });
        turma.Horarios.Add(new HorarioTurma { HorarioTurma_ID = Guid.NewGuid(), DiaSemana = DayOfWeek.Thursday, Inicio = new TimeOnly(18, 0), Fim = new TimeOnly(19, 0) });
        return turma;
    }

    private static List<SessaoTreino> Gerar(Turma turma, DateTime agora) =>
        GeradorSessoes.Planejar(turma, [], agora).Criar.Select(o => GeradorSessoes.Nova(turma, o)).ToList();

    [Theory]
    [InlineData("2026-09-26", "2026-10-31")]
    [InlineData("2026-12-15", "2027-01-31")]
    [InlineData("2027-01-31", "2027-02-28")]
    public void O_horizonte_vai_ate_o_fim_do_mes_seguinte(string hoje, string fim) =>
        Assert.Equal(DateOnly.Parse(fim), GeradorSessoes.FimDoHorizonte(DateOnly.Parse(hoje)));

    [Fact]
    public void A_turma_nova_ganha_as_sessoes_do_horizonte_com_local_e_professor()
    {
        var sessoes = Gerar(TurmaTercaQuinta(), Agora);

        Assert.Equal(10, sessoes.Count); // terças e quintas de 29/09 a 29/10
        Assert.Equal(new DateTime(2026, 9, 29, 18, 0, 0), sessoes.Min(s => s.Inicio));
        Assert.Equal(new DateTime(2026, 10, 29, 19, 0, 0), sessoes.Max(s => s.Fim));
        Assert.All(sessoes, s =>
        {
            Assert.Equal(SituacaoSessao.Prevista, s.Situacao);
            Assert.Equal(Local, s.Local_ID);
            Assert.Equal(Professor, s.Professor_ID);
        });
    }

    [Fact]
    public void Gerar_de_novo_nao_duplica()
    {
        var turma = TurmaTercaQuinta();
        var existentes = Gerar(turma, Agora);

        Assert.True(GeradorSessoes.Planejar(turma, existentes, Agora).Vazio);
        Assert.True(GeradorSessoes.Planejar(turma, existentes, Agora.AddHours(3)).Vazio);
    }

    [Fact]
    public void Mudar_o_horario_ajusta_as_previstas_futuras_e_preserva_a_cancelada()
    {
        var turma = TurmaTercaQuinta();
        var existentes = Gerar(turma, Agora);
        var cancelada = existentes.First(s => s.Data == new DateOnly(2026, 10, 6));
        cancelada.Cancelar("Feriado", Agora, null, "Gestor");

        turma.Horarios.First(h => h.DiaSemana == DayOfWeek.Tuesday).Inicio = new TimeOnly(17, 0);
        var plano = GeradorSessoes.Planejar(turma, existentes, Agora);

        Assert.Empty(plano.Criar);
        Assert.Empty(plano.Remover);
        Assert.Equal(4, plano.Ajustar.Count); // as 5 terças menos a cancelada
        Assert.All(plano.Ajustar, a => Assert.Equal(17, a.Ocorrencia.Inicio.Hour));
        Assert.DoesNotContain(plano.Ajustar, a => a.Sessao == cancelada);
    }

    [Fact]
    public void Mudar_o_dia_troca_as_previstas_e_retirar_o_horario_tira_as_dele()
    {
        var turma = TurmaTercaQuinta();
        var existentes = Gerar(turma, Agora);
        turma.Horarios.First(h => h.DiaSemana == DayOfWeek.Tuesday).DiaSemana = DayOfWeek.Wednesday;
        var quinta = turma.Horarios.First(h => h.DiaSemana == DayOfWeek.Thursday);
        turma.Horarios.Remove(quinta);

        var plano = GeradorSessoes.Planejar(turma, existentes, Agora);

        Assert.Equal(10, plano.Remover.Count); // 5 terças + 5 quintas
        Assert.Equal(5, plano.Criar.Count);    // quartas de 30/09 a 28/10
        Assert.All(plano.Criar, o => Assert.Equal(DayOfWeek.Wednesday, o.Data.DayOfWeek));
    }

    [Fact]
    public void A_sessao_que_ja_comecou_nao_muda_nem_e_recriada()
    {
        var turma = TurmaTercaQuinta();
        var terca = new DateTime(2026, 9, 29, 18, 30, 0); // no meio do treino de terça
        var existentes = Gerar(turma, Agora);

        turma.Horarios.First(h => h.DiaSemana == DayOfWeek.Tuesday).Inicio = new TimeOnly(17, 0);
        var plano = GeradorSessoes.Planejar(turma, existentes, terca);

        var emCurso = existentes.First(s => s.Data == new DateOnly(2026, 9, 29));
        Assert.DoesNotContain(plano.Ajustar, a => a.Sessao == emCurso);
        Assert.DoesNotContain(plano.Remover, s => s == emCurso);
        Assert.DoesNotContain(plano.Criar, o => o.Data == new DateOnly(2026, 9, 29));
    }

    [Fact]
    public void Turma_encerrada_tira_as_previstas_futuras()
    {
        var turma = TurmaTercaQuinta();
        var existentes = Gerar(turma, Agora);
        turma.Encerrar(Agora, "Fim do projeto");

        var plano = GeradorSessoes.Planejar(turma, existentes, Agora);

        Assert.Empty(plano.Criar);
        Assert.Equal(existentes.Count, plano.Remover.Count);
    }

    [Fact]
    public void A_turma_que_comeca_depois_so_gera_a_partir_do_inicio()
    {
        var sessoes = Gerar(TurmaTercaQuinta(inicio: new DateOnly(2026, 10, 15)), Agora);

        Assert.Equal(new DateOnly(2026, 10, 15), sessoes.Min(s => s.Data));
        Assert.Equal(5, sessoes.Count); // 15, 20, 22, 27 e 29/10
    }

    [Fact]
    public void O_resumo_dos_horarios_junta_os_dias_do_mesmo_horario()
    {
        Assert.Equal("Terça e Quinta, 18:00–19:00", TurmaTercaQuinta().ResumoHorarios());
        Assert.Equal("Sem horário", Turma.ResumirHorarios([]));
    }
}

public class DetectorConflitosTestes
{
    private static readonly HorarioSemanal Terca18 = new(DayOfWeek.Tuesday, new TimeOnly(18, 0), new TimeOnly(19, 0));

    private static Professor Professor(params (DayOfWeek Dia, int De, int Ate)[] faixas)
    {
        var professor = new Professor { Professor_ID = Guid.NewGuid() };
        professor.DefinirNome("Carlos");
        foreach (var (dia, de, ate) in faixas)
            professor.Disponibilidade.Add(new FaixaDisponibilidade { DiaSemana = dia, Inicio = new TimeOnly(de, 0), Fim = new TimeOnly(ate, 0) });
        return professor;
    }

    [Fact]
    public void Sem_disponibilidade_declarada_e_sem_outra_turma_nao_ha_alerta()
    {
        var conflitos = DetectorConflitos.ParaHorarios(Professor(), [Terca18], [], []);
        Assert.Empty(conflitos);
    }

    [Fact]
    public void Outra_turma_do_professor_no_mesmo_horario_alerta()
    {
        var outra = new HorarioOcupado("Sub-11", new HorarioSemanal(DayOfWeek.Tuesday, new TimeOnly(18, 30), new TimeOnly(19, 30)));

        var conflito = Assert.Single(DetectorConflitos.ParaHorarios(Professor(), [Terca18], [outra], []));
        Assert.Equal(TipoConflito.ProfessorOcupado, conflito.Tipo);
        Assert.Contains("Sub-11", conflito.Descricao);
    }

    [Fact]
    public void Horario_fora_da_faixa_declarada_alerta_e_dentro_nao()
    {
        var professor = Professor((DayOfWeek.Tuesday, 17, 21));

        Assert.Empty(DetectorConflitos.ParaHorarios(professor, [Terca18], [], []));
        var sabado = new HorarioSemanal(DayOfWeek.Saturday, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var conflito = Assert.Single(DetectorConflitos.ParaHorarios(professor, [sabado], [], []));
        Assert.Equal(TipoConflito.ForaDaDisponibilidade, conflito.Tipo);
        Assert.Contains("sem disponibilidade no sábado", conflito.Descricao);
    }

    [Fact]
    public void Indisponibilidade_pontual_alerta_as_sessoes_que_caem_nela()
    {
        var professor = Professor();
        professor.Indisponibilidades.Add(new IndisponibilidadeProfessor
        {
            Inicio = new DateTime(2026, 10, 5), Fim = new DateTime(2026, 10, 10), Motivo = "Viagem"
        });
        var ocorrencias = GeradorSessoes.Ocorrencias([Terca18], new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 31)).ToList();

        var conflito = Assert.Single(DetectorConflitos.ParaHorarios(professor, [Terca18], [], ocorrencias));
        Assert.Equal(TipoConflito.ProfessorIndisponivel, conflito.Tipo);
        Assert.Contains("1 sessão", conflito.Descricao);
        Assert.Contains("06/10 18:00", conflito.Descricao);
    }

    [Fact]
    public void Na_agenda_so_os_treinos_sobrepostos_do_mesmo_professor_ficam_marcados()
    {
        var carlos = Guid.NewGuid();
        var ana = Guid.NewGuid();
        Compromisso Treino(Guid professor, int hora, bool cancelado = false) => new(TipoCompromisso.Treino, Guid.NewGuid(), "T",
            new DateTime(2026, 10, 6, hora, 0, 0), new DateTime(2026, 10, 6, hora + 1, 0, 0), null, professor, null, cancelado);

        var a = Treino(carlos, 18);
        var b = Treino(carlos, 18);
        var c = Treino(carlos, 19);
        var d = Treino(ana, 18);
        var e = Treino(carlos, 18, cancelado: true);

        var sobrepostos = DetectorConflitos.SobrepostosPorProfessor([a, b, c, d, e]);
        Assert.Equal(new HashSet<Guid> { a.Id, b.Id }, sobrepostos);
    }

    [Fact]
    public void Outra_turma_no_mesmo_local_e_horario_alerta_e_horario_encostado_nao()
    {
        var sobrepoe = new HorarioOcupado("Sub-13", new HorarioSemanal(DayOfWeek.Tuesday, new TimeOnly(18, 30), new TimeOnly(19, 30)));
        var encostado = new HorarioOcupado("Sub-7", new HorarioSemanal(DayOfWeek.Tuesday, new TimeOnly(19, 0), new TimeOnly(20, 0)));
        var outroDia = new HorarioOcupado("Sub-15", new HorarioSemanal(DayOfWeek.Wednesday, new TimeOnly(18, 0), new TimeOnly(19, 0)));

        var conflito = Assert.Single(DetectorConflitos.LocalOcupado("Quadra Central", [Terca18], [sobrepoe, encostado, outroDia]));
        Assert.Equal(TipoConflito.LocalOcupado, conflito.Tipo);
        Assert.Contains("Quadra Central já recebe a turma Sub-13", conflito.Descricao);
        Assert.Empty(DetectorConflitos.LocalOcupado("Quadra Central", [Terca18], [encostado, outroDia]));
    }

    [Fact]
    public void Na_agenda_os_treinos_sobrepostos_no_mesmo_local_ficam_marcados()
    {
        var quadra = Guid.NewGuid();
        var ginasio = Guid.NewGuid();
        Compromisso Treino(Guid? local, int hora, bool cancelado = false) => new(TipoCompromisso.Treino, Guid.NewGuid(), "T",
            new DateTime(2026, 10, 6, hora, 0, 0), new DateTime(2026, 10, 6, hora + 1, 0, 0), local, Guid.NewGuid(), null, cancelado);

        var a = Treino(quadra, 18);
        var b = Treino(quadra, 18);
        var c = Treino(quadra, 19);
        var d = Treino(ginasio, 18);
        var e = Treino(quadra, 18, cancelado: true);
        var f = Treino(null, 18);

        Assert.Equal(new HashSet<Guid> { a.Id, b.Id }, DetectorConflitos.SobrepostosPorLocal([a, b, c, d, e, f]));
    }
}
