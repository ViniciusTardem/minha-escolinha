using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Infrastructure.Integracao.Asaas;

namespace MinhaEscolinha.Testes.Financeiro;

/// <summary>
/// Contrato HTTP com a API da Asaas (handler falso, nada sai da máquina): o endereço pelo ambiente da conta, a chave no
/// cabeçalho <c>access_token</c>, o corpo do cliente e da cobrança, e a classificação das falhas (regra de ouro 8).
/// </summary>
public class AsaasClienteHttpTestes
{
    private static readonly ContaAsaasAcesso Sandbox = new(Guid.NewGuid(), AmbienteAsaas.Sandbox, "$aact_hmlg_000chave", Guid.NewGuid());
    private static readonly ContaAsaasAcesso Producao = new(Guid.NewGuid(), AmbienteAsaas.Producao, "$aact_000chave", Guid.NewGuid());

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public sealed record Recebida(HttpMethod Metodo, string Url, string? Chave, string Corpo);

        public List<Recebida> Recebidas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var chave = request.Headers.TryGetValues("access_token", out var valores) ? string.Join(",", valores) : null;
            var corpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Recebidas.Add(new Recebida(request.Method, request.RequestUri!.ToString(), chave, corpo));
            return responder(request);
        }
    }

    private static (IGatewayAsaas Gateway, Handler Handler, ServiceProvider Provedor) Montar(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new Handler(responder);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Asaas:Modo"] = "Api",
            ["Asaas:UrlPublica"] = "https://localhost:7186"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAsaas(configuracao, "Development");
        services.AddHttpClient<IGatewayAsaas, AsaasClienteHttp>().ConfigurePrimaryHttpMessageHandler(() => handler);
        var provedor = services.BuildServiceProvider();
        return (provedor.GetRequiredService<IGatewayAsaas>(), handler, provedor);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode codigo, string json) =>
        new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task A_cobranca_vai_para_a_conta_da_escolinha_com_a_nossa_referencia_e_a_familia_escolhe_o_meio()
    {
        var cobranca = Guid.NewGuid();
        var (gateway, handler, provedor) = Montar(_ => Resposta(HttpStatusCode.OK, $$"""
            {"object":"payment","id":"pay_123","status":"PENDING","value":150.00,"dueDate":"2026-10-10",
             "externalReference":"{{cobranca}}","invoiceUrl":"https://sandbox.asaas.com/i/123"}
            """));
        await using var _ = provedor;

        var pagamento = await gateway.CriarCobrancaAsync(Sandbox,
            new CobrancaAsaasPedido(cobranca, "cus_1", 150m, new DateOnly(2026, 10, 10), "Mensalidade outubro/2026 · Pedro", 2m, 1m));

        var enviada = Assert.Single(handler.Recebidas);
        Assert.Equal(HttpMethod.Post, enviada.Metodo);
        Assert.Equal("https://api-sandbox.asaas.com/v3/payments", enviada.Url);
        Assert.Equal("$aact_hmlg_000chave", enviada.Chave);
        using var json = JsonDocument.Parse(enviada.Corpo);
        var raiz = json.RootElement;
        Assert.Equal("cus_1", raiz.GetProperty("customer").GetString());
        Assert.Equal("UNDEFINED", raiz.GetProperty("billingType").GetString());
        Assert.Equal(150m, raiz.GetProperty("value").GetDecimal());
        Assert.Equal("2026-10-10", raiz.GetProperty("dueDate").GetString());
        Assert.Equal(cobranca.ToString(), raiz.GetProperty("externalReference").GetString());
        Assert.Equal(2m, raiz.GetProperty("fine").GetProperty("value").GetDecimal());
        Assert.Equal("PERCENTAGE", raiz.GetProperty("fine").GetProperty("type").GetString());
        Assert.Equal(1m, raiz.GetProperty("interest").GetProperty("value").GetDecimal());

        Assert.Equal("pay_123", pagamento.Id);
        Assert.Equal(cobranca, pagamento.CobrancaId);
        Assert.Equal("https://sandbox.asaas.com/i/123", pagamento.UrlFatura);
    }

    [Fact]
    public async Task O_cliente_nasce_sem_as_notificacoes_da_asaas_e_a_conta_de_producao_usa_o_endereco_de_producao()
    {
        var (gateway, handler, provedor) = Montar(_ => Resposta(HttpStatusCode.OK, """{"object":"customer","id":"cus_9"}"""));
        await using var _ = provedor;

        var id = await gateway.CriarClienteAsync(Producao, new ClienteAsaasPedido(Guid.NewGuid(), "Maria Lima", "52998224725", null, "+5543988887777"));

        Assert.Equal("cus_9", id);
        var enviada = Assert.Single(handler.Recebidas);
        Assert.Equal("https://api.asaas.com/v3/customers", enviada.Url);
        using var json = JsonDocument.Parse(enviada.Corpo);
        Assert.True(json.RootElement.GetProperty("notificationDisabled").GetBoolean());
        Assert.Equal("43988887777", json.RootElement.GetProperty("mobilePhone").GetString());
        Assert.False(json.RootElement.TryGetProperty("email", out var email), email.ToString()); // campo vazio não vai
    }

    [Fact]
    public async Task A_cobranca_que_nao_existe_na_conta_volta_nula_e_a_busca_usa_a_referencia_externa()
    {
        var cobranca = Guid.NewGuid();
        var (gateway, handler, provedor) = Montar(r => r.RequestUri!.AbsolutePath.EndsWith("/payments")
            ? Resposta(HttpStatusCode.OK, """{"object":"list","hasMore":false,"totalCount":0,"data":[]}""")
            : Resposta(HttpStatusCode.NotFound, """{"errors":[{"code":"not_found","description":"Não encontrado."}]}"""));
        await using var _ = provedor;

        Assert.Null(await gateway.BuscarCobrancaAsync(Sandbox, cobranca));
        Assert.Contains($"externalReference={cobranca}", handler.Recebidas[0].Url);
        Assert.Null(await gateway.ObterCobrancaAsync(Sandbox, "pay_inexistente"));
    }

    /// <summary>P42: a atualização do cliente é um POST no próprio cliente, sem os campos vazios e com as notificações desligadas.</summary>
    [Fact]
    public async Task O_cliente_e_atualizado_com_post_sem_apagar_o_que_nao_veio()
    {
        var (gateway, handler, provedor) = Montar(_ => Resposta(HttpStatusCode.OK, """{"object":"customer","id":"cus_1"}"""));
        await using var _ = provedor;

        await gateway.AtualizarClienteAsync(Sandbox, "cus_1", new ClienteAsaasPedido(Guid.NewGuid(), "Maria Lima Souza", "52998224725", null, "+5543988887777"));

        var enviada = Assert.Single(handler.Recebidas);
        Assert.Equal((HttpMethod.Post, "https://api-sandbox.asaas.com/v3/customers/cus_1"), (enviada.Metodo, enviada.Url));
        using var json = JsonDocument.Parse(enviada.Corpo);
        var raiz = json.RootElement;
        Assert.Equal(("Maria Lima Souza", "52998224725", "43988887777", true),
            (raiz.GetProperty("name").GetString(), raiz.GetProperty("cpfCnpj").GetString(), raiz.GetProperty("mobilePhone").GetString(),
                raiz.GetProperty("notificationDisabled").GetBoolean()));
        Assert.False(raiz.TryGetProperty("email", out var email), email.ToString()); // sem e-mail no cadastro: o da Asaas fica como está
    }

    /// <summary>BL-M10-06: o Pix copia e cola vem do QR code da cobrança, com a validade em Brasília; cobrança sumida volta nula.</summary>
    [Fact]
    public async Task O_pix_copia_e_cola_vem_do_qr_code_da_cobranca_com_a_validade()
    {
        var (gateway, handler, provedor) = Montar(r => r.RequestUri!.AbsolutePath.Contains("/pay_123/")
            ? Resposta(HttpStatusCode.OK, """{"encodedImage":"iVBORw0KGgo=","payload":"00020126580014br.gov.bcb.pix0136abc","expirationDate":"2026-10-10 23:59:59"}""")
            : Resposta(HttpStatusCode.NotFound, """{"errors":[{"code":"not_found","description":"Não encontrado."}]}"""));
        await using var _ = provedor;

        var pix = await gateway.ObterPixAsync(Sandbox, "pay_123");
        // P49: a imagem do QR code vem junto, para o portal mostrar no computador.
        Assert.Equal(new PixAsaas("00020126580014br.gov.bcb.pix0136abc", new DateTime(2026, 10, 10, 23, 59, 59), "iVBORw0KGgo="), pix);
        Assert.Equal((HttpMethod.Get, "https://api-sandbox.asaas.com/v3/payments/pay_123/pixQrCode"), (handler.Recebidas[0].Metodo, handler.Recebidas[0].Url));
        Assert.Null(await gateway.ObterPixAsync(Sandbox, "pay_sumida"));
    }

    [Fact]
    public async Task Dado_recusado_e_definitivo_e_asaas_fora_do_ar_e_retentavel()
    {
        var (gateway, _, provedor) = Montar(_ => Resposta(HttpStatusCode.BadRequest,
            """{"errors":[{"code":"invalid_cpfCnpj","description":"O CPF/CNPJ informado é inválido."}]}"""));
        await using (provedor)
        {
            var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() =>
                gateway.CriarClienteAsync(Sandbox, new ClienteAsaasPedido(Guid.NewGuid(), "Maria", "000", null, null)));
            Assert.False(falha.Retentavel);
            Assert.Contains("invalid_cpfCnpj", falha.Message);
        }

        var (fora, _, outro) = Montar(_ => Resposta(HttpStatusCode.ServiceUnavailable, "{}"));
        await using (outro)
        {
            var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() =>
                fora.CriarCobrancaAsync(Sandbox, new CobrancaAsaasPedido(Guid.NewGuid(), "cus_1", 150m, new DateOnly(2026, 10, 10), "x", 0, 0)));
            Assert.True(falha.Retentavel);
        }
    }
}
