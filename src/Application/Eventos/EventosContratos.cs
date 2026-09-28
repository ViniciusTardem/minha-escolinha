using FluentValidation;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Eventos;

namespace MinhaEscolinha.Application.Eventos;

// ── Leitura ──

/// <summary>Uma turma que pode ter evento (as do escopo de quem monta), com o que o evento herda dela.</summary>
public sealed record OpcaoTurmaEvento(Guid Id, string Nome, Guid ModalidadeId, Guid ProfessorId, Guid LocalId, int MinimoConfirmados);

public sealed record OpcaoEvento(Guid Id, string Nome);

/// <summary>O formulário do evento: as opções no escopo de quem monta e os padrões.</summary>
/// <param name="PodeEscolherProfessor">Só a gestão escolhe o professor; o professor monta os próprios jogos.</param>
public sealed record FormularioEvento(
    IReadOnlyList<OpcaoTurmaEvento> Turmas,
    IReadOnlyList<OpcaoEvento> Locais,
    IReadOnlyList<OpcaoEvento> Professores,
    int DuracaoPadrao,
    bool PodeEscolherProfessor);

/// <summary>Um aluno que pode ser convocado para o evento, com os sinais que ajudam a escolher (BL-M07-05).</summary>
/// <param name="Situacao">A convocação dele neste evento; <c>null</c> = não escolhido.</param>
/// <param name="Bloqueado">A escolinha bloqueia a convocação de inadimplente (RF-CON-017, desligado por padrão: R9).</param>
/// <param name="SemCelular">Nenhum responsável que recebe notificação tem celular: a convocação não chega pelo WhatsApp.</param>
/// <param name="Conflitos">Outros eventos dele no mesmo horário (BL-M07-06): sinaliza, não bloqueia.</param>
public sealed record AlunoNaSelecao(
    Guid AlunoId,
    string Nome,
    Guid TurmaId,
    string Turma,
    SituacaoConvocacao? Situacao,
    bool Trial,
    bool EmRisco,
    bool Inadimplente,
    bool Bloqueado,
    bool SemCelular,
    IReadOnlyList<string> Conflitos)
{
    public bool Escolhido => Situacao is not null && Situacao != SituacaoConvocacao.Cancelada;

    /// <summary>A caixa só muda antes do disparo: a convocação que já saiu fica (a resposta se registra no painel).</summary>
    public bool Editavel => Situacao is null or SituacaoConvocacao.Selecionada;
}

/// <summary>Um evento na montagem do fim de semana: os dados, os números, os alertas de agenda e a seleção.</summary>
public sealed record EventoNaSemana(
    Guid Id,
    string Titulo,
    DateTime Inicio,
    DateTime Fim,
    string Local,
    SituacaoEvento Situacao,
    ContagemConvocacoes Contagem,
    int MinimoConfirmados,
    bool PodeConvocar,
    string? MotivoSemConvocacao,
    IReadOnlyList<string> Alertas,
    IReadOnlyList<AlunoNaSelecao> Alunos);

/// <summary>A tela da convocação em lote (BL-M07-08): os eventos da semana e o formulário do evento novo.</summary>
public sealed record SemanaConvocacoes(DateOnly Inicio, DateOnly Fim, IReadOnlyList<EventoNaSemana> Eventos, FormularioEvento Formulario,
    bool PodeMontar)
{
    public int ParaDisparar => Eventos.Where(e => e.PodeConvocar).Sum(e => e.Contagem.Selecionadas);
}

/// <summary>Um convocado no painel de confirmações.</summary>
public sealed record LinhaConvocado(
    Guid ConvocacaoId,
    Guid AlunoId,
    string Aluno,
    string Turma,
    SituacaoConvocacao Situacao,
    OrigemResposta? Origem,
    string? RespondidoPor,
    DateTime? RespondidaEm,
    DateTime? LembreteEm,
    string? OutraResposta,
    bool PodeRegistrar,
    bool MudouPertoDoJogo = false,
    string? TextoLivre = null);

