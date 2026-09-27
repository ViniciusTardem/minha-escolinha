using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Web.Filters;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Páginas de erro. Chamadas por re-execução (<c>UseExceptionHandler</c> / <c>UseStatusCodePagesWithReExecute</c>),
/// que mantêm o método original — por isso aceitam qualquer verbo e não validam antiforgery.
/// </summary>
[AllowAnonymous]
[PermitirSemTrocarSenha]
[IgnoreAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ErroController : Controller
{
    [Route("Erro")]
    public IActionResult Index() => ErroView(StatusCodes.Status500InternalServerError);

    [Route("Erro/{codigo:int}")]
    public IActionResult Codigo(int codigo) => ErroView(codigo);

    private ViewResult ErroView(int codigo)
    {
        ViewData["Codigo"] = codigo;
        ViewData["Rastreio"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Response.StatusCode = codigo;
        return View("Index");
    }
}
