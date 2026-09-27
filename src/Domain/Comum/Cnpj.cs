namespace MinhaEscolinha.Domain.Comum;

/// <summary>CNPJ: só dígitos e validação pelos dígitos verificadores (mesmo formato do <see cref="Cpf"/>).</summary>
public static class Cnpj
{
    private static readonly int[] PesosPrimeiro = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosSegundo = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Os 14 dígitos se o CNPJ for válido; <c>null</c> se vazio, com tamanho errado, repetido ou com dígito errado.</summary>
    public static string? Normalizar(string? texto)
    {
        var digitos = TextoHelper.SomenteDigitos(texto);
        return EhValido(digitos) ? digitos : null;
    }

    public static bool EhValido(string? digitos)
    {
        if (digitos is null || digitos.Length != 14 || !digitos.All(char.IsAsciiDigit)) return false;
        if (digitos.All(c => c == digitos[0])) return false;

        return digitos[12] - '0' == Digito(digitos, PesosPrimeiro) && digitos[13] - '0' == Digito(digitos, PesosSegundo);
    }

    /// <summary>"12345678000190" → "12.345.678/0001-90". Texto que não tem 14 dígitos volta como veio.</summary>
    public static string Formatar(string? digitos) =>
        digitos is { Length: 14 } ? $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}" : digitos ?? string.Empty;

    private static int Digito(string digitos, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
            soma += (digitos[i] - '0') * pesos[i];
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
