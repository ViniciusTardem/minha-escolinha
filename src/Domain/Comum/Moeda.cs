using System.Globalization;

namespace MinhaEscolinha.Domain.Comum;

/// <summary>Leitura de valor em reais digitado na tela: o gestor digita "150", "150,50", "1.500,00" ou "R$ 1.500,00".</summary>
public static class Moeda
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>
    /// Entende a vírgula como decimal (e o ponto como milhar). Sem vírgula, um único ponto seguido de 1 ou 2 dígitos é o
    /// decimal ("150.50", como o navegador e os arquivos mandam); qualquer outro ponto é de milhar ("1.500" = mil e quinhentos).
    /// Arredonda para centavos.
    /// </summary>
    public static bool TentarLer(string? texto, out decimal valor)
    {
        valor = 0;
        var limpo = texto?.Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace(" ", "").Trim();
        if (string.IsNullOrEmpty(limpo)) return false;

        bool lido;
        if (limpo.Contains(','))
        {
            lido = decimal.TryParse(limpo, NumberStyles.Number, PtBr, out valor);
        }
        else
        {
            var ponto = limpo.IndexOf('.');
            var pontoDecimal = ponto >= 0 && ponto == limpo.LastIndexOf('.') && limpo.Length - ponto - 1 is 1 or 2;
            lido = decimal.TryParse(limpo, NumberStyles.Number, pontoDecimal ? CultureInfo.InvariantCulture : PtBr, out valor);
        }

        if (!lido) return false;
        valor = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
        return true;
    }

    /// <summary>"1.500,00" (sem o símbolo): o valor de um campo de edição.</summary>
    public static string ParaCampo(decimal? valor) => valor?.ToString("#,##0.00", PtBr) ?? string.Empty;
}
