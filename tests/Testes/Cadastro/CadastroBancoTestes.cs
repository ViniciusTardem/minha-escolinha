using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Turmas;
using MinhaEscolinha.Testes.Infraestrutura;
using MinhaEscolinha.Web.Infraestrutura.Desenvolvimento;

namespace MinhaEscolinha.Testes.Cadastro;

/// <summary>
/// Fatia S1 no banco de testes: turma com horários e sessões (pronto do S1), responsável sem duplicidade (CA-1),
/// desligamento (CA-2 e CA-3, a parte do S1), escopo do professor (BL-M08-05) e dado de saúde restrito (BL-M03-10).
/// O relógio está em sábado, 26/09/2026, 12:00 de Brasília: as sessões vão de 29/09 a 31/10.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class CadastroBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private const string CpfMaria = "529.982.247-25";
    private static readonly Guid PessoaGestor = Guid.NewGuid();
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync()
    {
        // O motor de permissões só decide sobre um registro para quem está logado: o gestor dos testes tem uma pessoa.
        _app.Usuario.Id = PessoaGestor;
        _app.Usuario.Nome = "Gestor dos testes";
        return BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private sealed record Base(Guid Escolinha, Guid Local, Guid Professor, Guid Turma);

    private async Task<Guid> EscolinhaAsync(string nome, string email)
    {
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<ServicoPlataforma>().CriarEscolinhaComGestorAsync(
            new NovaEscolinhaRequisicao { Nome = nome, NomeModalidade = "Futsal", GestorNome = $"Gestor {nome}", GestorEmail = email }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return Assert.IsType<EscolinhaCriada>(resultado.Dados).EscolinhaId;
    }

    private Task<T> Gestor<T>(Guid escolinha, Func<IServiceProvider, Task<T>> acao) =>
        _app.NaEscolinhaAsync(escolinha, [PapelAtuacao.Gestor], acao);

    private static Guid Id(ResultadoOperacao r)
    {
        Assert.True(r.Sucesso, r.Mensagem);
        return Assert.IsType<Guid>(r.Dados);
    }

    private async Task<Base> MontarAsync(string nome = "Bola na Rede", string email = "gestor@bolanarede.com")
    {
        var escolinha = await EscolinhaAsync(nome, email);
        var local = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoLocais>()
            .SalvarAsync(new LocalRequisicao { Nome = "Quadra Central", Logradouro = "Rua das Flores", Numero = "10", Cidade = "Londrina", Uf = "PR" })));
        var professor = Id(await Gestor(escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Carlos Treinador", Telefone = "(43) 99999-0000" })));
        var turma = await CriarTurmaAsync(escolinha, "Sub-9", local, professor, DayOfWeek.Tuesday, DayOfWeek.Thursday);
        return new Base(escolinha, local, professor, turma);
    }

    private async Task<TurmaRequisicao> RequisicaoTurmaAsync(Guid escolinha, string nome, Guid local, Guid professor, int hora,
        params DayOfWeek[] dias)
    {
        var r = await Gestor(escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        r.Nome = nome;
        r.LocalId = local;
        r.ProfessorId = professor;
        r.Mensalidade = 150m;
        r.Horarios = dias.Select(d => new HorarioRequisicao { DiaSemana = d, Inicio = new TimeOnly(hora, 0), Fim = new TimeOnly(hora + 1, 0) }).ToList();
        return r;
    }

    private async Task<Guid> CriarTurmaAsync(Guid escolinha, string nome, Guid local, Guid professor, params DayOfWeek[] dias) =>
        Id(await Gestor(escolinha, async sp => await sp.GetRequiredService<ServicoTurmas>()
            .CriarAsync(await RequisicaoTurmaAsync(escolinha, nome, local, professor, 18, dias))));

    private static NovoAlunoRequisicao NovoAluno(string nome, Guid turma, string responsavel, string? cpfResponsavel, string telefone,
        DateOnly? nascimento = null)
    {
        var r = NovoAlunoRequisicao.Vazia(new DateOnly(2026, 9, 26));
        r.Nome = nome;
        r.DataNascimento = nascimento ?? new DateOnly(2017, 4, 12);
        r.TurmaId = turma;
        r.Responsaveis[0].Nome = responsavel;
        r.Responsaveis[0].Cpf = cpfResponsavel;
        r.Responsaveis[0].Telefone = telefone;
        return r;
    }

    private Task<ResultadoOperacao> CriarAlunoAsync(Guid escolinha, NovoAlunoRequisicao r) =>
        Gestor(escolinha, sp => sp.GetRequiredService<ServicoAlunos>().CriarAsync(r));

    // ── Turmas e sessões ──

    [FatoComBanco]
    public async Task A_turma_com_horarios_gera_as_sessoes_como_compromissos_datados()
    {
        var b = await MontarAsync();

        var agenda = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>()
            .ConsultarAsync(new FiltroAgenda { De = new DateOnly(2026, 9, 1), Ate = new DateOnly(2026, 10, 31) }));

        Assert.Equal(10, agenda.Itens.Count);
        Assert.All(agenda.Itens, i =>
        {
            Assert.Equal("Sub-9", i.Turma);
            Assert.Equal("Quadra Central", i.Local);
            Assert.Equal("Carlos Treinador", i.Professor);
            Assert.Equal(SituacaoSessao.Prevista, i.Situacao);
            Assert.Empty(i.Alertas);
        });

        // Recorte por professor e por quadra: a mesma ocupação (BL-M04-08, BL-M04-11).
        var porProfessor = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>()
            .ConsultarAsync(new FiltroAgenda { Recorte = RecorteAgenda.Professor, Id = b.Professor, De = new DateOnly(2026, 10, 1), Ate = new DateOnly(2026, 10, 31) }));
        Assert.Equal(9, porProfessor.Itens.Count);
        var compromissos = await Gestor(b.Escolinha, sp => sp.GetRequiredService<IOcupacaoAgenda>()
            .CompromissosAsync(new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), null, b.Local));
        Assert.Equal(9, compromissos.Count);
    }

    [FatoComBanco]
    public async Task A_geracao_recorrente_nao_duplica_e_empurra_o_horizonte()
    {
        var b = await MontarAsync();

        var denovo = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoSessoes>().GerarHorizonteAsync());
        Assert.Equal(0, denovo.Criadas);

        _app.Tempo.Advance(TimeSpan.FromDays(10)); // 06/10: o horizonte passa a ir até 30/11
        var novembro = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoSessoes>().GerarHorizonteAsync());
        Assert.Equal(8, novembro.Criadas); // terças e quintas de novembro

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(18, await db.Sessoes.IgnoreQueryFilters().CountAsync(s => s.Turma_ID == b.Turma));
    }

    [FatoComBanco]
    public async Task Mudar_o_horario_ajusta_as_sessoes_previstas_e_a_cancelada_fica()
    {
        var b = await MontarAsync();
        var agenda = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>().ConsultarAsync(new FiltroAgenda { De = new DateOnly(2026, 10, 6), Ate = new DateOnly(2026, 10, 6) }));
        var sessao = Assert.Single(agenda.Itens);

        var semMotivo = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoSessoes>().CancelarAsync(new CancelarSessaoRequisicao { SessaoId = sessao.SessaoId }));
        Assert.False(semMotivo.Sucesso);
        var cancelou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoSessoes>()
            .CancelarAsync(new CancelarSessaoRequisicao { SessaoId = sessao.SessaoId, Motivo = "Quadra interditada" }));
        Assert.True(cancelou.Sucesso, cancelou.Mensagem);
        Assert.False((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoSessoes>()
            .CancelarAsync(new CancelarSessaoRequisicao { SessaoId = sessao.SessaoId, Motivo = "De novo" }))).Sucesso);

        var edicao = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().ParaEdicaoAsync(b.Turma));
        edicao!.Horarios.First(h => h.DiaSemana == DayOfWeek.Tuesday).Inicio = new TimeOnly(17, 0);
        var editou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().EditarAsync(edicao));
        Assert.True(editou.Sucesso, editou.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var tercas = await db.Sessoes.IgnoreQueryFilters().Where(s => s.Turma_ID == b.Turma).ToListAsync();
        tercas = tercas.Where(s => s.Data.DayOfWeek == DayOfWeek.Tuesday).ToList();
        var cancelada = Assert.Single(tercas, s => s.Situacao == SituacaoSessao.Cancelada);
        Assert.Equal(18, cancelada.Inicio.Hour);
        Assert.Equal("Quadra interditada", cancelada.MotivoCancelamento);
        Assert.All(tercas.Where(s => s.Situacao == SituacaoSessao.Prevista), s => Assert.Equal(17, s.Inicio.Hour));
    }

    /// <summary>
    /// Achado na validação do S3: incluir um horário numa turma que já existe gravava cada sessão dele duas vezes (o EF já
    /// punha o horário novo na coleção da turma rastreada, e o serviço punha de novo) e a gravação falhava no índice único.
    /// </summary>
    [FatoComBanco]
    public async Task Incluir_um_horario_na_turma_gera_as_sessoes_dele_uma_vez_so()
    {
        var b = await MontarAsync();
        var edicao = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().ParaEdicaoAsync(b.Turma));
        edicao!.Horarios.Add(new HorarioRequisicao { DiaSemana = DayOfWeek.Saturday, Inicio = new TimeOnly(9, 0), Fim = new TimeOnly(10, 0) });
        var editou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().EditarAsync(edicao));
        Assert.True(editou.Sucesso, editou.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var sabados = (await db.Sessoes.IgnoreQueryFilters().Where(s => s.Turma_ID == b.Turma).ToListAsync())
            .Where(s => s.Data.DayOfWeek == DayOfWeek.Saturday).ToList();
        Assert.NotEmpty(sabados);
        Assert.Equal(sabados.Count, sabados.Select(s => s.Data).Distinct().Count());
        Assert.All(sabados, s => Assert.Equal(9, s.Inicio.Hour));
    }

    [FatoComBanco]
    public async Task A_turma_nova_nasce_com_os_valores_de_turma_da_escolinha_lidos_na_hora()
    {
        var b = await MontarAsync();
        Assert.Equal(20, (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync())).Capacidade);

        var alterou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoParametros>().AlterarAsync(
            new AlterarParametrosRequisicao { Valores = { [ChavesParametro.TurmaCapacidade] = "25", [ChavesParametro.TurmaMensalidade] = "180" } }));
        Assert.True(alterou.Sucesso, alterou.Mensagem);

        // CA-18: sem reiniciar, a próxima turma já nasce com os valores novos.
        var nova = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().NovaAsync());
        Assert.Equal(25, nova.Capacidade);
        Assert.Equal(180m, nova.Mensalidade);
    }

    [FatoComBanco]
    public async Task Horario_ja_ocupado_pelo_professor_alerta_e_so_salva_confirmado()
    {
        var b = await MontarAsync();
        // Em outro local, para o alerta ser só o do professor (o do local tem teste próprio).
        var ginasio = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao { Nome = "Ginásio" })));
        var r = await RequisicaoTurmaAsync(b.Escolinha, "Sub-11", ginasio, b.Professor, 18, DayOfWeek.Tuesday);

        var alerta = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(r));
        Assert.False(alerta.Sucesso);
        var conflitos = Assert.IsType<ConflitosTurma>(alerta.Dados);
        Assert.Contains("Sub-9", Assert.Single(conflitos.Conflitos).Descricao);

        r.ConfirmarConflitos = true;
        var salvou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(r));
        Assert.True(salvou.Sucesso, salvou.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.True(await db.RegistrosAlteracao.IgnoreQueryFilters().AnyAsync(x => x.Registro_ID == (Guid)salvou.Dados! && x.Campo == "Alertas de agenda confirmados"));
        var agenda = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>().ConsultarAsync(new FiltroAgenda { De = new DateOnly(2026, 9, 29), Ate = new DateOnly(2026, 9, 29) }));
        Assert.Equal(2, agenda.Itens.Count);
        Assert.All(agenda.Itens, i => Assert.Contains("Professor com outro treino no mesmo horário", i.Alertas));
        Assert.All(agenda.Itens, i => Assert.DoesNotContain("Local com outro treino no mesmo horário", i.Alertas));
    }

    [FatoComBanco]
    public async Task Local_ja_ocupado_por_outra_turma_alerta_e_so_salva_confirmado()
    {
        var b = await MontarAsync();
        var ana = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoProfessores>()
            .CriarAsync(new ProfessorRequisicao { Nome = "Ana Treinadora" })));
        var r = await RequisicaoTurmaAsync(b.Escolinha, "Sub-13", b.Local, ana, 18, DayOfWeek.Thursday);

        // P31: a quadra já recebe a Sub-9 na quinta às 18h. É alerta, não bloqueio: pode ser meia quadra (R9).
        var alerta = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(r));
        Assert.False(alerta.Sucesso);
        var conflito = Assert.Single(Assert.IsType<ConflitosTurma>(alerta.Dados).Conflitos);
        Assert.Equal(TipoConflito.LocalOcupado, conflito.Tipo);
        Assert.Contains("Quadra Central já recebe a turma Sub-9", conflito.Descricao);

        r.ConfirmarConflitos = true;
        var salvou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().CriarAsync(r));
        Assert.True(salvou.Sucesso, salvou.Mensagem);

        var agenda = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>()
            .ConsultarAsync(new FiltroAgenda { De = new DateOnly(2026, 10, 1), Ate = new DateOnly(2026, 10, 1) }));
        Assert.Equal(2, agenda.Itens.Count);
        Assert.All(agenda.Itens, i =>
        {
            Assert.Contains("Local com outro treino no mesmo horário", i.Alertas);
            Assert.DoesNotContain("Professor com outro treino no mesmo horário", i.Alertas);
        });
    }

    [FatoComBanco]
    public async Task Turma_com_matricula_ativa_nao_encerra_e_sem_matricula_encerra_tirando_as_sessoes()
    {
        var b = await MontarAsync();
        var aluno = Id(await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777")));

        var barrada = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().EncerrarAsync(new EncerrarTurmaRequisicao { TurmaId = b.Turma, Motivo = "Fim" }));
        Assert.False(barrada.Sucesso);

        var desligou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(new MudarSituacaoRequisicao
        {
            AlunoId = aluno, Para = SituacaoAluno.Desistente, Data = new DateOnly(2026, 9, 26), Motivo = "Mudou de cidade"
        }));
        Assert.True(desligou.Sucesso, desligou.Mensagem);
        var encerrou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().EncerrarAsync(new EncerrarTurmaRequisicao { TurmaId = b.Turma, Motivo = "Fim do projeto" }));
        Assert.True(encerrou.Sucesso, encerrou.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(0, await db.Sessoes.IgnoreQueryFilters().CountAsync(s => s.Turma_ID == b.Turma && s.Situacao == SituacaoSessao.Prevista));
    }

    // ── Alunos e responsáveis ──

    [FatoComBanco]
    public async Task O_segundo_filho_reaproveita_o_responsavel_pelo_cpf_sem_duplicidade()
    {
        var b = await MontarAsync();
        var pedro = await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777"));
        Assert.True(pedro.Sucesso, pedro.Mensagem);

        // CA-1: o mesmo CPF, digitado de novo no cadastro do irmão, não cria outra Maria.
        var ana = await CriarAlunoAsync(b.Escolinha, NovoAluno("Ana Lima", b.Turma, "Maria L.", "52998224725", "43911112222", new DateOnly(2019, 2, 3)));
        Assert.True(ana.Sucesso, ana.Mensagem);
        Assert.Contains("reaproveitado", ana.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var maria = await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Escolinha_ID == b.Escolinha);
        Assert.Equal("Maria Lima", maria.Nome);
        Assert.Equal("+5543988887777", maria.Telefone);
        Assert.Equal(2, await db.VinculosResponsavel.IgnoreQueryFilters().CountAsync(v => v.Responsavel_ID == maria.Responsavel_ID));

        var ficha = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync((Guid)ana.Dados!));
        Assert.Equal(["Pedro Lima"], Assert.Single(ficha!.Responsaveis).OutrosAlunos);
    }

    [FatoComBanco]
    public async Task Mesmo_celular_ou_mesmo_nome_so_sugere_e_quem_cadastra_decide()
    {
        var b = await MontarAsync();
        Id(await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777")));

        // Celular compartilhado (o pai usa o mesmo número): sem CPF, não junta sozinho.
        var r = NovoAluno("Ana Lima", b.Turma, "João Lima", null, "(43) 98888-7777", new DateOnly(2019, 2, 3));
        var sugestao = await CriarAlunoAsync(b.Escolinha, r);
        Assert.False(sugestao.Sucesso);
        var duplicidade = Assert.IsType<DuplicidadeResponsavel>(sugestao.Dados);
        Assert.Equal(0, duplicidade.Indice);
        Assert.Equal("Maria Lima", Assert.Single(duplicidade.Candidatos).Nome);

        r.Responsaveis[0].ConfirmarNovo = true;
        Assert.True((await CriarAlunoAsync(b.Escolinha, r)).Sucesso);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(2, await db.Responsaveis.IgnoreQueryFilters().CountAsync(x => x.Escolinha_ID == b.Escolinha));
    }

    [FatoComBanco]
    public async Task Aluno_com_mesmo_cpf_e_barrado_e_com_mesmo_nome_e_nascimento_pede_confirmacao()
    {
        var b = await MontarAsync();
        var primeiro = NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777");
        primeiro.Cpf = "111.444.777-35";
        Id(await CriarAlunoAsync(b.Escolinha, primeiro));

        var mesmoCpf = NovoAluno("Outro Nome", b.Turma, "Maria Lima", CpfMaria, "43988887777");
        mesmoCpf.Cpf = "11144477735";
        var barrado = await CriarAlunoAsync(b.Escolinha, mesmoCpf);
        Assert.False(barrado.Sucesso);
        Assert.Equal(nameof(NovoAlunoRequisicao.Cpf), barrado.Campo);

        var homonimo = NovoAluno("pedro  LIMA", b.Turma, "Maria Lima", CpfMaria, "43988887777");
        var sugestao = await CriarAlunoAsync(b.Escolinha, homonimo);
        Assert.IsType<DuplicidadeAluno>(sugestao.Dados);
        homonimo.ConfirmarAlunoNovo = true;
        Assert.True((await CriarAlunoAsync(b.Escolinha, homonimo)).Sucesso);
    }

    [FatoComBanco]
    public async Task Desligar_exige_data_e_motivo_encerra_as_matriculas_e_preserva_o_historico()
    {
        var b = await MontarAsync();
        var outra = await CriarTurmaAsync(b.Escolinha, "Sub-11", b.Local, b.Professor, DayOfWeek.Saturday);
        var pedro = Id(await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777")));
        var segunda = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMatriculas>()
            .NovaAsync(new NovaMatriculaRequisicao { AlunoId = pedro, TurmaId = outra, DataInicio = new DateOnly(2026, 9, 26), Mensalidade = 90m }));
        Assert.True(segunda.Sucesso, segunda.Mensagem);
        Assert.False((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoMatriculas>()
            .NovaAsync(new NovaMatriculaRequisicao { AlunoId = pedro, TurmaId = outra, DataInicio = new DateOnly(2026, 9, 26) }))).Sucesso);

        // CA-2: sem motivo não desliga.
        var semMotivo = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(
            new MudarSituacaoRequisicao { AlunoId = pedro, Para = SituacaoAluno.Desistente, Data = new DateOnly(2026, 9, 26) }));
        Assert.False(semMotivo.Sucesso);

        var desligou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(
            new MudarSituacaoRequisicao { AlunoId = pedro, Para = SituacaoAluno.Desistente, Data = new DateOnly(2026, 9, 26), Motivo = "Mudou de cidade" }));
        Assert.True(desligou.Sucesso, desligou.Mensagem);

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var matriculas = await db.Matriculas.IgnoreQueryFilters().Where(m => m.Aluno_ID == pedro).ToListAsync();
            Assert.Equal(2, matriculas.Count);
            Assert.All(matriculas, m =>
            {
                Assert.Equal(SituacaoMatricula.Encerrada, m.Situacao);
                Assert.Equal(new DateOnly(2026, 9, 26), m.DataFim);
            });
        }

        // CA-3: some da turma e das listas de trabalho, mas continua no histórico.
        var turmas = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().ListarAsync(false, null));
        Assert.All(turmas, t => Assert.Equal(0, t.Ocupadas));
        var lista = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ListarAsync(new FiltroAlunos(), 1));
        Assert.Empty(lista.Itens);
        var desistentes = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ListarAsync(new FiltroAlunos { Situacao = FiltroSituacaoAluno.Desistente }, 1));
        Assert.Equal("Pedro Lima", Assert.Single(desistentes.Itens).Nome);
        var ficha = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoTurmas>().ObterFichaAsync(b.Turma));
        var naTurma = Assert.Single(ficha!.Alunos);
        Assert.Equal(SituacaoMatricula.Encerrada, naTurma.SituacaoMatricula);
        var historico = (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(pedro)))!.Historico;
        Assert.Contains(historico, h => h.Campo == "Situação" && h.Motivo == "Mudou de cidade");

        // Desistente volta pela rematrícula, com matrícula nova.
        var rematricula = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(
            new MudarSituacaoRequisicao { AlunoId = pedro, Para = SituacaoAluno.Ativo, Data = new DateOnly(2026, 10, 5), TurmaId = b.Turma }));
        Assert.True(rematricula.Sucesso, rematricula.Mensagem);
        await using var depois = BancoDeTestes.NovoContextoSemEscolinha();
        Assert.Equal(3, await depois.Matriculas.IgnoreQueryFilters().CountAsync(m => m.Aluno_ID == pedro));
        Assert.Equal(new DateOnly(2026, 10, 5), (await depois.Alunos.IgnoreQueryFilters().SingleAsync(a => a.Aluno_ID == pedro)).DataMatricula);
    }

    [FatoComBanco]
    public async Task Aluno_em_pausa_e_desligado_direto_sem_reativar()
    {
        var b = await MontarAsync();
        var pedro = Id(await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777")));
        _app.Tempo.Advance(TimeSpan.FromDays(10)); // 06/10
        Task<ResultadoOperacao> Mudar(SituacaoAluno para, DateOnly data, string? motivo = null) => Gestor(b.Escolinha, sp =>
            sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(new MudarSituacaoRequisicao { AlunoId = pedro, Para = para, Data = data, Motivo = motivo }));

        var pausou = await Mudar(SituacaoAluno.Inativo, new DateOnly(2026, 10, 1));
        Assert.True(pausou.Sucesso, pausou.Mensagem);

        // P28: o atalho existe, mas o desligamento não volta para antes do início da pausa.
        var antesDaPausa = await Mudar(SituacaoAluno.Desistente, new DateOnly(2026, 9, 30), "Mudou de cidade");
        Assert.False(antesDaPausa.Sucesso);
        Assert.Equal(nameof(MudarSituacaoRequisicao.Data), antesDaPausa.Campo);

        var desligou = await Mudar(SituacaoAluno.Desistente, new DateOnly(2026, 10, 5), "Mudou de cidade");
        Assert.True(desligou.Sucesso, desligou.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var matricula = await db.Matriculas.IgnoreQueryFilters().SingleAsync(m => m.Aluno_ID == pedro);
        Assert.Equal(SituacaoMatricula.Encerrada, matricula.Situacao);
        Assert.Equal(new DateOnly(2026, 10, 5), matricula.DataFim);
        Assert.Equal(SituacaoAluno.Desistente, (await db.Alunos.IgnoreQueryFilters().SingleAsync(a => a.Aluno_ID == pedro)).Situacao);
    }

    [FatoComBanco]
    public async Task Um_pagador_por_aluno_e_o_ultimo_responsavel_nao_sai()
    {
        var b = await MontarAsync();
        var pedro = Id(await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777")));

        var vinculou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoResponsaveis>().VincularAsync(pedro,
            new ResponsavelDoAlunoRequisicao { Nome = "João Lima", Telefone = "43977776666", Parentesco = Parentesco.Pai, Pagador = true }));
        Assert.True(vinculou.Sucesso, vinculou.Mensagem);

        var ficha = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(pedro));
        Assert.Equal("João Lima", Assert.Single(ficha!.Responsaveis, r => r.Pagador).Nome);
        Assert.True(ficha.Responsaveis.Single(r => r.Nome == "João Lima").RecebeCobranca);

        var joao = ficha.Responsaveis.Single(r => r.Nome == "João Lima").VinculoId;
        var maria = ficha.Responsaveis.Single(r => r.Nome == "Maria Lima").VinculoId;
        Assert.True((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoResponsaveis>().DesvincularAsync(joao))).Sucesso);
        Assert.False((await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoResponsaveis>().DesvincularAsync(maria))).Sucesso);
    }

    [FatoComBanco]
    public async Task Consentimentos_separados_e_a_revogacao_da_imagem_nao_para_a_operacao()
    {
        var b = await MontarAsync();
        var r = NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777");
        r.Consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.TratamentoDados).Resposta = RespostaConsentimento.Concedido;
        r.Consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.UsoImagem).Resposta = RespostaConsentimento.Concedido;
        var pedro = Id(await CriarAlunoAsync(b.Escolinha, r));

        var ficha = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(pedro));
        Assert.Equal(EstadoConsentimento.Concedido, ficha!.EstadoConsentimentos[FinalidadeConsentimento.TratamentoDados]);
        var imagem = ficha.Consentimentos.Single(c => c.Finalidade == FinalidadeConsentimento.UsoImagem);
        Assert.Equal("Maria Lima", imagem.Responsavel);
        Assert.Equal(TermosConsentimento.Obter(FinalidadeConsentimento.UsoImagem).Versao, imagem.VersaoTermo);
        Assert.Equal(new DateTime(2026, 9, 26, 12, 0, 0), imagem.RegistradoEm);

        var revogou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoConsentimentos>()
            .RevogarAsync(new RevogarConsentimentoRequisicao { ConsentimentoId = imagem.Id, Motivo = "Pedido da mãe" }));
        Assert.True(revogou.Sucesso, revogou.Mensagem);

        ficha = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(pedro));
        Assert.Equal(EstadoConsentimento.Revogado, ficha!.EstadoConsentimentos[FinalidadeConsentimento.UsoImagem]);
        Assert.Equal(EstadoConsentimento.Concedido, ficha.EstadoConsentimentos[FinalidadeConsentimento.TratamentoDados]);
        Assert.Equal(SituacaoAluno.Ativo, ficha.Situacao);
        Assert.True(ficha.TemMatriculaAtiva);
    }

    [FatoComBanco]
    public async Task A_massa_de_demonstracao_monta_o_cenario_do_piloto_com_um_cadastro_por_responsavel()
    {
        var b = await MontarAsync();
        // Com duas modalidades a turma nova vem sem modalidade (achado no banco de desenvolvimento): a massa escolhe o futsal.
        var futebol = ModalidadeRequisicao.NovaDoPerfil(Domain.Configuracao.PerfilModalidade.Coletiva);
        futebol.Nome = "Futebol de campo";
        Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoModalidades>().CriarAsync(futebol)));

        var resultado = await Gestor(b.Escolinha, sp => ActivatorUtilities.CreateInstance<MassaDemonstracao>(sp).GerarAsync());
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        Assert.DoesNotContain("Avisos", resultado.Mensagem);
        Assert.False((await Gestor(b.Escolinha, sp => ActivatorUtilities.CreateInstance<MassaDemonstracao>(sp).GerarAsync())).Sucesso);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var alunos = await db.Alunos.IgnoreQueryFilters().Where(a => a.Escolinha_ID == b.Escolinha).ToListAsync();
        Assert.Equal(60, alunos.Count);
        Assert.Equal(5, alunos.Count(a => a.Situacao == SituacaoAluno.Trial));
        Assert.Equal(2, alunos.Count(a => a.Situacao == SituacaoAluno.Inativo));
        Assert.Single(alunos, a => a.Situacao == SituacaoAluno.Desistente);

        // 38 mães e 13 pais: irmãos (2 ou 3 filhos) reaproveitam o cadastro, com CPF ou pela sugestão de cadastro parecido (CA-1).
        Assert.Equal(51, await db.Responsaveis.IgnoreQueryFilters().CountAsync(r => r.Escolinha_ID == b.Escolinha));
        var vinculosPorResponsavel = await db.VinculosResponsavel.IgnoreQueryFilters().Where(v => v.Escolinha_ID == b.Escolinha)
            .GroupBy(v => v.Responsavel_ID).Select(g => g.Count()).ToListAsync();
        Assert.Equal(5, vinculosPorResponsavel.Count(n => n == 3));
        Assert.Equal(4, await db.Turmas.IgnoreQueryFilters().CountAsync(t => t.Escolinha_ID == b.Escolinha));
    }

    // ── Escopo do professor, saúde e isolamento ──

    [FatoComBanco]
    public async Task O_professor_ve_so_as_proprias_turmas_alunos_e_sessoes_e_so_o_necessario_da_saude()
    {
        var b = await MontarAsync();

        // Paula já atua na escolinha (tem vínculo) e vira professora reaproveitando a pessoa (BL-M08-01).
        var paula = Guid.NewGuid();
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            db.Pessoas.Add(new Pessoa { Pessoa_ID = paula, Nome = "Paula Souza" });
            db.VinculosAtuacao.Add(new VinculoAtuacao { Escolinha_ID = b.Escolinha, Pessoa_ID = paula, Papel = PapelAtuacao.Responsavel });
            await db.SaveChangesAsync();
        }
        var preparado = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoProfessores>().PrepararAsync(OrigemCadastroProfessor.PessoaComAcesso, paula));
        Assert.Equal("Paula Souza", preparado!.Nome);
        var professoraPaula = Id(await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoProfessores>().CriarAsync(preparado)));
        var turmaPaula = await CriarTurmaAsync(b.Escolinha, "Sub-13", b.Local, professoraPaula, DayOfWeek.Monday);

        var doCarlos = NovoAluno("Bruno Alves", b.Turma, "Rita Alves", null, "43955554444");
        var bruno = Id(await CriarAlunoAsync(b.Escolinha, doCarlos));
        var daPaula = NovoAluno("Ana Costa", turmaPaula, "Lia Costa", null, "43933332222");
        daPaula.Alergias = "Amendoim";
        daPaula.ObservacoesMedicas = "Asma controlada";
        daPaula.ContatoEmergenciaNome = "Tio Rui";
        var ana = Id(await CriarAlunoAsync(b.Escolinha, daPaula));

        // O irmão da Ana está na turma do Carlos: a Paula não pode ficar sabendo dele (achado da validação no navegador).
        Guid lia;
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            Assert.True(await db.VinculosAtuacao.IgnoreQueryFilters().AnyAsync(v => v.Pessoa_ID == paula && v.Papel == PapelAtuacao.Professor));
            lia = (await db.Responsaveis.IgnoreQueryFilters().SingleAsync(r => r.Nome == "Lia Costa")).Responsavel_ID;
        }
        var irmao = NovoAluno("Beto Costa", b.Turma, "", null, "", new DateOnly(2015, 3, 3));
        irmao.Responsaveis[0] = new ResponsavelDoAlunoRequisicao { ResponsavelId = lia, Pagador = true, RecebeNotificacao = true };
        Id(await CriarAlunoAsync(b.Escolinha, irmao));

        _app.Usuario.Id = paula;
        Task<T> ComoPaula<T>(Func<IServiceProvider, Task<T>> acao) => _app.NaEscolinhaAsync(b.Escolinha, [PapelAtuacao.Professor], acao);

        Assert.Equal(["Sub-13"], (await ComoPaula(sp => sp.GetRequiredService<ServicoTurmas>().ListarAsync(false, null))).Select(t => t.Nome));
        Assert.All(await ComoPaula(sp => sp.GetRequiredService<ServicoTurmas>().ListarAsync(false, null)), t => Assert.Null(t.Mensalidade));
        Assert.Equal(["Ana Costa"], (await ComoPaula(sp => sp.GetRequiredService<ServicoAlunos>().ListarAsync(new FiltroAlunos(), 1))).Itens.Select(a => a.Nome));
        Assert.Null(await ComoPaula(sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(bruno)));
        Assert.Null(await ComoPaula(sp => sp.GetRequiredService<ServicoTurmas>().ObterFichaAsync(b.Turma)));

        var fichaAna = await ComoPaula(sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(ana));
        Assert.NotNull(fichaAna);
        Assert.Equal(AcessoSaude.Treino, fichaAna.AcessoSaude);
        Assert.Equal("Amendoim", fichaAna.Saude!.Alergias);
        Assert.Equal("Tio Rui", fichaAna.Saude.ContatoEmergenciaNome);
        Assert.Null(fichaAna.Saude.ObservacoesMedicas);
        Assert.False(fichaAna.Pode.MudarSituacao);
        Assert.False(fichaAna.Pode.EditarSaude);
        Assert.All(fichaAna.Matriculas, m => Assert.Null(m.Mensalidade));
        Assert.All(fichaAna.Responsaveis, r => Assert.Null(r.Cpf));
        Assert.Empty(Assert.Single(fichaAna.Responsaveis).OutrosAlunos);
        var listaResponsaveis = await ComoPaula(sp => sp.GetRequiredService<ServicoResponsaveis>().ListarAsync(null, 1));
        Assert.Equal(["Ana Costa"], Assert.Single(listaResponsaveis.Itens).Alunos);
        var fichaLia = await ComoPaula(sp => sp.GetRequiredService<ServicoResponsaveis>().ObterFichaAsync(lia));
        Assert.Equal(["Ana Costa"], fichaLia!.Alunos.Select(a => a.Aluno));

        var agenda = await ComoPaula(sp => sp.GetRequiredService<ServicoAgenda>().ConsultarAsync(new FiltroAgenda { De = new DateOnly(2026, 9, 1), Ate = new DateOnly(2026, 10, 31) }));
        Assert.All(agenda.Itens, i => Assert.Equal("Sub-13", i.Turma));
        Assert.NotEmpty(agenda.Itens);

        var sessaoCarlos = (await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAgenda>()
            .ConsultarAsync(new FiltroAgenda { Id = b.Turma, De = new DateOnly(2026, 9, 29), Ate = new DateOnly(2026, 9, 29) }))).Itens.Single();
        Assert.False((await ComoPaula(sp => sp.GetRequiredService<ServicoSessoes>()
            .CancelarAsync(new CancelarSessaoRequisicao { SessaoId = sessaoCarlos.SessaoId, Motivo = "Chuva" }))).Sucesso);
        Assert.True((await ComoPaula(sp => sp.GetRequiredService<ServicoSessoes>()
            .CancelarAsync(new CancelarSessaoRequisicao { SessaoId = agenda.Itens[0].SessaoId, Motivo = "Chuva" }))).Sucesso);
        Assert.False((await ComoPaula(sp => sp.GetRequiredService<ServicoAlunos>().MudarSituacaoAsync(
            new MudarSituacaoRequisicao { AlunoId = ana, Para = SituacaoAluno.Inativo, Data = new DateOnly(2026, 9, 26) }))).Sucesso);

        _app.Usuario.Id = PessoaGestor;
        var fichaGestor = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(ana));
        Assert.Equal(AcessoSaude.Completo, fichaGestor!.AcessoSaude);
        Assert.Equal("Asma controlada", fichaGestor.Saude!.ObservacoesMedicas);
    }

    [FatoComBanco]
    public async Task O_registro_de_alteracoes_nao_guarda_o_conteudo_da_saude()
    {
        var b = await MontarAsync();
        var pedro = Id(await CriarAlunoAsync(b.Escolinha, NovoAluno("Pedro Lima", b.Turma, "Maria Lima", CpfMaria, "43988887777")));
        var saude = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().SaudeParaEdicaoAsync(pedro));
        saude!.Alergias = "Dipirona";
        var salvou = await Gestor(b.Escolinha, sp => sp.GetRequiredService<ServicoAlunos>().EditarSaudeAsync(saude));
        Assert.True(salvou.Sucesso, salvou.Mensagem);

        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        var registros = await db.RegistrosAlteracao.IgnoreQueryFilters().Where(x => x.Registro_ID == pedro).ToListAsync();
        Assert.Contains(registros, x => x.Campo == "Saúde");
        Assert.DoesNotContain(registros, x => (x.ValorAnterior + x.ValorNovo + x.Motivo).Contains("Dipirona"));
    }

    [FatoComBanco]
    public async Task A_escolinha_B_nao_ve_turma_nem_aluno_da_A()
    {
        var a = await MontarAsync();
        var pedro = Id(await CriarAlunoAsync(a.Escolinha, NovoAluno("Pedro Lima", a.Turma, "Maria Lima", CpfMaria, "43988887777")));
        var escolinhaB = await EscolinhaAsync("Escolinha B", "gestor@b.com");

        Assert.Empty(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoTurmas>().ListarAsync(true, null)));
        Assert.Null(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoTurmas>().ObterFichaAsync(a.Turma)));
        Assert.Null(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoAlunos>().ObterFichaAsync(pedro)));
        Assert.Empty((await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoAlunos>().ListarAsync(new FiltroAlunos { Situacao = FiltroSituacaoAluno.Todos }, 1))).Itens);
        Assert.Empty(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoResponsaveis>().BuscarAsync("Maria")));

        // O mesmo CPF de responsável é outra ficha na escolinha B (a B nunca vê o que a A digitou).
        var localB = Id(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoLocais>().SalvarAsync(new LocalRequisicao { Nome = "Ginásio" })));
        var professorB = Id(await Gestor(escolinhaB, sp => sp.GetRequiredService<ServicoProfessores>().CriarAsync(new ProfessorRequisicao { Nome = "Beto" })));
        var turmaB = await CriarTurmaAsync(escolinhaB, "Sub-9", localB, professorB, DayOfWeek.Wednesday);
        var outro = await CriarAlunoAsync(escolinhaB, NovoAluno("Caio", turmaB, "Maria Lima", CpfMaria, "43988887777"));
        Assert.True(outro.Sucesso, outro.Mensagem);
        Assert.DoesNotContain("reaproveitado", outro.Mensagem);
    }
}
