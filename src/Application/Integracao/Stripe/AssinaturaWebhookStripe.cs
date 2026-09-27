using System.Security.Cryptography;
using System.Text;

namespace MinhaEscolinha.Application.Integracao.Stripe;

/// <summary>
/// Conferência do cabeçalho <c>Stripe-Signature</c> (<c>t=...,v1=...</c>): HMAC-SHA256 de "<c>{t}.{corpo}</c>" com o
/// segredo do endpoint, comparado em tempo constante, e o carimbo dentro da tolerância (evita reenvio de um evento antigo).
/// O corpo precisa ser o bruto, exatamente como chegou.
/// </summary>
public static class AssinaturaWebhookStripe
{
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(5);

    public static bool Valida(string corpo, string? cabecalho, string segredo, DateTimeOffset agora)
    {
        if (string.IsNullOrWhiteSpace(cabecalho) || string.IsNullOrEmpty(segredo)) return false;

        long? carimbo = null;
        var assinaturas = new List<string>();
        foreach (var parte in cabecalho.Split(','))
        {
            var chaveValor = parte.Split('=', 2);
            if (chaveValor.Length != 2) continue;
            var chave = chaveValor[0].Trim();
            var valor = chaveValor[1].Trim();
            if (chave == "t" && long.TryParse(valor, out var t)) carimbo = t;
            else if (chave == "v1") assinaturas.Add(valor);
        }
        if (carimbo is not { } segundos || assinaturas.Count == 0) return false;
        if (Math.Abs(agora.ToUnixTimeSeconds() - segundos) > Tolerancia.TotalSeconds) return false;

        var esperada = Encoding.ASCII.GetBytes(Calcular(corpo, segundos, segredo));
        return assinaturas.Any(a => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), esperada));
    }

    public static string Calcular(string corpo, long carimbo, string segredo)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes($"{carimbo}.{corpo}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>O cabeçalho como o Stripe manda (usado pelo simulador e pelos testes).</summary>
    public static string Cabecalho(string corpo, long carimbo, string segredo) => $"t={carimbo},v1={Calcular(corpo, carimbo, segredo)}";
}
