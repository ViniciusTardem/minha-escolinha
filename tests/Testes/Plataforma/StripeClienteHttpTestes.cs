using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Infrastructure.Integracao.Stripe;

namespace MinhaEscolinha.Testes.Plataforma;

/// <summary>
/// Contrato HTTP com o Stripe (handler falso, nada sai da máquina): o que o cliente manda, o que ele lê e como classifica
/// as falhas. Padrão dos testes de contrato do AlternativaDepartamentoPessoal.
/// </summary>
public class StripeClienteHttpTestes
{
    private static readonly PedidoCheckout Pedido = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "Básico — 500 créditos de mensagem", 5000, "https://localhost:7186/Creditos/Compra/1",
        "https://localhost:7186/Creditos/Compra/1?cancelada=true", "gestor@escolinha.com");

    private const string SessaoCriada = """{"id":"cs_test_abc","object":"checkout.session","url":"https://checkout.stripe.com/c/pay/cs_test_abc","expires_at":1790000000}""";

    [Fact]
    public async Task Cria_a_sessao_com_valor_em_centavos_metadados_idempotencia_e_a_chave_de_teste()
    {
        var handler = new HandlerFalso(_ => Resposta(HttpStatusCode.OK, SessaoCriada));
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Stripe:Habilitado"] = "true",
            ["Stripe:Modo"] = "Teste",
            ["Stripe:ChaveSecreta"] = "sk_test_abc",
            ["Stripe:UrlPublica"] = "https://localhost:7186",
            ["Stripe:VersaoApi"] = "2025-06-30.basil"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStripe(configuracao, "Development");
        services.AddHttpClient<IGatewayStripe, StripeClienteHttp>().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provedor = services.BuildServiceProvider();

        var sessao = await provedor.GetRequiredService<IGatewayStripe>().CriarSessaoCheckoutAsync(Pedido);

        Assert.Equal("cs_test_abc", sessao.SessaoId);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_abc", sessao.Url);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1790000000).UtcDateTime, sessao.ExpiraEmUtc);

        var enviada = Assert.Single(handler.Recebidas);
        Assert.Equal(HttpMethod.Post, enviada.Metodo);
        Assert.Equal("https://api.stripe.com/v1/checkout/sessions", enviada.Url);
        Assert.Equal("Bearer sk_test_abc", enviada.Autorizacao);
        Assert.Equal("2025-06-30.basil", enviada.VersaoApi);
        Assert.Equal("compra-11111111111111111111111111111111", enviada.Idempotencia);

        var campos = QueryHelpers.ParseQuery(enviada.Corpo);
        Assert.Equal("payment", campos["mode"]);
        Assert.Equal("brl", campos["line_items[0][price_data][currency]"]);
        Assert.Equal("5000", campos["line_items[0][price_data][unit_amount]"]);
        Assert.Equal("Básico — 500 créditos de mensagem", campos["line_items[0][price_data][product_data][name]"]);
        Assert.Equal(Pedido.CompraId.ToString(), campos["client_reference_id"]);
        Assert.Equal(MetadadosStripe.ValorOrigem, campos[$"metadata[{MetadadosStripe.Origem}]"]);
        Assert.Equal(Pedido.EscolinhaId.ToString(), campos[$"metadata[{MetadadosStripe.Escolinha}]"]);
        Assert.Equal(Pedido.CompraId.ToString(), campos[$"payment_intent_data[metadata][{MetadadosStripe.Compra}]"]);
        Assert.Equal(Pedido.UrlSucesso, campos["success_url"]);
        Assert.Equal("gestor@escolinha.com", campos["customer_email"]);
        // Sem meios configurados, valem os ligados no painel do Stripe.
        Assert.DoesNotContain(campos.Keys, k => k.StartsWith("payment_method_types", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Meios_e_vencimento_do_boleto_configurados_vao_na_sessao()
    {
        var handler = new HandlerFalso(_ => Resposta(HttpStatusCode.OK, SessaoCriada));
        var cliente = Cliente(handler, new OpcoesStripe { MeiosPagamento = ["card", "boleto", "pix"], DiasVencimentoBoleto = 5 });

        await cliente.CriarSessaoCheckoutAsync(Pedido);

        var campos = QueryHelpers.ParseQuery(Assert.Single(handler.Recebidas).Corpo);
        Assert.Equal(new[] { "card", "boleto", "pix" },
            new[] { campos["payment_method_types[0]"].ToString(), campos["payment_method_types[1]"].ToString(), campos["payment_method_types[2]"].ToString() });
        Assert.Equal("5", campos["payment_method_options[boleto][expires_after_days]"]);
    }

    [Fact]
    public async Task Le_a_sessao_com_o_pagamento_expandido()
    {
        var handler = new HandlerFalso(_ => Resposta(HttpStatusCode.OK, """
            {"id":"cs_test_abc","status":"complete","payment_status":"paid",
             "payment_intent":{"id":"pi_1","status":"succeeded","payment_method":{"id":"pm_1","type":"pix"}},
             "metadata":{"origem":"minha-escolinha","compra_id":"11111111-1111-1111-1111-111111111111"}}
            """));

        var sessao = await Cliente(handler).ConsultarSessaoAsync("cs_test_abc");

        Assert.Equal("https://api.stripe.com/v1/checkout/sessions/cs_test_abc?expand[]=payment_intent.payment_method",
            Assert.Single(handler.Recebidas).Url);
        Assert.True(sessao.Paga);
        Assert.Equal(("pi_1", "succeeded", "pix"), (sessao.PagamentoId, sessao.SituacaoIntencao, sessao.MeioPagamento));
        Assert.Equal("11111111-1111-1111-1111-111111111111", sessao.Metadados[MetadadosStripe.Compra]);
    }

    [Fact]
    public async Task Pagamento_ainda_nao_criado_vem_como_texto_ou_nulo()
    {
        var handler = new HandlerFalso(_ => Resposta(HttpStatusCode.OK, """{"id":"cs_1","status":"open","payment_status":"unpaid","payment_intent":null}"""));

        var sessao = await Cliente(handler).ConsultarSessaoAsync("cs_1");

        Assert.False(sessao.Paga);
        Assert.Null(sessao.PagamentoId);
    }

    [Fact]
    public async Task Erro_de_validacao_e_definitivo_com_a_mensagem_do_Stripe()
    {
        var handler = new HandlerFalso(_ => Resposta(HttpStatusCode.BadRequest,
            """{"error":{"type":"invalid_request_error","message":"The payment method type \"pix\" is invalid."}}"""));

        var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() => Cliente(handler).CriarSessaoCheckoutAsync(Pedido));

        Assert.False(falha.Retentavel);
        Assert.Equal(400, falha.CodigoHttp);
        Assert.Contains("pix", falha.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Fora_do_ar_e_limite_de_uso_sao_retentaveis(HttpStatusCode codigo)
    {
        var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() =>
            Cliente(new HandlerFalso(_ => Resposta(codigo, "{}"))).ConsultarSessaoAsync("cs_1"));
        Assert.True(falha.Retentavel);
    }

    [Fact]
    public async Task Rede_fora_do_ar_e_retentavel()
    {
        var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() =>
            Cliente(new HandlerFalso(_ => throw new HttpRequestException("sem rede"))).ConsultarSessaoAsync("cs_1"));
        Assert.True(falha.Retentavel);
    }

    private static StripeClienteHttp Cliente(HandlerFalso handler, OpcoesStripe? opcoes = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(StripeClienteHttp.UrlApi) }, opcoes ?? new OpcoesStripe(),
            NullLogger<StripeClienteHttp>.Instance);

    private static HttpResponseMessage Resposta(HttpStatusCode codigo, string json) =>
        new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}

/// <summary>Responde o que o teste mandar e guarda o que recebeu (antes de a requisição ser descartada).</summary>
public sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public sealed record Recebida(HttpMethod Metodo, string Url, string? Autorizacao, string? VersaoApi, string? Idempotencia, string Corpo);

    public List<Recebida> Recebidas { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? Cabecalho(string nome) => request.Headers.TryGetValues(nome, out var valores) ? string.Join(",", valores) : null;
        var corpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Recebidas.Add(new Recebida(request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
            Cabecalho("Stripe-Version"), Cabecalho("Idempotency-Key"), corpo));
        return responder(request);
    }
}
