using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Turmas;

/// <summary>
/// Quadra ou local de treino e de jogo (spec §5): consumido pela turma (e pelas sessões geradas dela) e, no S5, pelo evento.
/// Os horários disponíveis do local (P31: a quadra alugada só em certos dias e horários) geram alerta quando um treino
/// ou jogo é marcado fora deles; a ocupação do local aparece na agenda (BL-M04-08).
/// </summary>
public class Local : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 100;
    public const int TamanhoObservacao = 300;

    public Guid Local_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public string Nome { get; set; } = string.Empty;
    public Endereco Endereco { get; set; } = new();

    /// <summary>Quantas pessoas o local comporta (opcional, informativo).</summary>
    public int? Capacidade { get; set; }

    /// <summary>Como chegar, portão de entrada, estacionamento.</summary>
    public string? Observacao { get; set; }

    /// <summary>Quando a escolinha pode usar o local (P31). Sem faixa, o local está sempre disponível: nenhum alerta.</summary>
    public List<FaixaHorarioLocal> Horarios { get; set; } = [];

    public bool TemHorariosDeclarados => Horarios.Count > 0;

    public bool Cobre(DayOfWeek dia, TimeOnly inicio, TimeOnly fim) =>
        !TemHorariosDeclarados || Horarios.Any(h => h.DiaSemana == dia && h.Inicio <= inicio && fim <= h.Fim);

    /// <summary>"Sáb 08:00–12:00 · Dom 08:00–12:00" ou "Sempre disponível".</summary>
    public string ResumoHorarios() =>
        TemHorariosDeclarados
            ? string.Join(" · ", Horarios.OrderBy(h => DiasSemana.Posicao(h.DiaSemana)).ThenBy(h => h.Inicio)
                .Select(h => $"{DiasSemana.Abreviado(h.DiaSemana)} {h.Inicio:HH\\:mm}–{h.Fim:HH\\:mm}"))
            : "Sempre disponível";

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }
}

/// <summary>Uma faixa semanal em que a escolinha pode usar o local (P31): "sábado, das 08:00 às 12:00".</summary>
public class FaixaHorarioLocal : IEntidadeDaEscolinha
{
    public Guid FaixaHorarioLocal_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Local_ID { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly Inicio { get; set; }
    public TimeOnly Fim { get; set; }
}