/// <summary>O painel de confirmações de um evento (BL-M07-09): atualiza sozinho, sem recarregar a página.</summary>
public sealed record PainelEvento(
    Guid Id,
    string Titulo,
    string Turmas,
    DateTime Inicio,
    DateTime Fim,
    string Local,
    string? Endereco,
    string? LinkMapa,
    string? Adversario,
    string? Logistica,
    string Professor,
    SituacaoEvento Situacao,
    string? Motivo,
    ContagemConvocacoes Contagem,
    int MinimoConfirmados,
    IReadOnlyList<LinhaConvocado> Convocados,
    bool PodeAlterar,
    DateTime AtualizadoEm,
    int? PlacarNos = null,
    int? PlacarAdversario = null,
    bool PodeRegistrarPlacar = false,
    bool SoAsTurmasDoProfessor = false);

/// <summary>Uma resposta que mudou perto do jogo (P51), para o painel inicial.</summary>
public sealed record MudancaPertoDoJogo(Guid EventoId, string Jogo, DateTime Inicio, string Aluno);

/// <summary>Os alertas de agenda que pedem confirmação para salvar o evento (R9: sinaliza, não bloqueia).</summary>
public sealed record AlertasEvento(IReadOnlyList<ConflitoAgenda> Alertas);

/// <summary>Um aluno que pode ser convocado: matrícula ativa numa turma do evento na data, em trial ou ativo (CA-3).</summary>
public sealed record AlunoConvocavel(Guid AlunoId, string Nome, Guid TurmaId, bool Trial, bool TemCelular);

// ── Pedidos ──

/// <summary>Novo evento ou alteração (com <see cref="Id"/>). As turmas só na criação.</summary>
public sealed class EventoRequisicao
{
    public Guid? Id { get; set; }
    public List<Guid> Turmas { get; set; } = [];
    public DateOnly? Data { get; set; }
    public TimeOnly? Hora { get; set; }
    public int? DuracaoMinutos { get; set; }
    /// <summary>Vazio no jogo novo = o local da turma.</summary>
    public Guid? LocalId { get; set; }

    /// <summary>Vazio = o professor da turma (só a gestão escolhe outro).</summary>
    public Guid? ProfessorId { get; set; }

    public string? Adversario { get; set; }
    public string? Logistica { get; set; }

    /// <summary>Quem salva viu os alertas de agenda e segue mesmo assim (R9).</summary>
    public bool ConfirmarAlertas { get; set; }

    public DateTime? Inicio => Data is { } data && Hora is { } hora ? data.ToDateTime(hora) : null;
}

public sealed class EventoRequisicaoValidador : AbstractValidator<EventoRequisicao>
{
    public EventoRequisicaoValidador()
    {
        RuleFor(r => r.Turmas).Must(t => t.Count > 0).When(r => r.Id is null).WithMessage("Escolha a turma do jogo.");
        RuleFor(r => r.Data).NotNull().WithMessage("Informe o dia.");
        RuleFor(r => r.Hora).NotNull().WithMessage("Informe o horário.");
        RuleFor(r => r.DuracaoMinutos).NotNull().WithMessage("Informe a duração.")
            .InclusiveBetween(Evento.DuracaoMinima, Evento.DuracaoMaxima)
            .WithMessage($"A duração vai de {Evento.DuracaoMinima} a {Evento.DuracaoMaxima} minutos.");
        // No jogo novo, vazio = o local da turma (R10, R11); na alteração, o local atual vem preenchido.
        RuleFor(r => r.LocalId).NotNull().When(r => r.Id is not null).WithMessage("Escolha o local.");
        RuleFor(r => r.Adversario).MaximumLength(Evento.TamanhoAdversario);
        RuleFor(r => r.Logistica).MaximumLength(Evento.TamanhoLogistica);
    }
}

/// <summary>Cancelar ou adiar (BL-M07-13).</summary>
public sealed class EncerrarEventoRequisicao
{
    public Guid Id { get; set; }
    public bool Adiar { get; set; }
    public string? Motivo { get; set; }
}

public sealed class EncerrarEventoRequisicaoValidador : AbstractValidator<EncerrarEventoRequisicao>
{
    public EncerrarEventoRequisicaoValidador()
    {
        RuleFor(r => r.Motivo).NotEmpty().WithMessage("Informe o motivo: ele vai na mensagem às famílias.")
            .MaximumLength(Evento.TamanhoMotivo);
    }
}

