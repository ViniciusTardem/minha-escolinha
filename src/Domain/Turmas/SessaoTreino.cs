using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Turmas;

/// <summary>Máquina da sessão (spec §6): prevista → realizada | cancelada | remarcada.</summary>
public enum SituacaoSessao
{
    Prevista = 10,

    /// <summary>A chamada foi feita (S3).</summary>
    Realizada = 20,

    Cancelada = 30,

    /// <summary>Existe na máquina da spec, mas a remarcação (BL-M04-07) foi adiada: no piloto, remarcar = cancelar + criar (P11).</summary>
    Remarcada = 40
}

/// <summary>
/// Ocorrência datada de um horário da turma (BL-M04-04) — é sobre ela que a presença é registrada (S3).
/// Guarda o local e o professor do momento em que foi gerada; é um compromisso datado da agenda do professor e da
/// quadra (BL-M04-08), como o evento do S5.
/// </summary>
public class SessaoTreino : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoMotivo = 300;
    public const int TamanhoAutor = 150;

    public Guid SessaoTreino_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Turma_ID { get; set; }
    public Turma? Turma { get; set; }

    /// <summary>O horário que gerou a sessão. Nulo quando o horário foi retirado da turma depois (a sessão fica no histórico).</summary>
    public Guid? HorarioTurma_ID { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>Horário de Brasília.</summary>
    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }
    public Guid Local_ID { get; set; }
    public Local? Local { get; set; }
    public Guid Professor_ID { get; set; }
    public Equipe.Professor? Professor { get; set; }

    public SituacaoSessao Situacao { get; set; } = SituacaoSessao.Prevista;

    public string? MotivoCancelamento { get; set; }
    public DateTime? CanceladaEm { get; set; }
    public Guid? CanceladaPor_ID { get; set; }
    public string? CanceladaPorNome { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>
    /// O que a geração pode refazer quando o padrão da turma muda: só a sessão ainda prevista e que ainda não começou.
    /// Cancelada, realizada ou já começada fica como está (histórico).
    /// </summary>
    public bool Mutavel(DateTime agora) => Situacao == SituacaoSessao.Prevista && Inicio >= agora;

    public bool PodeCancelar => Situacao == SituacaoSessao.Prevista;

    public void Cancelar(string motivo, DateTime agora, Guid? autorId, string? autorNome)
    {
        if (!PodeCancelar) throw new InvalidOperationException($"Sessão {Situacao} não pode ser cancelada.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("O cancelamento exige motivo.", nameof(motivo));
        Situacao = SituacaoSessao.Cancelada;
        MotivoCancelamento = TextoHelper.Truncar(motivo, TamanhoMotivo);
        CanceladaEm = agora;
        CanceladaPor_ID = autorId;
        CanceladaPorNome = TextoHelper.Truncar(autorNome ?? "Sistema", TamanhoAutor);
    }
}

/// <summary>Uma sessão que o padrão da turma pede: o horário, a data e o início/fim em Brasília.</summary>
public sealed record OcorrenciaSessao(Guid HorarioTurmaId, DateOnly Data, DateTime Inicio, DateTime Fim);

/// <summary>O que a geração vai fazer numa turma: criar as que faltam, ajustar as previstas que mudaram e retirar as que sobraram.</summary>
public sealed record PlanoSessoes(
    IReadOnlyList<OcorrenciaSessao> Criar,
    IReadOnlyList<(SessaoTreino Sessao, OcorrenciaSessao Ocorrencia)> Ajustar,
    IReadOnlyList<SessaoTreino> Remover)
{
    public bool Vazio => Criar.Count == 0 && Ajustar.Count == 0 && Remover.Count == 0;
}

/// <summary>
/// Geração das sessões a partir dos horários da turma (BL-M04-04), num horizonte rolante: de hoje até o fim do mês
/// seguinte (as sessões do mês sempre existem, e o recorrente diário empurra o horizonte).
/// <para>
/// A geração é uma projeção do padrão da turma e é idempotente pela chave (horário, data): rodar de novo não duplica.
/// Quando o padrão muda (horário, local, professor, data de início, turma encerrada), as sessões <b>previstas que ainda
/// não começaram</b> acompanham; cancelada, realizada ou já começada nunca é tocada. Uma sessão cancelada continua
/// ocupando a sua chave, então a geração não a recria.
/// </para>
/// </summary>
public static class GeradorSessoes
{
    /// <summary>Último dia do mês seguinte ao de hoje.</summary>
    public static DateOnly FimDoHorizonte(DateOnly hoje) => new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(2).AddDays(-1);

    /// <summary>As ocorrências dos horários da turma entre as duas datas (inclusive), em ordem.</summary>
    public static IEnumerable<OcorrenciaSessao> Ocorrencias(Turma turma, DateOnly de, DateOnly ate)
    {
        for (var data = de; data <= ate; data = data.AddDays(1))
        {
            foreach (var horario in turma.Horarios.Where(h => h.DiaSemana == data.DayOfWeek).OrderBy(h => h.Inicio))
            {
                if (horario.Fim <= horario.Inicio) continue;
                yield return new OcorrenciaSessao(horario.HorarioTurma_ID, data,
                    data.ToDateTime(horario.Inicio), data.ToDateTime(horario.Fim));
            }
        }
    }

    /// <summary>Início e fim das ocorrências de horários semanais (ainda não gravados) entre as duas datas.</summary>
    public static IEnumerable<(DateTime Inicio, DateTime Fim)> Ocorrencias(IReadOnlyCollection<HorarioSemanal> horarios, DateOnly de, DateOnly ate)
    {
        for (var data = de; data <= ate; data = data.AddDays(1))
            foreach (var horario in horarios.Where(h => h.Dia == data.DayOfWeek && h.Fim > h.Inicio).OrderBy(h => h.Inicio))
                yield return (data.ToDateTime(horario.Inicio), data.ToDateTime(horario.Fim));
    }

    /// <param name="existentes">As sessões da turma de hoje em diante (todas as situações: as canceladas também seguram a chave).</param>
    public static PlanoSessoes Planejar(Turma turma, IReadOnlyCollection<SessaoTreino> existentes, DateTime agora)
    {
        var hoje = DateOnly.FromDateTime(agora);
        var de = turma.DataInicio > hoje ? turma.DataInicio : hoje;
        var planejadas = turma.Ativa
            ? Ocorrencias(turma, de, FimDoHorizonte(hoje)).Where(o => o.Inicio >= agora).ToList()
            : [];

        var porChave = existentes
            .Where(s => s.HorarioTurma_ID is not null)
            .GroupBy(s => (Horario: s.HorarioTurma_ID!.Value, s.Data))
            .ToDictionary(g => g.Key, g => g.First());

        var criar = new List<OcorrenciaSessao>();
        var ajustar = new List<(SessaoTreino, OcorrenciaSessao)>();
        var usadas = new HashSet<SessaoTreino>();
        foreach (var ocorrencia in planejadas)
        {
            if (!porChave.TryGetValue((ocorrencia.HorarioTurmaId, ocorrencia.Data), out var sessao))
            {
                criar.Add(ocorrencia);
                continue;
            }

            usadas.Add(sessao);
            if (sessao.Mutavel(agora) && (sessao.Inicio != ocorrencia.Inicio || sessao.Fim != ocorrencia.Fim
                                          || sessao.Local_ID != turma.Local_ID || sessao.Professor_ID != turma.Professor_ID))
                ajustar.Add((sessao, ocorrencia));
        }

        var remover = existentes.Where(s => s.Mutavel(agora) && !usadas.Contains(s)).ToList();
        return new PlanoSessoes(criar, ajustar, remover);
    }

    /// <summary>A sessão nova de uma ocorrência, com o local e o professor atuais da turma.</summary>
    public static SessaoTreino Nova(Turma turma, OcorrenciaSessao ocorrencia) => new()
    {
        Turma_ID = turma.Turma_ID,
        HorarioTurma_ID = ocorrencia.HorarioTurmaId,
        Data = ocorrencia.Data,
        Inicio = ocorrencia.Inicio,
        Fim = ocorrencia.Fim,
        Local_ID = turma.Local_ID,
        Professor_ID = turma.Professor_ID,
        Situacao = SituacaoSessao.Prevista
    };

    public static void Ajustar(SessaoTreino sessao, Turma turma, OcorrenciaSessao ocorrencia)
    {
        sessao.Inicio = ocorrencia.Inicio;
        sessao.Fim = ocorrencia.Fim;
        sessao.Local_ID = turma.Local_ID;
        sessao.Professor_ID = turma.Professor_ID;
    }
}
