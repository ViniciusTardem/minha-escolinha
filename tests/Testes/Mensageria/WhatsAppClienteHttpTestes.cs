using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Infrastructure.Integracao.WhatsApp;
using MinhaEscolinha.Testes.Plataforma;

namespace MinhaEscolinha.Testes.Mensageria;

/// <summary>
/// Contrato HTTP com a Cloud API do WhatsApp (handler falso, nada sai da máquina): o que o cliente manda, o que lê e
/// como classifica as falhas (regra de ouro 8).
/// </summary>
public class WhatsAppClienteHttpTestes
{
    private static readonly PedidoEnvioWhatsApp Pedido = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "5543988887777", "convocacao_v1", "pt_BR", ["Maria", "Pedro", "Jogo contra Leões", "sábado, 03/10 às 09:30", "Ginásio", "https://maps"],
        ["Vai", "Não vai"], "texto");

    private const string Aceita = """
        {"messaging_product":"whatsapp","contacts":[{"input":"5543988887777","wa_id":"554388887777"}],"messages":[{"id":"wamid.HBgM123"}]}
        """;

    private static async Task<(IGatewayWhatsApp Gateway, HandlerFalso Handler, ServiceProvider Provedor)> MontarAsync(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new HandlerFalso(responder);
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsApp:Habilitado"] = "true",
            ["WhatsApp:Modo"] = "Api",
            ["WhatsApp:IdNumero"] = "1234567890",
            ["WhatsApp:TokenAcesso"] = "EAAtoken",
            ["WhatsApp:SegredoApp"] = "segredo",
            ["WhatsApp:TokenVerificacao"] = "verifica",
            ["WhatsApp:NumerosTeste:0"] = "+5543988887777",
            ["WhatsApp:UrlPublica"] = "https://localhost:7186",
            ["WhatsApp:VersaoApi"] = "v21.0"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWhatsApp(configuracao, "Development");
        services.AddHttpClient<IGatewayWhatsApp, WhatsAppClienteHttp>().ConfigurePrimaryHttpMessageHandler(() => handler);
        var provedor = services.BuildServiceProvider();
        await Task.CompletedTask;
        return (provedor.GetRequiredService<IGatewayWhatsApp>(), handler, provedor);
    }

    private static HttpResponseMessage Resposta(HttpStatusCode codigo, string json) =>
        new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Manda_o_template_com_os_parametros_em_ordem_e_o_payload_de_cada_botao()
    {
        var (gateway, handler, provedor) = await MontarAsync(_ => Resposta(HttpStatusCode.OK, Aceita));
        await using var _ = provedor;

        var envio = await gateway.EnviarTemplateAsync(Pedido);

        Assert.Equal("wamid.HBgM123", envio.IdExterno);
        Assert.Equal("554388887777", envio.IdContato);
        var enviada = Assert.Single(handler.Recebidas);
        Assert.Equal(HttpMethod.Post, enviada.Metodo);
        Assert.Equal("https://graph.facebook.com/v21.0/1234567890/messages", enviada.Url);
        Assert.Equal("Bearer EAAtoken", enviada.Autorizacao);

        using var json = JsonDocument.Parse(enviada.Corpo);
        var raiz = json.RootElement;
        Assert.Equal("whatsapp", raiz.GetProperty("messaging_product").GetString());
        Assert.Equal("5543988887777", raiz.GetProperty("to").GetString());
        var template = raiz.GetProperty("template");
        Assert.Equal("convocacao_v1", template.GetProperty("name").GetString());
        Assert.Equal("pt_BR", template.GetProperty("language").GetProperty("code").GetString());
        var componentes = template.GetProperty("components").EnumerateArray().ToList();
        Assert.Equal(Pedido.Parametros, componentes[0].GetProperty("parameters").EnumerateArray().Select(p => p.GetProperty("text").GetString()));
        Assert.Equal("quick_reply", componentes[1].GetProperty("sub_type").GetString());
        Assert.Equal(PayloadBotao.Montar(Pedido.MensagemId, 1),
            componentes[2].GetProperty("parameters")[0].GetProperty("payload").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 131026, NaturezaFalha.Definitiva)]
    [InlineData(HttpStatusCode.BadRequest, 132001, NaturezaFalha.Definitiva)]
    [InlineData(HttpStatusCode.BadRequest, 131056, NaturezaFalha.Retentavel)]
    [InlineData(HttpStatusCode.BadRequest, 130429, NaturezaFalha.Retentavel)]
    [InlineData(HttpStatusCode.Unauthorized, 190, NaturezaFalha.Definitiva)]
    [InlineData(HttpStatusCode.InternalServerError, 131000, NaturezaFalha.Retentavel)]
    public async Task Classifica_a_falha_pelo_codigo_da_meta(HttpStatusCode http, int codigo, NaturezaFalha esperada)
    {
        var (gateway, _, provedor) = await MontarAsync(_ => Resposta(http,
            "{\"error\":{\"message\":\"falhou\",\"type\":\"OAuthException\",\"code\":" + codigo + ",\"error_data\":{\"details\":\"detalhe\"}}}"));
        await using var __ = provedor;

        var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() => gateway.EnviarTemplateAsync(Pedido));
        Assert.Equal(esperada, falha.Natureza);
        Assert.Contains(codigo.ToString(), falha.Message);
        Assert.Contains("detalhe", falha.Message);
    }

    [Fact]
    public async Task Rede_fora_do_ar_e_retentavel()
    {
        var (gateway, _, provedor) = await MontarAsync(_ => throw new HttpRequestException("sem rede"));
        await using var __ = provedor;
        var falha = await Assert.ThrowsAsync<FalhaIntegracaoException>(() => gateway.EnviarTemplateAsync(Pedido));
        Assert.True(falha.Retentavel);
    }
}

