using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Web.Filters;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Vitrine do design system (/dev/design) — só em Development. Tokens, selos, componentes e as quatro superfícies
/// (painel, quadra, portal, autenticação): é onde o visual é conferido antes de virar tela de verdade.
/// </summary>
[AllowAnonymous]
[PermitirSemTrocarSenha]
[Route("dev/design")]
public class DesignController(IWebHostEnvironment ambiente) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => ambiente.IsDevelopment() ? View() : NotFound();

    [HttpGet("quadra")]
    public IActionResult Quadra() => ambiente.IsDevelopment() ? View() : NotFound();

    [HttpGet("portal")]
    public IActionResult Portal() => ambiente.IsDevelopment() ? View() : NotFound();

    [HttpGet("autenticacao")]
    public IActionResult Autenticacao() => ambiente.IsDevelopment() ? View() : NotFound();
}
