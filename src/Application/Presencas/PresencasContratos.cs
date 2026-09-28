using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Application.Presencas;

/// <summary>Uma sessão na lista da chamada (hoje e as que ficaram sem chamada).</summary>
public sealed record SessaoDaChamada(
    Guid Id,
    DateOnly Data,
    DateTime Inicio,
    DateTime Fim,
    string Turma,
    string Local,
    string Professor,
    SituacaoSessao Situacao,
    int Marcados);

public sealed record ChamadaDoDia(DateOnly Data, IReadOnlyList<SessaoDaChamada> Hoje, IReadOnlyList<SessaoDaChamada> SemChamada);

/// <summary>Um aluno na lista da chamada. O motivo de saúde não vem aqui (a página fica guardada no aparelho).</summary>
public sealed record AlunoNaChamada(
    Guid Id,
    string Nome,
    bool Trial,
    SituacaoPresenca? Situacao,
    string? Justificativa,
    bool JustificativaSaude);

public sealed record ChamadaDaSessao(
    Guid Id,
    Guid TurmaId,
    string Turma,
    DateOnly Data,
    DateTime Inicio,
    DateTime Fim,
    string Local,
    string Professor,
    SituacaoSessao Situacao,
    bool PodeMarcar,
    string? Aviso,
    IReadOnlyList<AlunoNaChamada> Alunos)
{
    public int Marcados => Alunos.Count(a => a.Situacao is not null);
}

/// <summary>Quem pode estar na chamada da sessão (a matrícula vigente na data) e a presença que já existe.</summary>
public sealed record AlunoDaSessao(Guid Id, string Nome, bool Trial, bool Elegivel);

/// <summary>Uma marcação vinda do aparelho: online ou da fila guardada sem sinal (CA-21).</summary>
public sealed class MarcacaoRequisicao
{
    /// <summary>O id (UUID) gerado no aparelho: o reenvio da mesma marcação é reconhecido e não refaz nada.</summary>
    public Guid Id { get; set; }

    public Guid SessaoId { get; set; }
    public Guid AlunoId { get; set; }
    public SituacaoPresenca Situacao { get; set; }
    public string? Justificativa { get; set; }
    public bool Saude { get; set; }

    /// <summary>O aparelho (id guardado no navegador) e a ordem da marcação nele.</summary>
    public string Dispositivo { get; set; } = string.Empty;

    public long Sequencia { get; set; }

    /// <summary>Quando foi marcada no aparelho, em UTC (só informativo: o relógio do aparelho não decide nada).</summary>
    public DateTime? MarcadaEm { get; set; }
}

public sealed class LoteMarcacoes
{
    public const int Maximo = 500;
    public List<MarcacaoRequisicao> Marcacoes { get; set; } = [];
}

public sealed class LoteMarcacoesValidador : AbstractValidator<LoteMarcacoes>
{
    public LoteMarcacoesValidador()
    {
        RuleFor(l => l.Marcacoes).NotEmpty().WithMessage("Nenhuma marcação para enviar.")
            .Must(m => m.Count <= LoteMarcacoes.Maximo).WithMessage($"Mande no máximo {LoteMarcacoes.Maximo} marcações por vez.");
        RuleForEach(l => l.Marcacoes).ChildRules(m =>
        {
            m.RuleFor(x => x.Id).NotEmpty().WithMessage("Marcação sem id.");
            m.RuleFor(x => x.SessaoId).NotEmpty().WithMessage("Marcação sem sessão.");
            m.RuleFor(x => x.AlunoId).NotEmpty().WithMessage("Marcação sem aluno.");
            m.RuleFor(x => x.Situacao).IsInEnum().WithMessage("Situação de presença inválida.");
            m.RuleFor(x => x.Justificativa).MaximumLength(Presenca.TamanhoJustificativa)
                .WithMessage($"O motivo tem no máximo {Presenca.TamanhoJustificativa} caracteres.");
            m.RuleFor(x => x.Dispositivo).NotEmpty().MaximumLength(Presenca.TamanhoDispositivo).WithMessage("Marcação sem o aparelho.");
            m.RuleFor(x => x.Sequencia).GreaterThan(0).WithMessage("Marcação sem a ordem do aparelho.");
        });
    }
}

/// <summary>O que aconteceu com cada marcação: aplicada, repetida (já recebida), ignorada (mais antiga) ou recusada.</summary>
public sealed record ResultadoDaMarcacao(Guid Id, string Resultado, string? Mensagem)
{
    public const string Aplicada = "aplicada";
    public const string Repetida = "repetida";
    public const string Ignorada = "ignorada";
    public const string Recusada = "recusada";
}

public sealed record ResultadoSincronizacao(IReadOnlyList<ResultadoDaMarcacao> Marcacoes, int Alertas, string? Erro = null)
{
    public static ResultadoSincronizacao Invalido(string erro) => new([], 0, erro);
}

/// <summary>A presença que mudou numa sincronização: como estava antes do lote e como ficou.</summary>
public sealed record MudancaPresenca(Presenca Presenca, SituacaoPresenca? Antes, string Aluno, string Turma);

// ── Ficha do aluno e risco ──

public sealed record FrequenciaDoAluno(int Presentes, int Consideradas, decimal? Percentual, int JanelaDias, decimal Minimo, bool EmRisco);