/// <summary>A trava de ambiente do WhatsApp (spec §2.3): mensagem real só para número de teste fora de produção.</summary>
public class GuardaWhatsAppTestes
{
    private static OpcoesWhatsApp Api(params string[] numeros) => new()
    {
        Modo = OpcoesWhatsApp.ModoApi, IdNumero = "1", TokenAcesso = "t", SegredoApp = "s", TokenVerificacao = "v", NumerosTeste = numeros
    };

    [Fact]
    public void Simulado_em_producao_desliga_o_canal_sem_derrubar_o_site()
    {
        var opcoes = new OpcoesWhatsApp();
        string? aviso = null;
        GuardaWhatsApp.Validar("Production", opcoes, a => aviso = a);
        Assert.False(opcoes.Habilitado);
        Assert.NotNull(aviso);
    }

    [Fact]
    public void Api_fora_de_producao_exige_numeros_de_teste()
    {
        Assert.Throws<InvalidOperationException>(() => GuardaWhatsApp.Validar("Development", Api()));
        GuardaWhatsApp.Validar("Development", Api("+5543988887777"));
    }

    [Fact]
    public void Api_em_producao_exige_a_confirmacao_e_as_chaves()
    {
        Assert.Throws<InvalidOperationException>(() => GuardaWhatsApp.Validar("Production", Api()));
        var semToken = Api();
        semToken.TokenAcesso = "";
        semToken.ConfirmarProducao = true;
        Assert.Throws<InvalidOperationException>(() => GuardaWhatsApp.Validar("Production", semToken));
        var pronta = Api();
        pronta.ConfirmarProducao = true;
        GuardaWhatsApp.Validar("Production", pronta);
    }

    [Fact]
    public void Url_publica_precisa_ser_https()
    {
        var opcoes = new OpcoesWhatsApp { UrlPublica = "http://escolinha.com.br" };
        Assert.Throws<InvalidOperationException>(() => GuardaWhatsApp.Validar("Development", opcoes));
    }
}
