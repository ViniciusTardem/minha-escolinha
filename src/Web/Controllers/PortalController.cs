using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Portal;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O portal do responsável (S2 e S6), no celular e sem instalar nada (R2): os filhos numa conta só, os jogos com a resposta,
/// as mensalidades com o pagamento pela Asaas, as presenças e os avisos. Só para quem tem o papel Responsável: os demais
/// voltam para o painel. Cada tela mostra só os filhos com vínculo confirmado (BL-M10-11).
/// </summary>
[SomenteAutenticado]
public class PortalController(ServicoPortal portal, ServicoMensagens mensagens) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await portal.InicioAsync(ct) is not { } inicio) return RedirectToAction("Index", "Painel");
        Preparar("inicio");
        return View(inicio);
    }

    [HttpGet]
    public async Task<IActionResult> Jogos(CancellationToken ct)
    {
        if (await portal.JogosAsync(ct) is not { } jogos) return RedirectToAction("Index", "Painel");
        Preparar("jogos");
        return View(jogos);
    }

    /// <summary>"Vai" ou "Não vai" pelo portal (BL-M10-07), com a mesma regra do WhatsApp.</summary>
    [HttpPost]
    [Permissao(Recurso.ResponderConvocacao, Operacao.Alterar)]
    public async Task<IActionResult> Responder(Guid id, bool vai, CancellationToken ct)
    {
        var resultado = await portal.ResponderAsync(id, vai, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return Redirect($"/Portal/Jogos#convocacao-{id:N}");
    }

    [HttpGet]
    public async Task<IActionResult> Pagamentos(CancellationToken ct)
    {
        if (await portal.PagamentosAsync(ct) is not { } pagamentos) return RedirectToAction("Index", "Painel");
        Preparar("pagamentos");
        return View(pagamentos);
    }

    [HttpGet]
    public async Task<IActionResult> Presencas(Guid id, CancellationToken ct)
    {
        if (!portal.EhResponsavel) return RedirectToAction("Index", "Painel");
        if (await portal.PresencasAsync(id, ct) is not { } presencas) return NotFound();
        Preparar("inicio");
        return View(presencas);
    }

    [HttpGet]
    public async Task<IActionResult> Avisos(CancellationToken ct)
    {
        if (!portal.EhResponsavel) return RedirectToAction("Index", "Painel");
        Preparar("avisos");
        return View(await mensagens.AvisosDoPortalAsync(ct));
    }

    private void Preparar(string aba)
    {
        ViewData["AbaPortal"] = aba;
        ViewData["Escolinha"] = User.FindFirst(ClaimsEscolinha.EscolinhaNome)?.Value ?? "Escolinha";
        var nome = User.Identity?.Name;
        ViewData["Inicial"] = string.IsNullOrWhiteSpace(nome) ? "R" : nome.Trim()[..1].ToUpperInvariant();
    }
}
