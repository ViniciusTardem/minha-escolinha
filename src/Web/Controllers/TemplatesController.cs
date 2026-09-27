using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O catálogo de templates de WhatsApp (BL-M09-03), da equipe LDNA: texto, categoria e aprovação na Meta de cada gatilho.
/// Os templates são da conta WhatsApp Business da LDNA, não de uma escolinha.
/// </summary>
[Plataforma]
public class TemplatesController(ServicoTemplates servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "templates";
        return View(await servico.ListarAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "templates";
        var template = await servico.ParaEdicaoAsync(id, ct);
        if (template is null) return NotFound();
        await InformarAsync(id, ct);
        return View(template);
    }

    [HttpPost]
    public async Task<IActionResult> Editar(TemplateRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.SalvarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "templates";
            ModelState.AdicionarErro(resultado);
            await InformarAsync(requisicao.Id, ct);
            return View(requisicao);
        }
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>O gatilho, o tom e as variáveis do template (não mudam na edição).</summary>
    private async Task InformarAsync(Guid id, CancellationToken ct) =>
        ViewData["Info"] = (await servico.ListarAsync(ct)).FirstOrDefault(t => t.Id == id);
}
