using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Turmas;

/// <summary>Locais, turmas, horários e sessões da escolinha do contexto (tudo pelo filtro de escolinha).</summary>
public interface ITurmasRepositorio
{
    Task<IReadOnlyList<Local>> ListarLocaisAsync(CancellationToken ct = default);
    Task<Local?> ObterLocalAsync(Guid id, CancellationToken ct = default);
    Task<bool> NomeLocalEmUsoAsync(string nome, Guid? excetoId, CancellationToken ct = default);
    void AdicionarLocal(Local local);

    /// <summary>Turmas (com horários, local e professor), limitadas ao escopo.</summary>
    Task<IReadOnlyList<Turma>> ListarTurmasAsync(EscopoTurmas escopo, bool incluirEncerradas, CancellationToken ct = default);

    /// <summary>A turma para alterar (rastreada, com horários, local e professor).</summary>
    Task<Turma?> ObterTurmaAsync(Guid id, CancellationToken ct = default);

    Task<bool> NomeTurmaEmUsoAsync(string nome, Guid? excetoId, CancellationToken ct = default);

    /// <summary>As outras turmas ativas do professor, com os horários (para o alerta de conflito).</summary>
    Task<IReadOnlyList<Turma>> TurmasAtivasDoProfessorAsync(Guid professorId, Guid? excetoTurmaId, CancellationToken ct = default);

    /// <summary>As outras turmas ativas no local, com os horários (para o alerta de quadra ocupada, P31).</summary>
    Task<IReadOnlyList<Turma>> TurmasAtivasDoLocalAsync(Guid localId, Guid? excetoTurmaId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> IdsTurmasAtivasAsync(CancellationToken ct = default);
    void AdicionarTurma(Turma turma);

    /// <summary>Horário novo de uma turma já gravada (entra explicitamente como inclusão: o id vem da aplicação).</summary>
    void AdicionarHorario(HorarioTurma horario);

    void RemoverHorario(HorarioTurma horario);

    /// <summary>A versão que o formulário viu: se a turma mudou depois, o salvar dá conflito (D27).</summary>
    void DefinirVersaoOriginal(Turma turma, byte[] versao);

    /// <summary>Matrículas ativas por turma (a ocupação de vagas).</summary>
    Task<IReadOnlyDictionary<Guid, int>> OcupacaoAsync(CancellationToken ct = default);

    /// <summary>As sessões da turma de uma data em diante (rastreadas, todas as situações).</summary>
    Task<IReadOnlyList<SessaoTreino>> SessoesDaTurmaDesdeAsync(Guid turmaId, DateOnly desde, CancellationToken ct = default);

    Task<SessaoTreino?> ObterSessaoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Sessões do período, com turma, local e professor (sem rastreio).</summary>
    Task<IReadOnlyList<SessaoTreino>> ListarSessoesAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    void AdicionarSessao(SessaoTreino sessao);
    void RemoverSessao(SessaoTreino sessao);

    Task SalvarAsync(CancellationToken ct = default);
}

// ── Locais ──

public class LocalRequisicao : IComEndereco
{
    public Guid? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int? Capacidade { get; set; }
    public string? Observacao { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
}

public record LocalResposta(Guid Id, string Nome, Endereco Endereco, int? Capacidade, string? Observacao, int TurmasAtivas);

public class LocalRequisicaoValidador : AbstractValidator<LocalRequisicao>
{
    public LocalRequisicaoValidador()
    {
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome do local (ex.: Quadra do Colégio Marista).")
            .MaximumLength(Local.TamanhoNome).WithMessage($"O nome pode ter no máximo {Local.TamanhoNome} caracteres.");
        RuleFor(x => x.Capacidade).InclusiveBetween(1, 100000).When(x => x.Capacidade is not null)
            .WithMessage("A capacidade do local fica entre 1 e 100.000 pessoas.");
        RuleFor(x => x.Observacao).MaximumLength(Local.TamanhoObservacao)
            .WithMessage($"A observação pode ter no máximo {Local.TamanhoObservacao} caracteres.");
        Cadastros.RegrasEndereco(this);
    }
}

// ── Turmas ──

public class HorarioRequisicao
{
    public Guid? Id { get; set; }
    public DayOfWeek DiaSemana { get; set; } = DayOfWeek.Monday;
    public TimeOnly? Inicio { get; set; }
    public TimeOnly? Fim { get; set; }

