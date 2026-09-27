using System.Text;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Infrastructure.Integracao.Stripe;
using MinhaEscolinha.Web.Controllers.Api;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O Stripe de mentira (/dev/stripe): só em Development e no modo Simulado. A página de pagamento paga com cartão,
/// emite boleto ou Pix, ou volta sem pagar; a lista confirma, recusa e expira. Cada ação manda o webhook pelo endpoint
/// de verdade, assinado: o caminho do pagamento roda inteiro, sem conta no Stripe. Padrão do /dev do DP.
/// </summary>
[SomenteAutenticado]
[Route("dev/stripe")]
public class DevStripeController(
    ArmazemSimuladorStripe armazem,
    OpcoesStripe opcoes,
    IHttpClientFactory fabrica,
    IWebHostEnvironment ambiente,
    TimeProvider tempo,
    ILogger<DevStripeController> logger) : Controller
{
    private bool Disponivel => ambiente.IsDevelopment() && opcoes is { Habilitado: true, Simulado: true };

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!Disponivel) return NotFound();
        ViewData["ModuloAtivo"] = "pacotes";
        return View(await armazem.ListarAsync());
    }

    [HttpGet("checkout/{id}")]
    public async Task<IActionResult> Checkout(string id)
    {
        if (!Disponivel || await armazem.ObterAsync(id) is not { } sessao) return NotFound();
        return View(sessao);
    }

    /// <summary>cartão: pago na hora · boleto ou pix: emitido, o pagamento vem depois (confirmar na lista).</summary>
    [HttpPost("checkout/{id}/pagar")]
    public async Task<IActionResult> Pagar(string id, string meio, CancellationToken ct)
    {
        if (!Disponivel) return NotFound();
        var instantaneo = meio == "card";
        var sessao = await armazem.AlterarAsync(id, s =>
        {
            if (s.Status != "open") return;
            s.Status = "complete";
            s.MeioPagamento = meio is "card" or "boleto" or "pix" ? meio : "card";
            s.PagamentoId ??= $"pi_sim_{Guid.NewGuid():N}";
            s.SituacaoPagamento = instantaneo ? "paid" : "unpaid";
            s.SituacaoIntencao = instantaneo ? "succeeded" : "requires_action";
        });
        if (sessao is null) return NotFound();
        await EnviarWebhookAsync(EventosStripe.SessaoConcluida, sessao, ct);
        return Redirect(sessao.UrlSucesso);
    }

    [HttpPost("checkout/{id}/desistir")]
    public async Task<IActionResult> Desistir(string id)
    {
        if (!Disponivel || await armazem.ObterAsync(id) is not { } sessao) return NotFound();
        return Redirect(sessao.UrlCancelamento);
    }

    [HttpPost("{id}/confirmar")]
    public Task<IActionResult> Confirmar(string id, CancellationToken ct) =>
        MudarAsync(id, EventosStripe.PagamentoAssincronoConfirmado, s => s is { Status: "complete", SituacaoPagamento: "unpaid" }, s =>
        {
            s.SituacaoPagamento = "paid";
            s.SituacaoIntencao = "succeeded";
        }, ct);

    [HttpPost("{id}/recusar")]
    public Task<IActionResult> Recusar(string id, CancellationToken ct) =>
        MudarAsync(id, EventosStripe.PagamentoAssincronoRecusado, s => s is { Status: "complete", SituacaoPagamento: "unpaid" },
            s => s.SituacaoIntencao = "requires_payment_method", ct);

    [HttpPost("{id}/expirar")]
    public Task<IActionResult> Expirar(string id, CancellationToken ct) =>
        MudarAsync(id, EventosStripe.SessaoExpirada, s => s.Status == "open", s => s.Status = "expired", ct);

    private async Task<IActionResult> MudarAsync(string id, string evento, Func<ArmazemSimuladorStripe.SessaoSimulada, bool> pode,
        Action<ArmazemSimuladorStripe.SessaoSimulada> mudanca, CancellationToken ct)
    {
        if (!Disponivel) return NotFound();
        var mudou = false;
        var sessao = await armazem.AlterarAsync(id, s =>
        {
            if (!pode(s)) return;
            mudanca(s);
            mudou = true;
        });
        if (sessao is null) return NotFound();
        if (mudou) await EnviarWebhookAsync(evento, sessao, ct);
        else TempData[ChavesTempData.Erro] = "Essa ação não vale para a sessão no estado em que ela está.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Manda o evento para o endpoint de verdade, assinado com o segredo do simulador (ou o configurado).</summary>
    private async Task EnviarWebhookAsync(string tipo, ArmazemSimuladorStripe.SessaoSimulada sessao, CancellationToken ct)
    {
        var agora = tempo.GetUtcNow();
        var corpo = ArmazemSimuladorStripe.MontarEvento(tipo, sessao, agora);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, opcoes.UrlWebhook)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json")
        };
        requisicao.Headers.Add(WebhooksController.CabecalhoAssinaturaStripe,
            AssinaturaWebhookStripe.Cabecalho(corpo, agora.ToUnixTimeSeconds(), opcoes.SegredoWebhookEfetivo));
        try
        {
            using var resposta = await fabrica.CreateClient(SimuladorStripe.ClienteWebhook).SendAsync(requisicao, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData[ChavesTempData.Sucesso] = $"Simulador: {tipo} enviado ao sistema.";
                return;
            }
            logger.LogWarning("Simulador do Stripe: o webhook {Tipo} voltou {Codigo}.", tipo, (int)resposta.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Simulador do Stripe: o webhook {Tipo} não foi entregue.", tipo);
        }
        TempData[ChavesTempData.Erro] = "Simulador: o webhook não foi entregue. A volta do pagamento e a varredura (20 min) conferem a compra.";
    }
}