public sealed record PresencaNaFicha(
    Guid SessaoId,
    DateTime Inicio,
    Guid TurmaId,
    string Turma,
    SituacaoPresenca Situacao,
    string? Justificativa,
    bool JustificativaSaude,
    bool MotivoVisivel,
    string MarcadaPor,
    DateTime MarcadaEm);

public sealed record DestinoDoAlerta(Guid MensagemId, string Nome, CanalMensagem Canal, SituacaoMensagem Situacao, DateTime? EnviadaEm);

public sealed record AlertaNaFicha(
    Guid Id,
    DateTime Em,
    string Turma,
    string Descricao,
    SituacaoAlertaFaltas Situacao,
    string? Motivo,
    IReadOnlyList<DestinoDoAlerta> Destinos);

/// <summary>A aba Presença da ficha: a frequência da janela de risco, as presenças recentes e (para a gestão) os alertas (CA-6).</summary>
public sealed record PresencaDoAluno(FrequenciaDoAluno Frequencia, IReadOnlyList<PresencaNaFicha> Presencas, IReadOnlyList<AlertaNaFicha>? Alertas);

public sealed record RiscoEvasao(Guid AlunoId, string Nome, int Presentes, int Consideradas, decimal Percentual);

/// <summary>Quem está em risco de evasão agora (BL-M05-11), no escopo de quem pergunta.</summary>
public interface IRiscoEvasao
{
    Task<IReadOnlyList<RiscoEvasao>> AlunosEmRiscoAsync(CancellationToken ct = default);
}

/// <summary>Presenças, marcações e alertas de faltas (na escolinha do contexto).</summary>
public interface IPresencaRepositorio
{
    /// <summary>As sessões do período, das turmas do escopo, com o número de alunos já marcados.</summary>
    Task<IReadOnlyList<SessaoDaChamada>> SessoesAsync(DateOnly de, DateOnly ate, EscopoTurmas escopo, CancellationToken ct = default);

    /// <summary>A sessão com turma, local e professor (rastreada: a chamada a faz "realizada").</summary>
    Task<SessaoTreino?> ObterSessaoAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Quem está na chamada da sessão: matrícula vigente na data e, para a data de hoje em diante, sem pausa nem
    /// desligamento (CA-3). "Elegivel" falso = só aparece porque já tem presença marcada (histórico).
    /// </summary>
    Task<IReadOnlyList<AlunoDaSessao>> AlunosDaSessaoAsync(SessaoTreino sessao, CancellationToken ct = default);

    /// <summary>As presenças da sessão (rastreadas).</summary>
    Task<IReadOnlyList<Presenca>> PresencasDaSessaoAsync(Guid sessaoId, CancellationToken ct = default);

    /// <summary>A maior sequência já aplicada por aparelho e aluno na sessão (a marcação atrasada não desfaz a mais nova).</summary>
    Task<Dictionary<(Guid AlunoId, string Dispositivo), long>> MaioresSequenciasAsync(Guid sessaoId, CancellationToken ct = default);

    /// <summary>Dos ids de marcação informados, os que já foram recebidos antes.</summary>
    Task<IReadOnlySet<Guid>> MarcacoesRecebidasAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>As presenças do aluno na turma desde a data (rastreadas: veem as mudanças ainda não gravadas).</summary>
    Task<IReadOnlyList<Presenca>> PresencasDoAlunoNaTurmaAsync(Guid alunoId, Guid turmaId, DateTime desde, CancellationToken ct = default);

    Task<DateTime?> UltimoAlertaDisparadoAsync(Guid alunoId, CancellationToken ct = default);

    /// <summary>Os alertas disparados do aluno na turma desde a data (rastreados).</summary>
    Task<IReadOnlyList<AlertaFaltas>> AlertasDisparadosAsync(Guid alunoId, Guid turmaId, DateTime desde, CancellationToken ct = default);

    Task<AlertaFaltas?> ObterAlertaAsync(Guid id, CancellationToken ct = default);

    // ── Ficha e risco ──

    Task<IReadOnlyList<PresencaNaFicha>> PresencasDoAlunoAsync(Guid alunoId, int limite, CancellationToken ct = default);

    /// <summary>As situações marcadas do aluno desde a data (para a frequência).</summary>
    /// <summary>As situações marcadas desde a data, com a turma e a meta de frequência dela (P32).</summary>
    Task<IReadOnlyList<(Guid TurmaId, int? Meta, SituacaoPresenca Situacao)>> SituacoesDoAlunoAsync(Guid alunoId, DateTime desde, CancellationToken ct = default);

    Task<IReadOnlyList<(AlertaFaltas Alerta, string Turma)>> AlertasDoAlunoAsync(Guid alunoId, int limite, CancellationToken ct = default);

    /// <summary>As situações marcadas desde a data, dos alunos em trial ou ativos, nas turmas do escopo (para o risco).</summary>
    Task<IReadOnlyList<(Guid AlunoId, string Nome, int? Meta, SituacaoPresenca Situacao)>> SituacoesDesdeAsync(DateTime desde, EscopoTurmas escopo,
        CancellationToken ct = default);

    void AdicionarPresenca(Presenca presenca);
    void AdicionarMarcacao(MarcacaoPresenca marcacao);
    void AdicionarAlerta(AlertaFaltas alerta);

    Task SalvarAsync(CancellationToken ct = default);
}
