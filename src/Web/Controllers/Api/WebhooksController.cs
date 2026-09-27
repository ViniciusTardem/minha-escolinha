using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;

namespace MinhaEscolinha.Web.Controllers.Api;

/// <summary>
/// Receptores de webhook: anônimos, protegidos pela assinatura do parceiro. Gravam o corpo bruto e respondem 200 antes de
/// processar (regra de ouro 6); o processamento é job. Padrão do AlternativaDepartamentoPessoal (api/v1/webhooks).
/// </summary>
[ApiController]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("api/v1/webhooks")]
public class WebhooksController(ProcessadorPagamentosStripe processador, ProcessadorWebhooksWhatsApp whatsApp) : ControllerBase
{
    public const string CabecalhoAssinaturaStripe = "Stripe-Signature";

    /// <summary>Eventos das sessões de Checkout dos pacotes de créditos (Stripe da LDNA).</summary>
    [HttpPost("stripe")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Stripe(CancellationToken ct)
    {
        using var leitor = new StreamReader(Request.Body, Encoding.UTF8);
        var corpo = await leitor.ReadToEndAsync(ct);
        var resultado = await processador.ReceberWebhookAsync(corpo, Request.Headers[CabecalhoAssinaturaStripe].ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        return Responder(resultado);
    }

    /// <summary>Handshake da Meta ao cadastrar o webhook: devolve o desafio se o token de verificação confere.</summary>
    [HttpGet("whatsapp")]
    public IActionResult WhatsAppVerificacao([FromQuery(Name = "hub.mode")] string? modo, [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? desafio) =>
        whatsApp.Verificar(modo, token, desafio) is { } resposta ? Content(resposta, "text/plain") : StatusCode(StatusCodes.Status403Forbidden);

    /// <summary>Retornos de entrega e respostas das famílias (WhatsApp Cloud API), assinados com X-Hub-Signature-256.</summary>
    [HttpPost("whatsapp")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> WhatsApp(CancellationToken ct)
    {
        using var leitor = new StreamReader(Request.Body, Encoding.UTF8);
        var corpo = await leitor.ReadToEndAsync(ct);
        return Responder(await whatsApp.ReceberAsync(corpo, Request.Headers[AssinaturaWebhookWhatsApp.Cabecalho].ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct));
    }

    private IActionResult Responder(ResultadoRecebimentoWebhook resultado) => resultado switch
    {
        ResultadoRecebimentoWebhook.Aceito or ResultadoRecebimentoWebhook.Duplicado => Ok(),
        ResultadoRecebimentoWebhook.Desligado => StatusCode(StatusCodes.Status503ServiceUnavailable),
        _ => BadRequest()
    };
}
