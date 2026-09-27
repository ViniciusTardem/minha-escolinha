using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;
using Polly.Timeout;

namespace MinhaEscolinha.Infrastructure.Integracao.Stripe;

/// <summary>
/// Cliente da API do Stripe (modos Teste e Producao) com as duas chamadas que o sistema usa. HttpClient tipado com
/// resiliência (retentativa em 5xx, 429 e timeout); a criação da sessão leva uma chave de idempotência, então a
/// retentativa não abre um segundo pagamento. Falha sai classificada (<see cref="FalhaIntegracaoException"/>).
/// Sem SDK: o mesmo padrão dos clientes do AlternativaDepartamentoPessoal, sem dependência nova.
/// </summary>
public sealed class StripeClienteHttp(HttpClient http, OpcoesStripe opcoes, ILogger<StripeClienteHttp> logger) : IGatewayStripe
{
    public const string UrlApi = "https://api.stripe.com/";

    public async Task<SessaoCheckoutCriada> CriarSessaoCheckoutAsync(PedidoCheckout pedido, CancellationToken ct = default)
    {
        var campos = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("locale", "pt-BR"),
            new("client_reference_id", pedido.CompraId.ToString()),
            new("success_url", pedido.UrlSucesso),
            new("cancel_url", pedido.UrlCancelamento),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", "brl"),
            new("line_items[0][price_data][unit_amount]", pedido.ValorCentavos.ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", pedido.Descricao)
        };
        // Na sessão e no pagamento: o evento e o painel do Stripe mostram de que escolinha e compra se trata.
        foreach (var prefixo in new[] { "metadata", "payment_intent_data[metadata]" })
        {
            campos.Add(new($"{prefixo}[{MetadadosStripe.Origem}]", MetadadosStripe.ValorOrigem));
            campos.Add(new($"{prefixo}[{MetadadosStripe.Escolinha}]", pedido.EscolinhaId.ToString()));
            campos.Add(new($"{prefixo}[{MetadadosStripe.Compra}]", pedido.CompraId.ToString()));
        }
        if (!string.IsNullOrWhiteSpace(pedido.EmailCliente)) campos.Add(new("customer_email", pedido.EmailCliente));
        for (var i = 0; i < opcoes.MeiosPagamento.Length; i++) campos.Add(new($"payment_method_types[{i}]", opcoes.MeiosPagamento[i]));
        if (opcoes.DiasVencimentoBoleto is { } dias)
            campos.Add(new("payment_method_options[boleto][expires_after_days]", dias.ToString(CultureInfo.InvariantCulture)));

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "v1/checkout/sessions") { Content = new FormUrlEncodedContent(campos) };
        requisicao.Headers.Add("Idempotency-Key", $"compra-{pedido.CompraId:N}");
        using var resposta = await EnviarAsync(requisicao, ct);
        var raiz = resposta.RootElement;

        var id = Texto(raiz, "id") ?? throw RespostaIncompleta("id");
        var url = Texto(raiz, "url") ?? throw RespostaIncompleta("url");
        DateTime? expira = raiz.TryGetProperty("expires_at", out var e) && e.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(e.GetInt64()).UtcDateTime
            : null;
        return new SessaoCheckoutCriada(id, url, expira);
    }

    public async Task<SessaoCheckoutConsultada> ConsultarSessaoAsync(string sessaoId, CancellationToken ct = default)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Get,
            $"v1/checkout/sessions/{Uri.EscapeDataString(sessaoId)}?expand[]=payment_intent.payment_method");
        using var resposta = await EnviarAsync(requisicao, ct);
        var raiz = resposta.RootElement;

        string? pagamentoId = null, situacaoIntencao = null, meio = null;
        if (raiz.TryGetProperty("payment_intent", out var intencao))
        {
            if (intencao.ValueKind == JsonValueKind.String) pagamentoId = intencao.GetString();
            else if (intencao.ValueKind == JsonValueKind.Object)
            {
                pagamentoId = Texto(intencao, "id");
                situacaoIntencao = Texto(intencao, "status");
                if (intencao.TryGetProperty("payment_method", out var metodo) && metodo.ValueKind == JsonValueKind.Object)
                    meio = Texto(metodo, "type");
            }
        }

        var metadados = new Dictionary<string, string>(StringComparer.Ordinal);
        if (raiz.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
            foreach (var item in meta.EnumerateObject())
                if (item.Value.ValueKind == JsonValueKind.String) metadados[item.Name] = item.Value.GetString()!;

        return new SessaoCheckoutConsultada(
            Texto(raiz, "id") ?? sessaoId,
            Texto(raiz, "status") ?? throw RespostaIncompleta("status"),
            Texto(raiz, "payment_status") ?? throw RespostaIncompleta("payment_status"),
            pagamentoId, situacaoIntencao, meio, metadados);
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
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, $"Stripe inacessível: {ex.Message}", interna: ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "O Stripe demorou demais para responder.", interna: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "O Stripe demorou demais para responder.", interna: ex);
        }

        using (resposta)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(ct);
            if (!resposta.IsSuccessStatusCode)
            {
                var codigo = (int)resposta.StatusCode;
                var mensagem = MensagemDeErro(corpo) ?? resposta.ReasonPhrase ?? "erro sem mensagem";
                logger.LogWarning("Stripe respondeu {Codigo} em {Metodo} {Caminho}: {Mensagem}",
                    codigo, requisicao.Method, requisicao.RequestUri?.AbsolutePath, mensagem);
                throw new FalhaIntegracaoException(FalhaIntegracaoException.Classificar(codigo), $"Stripe {codigo}: {mensagem}", codigo);
            }
            try
            {
                return JsonDocument.Parse(corpo);
            }
            catch (JsonException ex)
            {
                throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "Resposta do Stripe ilegível.", interna: ex);
            }
        }
    }

    private static string? MensagemDeErro(string corpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            return documento.RootElement.TryGetProperty("error", out var erro) && erro.ValueKind == JsonValueKind.Object
                ? Texto(erro, "message") ?? Texto(erro, "code")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Texto(JsonElement elemento, string propriedade) =>
        elemento.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static FalhaIntegracaoException RespostaIncompleta(string campo) =>
        new(NaturezaFalha.Definitiva, $"Resposta do Stripe sem o campo {campo}.");
}
