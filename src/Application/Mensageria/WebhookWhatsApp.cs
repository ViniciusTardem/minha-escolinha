using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MinhaEscolinha.Application.Mensageria;

/// <summary>
/// Assinatura dos webhooks da Meta: <c>X-Hub-Signature-256: sha256=&lt;HMAC-SHA256 do corpo com o segredo do app&gt;</c>.
/// Conferida sobre o corpo bruto, antes de qualquer leitura.
/// </summary>
public static class AssinaturaWebhookWhatsApp
{
    public const string Cabecalho = "X-Hub-Signature-256";

    public static bool Valida(string corpo, string? assinatura, string segredo)
    {
        if (string.IsNullOrWhiteSpace(assinatura) || string.IsNullOrWhiteSpace(segredo)) return false;
        const string prefixo = "sha256=";
        if (!assinatura.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)) return false;

        var esperada = Calcular(corpo, segredo);
        var recebida = assinatura[prefixo.Length..].Trim().ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperada), Encoding.ASCII.GetBytes(recebida));
    }

    public static string Calcular(string corpo, string segredo) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(segredo), Encoding.UTF8.GetBytes(corpo))).ToLowerInvariant();

    public static string Montar(string corpo, string segredo) => $"sha256={Calcular(corpo, segredo)}";

    /// <summary>A Meta não manda id de evento: o hash do corpo identifica o reenvio do mesmo webhook.</summary>
    public static string IdDoCorpo(string corpo) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(corpo))).ToLowerInvariant();
}

/// <summary>Retorno de entrega de uma mensagem enviada (<c>statuses</c>).</summary>
/// <param name="Status">sent · delivered · read · failed.</param>
public sealed record StatusWhatsApp(string IdExterno, string Status, DateTime? EmUtc, string? Destinatario, string? Erro);

/// <summary>Mensagem que a pessoa mandou (<c>messages</c>): texto, botão de resposta rápida ou resposta interativa.</summary>
/// <param name="Contexto">O wamid da mensagem respondida (quando a pessoa respondeu a uma mensagem nossa).</param>
public sealed record MensagemRecebidaWhatsApp(string IdExterno, string De, DateTime? EmUtc, string Tipo, string? Texto, string? Payload,
    string? Contexto);

/// <summary>O que o sistema lê de um webhook da Cloud API (o resto fica no corpo bruto gravado).</summary>
public sealed record EventoWhatsApp(IReadOnlyList<StatusWhatsApp> Status, IReadOnlyList<MensagemRecebidaWhatsApp> Mensagens)
{
    public bool Vazio => Status.Count == 0 && Mensagens.Count == 0;

    /// <summary><c>null</c> se o corpo não é um webhook do WhatsApp legível.</summary>
    public static EventoWhatsApp? Ler(string corpo)
    {
        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || Texto(raiz, "object") != "whatsapp_business_account") return null;

            var status = new List<StatusWhatsApp>();
            var mensagens = new List<MensagemRecebidaWhatsApp>();
            foreach (var entrada in Lista(raiz, "entry"))
            foreach (var mudanca in Lista(entrada, "changes"))
            {
                if (!mudanca.TryGetProperty("value", out var valor) || valor.ValueKind != JsonValueKind.Object) continue;

                foreach (var s in Lista(valor, "statuses"))
                {
                    if (Texto(s, "id") is not { } id || Texto(s, "status") is not { } situacao) continue;
                    var erro = Lista(s, "errors").Select(e =>
                    {
                        var detalhe = e.TryGetProperty("error_data", out var dados) ? Texto(dados, "details") : null;
                        var codigo = e.TryGetProperty("code", out var c) ? c.ToString() : "?";
                        return $"{codigo}: {Texto(e, "title") ?? Texto(e, "message") ?? "erro"}{(detalhe is null ? "" : $" ({detalhe})")}";
                    }).FirstOrDefault();
                    status.Add(new StatusWhatsApp(id, situacao, Momento(s), Texto(s, "recipient_id"), erro));
                }

                foreach (var m in Lista(valor, "messages"))
                {
                    if (Texto(m, "id") is not { } id || Texto(m, "from") is not { } de) continue;
                    var tipo = Texto(m, "type") ?? "desconhecido";
                    string? texto = null, payload = null;
                    switch (tipo)
                    {
                        case "text" when m.TryGetProperty("text", out var t):
                            texto = Texto(t, "body");
                            break;
                        case "button" when m.TryGetProperty("button", out var b):
                            texto = Texto(b, "text");
                            payload = Texto(b, "payload");
                            break;
                        case "interactive" when m.TryGetProperty("interactive", out var i):
                            if (i.TryGetProperty("button_reply", out var br))
                            {
                                texto = Texto(br, "title");
                                payload = Texto(br, "id");
                            }
                            break;
                    }
                    var contexto = m.TryGetProperty("context", out var ctx) ? Texto(ctx, "id") : null;
                    mensagens.Add(new MensagemRecebidaWhatsApp(id, de, Momento(m), tipo, texto, payload, contexto));
                }
            }
            return new EventoWhatsApp(status, mensagens);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Lista(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var lista) && lista.ValueKind == JsonValueKind.Array ? lista.EnumerateArray() : [];

    private static string? Texto(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static DateTime? Momento(JsonElement elemento) =>
        long.TryParse(Texto(elemento, "timestamp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos)
            ? DateTimeOffset.FromUnixTimeSeconds(segundos).UtcDateTime
            : null;
}
