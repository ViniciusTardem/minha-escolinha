using System.Globalization;
using System.Text;

namespace MinhaEscolinha.Domain.Comum;

/// <summary>
/// Cortes defensivos de string antes de gravar no banco e normalização para busca.
/// Portado do AlternativaDepartamentoPessoal (que veio da Plataforma de Vagas).
/// </summary>
public static class TextoHelper
{
    /// <summary>
    /// Faz <c>Trim()</c> e corta em <paramref name="max"/> unidades UTF-16 — a mesma unidade
    /// que <c>nvarchar(n)</c> do SQL Server conta. Nunca parte um par surrogate no meio
    /// (emoji colado do WhatsApp geraria caractere inválido).
    /// Devolve <c>null</c>/vazio inalterado.
    /// </summary>
    public static string? Truncar(string? valor, int max)
    {
        if (string.IsNullOrEmpty(valor)) return valor;

        var v = valor.Trim();
        if (v.Length <= max) return v;
        if (max <= 0) return string.Empty;

        // Se o corte cairia entre o high e o low surrogate, recua uma posição.
        if (char.IsHighSurrogate(v[max - 1])) max--;

        return v[..max];
    }

    /// <summary>Só os dígitos de <paramref name="s"/>. Vazio quando null.</summary>
    public static string SomenteDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? "" : new string(s.Where(char.IsDigit).ToArray());

    /// <summary>
    /// Texto para comparar e buscar: sem acento, minúsculo, espaços colapsados ("  João  da Silva " → "joao da silva").
    /// O banco usa collation sensível a acento (SQL_Latin1_General_CP1_CI_AS): a busca por nome compara esta forma.
    /// </summary>
    public static string NormalizarParaBusca(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        var decomposto = s.Trim().Normalize(NormalizationForm.FormD);
        var saida = new StringBuilder(decomposto.Length);
        var espacoAnterior = false;
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!espacoAnterior) saida.Append(' ');
                espacoAnterior = true;
                continue;
            }
            saida.Append(char.ToLowerInvariant(c));
            espacoAnterior = false;
        }
        return saida.ToString().Normalize(NormalizationForm.FormC);
    }
}
