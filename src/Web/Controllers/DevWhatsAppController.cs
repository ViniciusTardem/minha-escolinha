using System.Text;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Infrastructure.Integracao.WhatsApp;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O WhatsApp de mentira (/dev/whatsapp): só em Development e no modo Simulado. Faz o papel do celular das famílias:
/// cada mensagem enviada aparece aqui, e dá para entregar, ler, falhar, tocar num botão ou responder com texto. Cada ação
/// manda o webhook pelo endpoint de verdade, assinado: o caminho inteiro roda sem conta na Meta. Padrão do /dev do DP.
/// </summary>
[SomenteAutenticado]
[Route("dev/whatsapp")]
public class DevWhatsAppController(
    ArmazemSimuladorWhatsApp armazem,
    OpcoesWhatsApp opcoes,
    IHttpClientFactory fabrica,
    IWebHostEnvironment ambiente,
    TimeProvider tempo,
    ILogger<DevWhatsAppController> logger) : Controller
{
    private bool Disponivel => ambiente.IsDevelopment() && opcoes is { Habilitado: true, Simulado: true };

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!Disponivel) return NotFound();
        ViewData["ModuloAtivo"] = "mensagens";
        return View(await armazem.ListarAsync());
    }

    [HttpPost("{id}/entregar")]
    public Task<IActionResult> Entregar(string id, CancellationToken ct) => StatusAsync(id, "delivered", null, ct);

    [HttpPost("{id}/ler")]
    public Task<IActionResult> Ler(string id, CancellationToken ct) => StatusAsync(id, "read", null, ct);

    [HttpPost("{id}/falhar")]
    public Task<IActionResult> Falhar(string id, CancellationToken ct) =>
        StatusAsync(id, "failed", "Mensagem não entregue (número sem WhatsApp, simulado)", ct);

    /// <summary>A família toca num botão de resposta rápida: o payload leva a mensagem e o índice do botão.</summary>
    [HttpPost("{id}/botao/{indice:int}")]
    public async Task<IActionResult> Botao(string id, int indice, CancellationToken ct)
    {
        if (!Disponivel || await armazem.ObterAsync(id) is not { } mensagem || indice < 0 || indice >= mensagem.Botoes.Count) return NotFound();
        await ResponderAsync(mensagem, mensagem.Botoes[indice], PayloadBotao.Montar(mensagem.MensagemId, indice), mensagem.Id, ct);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>A família responde com texto: com a mensagem citada (resposta a ela) ou solto (o sistema acha pelo telefone).</summary>
    [HttpPost("{id}/responder")]
    public async Task<IActionResult> Responder(string id, string? texto, bool citar, CancellationToken ct)
    {
        if (!Disponivel || await armazem.ObterAsync(id) is not { } mensagem) return NotFound();
        if (string.IsNullOrWhiteSpace(texto))
        {
            TempData[ChavesTempData.Erro] = "Escreva a resposta.";
            return RedirectToAction(nameof(Index));
        }
        await ResponderAsync(mensagem, texto.Trim(), null, citar ? mensagem.Id : null, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("limpar")]
    public async Task<IActionResult> Limpar()
    {
        if (!Disponivel) return NotFound();
        await armazem.LimparAsync();
        TempData[ChavesTempData.Sucesso] = "Simulador: conversas apagadas (o log de mensagens do sistema continua).";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> StatusAsync(string id, string status, string? erro, CancellationToken ct)
    {
        if (!Disponivel) return NotFound();
        var mensagem = await armazem.AlterarAsync(id, m => m.Status = status);
        if (mensagem is null) return NotFound();
        await EnviarWebhookAsync(ArmazemSimuladorWhatsApp.MontarStatus(mensagem, status, tempo.GetUtcNow(), erro), $"retorno {status}", ct);
        return RedirectToAction(nameof(Index));
    }

    private async Task ResponderAsync(ArmazemSimuladorWhatsApp.MensagemSimulada mensagem, string texto, string? payload, string? contexto,
        CancellationToken ct)
    {
        var agora = tempo.GetUtcNow();
        var resposta = new ArmazemSimuladorWhatsApp.RespostaSimulada
        {
            Id = $"wamid.SIMIN{Guid.NewGuid():N}", Texto = texto, Payload = payload, EmUtc = agora.UtcDateTime
        };
        await armazem.AlterarAsync(mensagem.Id, m =>
        {
            m.Respostas.Add(resposta);
            if (m.Status is "sent" or "delivered") m.Status = "read";
        });
        await EnviarWebhookAsync(ArmazemSimuladorWhatsApp.MontarResposta(mensagem.Telefone, resposta.Id, texto, payload, contexto, agora),
            payload is null ? "resposta de texto" : $"botão {texto}", ct);
    }

    /// <summary>Manda o webhook para o endpoint de verdade, assinado com o segredo do simulador (ou o configurado).</summary>
    private async Task EnviarWebhookAsync(string corpo, string descricao, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, opcoes.UrlWebhook)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json")
        };
        requisicao.Headers.Add(AssinaturaWebhookWhatsApp.Cabecalho, AssinaturaWebhookWhatsApp.Montar(corpo, opcoes.SegredoAppEfetivo));
        try
        {
            using var resposta = await fabrica.CreateClient(SimuladorWhatsApp.ClienteWebhook).SendAsync(requisicao, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData[ChavesTempData.Sucesso] = $"Simulador: webhook ({descricao}) entregue ao sistema; o log atualiza em instantes.";
                return;
            }
            logger.LogWarning("Simulador do WhatsApp: o webhook ({Descricao}) voltou {Codigo}.", descricao, (int)resposta.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Simulador do WhatsApp: o webhook ({Descricao}) não foi entregue.", descricao);
        }
        TempData[ChavesTempData.Erro] = "Simulador: o webhook não foi entregue ao sistema (veja o log).";
    }
}
