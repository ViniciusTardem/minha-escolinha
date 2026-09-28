using System.Globalization;
using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Turmas;

/// <summary>Quadras e locais de treino (spec §5, consumidos pela turma e pelo evento).</summary>
public class ServicoLocais(ITurmasRepositorio repositorio, RegistroDeAlteracoes registro, IValidator<LocalRequisicao> validador)
{
    public async Task<IReadOnlyList<LocalResposta>> ListarAsync(CancellationToken ct = default)
    {
        var locais = await repositorio.ListarLocaisAsync(ct);
        var turmas = await repositorio.ListarTurmasAsync(EscopoTurmas.Tudo, incluirEncerradas: false, ct);
        var porLocal = turmas.GroupBy(t => t.Local_ID).ToDictionary(g => g.Key, g => g.Count());
        return locais.Select(l => new LocalResposta(l.Local_ID, l.Nome, l.Endereco, l.Capacidade, l.Observacao,
            porLocal.GetValueOrDefault(l.Local_ID))).ToList();
    }

    public async Task<LocalRequisicao?> ParaEdicaoAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterLocalAsync(id, ct) is not { } local) return null;
        var r = new LocalRequisicao { Id = local.Local_ID, Nome = local.Nome, Capacidade = local.Capacidade, Observacao = local.Observacao };
        Cadastros.PreencherEndereco(r, local.Endereco);
        return r;
    }

    public async Task<ResultadoOperacao> SalvarAsync(LocalRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        var nome = r.Nome.Trim();
        if (await repositorio.NomeLocalEmUsoAsync(nome, r.Id, ct))
            return ResultadoOperacao.Erro($"Já existe o local {nome}.", nameof(LocalRequisicao.Nome));

        Local local;
        string? antes = null;
        if (r.Id is { } id)
        {
            if (await repositorio.ObterLocalAsync(id, ct) is not { } existente) return ResultadoOperacao.Erro("Local não encontrado.");
            local = existente;
            antes = Descrever(local);
        }
        else
        {
            local = new Local { Local_ID = Guid.NewGuid() };
            repositorio.AdicionarLocal(local);
        }

        local.Nome = nome;
        local.Capacidade = r.Capacidade;
        local.Observacao = Cadastros.Vazio(r.Observacao);
        local.Endereco = Cadastros.Endereco(r);

        if (antes is null) registro.Registrar(nameof(Local), local.Local_ID, "Criação", null, Descrever(local));
        else if (!registro.SeMudou(nameof(Local), local.Local_ID, "Cadastro", antes, Descrever(local))) return ResultadoOperacao.Ok("Nada mudou.");

        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok(antes is null ? $"Local {local.Nome} cadastrado." : $"Local {local.Nome} atualizado.", local.Local_ID);
    }

    private static string Descrever(Local l) =>
        $"{l.Nome} · {(string.IsNullOrEmpty(l.Endereco.Resumo()) ? "sem endereço" : l.Endereco.Resumo())}" +
        (l.Capacidade is { } c ? $" · {c} pessoas" : string.Empty);
}

public record OpcaoCadastro(Guid Id, string Nome, string? Detalhe = null);

/// <summary>As listas dos selects do formulário da turma.</summary>
public record OpcoesTurma(IReadOnlyList<OpcaoCadastro> Modalidades, IReadOnlyList<OpcaoCadastro> Locais, IReadOnlyList<OpcaoCadastro> Professores);

public record PermissoesTurma(bool Editar, bool CancelarSessao, bool VerValores, bool VerAlunos);

/// <summary>A ficha da turma: dados, horários, composição (BL-M04-03) e sessões (BL-M04-04).</summary>
public record FichaTurma(
    Guid Id,
    string Nome,
    string Modalidade,
    string? Categoria,
    string Local,
    string? EnderecoLocal,
    Guid ProfessorId,
    string Professor,
    int Capacidade,
    int Ocupadas,
    decimal? Mensalidade,
    int MinimoConfirmados,
    int? MetaFrequenciaPercentual,
    decimal FrequenciaMinimaEscolinha,
    DateOnly DataInicio,
    SituacaoTurma Situacao,
    DateTime? EncerradaEm,
    string? MotivoEncerramento,
    IReadOnlyList<HorarioSemanal> Horarios,
    IReadOnlyList<AlunoDaTurma> Alunos,
    IReadOnlyList<SessaoResposta> ProximasSessoes,
    IReadOnlyList<SessaoResposta> Canceladas,
    PermissoesTurma Pode)
{
    public int Inativos => Alunos.Count(a => a.SituacaoMatricula == Domain.Alunos.SituacaoMatricula.Ativa && a.Situacao == Domain.Alunos.SituacaoAluno.Inativo);
}

