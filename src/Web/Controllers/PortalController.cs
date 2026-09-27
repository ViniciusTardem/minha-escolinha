using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O começo do portal do responsável (S2), no celular e sem instalar nada: os filhos com vínculo confirmado e os avisos
/// (o segundo canal do motor, BL-M09-02). Jogos e pagamentos chegam no S5 e no S6. Só para quem tem o papel Responsável.
/// </summary>
[SomenteAutenticado]
public class PortalController(ServicoPortal portal, ServicoMensagens mensagens) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (await portal.InicioAsync(ct) is not { } inicio) return RedirectToAction("Index", "Painel");
        Preparar("inicio", inicio.Escolinha, inicio.Nome);
        return View(inicio);
    }

    [HttpGet]
    public async Task<IActionResult> Avisos(CancellationToken ct)
    {
        if (await portal.InicioAsync(ct) is not { } inicio) return RedirectToAction("Index", "Painel");
        Preparar("avisos", inicio.Escolinha, inicio.Nome);
        return View(await mensagens.AvisosDoPortalAsync(ct));
    }

    /// <summary>Jogos e pagamentos: o que chega nas próximas fatias.</summary>
    [HttpGet]
    public async Task<IActionResult> EmBreve(string aba, CancellationToken ct)
    {
        if (await portal.InicioAsync(ct) is not { } inicio) return RedirectToAction("Index", "Painel");
        var chave = aba == "pagamentos" ? "pagamentos" : "jogos";
        Preparar(chave, inicio.Escolinha, inicio.Nome);
        return View((object)chave);
    }

    private void Preparar(string aba, string escolinha, string nome)
    {
        ViewData["AbaPortal"] = aba;
        ViewData["Escolinha"] = escolinha;
        ViewData["Inicial"] = string.IsNullOrWhiteSpace(nome) ? "R" : nome.Trim()[..1].ToUpperInvariant();
    }
}
