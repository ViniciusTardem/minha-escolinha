using System.Text.Json;

namespace MinhaEscolinha.Application.Integracao.Stripe;

/// <summary>
/// O mínimo que se lê de um evento do Stripe: id, tipo e, do objeto (a sessão de Checkout), o id, a referência e os
/// metadados. O estado do pagamento NÃO sai daqui: a conferência relê a sessão na API do Stripe antes de agir.
/// </summary>
public sealed record EventoStripe(string Id, string Tipo, string? ObjetoId, IReadOnlyDictionary<string, string> Metadados)
{
    /// <summary><c>null</c> se o corpo não é um evento do Stripe.</summary>
    public static EventoStripe? Ler(string corpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;
            if (Texto(raiz, "id") is not { } id || Texto(raiz, "type") is not { } tipo) return null;

            string? objetoId = null;
            var metadados = new Dictionary<string, string>(StringComparer.Ordinal);
            if (raiz.TryGetProperty("data", out var dados) && dados.ValueKind == JsonValueKind.Object
                && dados.TryGetProperty("object", out var objeto) && objeto.ValueKind == JsonValueKind.Object)
            {
                objetoId = Texto(objeto, "id");
                if (objeto.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
                {
                    foreach (var item in meta.EnumerateObject())
                        if (item.Value.ValueKind == JsonValueKind.String) metadados[item.Name] = item.Value.GetString()!;
                }
            }
            return new EventoStripe(id, tipo, objetoId, metadados);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Texto(JsonElement elemento, string propriedade) =>
        elemento.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