/// <summary>
/// Cadastro e edição de turma (BL-M04-01), composição e ocupação (BL-M04-03) e o alerta de conflito do professor e do local
/// ao marcar treino (BL-M04-09 e P31). Salvar a turma sincroniza as sessões no mesmo SaveChanges (BL-M04-04).
/// </summary>
public class ServicoTurmas(
    ITurmasRepositorio repositorio,
    IEquipeRepositorio equipe,
    IConfiguracaoRepositorio configuracao,
    IAlunosRepositorio alunos,
    IParametros parametros,
    IEscopoDeLeitura escopo,
    IAutorizacao autorizacao,
    ServicoSessoes sessoes,
    RegistroDeAlteracoes registro,
    Mensageria.EmissorGatilhos emissor,
    IRelogioBrasil relogio,
    IValidator<TurmaRequisicao> validador)
{
    public const string Entidade = nameof(Turma);
    private static readonly CultureInfo PtBr = new("pt-BR");

    public async Task<IReadOnlyList<TurmaResumo>> ListarAsync(bool incluirEncerradas, Guid? modalidadeId, CancellationToken ct = default)
    {
        var alcance = await escopo.TurmasAsync(Recurso.CadastroAlunosResponsaveis, ct);
        var turmas = await repositorio.ListarTurmasAsync(alcance, incluirEncerradas, ct);
        var ocupacao = await repositorio.OcupacaoAsync(ct);
        var modalidades = (await configuracao.ListarModalidadesAsync(ct)).ToDictionary(m => m.Modalidade_ID, m => m.Nome);
        var verValores = autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler);

        return turmas
            .Where(t => modalidadeId is null || t.Modalidade_ID == modalidadeId)
            .OrderBy(t => t.Situacao).ThenBy(t => t.Nome)
            .Select(t => new TurmaResumo(t.Turma_ID, t.Nome, modalidades.GetValueOrDefault(t.Modalidade_ID) ?? "—", t.Categoria,
                t.Professor?.Nome ?? "—", t.Local?.Nome ?? "—", t.ResumoHorarios(), ocupacao.GetValueOrDefault(t.Turma_ID),
                t.Capacidade, verValores ? t.Mensalidade : null, t.Situacao))
            .ToList();
    }

    public async Task<OpcoesTurma> OpcoesAsync(CancellationToken ct = default)
    {
        var modalidades = await configuracao.ListarModalidadesAsync(ct);
        var locais = await repositorio.ListarLocaisAsync(ct);
        var professores = await equipe.ListarProfessoresAsync(ct);
        return new OpcoesTurma(
            modalidades.Select(m => new OpcaoCadastro(m.Modalidade_ID, m.Nome)).ToList(),
            locais.Select(l => new OpcaoCadastro(l.Local_ID, l.Nome, l.Endereco.Resumo())).ToList(),
            professores.Select(p => new OpcaoCadastro(p.Professor_ID, p.Nome, ResumoDisponibilidade.De(p.Disponibilidade))).ToList());
    }

    /// <summary>O formulário da turma nova, com os valores iniciais dos parâmetros de turma (BL-M02-12) lidos agora.</summary>
    public async Task<TurmaRequisicao> NovaAsync(CancellationToken ct = default)
    {
        var inicial = (await parametros.ObterAsync(ct)).Matricula;
        var modalidades = await configuracao.ListarModalidadesAsync(ct);
        return new TurmaRequisicao
        {
            ModalidadeId = modalidades.Count == 1 ? modalidades[0].Modalidade_ID : null,
            Capacidade = inicial.CapacidadeInicial,
            Mensalidade = inicial.MensalidadeInicial,
            MinimoConfirmados = inicial.MinimoConfirmadosInicial,
            DataInicio = DateOnly.FromDateTime(relogio.Hoje()),
            Horarios = [new HorarioRequisicao { DiaSemana = DayOfWeek.Tuesday }, new HorarioRequisicao { DiaSemana = DayOfWeek.Thursday }]
        };
    }

    public async Task<TurmaRequisicao?> ParaEdicaoAsync(Guid id, CancellationToken ct = default)
    {
        if (await repositorio.ObterTurmaAsync(id, ct) is not { } t) return null;
        return new TurmaRequisicao
        {
            Id = t.Turma_ID,
            Nome = t.Nome,
            ModalidadeId = t.Modalidade_ID,
            Categoria = t.Categoria,
            LocalId = t.Local_ID,
            ProfessorId = t.Professor_ID,
            Capacidade = t.Capacidade,
            Mensalidade = t.Mensalidade,
            PeriodicidadeMeses = t.PeriodicidadeMeses,
            MinimoConfirmados = t.MinimoConfirmados,
            MetaFrequenciaPercentual = t.MetaFrequenciaPercentual,
            DataInicio = t.DataInicio,
            Versao = Cadastros.Versao(t.Versao),
            Horarios = t.Horarios.OrderBy(h => DiasSemana.Posicao(h.DiaSemana)).ThenBy(h => h.Inicio)
                .Select(h => new HorarioRequisicao { Id = h.HorarioTurma_ID, DiaSemana = h.DiaSemana, Inicio = h.Inicio, Fim = h.Fim })
                .ToList()
        };
    }

    public async Task<ResultadoOperacao> CriarAsync(TurmaRequisicao r, CancellationToken ct = default)
    {
        r.Id = null;
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await ConferirReferenciasAsync(r, null, ct) is { } erro) return erro;

        var professor = (await equipe.ObterProfessorAsync(r.ProfessorId!.Value, ct))!;
        var conflitos = await ConflitosAsync(r, professor, null, ct);
        if (conflitos.Count > 0 && !r.ConfirmarConflitos) return PedirConfirmacao(conflitos);

        var turma = new Turma { Turma_ID = Guid.NewGuid(), Situacao = SituacaoTurma.Ativa };
        Aplicar(turma, r);
        foreach (var h in r.HorariosPreenchidos)
            turma.Horarios.Add(new HorarioTurma
            {
                HorarioTurma_ID = Guid.NewGuid(), Turma_ID = turma.Turma_ID, DiaSemana = h.DiaSemana, Inicio = h.Inicio!.Value, Fim = h.Fim!.Value
            });
        repositorio.AdicionarTurma(turma);
        turma.Professor = professor;

        var plano = await sessoes.SincronizarAsync(turma, ct);
        registro.Registrar(Entidade, turma.Turma_ID, "Criação", null, Descrever(turma, professor.Nome));
        if (conflitos.Count > 0) RegistrarAlertas(turma.Turma_ID, conflitos);

        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Turma {turma.Nome} criada, com {Sessoes(plano.Criar.Count)} até {FimDoHorizonte()}.", turma.Turma_ID);
    }

    public async Task<ResultadoOperacao> EditarAsync(TurmaRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (r.Id is not { } id || await repositorio.ObterTurmaAsync(id, ct) is not { } turma)
            return ResultadoOperacao.Erro("Turma não encontrada.");
        if (!turma.Ativa) return ResultadoOperacao.Erro("A turma está encerrada: não pode mais ser alterada.");
        if (await ConferirReferenciasAsync(r, id, ct) is { } erro) return erro;
        if (Cadastros.LerVersao(r.Versao) is { } versao) repositorio.DefinirVersaoOriginal(turma, versao);

        var professor = (await equipe.ObterProfessorAsync(r.ProfessorId!.Value, ct))!;
        var conflitos = await ConflitosAsync(r, professor, id, ct);
        if (conflitos.Count > 0 && !r.ConfirmarConflitos) return PedirConfirmacao(conflitos);

        var mensalidadeAntes = turma.Mensalidade;
        var antes = Descrever(turma, turma.Professor?.Nome);
        var horariosAntes = turma.ResumoHorarios();
        Aplicar(turma, r);
        SincronizarHorarios(turma, r);
        var horariosDepois = turma.ResumoHorarios();
        turma.Professor = professor;
        var depois = Descrever(turma, professor.Nome);

        var mudou = registro.SeMudou(Entidade, id, "Configuração", antes, depois);
        if (mensalidadeAntes != turma.Mensalidade)
        {
            // Ação sensível (spec §7): alteração de mensalidade com autor, data e valor anterior.
            registro.Registrar(Entidade, id, "Mensalidade", Formatar(mensalidadeAntes), Formatar(turma.Mensalidade),
                "Vale para as matrículas novas; as existentes mantêm o próprio valor.");
            mudou = true;
        }
        if (!mudou) return ResultadoOperacao.Ok("Nada mudou.");
        if (conflitos.Count > 0) RegistrarAlertas(id, conflitos);

        var plano = await sessoes.SincronizarAsync(turma, ct);
        // P33: mudou o horário da turma, as famílias recebem o "Treino remarcado", um aviso por família, na mesma gravação.
        var avisos = horariosAntes != horariosDepois
            ? await emissor.TreinoRemarcadoAsync(turma.Turma_ID, turma.Nome, Mensageria.EmissorGatilhos.OrigemTurma, turma.Turma_ID,
                horariosAntes, horariosDepois, false, null, $"H{relogio.Agora():yyMMddHHmmss}", ct)
            : 0;
        try
        {
            await repositorio.SalvarAsync(ct);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            return ResultadoOperacao.Erro(Cadastros.MensagemConflito);
        }

        var ajuste = plano.Vazio ? string.Empty
            : $" Sessões previstas: {plano.Criar.Count} nova(s), {plano.Ajustar.Count} ajustada(s), {plano.Remover.Count} retirada(s).";
        var aviso = avisos > 0 ? $" {avisos} aviso(s) de horário novo às famílias na fila de mensagens." : string.Empty;
        return ResultadoOperacao.Ok($"Turma {turma.Nome} atualizada.{ajuste}{aviso}");
    }

    /// <summary>Encerra a turma sem matrícula ativa: as sessões previstas que ainda não aconteceram saem da agenda.</summary>
    public async Task<ResultadoOperacao> EncerrarAsync(EncerrarTurmaRequisicao r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.Motivo)) return ResultadoOperacao.Erro("Informe o motivo do encerramento.", nameof(r.Motivo));
        if (await repositorio.ObterTurmaAsync(r.TurmaId, ct) is not { } turma) return ResultadoOperacao.Erro("Turma não encontrada.");
        if (!turma.Ativa) return ResultadoOperacao.Erro("A turma já está encerrada.");
        var ocupadas = (await repositorio.OcupacaoAsync(ct)).GetValueOrDefault(turma.Turma_ID);
        if (ocupadas > 0)
            return ResultadoOperacao.Erro(
                $"A turma ainda tem {ocupadas} matrícula(s) ativa(s). Encerre ou transfira as matrículas antes (na ficha de cada aluno).");

        turma.Encerrar(relogio.Agora(), r.Motivo);
        var plano = await sessoes.SincronizarAsync(turma, ct);
        registro.Registrar(Entidade, turma.Turma_ID, "Situação", "Ativa", "Encerrada", r.Motivo);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Turma {turma.Nome} encerrada. {Sessoes(plano.Remover.Count)} prevista(s) saíram da agenda.");
    }

    public async Task<FichaTurma?> ObterFichaAsync(Guid id, CancellationToken ct = default)
    {
        if (!await autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Ler, new AlvoEscopo(TipoAlvoEscopo.Turma, id), ct))
            return null;
        if (await repositorio.ObterTurmaAsync(id, ct) is not { } t) return null;

        var modalidade = (await configuracao.ObterModalidadeAsync(t.Modalidade_ID, ct))?.Nome ?? "—";
        var composicao = await alunos.AlunosDaTurmaAsync(id, ct);
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var sessoesTurma = await repositorio.SessoesDaTurmaDesdeAsync(id, hoje.AddDays(-30), ct);
        var agora = relogio.Agora();
        var verValores = autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler);
        var frequencia = (await parametros.ObterAsync(ct)).Presenca.FrequenciaMinimaPercentual;

        var pode = new PermissoesTurma(
            Editar: t.Ativa && autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Alterar),
            CancelarSessao: autorizacao.Pode(Recurso.RegistroPresenca, Operacao.Alterar),
            VerValores: verValores,
            VerAlunos: true);

        return new FichaTurma(t.Turma_ID, t.Nome, modalidade, t.Categoria, t.Local?.Nome ?? "—",
            string.IsNullOrEmpty(t.Local?.Endereco.Resumo()) ? null : t.Local!.Endereco.Resumo(),
            t.Professor_ID, t.Professor?.Nome ?? "—", t.Capacidade,
            composicao.Count(a => a.SituacaoMatricula == Domain.Alunos.SituacaoMatricula.Ativa),
            verValores ? t.Mensalidade : null, t.MinimoConfirmados, t.MetaFrequenciaPercentual, frequencia, t.DataInicio,
            t.Situacao, t.EncerradaEm, t.MotivoEncerramento,
            t.Horarios.Select(h => h.Semanal).OrderBy(h => DiasSemana.Posicao(h.Dia)).ThenBy(h => h.Inicio).ToList(),
            composicao,
            sessoesTurma.Where(s => s.Fim >= agora && !s.Encerrada).OrderBy(s => s.Inicio).Take(12)
                .Select(ServicoSessoes.Resposta).ToList(),
            sessoesTurma.Where(s => s.Encerrada).OrderByDescending(s => s.Inicio).Take(10)
                .Select(ServicoSessoes.Resposta).ToList(),
            pode);
    }

    private async Task<ResultadoOperacao?> ConferirReferenciasAsync(TurmaRequisicao r, Guid? turmaId, CancellationToken ct)
    {
        if (await configuracao.ObterModalidadeAsync(r.ModalidadeId!.Value, ct) is null)
            return ResultadoOperacao.Erro("Modalidade não encontrada.", nameof(TurmaRequisicao.ModalidadeId));
        if (await repositorio.ObterLocalAsync(r.LocalId!.Value, ct) is null)
            return ResultadoOperacao.Erro("Local não encontrado.", nameof(TurmaRequisicao.LocalId));
        if (await equipe.ObterProfessorAsync(r.ProfessorId!.Value, ct) is null)
            return ResultadoOperacao.Erro("Professor não encontrado.", nameof(TurmaRequisicao.ProfessorId));
        if (await repositorio.NomeTurmaEmUsoAsync(r.Nome.Trim(), turmaId, ct))
            return ResultadoOperacao.Erro($"Já existe uma turma ativa chamada {r.Nome.Trim()}.", nameof(TurmaRequisicao.Nome));
        return null;
    }

    /// <summary>
    /// BL-M04-09: outra turma do professor no mesmo horário, fora da disponibilidade ou dentro de uma indisponibilidade.
    /// P31: outra turma no mesmo local e horário.
    /// </summary>
    private async Task<IReadOnlyList<ConflitoAgenda>> ConflitosAsync(TurmaRequisicao r, Professor professor, Guid? turmaId, CancellationToken ct)
    {
        var horarios = r.Semanais();
        var outras = await repositorio.TurmasAtivasDoProfessorAsync(professor.Professor_ID, turmaId, ct);
        var ocupados = outras.SelectMany(t => t.Horarios.Select(h => new HorarioOcupado(t.Nome, h.Semanal))).ToList();
        var local = await repositorio.ObterLocalAsync(r.LocalId!.Value, ct);
        var noLocal = await repositorio.TurmasAtivasDoLocalAsync(r.LocalId!.Value, turmaId, ct);
        var ocupadosNoLocal = noLocal.SelectMany(t => t.Horarios.Select(h => new HorarioOcupado(t.Nome, h.Semanal))).ToList();

        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora);
        var de = r.DataInicio is { } inicio && inicio > hoje ? inicio : hoje;
        var ocorrencias = GeradorSessoes.Ocorrencias(horarios, de, GeradorSessoes.FimDoHorizonte(hoje)).Where(o => o.Inicio >= agora);
        return
        [
            .. DetectorConflitos.ParaHorarios(professor, horarios, ocupados, ocorrencias),
            .. DetectorConflitos.LocalOcupado(local?.Nome ?? "O local", horarios, ocupadosNoLocal)
        ];
    }

    private static ResultadoOperacao PedirConfirmacao(IReadOnlyList<ConflitoAgenda> conflitos) => new()
    {
        Sucesso = false,
        Mensagem = conflitos.Count == 1
            ? "Há um alerta de agenda. Confira e confirme para salvar mesmo assim."
            : $"Há {conflitos.Count} alertas de agenda. Confira e confirme para salvar mesmo assim.",
        Campo = nameof(TurmaRequisicao.ConfirmarConflitos),
        Dados = new ConflitosTurma(conflitos)
    };

    private void RegistrarAlertas(Guid turmaId, IReadOnlyList<ConflitoAgenda> conflitos) =>
        registro.Registrar(Entidade, turmaId, "Alertas de agenda confirmados", null,
            string.Join(" | ", conflitos.Select(c => c.Descricao)), "Quem salvou viu os alertas e decidiu seguir (R9).");

    private static void Aplicar(Turma turma, TurmaRequisicao r)
    {
        turma.Nome = r.Nome.Trim();
        turma.Modalidade_ID = r.ModalidadeId!.Value;
        turma.Categoria = Cadastros.Vazio(r.Categoria);
        turma.Local_ID = r.LocalId!.Value;
        turma.Professor_ID = r.ProfessorId!.Value;
        turma.Capacidade = r.Capacidade;
        turma.Mensalidade = r.Mensalidade!.Value;
        turma.PeriodicidadeMeses = r.PeriodicidadeMeses;
        turma.MinimoConfirmados = r.MinimoConfirmados;
        turma.MetaFrequenciaPercentual = r.MetaFrequenciaPercentual;
        turma.DataInicio = r.DataInicio!.Value;
    }

    /// <summary>Atualiza os horários pelo id (a sessão acompanha a mudança do seu horário), inclui os novos e retira os que saíram.</summary>
    private void SincronizarHorarios(Turma turma, TurmaRequisicao r)
    {
        var ficam = new HashSet<Guid>();
        foreach (var h in r.HorariosPreenchidos)
        {
            var existente = h.Id is { } hid ? turma.Horarios.FirstOrDefault(x => x.HorarioTurma_ID == hid) : null;
            if (existente is null)
            {
                var novo = new HorarioTurma
                {
                    HorarioTurma_ID = Guid.NewGuid(), Turma_ID = turma.Turma_ID, DiaSemana = h.DiaSemana, Inicio = h.Inicio!.Value, Fim = h.Fim!.Value
                };
                // A turma está rastreada: ao incluir o horário, o EF já o põe na coleção. Incluir de novo duplicaria as sessões.
                if (!turma.Horarios.Contains(novo)) turma.Horarios.Add(novo);
                repositorio.AdicionarHorario(novo);
                ficam.Add(novo.HorarioTurma_ID);
                continue;
            }

            existente.DiaSemana = h.DiaSemana;
            existente.Inicio = h.Inicio!.Value;
            existente.Fim = h.Fim!.Value;
            ficam.Add(existente.HorarioTurma_ID);
        }

        foreach (var saiu in turma.Horarios.Where(h => !ficam.Contains(h.HorarioTurma_ID)).ToList())
        {
            turma.Horarios.Remove(saiu);
            repositorio.RemoverHorario(saiu);
        }
    }

    private static string Descrever(Turma t, string? professor) =>
        $"{t.Nome} · {t.Categoria ?? "sem categoria"} · {t.ResumoHorarios()} · professor {professor ?? "—"} · " +
        (t.PeriodicidadeMeses == 1 ? string.Empty : $"cobrança {Turma.RotuloPeriodicidade(t.PeriodicidadeMeses).ToLowerInvariant()} · ") +
        $"{t.Capacidade} vagas · mínimo {t.MinimoConfirmados} confirmados" +
        (t.MetaFrequenciaPercentual is { } meta ? $" · meta {meta}%" : string.Empty) +
        $" · início {t.DataInicio.ToString("dd/MM/yyyy", PtBr)}";

    private static string Formatar(decimal valor) => valor.ToString("C", PtBr);

    private string FimDoHorizonte() =>
        GeradorSessoes.FimDoHorizonte(DateOnly.FromDateTime(relogio.Hoje())).ToString("dd/MM", PtBr);

    private static string Sessoes(int n) => n == 1 ? "1 sessão" : $"{n} sessões";
}