    /// <summary>Linha em branco do formulário (ignorada).</summary>
    public bool Vazio => Inicio is null && Fim is null;
}

public class TurmaRequisicao
{
    public Guid? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Guid? ModalidadeId { get; set; }
    public string? Categoria { get; set; }
    public Guid? LocalId { get; set; }
    public Guid? ProfessorId { get; set; }
    public int Capacidade { get; set; }
    public decimal? Mensalidade { get; set; }

    /// <summary>De quantos em quantos meses a turma é cobrada (P44).</summary>
    public int PeriodicidadeMeses { get; set; } = 1;

    public int MinimoConfirmados { get; set; }
    public int? MetaFrequenciaPercentual { get; set; }
    public DateOnly? DataInicio { get; set; }
    public List<HorarioRequisicao> Horarios { get; set; } = [];

    /// <summary>Quem marcou viu os alertas de agenda e decidiu seguir (R9: sinalizar, não bloquear).</summary>
    public bool ConfirmarConflitos { get; set; }

    public string? Versao { get; set; }

    public IEnumerable<HorarioRequisicao> HorariosPreenchidos => Horarios.Where(h => !h.Vazio);

    public IReadOnlyList<HorarioSemanal> Semanais() =>
        HorariosPreenchidos.Where(h => h.Inicio is not null && h.Fim is not null)
            .Select(h => new HorarioSemanal(h.DiaSemana, h.Inicio!.Value, h.Fim!.Value)).ToList();
}

public class TurmaRequisicaoValidador : AbstractValidator<TurmaRequisicao>
{
    public TurmaRequisicaoValidador()
    {
        var capacidade = CatalogoParametros.ObterObrigatorio(ChavesParametro.TurmaCapacidade);
        var mensalidade = CatalogoParametros.ObterObrigatorio(ChavesParametro.TurmaMensalidade);
        var minimo = CatalogoParametros.ObterObrigatorio(ChavesParametro.TurmaMinimoConfirmados);
        var frequencia = CatalogoParametros.ObterObrigatorio(ChavesParametro.PresencaRiscoPercentual);

        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome da turma (ex.: Sub-9 · terça e quinta).")
            .MaximumLength(Turma.TamanhoNome).WithMessage($"O nome pode ter no máximo {Turma.TamanhoNome} caracteres.");
        RuleFor(x => x.ModalidadeId).NotEmpty().WithMessage("Escolha a modalidade da turma.");
        RuleFor(x => x.Categoria).MaximumLength(Turma.TamanhoCategoria)
            .WithMessage($"A categoria pode ter no máximo {Turma.TamanhoCategoria} caracteres.");
        RuleFor(x => x.LocalId).NotEmpty().WithMessage("Escolha o local de treino.");
        RuleFor(x => x.ProfessorId).NotEmpty().WithMessage("Escolha o professor responsável.");
        RuleFor(x => x.Capacidade).InclusiveBetween((int)capacidade.Minimo!, (int)capacidade.Maximo!)
            .WithMessage($"A capacidade fica entre {capacidade.Minimo:0} e {capacidade.Maximo:0} vagas.");
        RuleFor(x => x.Mensalidade)
            .NotNull().WithMessage("Informe a mensalidade da turma.")
            .InclusiveBetween(mensalidade.Minimo!.Value, mensalidade.Maximo!.Value)
            .WithMessage($"A mensalidade fica entre R$ {mensalidade.Minimo:0} e R$ {mensalidade.Maximo:N0}.");
        RuleFor(x => x.PeriodicidadeMeses).Must(p => Turma.Periodicidades.Contains(p))
            .WithMessage("Escolha a cobrança mensal, trimestral, semestral ou anual.");
        RuleFor(x => x.MinimoConfirmados).InclusiveBetween((int)minimo.Minimo!, (int)minimo.Maximo!)
            .WithMessage($"O mínimo de confirmados fica entre {minimo.Minimo:0} e {minimo.Maximo:0}.");
        RuleFor(x => x.MetaFrequenciaPercentual)
            .InclusiveBetween((int)frequencia.Minimo!, (int)frequencia.Maximo!).When(x => x.MetaFrequenciaPercentual is not null)
            .WithMessage($"A meta de frequência fica entre {frequencia.Minimo:0}% e {frequencia.Maximo:0}%.");
        RuleFor(x => x.DataInicio).NotNull().WithMessage("Informe a data de início da turma.");

        RuleFor(x => x.Horarios)
            .Must(h => h.Any(l => !l.Vazio)).WithMessage("Informe ao menos um horário de treino.")
            .Must(h => h.Where(l => !l.Vazio).All(l => l.Inicio is not null && l.Fim is not null))
            .WithMessage("Cada horário precisa de início e fim.")
            .Must(h => h.Where(l => !l.Vazio && l.Inicio is not null && l.Fim is not null).All(l => l.Fim > l.Inicio))
            .WithMessage("O fim do treino precisa ser depois do início.")
            .Must(h => h.All(l => Enum.IsDefined(l.DiaSemana))).WithMessage("Dia da semana inválido.")
            .Must(SemSobreposicao).WithMessage("Dois horários da turma se sobrepõem no mesmo dia.");
    }

