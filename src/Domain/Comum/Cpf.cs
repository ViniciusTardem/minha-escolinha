namespace MinhaEscolinha.Domain.Comum;

/// <summary>CPF: só dígitos e validação pelo dígito verificador. Portado do AlternativaDepartamentoPessoal.</summary>
public static class Cpf
{
    /// <summary>
    /// Devolve os 11 dígitos se o CPF for válido; <c>null</c> se vazio, com tamanho errado, com todos os
    /// dígitos iguais (000.000.000-00...) ou com dígito verificador errado.
    /// </summary>
    public static string? Normalizar(string? texto)
    {
        var digitos = TextoHelper.SomenteDigitos(texto);
        return EhValido(digitos) ? digitos : null;
    }

    public static bool EhValido(string? digitos)
    {
        if (digitos is null || digitos.Length != 11 || !digitos.All(char.IsAsciiDigit)) return false;
        if (digitos.All(c => c == digitos[0])) return false;

        return digitos[9] - '0' == Digito(digitos, 9) && digitos[10] - '0' == Digito(digitos, 10);
    }

    /// <summary>"12345678909" → "123.456.789-09". Texto que não tem 11 dígitos volta como veio.</summary>
    public static string Formatar(string? digitos) =>
        digitos is { Length: 11 } ? $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}" : digitos ?? string.Empty;

    private static int Digito(string digitos, int posicao)
    {
        var soma = 0;
        for (var i = 0; i < posicao; i++)
            soma += (digitos[i] - '0') * (posicao + 1 - i);
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
