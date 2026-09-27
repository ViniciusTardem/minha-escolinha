namespace MinhaEscolinha.Domain.Comum;

/// <summary>
/// Telefone brasileiro no formato E.164 (+55DDNNNNNNNNN) — o formato que a API do WhatsApp espera.
/// Portado do AlternativaDepartamentoPessoal.
/// </summary>
public static class Telefone
{
    /// <summary>Número inteiro (DDD dentro) ou DDD separado. Devolve <c>null</c> quando não dá para montar um número válido.</summary>
    public static string? ParaE164(string? numero) => ParaE164(null, numero);

    /// <summary>
    /// Junta DDD e número (às vezes o DDD vem dentro do número).
    /// Devolve <c>null</c> quando não dá para montar um número brasileiro válido (10 ou 11 dígitos com DDD).
    /// </summary>
    public static string? ParaE164(string? ddd, string? numero)
    {
        var num = TextoHelper.SomenteDigitos(numero);
        if (num.Length == 0) return null;

        // Já veio com o código do país.
        if (num.StartsWith("55") && num.Length is 12 or 13) return Valido(num[2..]) ? "+" + num : null;

        // Discagem com zero na frente (0 + DDD + número).
        if (num.StartsWith('0') && num.Length is 11 or 12) num = num[1..];

        // Já veio com o DDD dentro do número.
        if (num.Length is 10 or 11) return Valido(num) ? "+55" + num : null;

        // DDD separado: 2 dígitos ("011" → "11"; "05" é inválido, não vira "5").
        var dddDigitos = TextoHelper.SomenteDigitos(ddd);
        if (dddDigitos.Length == 3 && dddDigitos[0] == '0') dddDigitos = dddDigitos[1..];
        if (dddDigitos.Length != 2) return null;

        var completo = dddDigitos + num;
        return completo.Length is 10 or 11 && Valido(completo) ? "+55" + completo : null;
    }

    /// <summary>O número como a API do WhatsApp recebe: só dígitos, com o código do país ("+5543988887777" → "5543988887777").</summary>
    public static string ParaWhatsApp(string e164) => TextoHelper.SomenteDigitos(e164);

    /// <summary>
    /// Chave para casar números do WhatsApp: no Brasil, o id da conta (wa_id) de muitos celulares vem sem o 9º dígito
    /// ("554388887777" para "+5543988887777"). A chave tira o 9 do celular brasileiro; os outros números ficam só com os dígitos.
    /// </summary>
    public static string ChaveWhatsApp(string? numero)
    {
        var digitos = TextoHelper.SomenteDigitos(numero);
        return digitos.Length == 13 && digitos.StartsWith("55", StringComparison.Ordinal) && digitos[4] == '9'
            ? digitos[..4] + digitos[5..]
            : digitos;
    }

    /// <summary>DDD entre 11 e 99; celular (11 dígitos) começa com 9.</summary>
    private static bool Valido(string comDdd)
    {
        if (comDdd.Length is not (10 or 11)) return false;
        var ddd = int.Parse(comDdd[..2]);
        if (ddd < 11) return false;
        return comDdd.Length == 10 || comDdd[2] == '9';
    }
}