    private static bool SemSobreposicao(List<HorarioRequisicao> horarios)
    {
        var validos = horarios.Where(h => !h.Vazio && h.Inicio is not null && h.Fim is not null && h.Fim > h.Inicio)
            .Select(h => new HorarioSemanal(h.DiaSemana, h.Inicio!.Value, h.Fim!.Value)).ToList();
        for (var i = 0; i < validos.Count; i++)
        for (var j = i + 1; j < validos.Count; j++)
            if (validos[i].Sobrepoe(validos[j])) return false;
        return true;
    }
}

public class EncerrarTurmaRequisicao
{
    public Guid TurmaId { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Uma turma na lista, com a ocupação de vagas (BL-M04-03). A mensalidade só vai para quem lê o financeiro.</summary>
public record TurmaResumo(
    Guid Id,
    string Nome,
    string Modalidade,
    string? Categoria,
    string Professor,
    string Local,
    string Horarios,
    int Ocupadas,
    int Capacidade,
    decimal? Mensalidade,
    SituacaoTurma Situacao)
{
    public bool Lotada => Ocupadas >= Capacidade;
    public int Percentual => Capacidade == 0 ? 0 : (int)Math.Min(100, Math.Round(Ocupadas * 100m / Capacidade));
}

/// <summary>Resultado de salvar uma turma com alertas de agenda: a tela mostra e pede confirmação.</summary>
public record ConflitosTurma(IReadOnlyList<ConflitoAgenda> Conflitos);

// ── Sessões e agenda ──

public class CancelarSessaoRequisicao
{
    public Guid SessaoId { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public class CancelarSessaoRequisicaoValidador : AbstractValidator<CancelarSessaoRequisicao>
{
    public CancelarSessaoRequisicaoValidador()
    {
        RuleFor(x => x.SessaoId).NotEmpty();
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento (ele vai no aviso às famílias).")
            .MaximumLength(SessaoTreino.TamanhoMotivo).WithMessage($"O motivo pode ter no máximo {SessaoTreino.TamanhoMotivo} caracteres.");
    }
}

/// <summary>Remarcar uma sessão para outro dia ou hora (BL-M04-07).</summary>
public class RemarcarSessaoRequisicao
{
    public Guid SessaoId { get; set; }
    public DateOnly? Data { get; set; }
    public TimeOnly? Inicio { get; set; }
    public TimeOnly? Fim { get; set; }
    public string Motivo { get; set; } = string.Empty;
}

public class RemarcarSessaoRequisicaoValidador : AbstractValidator<RemarcarSessaoRequisicao>
{
    public RemarcarSessaoRequisicaoValidador()
    {
        RuleFor(x => x.SessaoId).NotEmpty();
        RuleFor(x => x.Data).NotNull().WithMessage("Informe o novo dia do treino.");
        RuleFor(x => x.Inicio).NotNull().WithMessage("Informe o novo horário de início.");
        RuleFor(x => x.Fim).NotNull().WithMessage("Informe o novo horário de fim.")
            .GreaterThan(x => x.Inicio).When(x => x.Inicio is not null && x.Fim is not null).WithMessage("O fim precisa ser depois do início.");
        RuleFor(x => x.Motivo)
            .NotEmpty().WithMessage("Informe o motivo da remarcação (ele fica no histórico).")
            .MaximumLength(SessaoTreino.TamanhoMotivo).WithMessage($"O motivo pode ter no máximo {SessaoTreino.TamanhoMotivo} caracteres.");
    }
}

public enum RecorteAgenda
{
    Turma = 10,
    Professor = 20,
    Local = 30
}

/// <summary>Filtros da agenda (BL-M04-11): recorte, período, modalidade e situação.</summary>
public class FiltroAgenda
{
    public RecorteAgenda Recorte { get; set; } = RecorteAgenda.Turma;

    /// <summary>A turma, o professor ou o local do recorte (vazio = todos do escopo).</summary>
    public Guid? Id { get; set; }

    public DateOnly? De { get; set; }
    public DateOnly? Ate { get; set; }
    public Guid? ModalidadeId { get; set; }
    public SituacaoSessao? Situacao { get; set; }
}

public record ItemAgenda(
    Guid SessaoId,
    DateOnly Data,
    DateTime Inicio,
    DateTime Fim,
    Guid TurmaId,
    string Turma,
    string? Categoria,
    string Modalidade,
    Guid LocalId,
    string Local,
    Guid ProfessorId,
    string Professor,
    SituacaoSessao Situacao,
    string? MotivoCancelamento,
    IReadOnlyList<string> Alertas,
    bool PodeCancelar,
    bool PodeChamada);

public record OpcaoAgenda(Guid Id, string Nome);

public record AgendaResposta(
    RecorteAgenda Recorte,
    Guid? Id,
    DateOnly De,
    DateOnly Ate,
    Guid? ModalidadeId,
    SituacaoSessao? Situacao,
    IReadOnlyList<ItemAgenda> Itens,
    IReadOnlyList<OpcaoAgenda> Turmas,
    IReadOnlyList<OpcaoAgenda> Professores,
    IReadOnlyList<OpcaoAgenda> Locais,
    IReadOnlyList<OpcaoAgenda> Modalidades);

public record SessaoResposta(
    Guid Id,
    Guid TurmaId,
    string Turma,
    DateOnly Data,
    DateTime Inicio,
    DateTime Fim,
    string Local,
    string Professor,
    SituacaoSessao Situacao,
    string? MotivoCancelamento,
    DateTime? CanceladaEm,
    string? CanceladaPorNome);

/// <summary>
/// Ocupação de agenda do professor e da quadra (BL-M04-08): os compromissos datados de um período. Hoje, as sessões
/// de treino; no S5, também os eventos. É por aqui que o evento (M07) confere a agenda do professor antes de marcar.
/// </summary>
public interface IOcupacaoAgenda
{
    Task<IReadOnlyList<Compromisso>> CompromissosAsync(DateTime de, DateTime ate, Guid? professorId, Guid? localId,
        CancellationToken ct = default);
}
