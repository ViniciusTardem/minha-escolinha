using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Domain.Configuracao;
using Polly.Timeout;

namespace MinhaEscolinha.Infrastructure.Integracao.Asaas;

/// <summary>
/// Cliente da API da Asaas (modo Api), sem SDK, no padrão dos clientes do DP. A chave é a da conta de cada escolinha (R1)
/// e vai no cabeçalho <c>access_token</c> de cada chamada; o endereço depende do ambiente da conta (Sandbox ou Produção).
/// A criação de cobrança não é idempotente na Asaas: a resiliência não repete POST, e quem repete é o job, que antes
/// procura a cobrança pela referência externa (a nossa).
/// </summary>
public sealed class AsaasClienteHttp(HttpClient http, ILogger<AsaasClienteHttp> logger) : IGatewayAsaas
{
    public const string UrlSandbox = "https://api-sandbox.asaas.com/v3/";
    public const string UrlProducao = "https://api.asaas.com/v3/";
    public const string AgenteUsuario = "MinhaEscolinha/1.0";

    public async Task<string?> BuscarClienteAsync(ContaAsaasAcesso conta, Guid responsavelId, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(conta, HttpMethod.Get, $"customers?externalReference={responsavelId}&limit=1", null, ct);
        return Primeiro(resposta!.RootElement) is { } cliente ? Texto(cliente, "id") : null;
    }

    public async Task<string> CriarClienteAsync(ContaAsaasAcesso conta, ClienteAsaasPedido pedido, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(conta, HttpMethod.Post, "customers", new
        {
            name = pedido.Nome,
            cpfCnpj = pedido.Cpf,
            email = string.IsNullOrWhiteSpace(pedido.Email) ? null : pedido.Email,
            mobilePhone = CelularNacional(pedido.Celular),
            externalReference = pedido.ResponsavelId.ToString(),
            // Só o motor da escolinha fala com a família (R3): a Asaas não manda e-mail nem SMS por conta própria.
            notificationDisabled = true
        }, ct);
        return Texto(resposta!.RootElement, "id")
               ?? throw new FalhaIntegracaoException(NaturezaFalha.Definitiva, "Resposta da Asaas sem o id do cliente.");
    }

    public async Task<PagamentoAsaas?> BuscarCobrancaAsync(ContaAsaasAcesso conta, Guid cobrancaId, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(conta, HttpMethod.Get, $"payments?externalReference={cobrancaId}&limit=1", null, ct);
        return Primeiro(resposta!.RootElement) is { } pagamento ? EventoAsaas.LerPagamento(pagamento) : null;
    }

    public async Task<PagamentoAsaas> CriarCobrancaAsync(ContaAsaasAcesso conta, CobrancaAsaasPedido pedido, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(conta, HttpMethod.Post, "payments", new
        {
            customer = pedido.ClienteId,
            // A família escolhe na fatura: Pix, boleto ou cartão (R1).
            billingType = "UNDEFINED",
            value = pedido.Valor,
            dueDate = pedido.Vencimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            description = pedido.Descricao,
            externalReference = pedido.CobrancaId.ToString(),
            fine = pedido.MultaPercentual > 0 ? new { value = pedido.MultaPercentual, type = "PERCENTAGE" } : null,
            interest = pedido.JurosPercentualMes > 0 ? new { value = pedido.JurosPercentualMes } : null
        }, ct);
        return EventoAsaas.LerPagamento(resposta!.RootElement)
               ?? throw new FalhaIntegracaoException(NaturezaFalha.Definitiva, "Resposta da Asaas sem o id da cobrança.");
    }

