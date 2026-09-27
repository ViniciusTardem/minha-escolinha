using System.Globalization;

namespace MinhaEscolinha.Domain.Configuracao.Parametros;

/// <summary>
/// Faixa de horário do dia, que pode virar a meia-noite (21:00–07:00). Usada na janela de silêncio e na janela
/// de disparo das convocações. Início incluído, fim excluído.
/// </summary>
public readonly record struct JanelaHorario(TimeOnly Inicio, TimeOnly Fim)
{
    public bool ViraMeiaNoite => Fim < Inicio;

    public bool Contem(TimeOnly hora) =>
        Inicio == Fim
            ? false
            : ViraMeiaNoite ? hora >= Inicio || hora < Fim : hora >= Inicio && hora < Fim;

    /// <summary>"21:00-07:00".</summary>
    public string ParaTexto() => $"{Inicio:HH\\:mm}-{Fim:HH\\:mm}";

    public string ParaExibicao() => $"{Inicio:HH\\:mm} às {Fim:HH\\:mm}";

    public static bool TentarLer(string? texto, out JanelaHorario janela)
    {
        janela = default;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var partes = texto.Replace('–', '-').Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length != 2) return false;
        if (!TimeOnly.TryParseExact(partes[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio)) return false;
        if (!TimeOnly.TryParseExact(partes[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fim)) return false;

        janela = new JanelaHorario(inicio, fim);
        return true;
    }
}
