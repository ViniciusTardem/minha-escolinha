using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Mensageria;
using Polly.Timeout;

namespace MinhaEscolinha.Infrastructure.Integracao.WhatsApp;

/// <summary>
/// Cliente da Cloud API do WhatsApp (modo Api): manda template aprovado, com os parâmetros do corpo e o payload de cada
/// botão de resposta rápida. Sem SDK, no padrão dos clientes do DP. A mensagem não tem chave de idempotência na Meta,
/// então a resiliência não repete o POST (ele pode ter chegado): quem repete é a fila, com o limite de tentativas do motor.
/// </summary>
public sealed class WhatsAppClienteHttp(HttpClient http, OpcoesWhatsApp opcoes, ILogger<WhatsAppClienteHttp> logger) : IGatewayWhatsApp
{
    public const string UrlApi = "https://graph.facebook.com/";

    /// <summary>
    /// Erros da Meta que passam com o tempo (limite de envio, serviço indisponível): a fila tenta de novo. Os demais
    /// (template inexistente, parâmetro errado, número sem WhatsApp, token inválido) são definitivos.
    /// </summary>
    private static readonly HashSet<int> CodigosRetentaveis = [1, 2, 4, 80007, 130429, 131000, 131016, 131056, 133004];

    public async Task<EnvioWhatsApp> EnviarTemplateAsync(PedidoEnvioWhatsApp pedido, CancellationToken ct = default)
    {
        var componentes = new List<object>();
        if (pedido.Parametros.Count > 0)
            componentes.Add(new { type = "body", parameters = pedido.Parametros.Select(p => new { type = "text", text = p }).ToArray() });
        for (var i = 0; i < pedido.Botoes.Count; i++)
            componentes.Add(new
            {
                type = "button",
                sub_type = "quick_reply",
                index = i.ToString(CultureInfo.InvariantCulture),
                parameters = new[] { new { type = "payload", payload = PayloadBotao.Montar(pedido.MensagemId, i) } }
            });

        var corpo = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = pedido.Telefone,
            type = "template",
            template = new { name = pedido.Template, language = new { code = pedido.Idioma }, components = componentes }
        };

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, $"{opcoes.VersaoApi}/{Uri.EscapeDataString(opcoes.IdNumero)}/messages")
        {
            Content = JsonContent.Create(corpo)
        };
        using var resposta = await EnviarAsync(requisicao, ct);
        var raiz = resposta.RootElement;

        var id = raiz.TryGetProperty("messages", out var mensagens) && mensagens.ValueKind == JsonValueKind.Array
                 && mensagens.GetArrayLength() > 0 && mensagens[0].TryGetProperty("id", out var wamid) && wamid.ValueKind == JsonValueKind.String
            ? wamid.GetString()!
            : throw new FalhaIntegracaoException(NaturezaFalha.Definitiva, "Resposta do WhatsApp sem o id da mensagem.");
        var contato = raiz.TryGetProperty("contacts", out var contatos) && contatos.ValueKind == JsonValueKind.Array
                      && contatos.GetArrayLength() > 0 && contatos[0].TryGetProperty("wa_id", out var wa) && wa.ValueKind == JsonValueKind.String
            ? wa.GetString()
            : null;
        return new EnvioWhatsApp(id, contato);
    }

    private async Task<JsonDocument> EnviarAsync(HttpRequestMessage requisicao, CancellationToken ct)
    {
        HttpResponseMessage resposta;
        try
        {
            resposta = await http.SendAsync(requisicao, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, $"WhatsApp inacessível: {ex.Message}", interna: ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "O WhatsApp demorou demais para responder.", interna: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "O WhatsApp demorou demais para responder.", interna: ex);
        }

        using (resposta)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            if (!resposta.IsSuccessStatusCode)
            {
                var codigoHttp = (int)resposta.StatusCode;
                var (codigo, mensagem) = Erro(corpo);
                var natureza = codigo is { } c && CodigosRetentaveis.Contains(c)
                    ? NaturezaFalha.Retentavel
                    : FalhaIntegracaoException.Classificar(codigoHttp);
                logger.LogWarning("WhatsApp respondeu {Http} (erro {Codigo}): {Mensagem}", codigoHttp, codigo, mensagem);
                throw new FalhaIntegracaoException(natureza,
                    $"WhatsApp {codigoHttp}{(codigo is null ? "" : $" (erro {codigo})")}: {mensagem}", codigoHttp);
            }
            try
            {
                return JsonDocument.Parse(corpo);
            }
            catch (JsonException ex)
            {
                throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "Resposta do WhatsApp ilegível.", interna: ex);
            }
        }
    }

    private static (int? Codigo, string Mensagem) Erro(string corpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            if (!documento.RootElement.TryGetProperty("error", out var erro) || erro.ValueKind != JsonValueKind.Object)
                return (null, "erro sem mensagem");
            int? codigo = erro.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null;
            var mensagem = erro.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()! : "erro sem mensagem";
            if (erro.TryGetProperty("error_data", out var dados) && dados.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.String)
                mensagem += $" ({d.GetString()})";
            return (codigo, mensagem);
        }
        catch (JsonException)
        {
            return (null, "erro sem mensagem");
        }
    }
}