    public async Task<PagamentoAsaas?> ObterCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(conta, HttpMethod.Get, $"payments/{Uri.EscapeDataString(id)}", null, ct, aceitar404: true);
        return resposta is null ? null : EventoAsaas.LerPagamento(resposta.RootElement);
    }

    public async Task<PixAsaas?> ObterPixAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default)
    {
        using var resposta = await EnviarAsync(conta, HttpMethod.Get, $"payments/{Uri.EscapeDataString(id)}/pixQrCode", null, ct, aceitar404: true);
        if (resposta is null || Texto(resposta.RootElement, "payload") is not { Length: > 0 } copiaECola) return null;
        // A validade vem no horário de Brasília, como as demais datas da Asaas.
        DateTime? expiraEm = DateTime.TryParseExact(Texto(resposta.RootElement, "expirationDate"), "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data : null;
        return new PixAsaas(copiaECola, expiraEm);
    }

    public async Task ExcluirCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default)
    {
        using var _ = await EnviarAsync(conta, HttpMethod.Delete, $"payments/{Uri.EscapeDataString(id)}", null, ct, aceitar404: true);
    }

    public async Task ReceberEmDinheiroAsync(ContaAsaasAcesso conta, string id, DateOnly data, decimal valor, CancellationToken ct = default)
    {
        using var _ = await EnviarAsync(conta, HttpMethod.Post, $"payments/{Uri.EscapeDataString(id)}/receiveInCash", new
        {
            paymentDate = data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            value = valor,
            notifyCustomer = false
        }, ct);
    }

    public async Task RegistrarWebhookAsync(ContaAsaasAcesso conta, string url, string token, string? email, CancellationToken ct = default)
    {
        using var lista = await EnviarAsync(conta, HttpMethod.Get, "webhooks?limit=100", null, ct);
        var existente = lista!.RootElement.TryGetProperty("data", out var dados) && dados.ValueKind == JsonValueKind.Array
            ? dados.EnumerateArray().FirstOrDefault(w => string.Equals(Texto(w, "url"), url, StringComparison.OrdinalIgnoreCase))
            : default;
        var corpo = new
        {
            name = "Minha Escolinha — mensalidades",
            url,
            email,
            enabled = true,
            interrupted = false,
            apiVersion = 3,
            authToken = token,
            sendType = "SEQUENTIALLY",
            events = EventoAsaas.Assinados
        };
        if (existente.ValueKind == JsonValueKind.Object && Texto(existente, "id") is { } id)
        {
            using var _ = await EnviarAsync(conta, HttpMethod.Put, $"webhooks/{Uri.EscapeDataString(id)}", corpo, ct);
        }
        else
        {
            using var _ = await EnviarAsync(conta, HttpMethod.Post, "webhooks", corpo, ct);
        }
    }

    /// <returns><c>null</c> no 404 aceito.</returns>
    private async Task<JsonDocument?> EnviarAsync(ContaAsaasAcesso conta, HttpMethod metodo, string caminho, object? corpo, CancellationToken ct,
        bool aceitar404 = false)
    {
        var baseUrl = conta.Ambiente == AmbienteAsaas.Producao ? UrlProducao : UrlSandbox;
        using var requisicao = new HttpRequestMessage(metodo, new Uri(new Uri(baseUrl), caminho));
        requisicao.Headers.Add("access_token", conta.ChaveApi);
        requisicao.Headers.UserAgent.ParseAdd(AgenteUsuario);
        if (corpo is not null) requisicao.Content = JsonContent.Create(corpo, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        HttpResponseMessage resposta;
        try
        {
            resposta = await http.SendAsync(requisicao, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, $"Asaas inacessível: {ex.Message}", interna: ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "A Asaas demorou demais para responder.", interna: ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "A Asaas demorou demais para responder.", interna: ex);
        }

        using (resposta)
        {
            if (aceitar404 && resposta.StatusCode == HttpStatusCode.NotFound) return null;
            var texto = await resposta.Content.ReadAsStringAsync(ct);
            if (!resposta.IsSuccessStatusCode)
            {
                var codigoHttp = (int)resposta.StatusCode;
                var (codigo, mensagem) = Erro(texto);
                logger.LogWarning("Asaas respondeu {Http} em {Metodo} {Caminho} ({Codigo}): {Mensagem}", codigoHttp, metodo, SemConsulta(caminho), codigo, mensagem);
                throw new FalhaIntegracaoException(FalhaIntegracaoException.Classificar(codigoHttp),
                    $"Asaas {codigoHttp}{(codigo is null ? "" : $" ({codigo})")}: {mensagem}", codigoHttp);
            }
            try
            {
                return JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "{}" : texto);
            }
            catch (JsonException ex)
            {
                throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "Resposta da Asaas ilegível.", interna: ex);
            }
        }
    }

    /// <summary>A Asaas quer o celular com DDD e sem o 55 ("43988887777").</summary>
    public static string? CelularNacional(string? telefone)
    {
        var digitos = new string((telefone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55", StringComparison.Ordinal)) digitos = digitos[2..];
        return digitos.Length is 10 or 11 ? digitos : null;
    }

    private static JsonElement? Primeiro(JsonElement raiz) =>
        raiz.TryGetProperty("data", out var dados) && dados.ValueKind == JsonValueKind.Array && dados.GetArrayLength() > 0 ? dados[0] : null;

    private static string? Texto(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static string SemConsulta(string caminho) => caminho.Split('?')[0];

    private static (string? Codigo, string Mensagem) Erro(string corpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(corpo);
            if (documento.RootElement.TryGetProperty("errors", out var erros) && erros.ValueKind == JsonValueKind.Array && erros.GetArrayLength() > 0)
                return (Texto(erros[0], "code"), Texto(erros[0], "description") ?? "erro sem descrição");
        }
        catch (JsonException)
        {
        }
        return (null, "erro sem descrição");
    }
}
