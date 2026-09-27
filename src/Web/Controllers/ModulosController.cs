using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Página explicativa dos módulos das próximas fatias (/turmas, /chamada, ...): o que o módulo vai fazer e em que fatia chega.
/// A rota só aceita as chaves de <see cref="CatalogoModulos.ChavesPaginaExplicativa"/> (Program.cs).
/// </summary>
[SomenteAutenticado]
public class ModulosController : Controller
{
    [HttpGet]
    public IActionResult Index(string chave)
    {
        var modulo = CatalogoModulos.Obter(chave);
        if (modulo is null || modulo.Situacao != SituacaoModulo.Planejado) return NotFound();

        ViewData["ModuloAtivo"] = modulo.Chave;
        return View(modulo);
    }
}
