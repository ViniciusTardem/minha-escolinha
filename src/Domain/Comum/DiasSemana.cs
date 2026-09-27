namespace MinhaEscolinha.Domain.Comum;

/// <summary>Dias da semana em pt-BR, na ordem da escolinha (segunda primeiro).</summary>
public static class DiasSemana
{
    public static readonly IReadOnlyList<DayOfWeek> Ordem =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public static string Nome(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "Segunda",
        DayOfWeek.Tuesday => "Terça",
        DayOfWeek.Wednesday => "Quarta",
        DayOfWeek.Thursday => "Quinta",
        DayOfWeek.Friday => "Sexta",
        DayOfWeek.Saturday => "Sábado",
        _ => "Domingo"
    };

    public static string Abreviado(DayOfWeek dia) => Nome(dia)[..3];

    /// <summary>"na terça", "no sábado".</summary>
    public static string ComArtigo(DayOfWeek dia) =>
        (dia is DayOfWeek.Saturday or DayOfWeek.Sunday ? "no " : "na ") + Nome(dia).ToLowerInvariant();

    /// <summary>Posição na <see cref="Ordem"/> (segunda = 0), para ordenar horários.</summary>
    public static int Posicao(DayOfWeek dia) => ((int)dia + 6) % 7;
}

/// <summary>Um horário que se repete toda semana: "terça, das 18:00 às 19:00".</summary>
public readonly record struct HorarioSemanal(DayOfWeek Dia, TimeOnly Inicio, TimeOnly Fim)
{
    public bool Valido => Enum.IsDefined(Dia) && Fim > Inicio;

    public bool Sobrepoe(HorarioSemanal outro) => Dia == outro.Dia && Inicio < outro.Fim && outro.Inicio < Fim;

    public string Descricao() => $"{DiasSemana.Nome(Dia)}, {Inicio:HH\\:mm}–{Fim:HH\\:mm}";
}
