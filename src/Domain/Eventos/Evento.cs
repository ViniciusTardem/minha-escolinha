using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Eventos;

/// <summary>
/// Máquina do evento (spec §6): agendado → confirmado → realizado → resultado homologado | cancelado | adiado. As
/// transições são manuais (P19). Adiado e cancelado são finais e levam as convocações a canceladas; o jogo remarcado é
/// um evento novo. O "resultado homologado" fica sem uso no piloto (P11).
/// </summary>
public enum SituacaoEvento
{
    Agendado = 10,
    Confirmado = 20,
    Realizado = 30,
    ResultadoHomologado = 40,
    Cancelado = 50,
    Adiado = 60
}

/// <summary>O que mudou numa alteração do evento: a família só é avisada quando muda o quando ou o onde.</summary>
public sealed record AlteracaoEvento(bool MudouQuando, bool MudouLocal, bool MudouOutros)
{
    public bool AvisaFamilias => MudouQuando || MudouLocal;
    public bool Mudou => MudouQuando || MudouLocal || MudouOutros;
}

/// <summary>
/// Evento com tipo (BL-M07-01, R8): o compromisso datado além do treino. Nesta versão é sempre o jogo, mas nada aqui o
/// pressupõe: o adversário é opcional e só existe se a modalidade permite (<see cref="PoliticaPerfil"/>). Tem duração
/// (a detecção de conflito precisa do fim, P01), local, professor responsável e uma ou mais turmas. As convocações são
/// uma por convocado (<see cref="Convocacao"/>).
/// </summary>
public class Evento : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoAdversario = 120;
    public const int TamanhoLogistica = 500;
    public const int TamanhoMotivo = 300;
    public const int TamanhoAutor = 150;
    public const int DuracaoMinima = 15;
    public const int DuracaoMaxima = 600;

    public Guid Evento_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public TipoEvento Tipo { get; private set; } = TipoEvento.Jogo;
    public Guid Modalidade_ID { get; private set; }

    /// <summary>Horário de Brasília.</summary>
    public DateTime Inicio { get; private set; }

    public int DuracaoMinutos { get; private set; }
    public Guid Local_ID { get; private set; }
    public Guid Professor_ID { get; private set; }

    /// <summary>Só quando a modalidade tem o recurso "adversário"; mesmo então, opcional.</summary>
    public string? Adversario { get; private set; }

    /// <summary>Informações para a família: horário de chegada, uniforme, transporte.</summary>
    public string? Logistica { get; private set; }

    public SituacaoEvento Situacao { get; private set; } = SituacaoEvento.Agendado;

    /// <summary>Motivo do cancelamento ou do adiamento.</summary>
    public string? Motivo { get; private set; }

    public DateTime? EncerradoEm { get; private set; }
    public string? EncerradoPorNome { get; private set; }

    public List<EventoTurma> Turmas { get; set; } = [];

    public byte[] Versao { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public DateTime Fim => Inicio.AddMinutes(DuracaoMinutos);

    /// <summary>Agendado ou confirmado: aceita convocação, alteração, cancelamento e adiamento.</summary>
    public bool Aberto => Situacao is SituacaoEvento.Agendado or SituacaoEvento.Confirmado;

    /// <summary>Cancelado ou adiado: as convocações dele estão canceladas.</summary>
    public bool Encerrado => Situacao is SituacaoEvento.Cancelado or SituacaoEvento.Adiado;

    public IReadOnlyList<Guid> IdsDasTurmas => Turmas.Select(t => t.Turma_ID).ToList();

    /// <param name="adversarioPermitido">O perfil da modalidade tem o recurso "adversário" (R8).</param>
    public static Evento Novo(TipoEvento tipo, Guid modalidadeId, IReadOnlyCollection<Guid> turmas, DateTime inicio, int duracaoMinutos,
        Guid localId, Guid professorId, string? adversario, bool adversarioPermitido, string? logistica)
    {
        if (!Enum.IsDefined(tipo)) throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de evento desconhecido.");
        var ids = turmas.Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException("O evento tem pelo menos uma turma.", nameof(turmas));
        var evento = new Evento
        {
            Evento_ID = Guid.NewGuid(),
            Tipo = tipo,
            Modalidade_ID = modalidadeId,
            Situacao = SituacaoEvento.Agendado,
            Turmas = ids.Select(t => new EventoTurma { Turma_ID = t }).ToList()
        };
        evento.Definir(inicio, duracaoMinutos, localId, professorId, adversario, adversarioPermitido, logistica);
        return evento;
    }

    /// <summary>Muda os dados do evento aberto. As turmas ficam: para outra composição, cancela-se e cria-se outro.</summary>
    public AlteracaoEvento Alterar(DateTime inicio, int duracaoMinutos, Guid localId, Guid professorId, string? adversario,
        bool adversarioPermitido, string? logistica)
    {
        if (!Aberto) throw new InvalidOperationException($"Evento {Rotulo(Situacao).ToLowerInvariant()} não muda mais.");
        var antes = (Inicio, DuracaoMinutos, Local_ID, Professor_ID, Adversario, Logistica);
        Definir(inicio, duracaoMinutos, localId, professorId, adversario, adversarioPermitido, logistica);
        return new AlteracaoEvento(
            MudouQuando: antes.Inicio != Inicio || antes.DuracaoMinutos != DuracaoMinutos,
            MudouLocal: antes.Local_ID != Local_ID,
            MudouOutros: antes.Professor_ID != Professor_ID || antes.Adversario != Adversario || antes.Logistica != Logistica);
    }

    private void Definir(DateTime inicio, int duracaoMinutos, Guid localId, Guid professorId, string? adversario, bool adversarioPermitido,
        string? logistica)
    {
        if (duracaoMinutos is < DuracaoMinima or > DuracaoMaxima)
            throw new ArgumentOutOfRangeException(nameof(duracaoMinutos), duracaoMinutos, $"A duração vai de {DuracaoMinima} a {DuracaoMaxima} minutos.");
        if (localId == Guid.Empty) throw new ArgumentException("O evento tem local.", nameof(localId));
        if (professorId == Guid.Empty) throw new ArgumentException("O evento tem professor responsável.", nameof(professorId));
        var nomeAdversario = TextoHelper.Truncar(adversario?.Trim(), TamanhoAdversario);
        if (!string.IsNullOrEmpty(nomeAdversario) && !adversarioPermitido)
            throw new InvalidOperationException("A modalidade deste evento não tem adversário.");

        Inicio = inicio;
        DuracaoMinutos = duracaoMinutos;
        Local_ID = localId;
        Professor_ID = professorId;
        Adversario = string.IsNullOrEmpty(nomeAdversario) ? null : nomeAdversario;
        Logistica = string.IsNullOrWhiteSpace(logistica) ? null : TextoHelper.Truncar(logistica.Trim(), TamanhoLogistica);
    }

    public void Confirmar()
    {
        if (Situacao != SituacaoEvento.Agendado) throw new InvalidOperationException($"Só o evento agendado é confirmado (está {Rotulo(Situacao).ToLowerInvariant()}).");
        Situacao = SituacaoEvento.Confirmado;
    }

    /// <summary>O evento aconteceu: só depois de começar.</summary>
    public void Realizar(DateTime agora)
    {
        if (!Aberto) throw new InvalidOperationException($"Evento {Rotulo(Situacao).ToLowerInvariant()} não pode ser realizado.");
        if (Inicio > agora) throw new InvalidOperationException("O evento ainda não começou.");
        Situacao = SituacaoEvento.Realizado;
    }

    /// <summary>Cancelar ou adiar, com motivo (spec §6: os dois avisam todos e cancelam as convocações).</summary>
    public void Encerrar(bool adiar, string motivo, DateTime agora, string autorNome)
    {
        if (!Aberto) throw new InvalidOperationException($"Evento {Rotulo(Situacao).ToLowerInvariant()} não pode ser {(adiar ? "adiado" : "cancelado")}.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("Cancelar ou adiar exige motivo.", nameof(motivo));
        Situacao = adiar ? SituacaoEvento.Adiado : SituacaoEvento.Cancelado;
        Motivo = TextoHelper.Truncar(motivo.Trim(), TamanhoMotivo);
        EncerradoEm = agora;
        EncerradoPorNome = TextoHelper.Truncar(autorNome, TamanhoAutor);
    }

    /// <summary>"Jogo da Sub-11 contra o Tigre" (o nome das turmas vem de quem chama).</summary>
    public string Titulo(string turmas) =>
        $"{PoliticaPerfil.Rotulo(Tipo)} da {turmas}" + (Adversario is null ? string.Empty : $" contra {Adversario}");

    public static string Rotulo(SituacaoEvento situacao) => situacao switch
    {
        SituacaoEvento.Agendado => "Agendado",
        SituacaoEvento.Confirmado => "Confirmado",
        SituacaoEvento.Realizado => "Realizado",
        SituacaoEvento.ResultadoHomologado => "Resultado homologado",
        SituacaoEvento.Cancelado => "Cancelado",
        SituacaoEvento.Adiado => "Adiado",
        _ => situacao.ToString()
    };
}

/// <summary>Uma turma envolvida no evento (N:N). A chave nasce no banco: o filho novo entra pela coleção (D56).</summary>
public class EventoTurma : IEntidadeDaEscolinha
{
    public Guid EventoTurma_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Evento_ID { get; set; }
    public Guid Turma_ID { get; set; }
}