// ── Repositório ──

public interface IEventosRepositorio
{
    void Adicionar(Evento evento);
    void AdicionarConvocacao(Convocacao convocacao);
    void RemoverConvocacao(Convocacao convocacao);

    /// <summary>O evento rastreado, com as turmas.</summary>
    Task<Evento?> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>Os eventos do período (início) com alguma turma no alcance, com as turmas, sem rastreio.</summary>
    Task<IReadOnlyList<Evento>> ListarAsync(DateTime de, DateTime ate, EscopoTurmas alcance, CancellationToken ct = default);

    /// <summary>As convocações dos eventos, rastreadas (a seleção e o disparo mudam).</summary>
    Task<IReadOnlyList<Convocacao>> ConvocacoesAsync(IReadOnlyCollection<Guid> eventos, CancellationToken ct = default);

    Task<Convocacao?> ObterConvocacaoAsync(Guid id, CancellationToken ct = default);

    /// <summary>As convocações dos eventos com o histórico, só para mostrar (P51).</summary>
    Task<IReadOnlyList<Convocacao>> ConvocacoesComHistoricoAsync(IReadOnlyCollection<Guid> eventos, CancellationToken ct = default);

    /// <summary>Quem pode ser convocado: matrícula ativa numa das turmas na data, aluno em trial ou ativo (CA-3).</summary>
    Task<IReadOnlyList<AlunoConvocavel>> AlunosConvocaveisAsync(IReadOnlyCollection<Guid> turmas, DateOnly data, CancellationToken ct = default);

    /// <summary>As convocações ativas desses alunos em eventos abertos que começam no período (alerta de dois jogos).</summary>
    Task<IReadOnlyList<ConvocacaoDoAluno>> ConvocacoesAtivasAsync(IReadOnlyCollection<Guid> alunos, DateTime de, DateTime ate,
        CancellationToken ct = default);

    /// <summary>Os alunos com alguma mensalidade vencida com vencimento até a data (RF-CON-016).</summary>
    Task<IReadOnlySet<Guid>> InadimplentesAsync(IReadOnlyCollection<Guid> alunos, DateOnly vencidasAte, CancellationToken ct = default);

    /// <summary>Os eventos abertos que começam até a data e ainda têm convocação à espera de resposta, rastreados.</summary>
    Task<IReadOnlyList<Evento>> ComPendentesAteAsync(DateTime ate, CancellationToken ct = default);

    /// <summary>As convocações do aluno em eventos abertos que começam a partir da data (de uma turma, ou de todas), rastreadas.</summary>
    Task<IReadOnlyList<Convocacao>> ConvocacoesFuturasDoAlunoAsync(Guid alunoId, Guid? turmaId, DateTime aPartirDe, CancellationToken ct = default);

    /// <summary>Os eventos do período como compromissos datados, na agenda do professor ou do local (BL-M04-08).</summary>
    Task<IReadOnlyList<Compromisso>> CompromissosAsync(DateTime de, DateTime ate, Guid? professorId, Guid? localId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, string>> NomesDosAlunosAsync(IReadOnlyCollection<Guid> alunos, CancellationToken ct = default);

    /// <summary>O nome das turmas (as de qualquer escopo: o título do evento mostra todas).</summary>
    Task<IReadOnlyDictionary<Guid, string>> NomesDasTurmasAsync(IReadOnlyCollection<Guid> turmas, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}

// ── Tempo real ──

/// <summary>
/// Avisa as telas abertas que as convocações de um evento mudaram (BL-M07-09, CA-12), no padrão do DP: só o id e a hora; a
/// tela busca o painel de novo (a autorização fica na página). Chamado depois da gravação.
/// </summary>
public interface INotificadorConvocacoes
{
    void EventosAlterados(IReadOnlyCollection<(Guid Escolinha, Guid Evento)> eventos);
}

/// <summary>Sem tempo real (testes, jobs fora da web): nada acontece.</summary>
public sealed class NotificadorConvocacoesNulo : INotificadorConvocacoes
{
    public void EventosAlterados(IReadOnlyCollection<(Guid Escolinha, Guid Evento)> eventos) { }
}
