using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Pacotes de créditos que as escolinhas compram pelo Stripe da LDNA, e a situação da integração (equipe LDNA).</summary>
[Plataforma]
public class PacotesController(ServicoPacotesCreditos servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "pacotes";
        return View(new PacotesViewModel(await servico.ListarAsync(ct), await servico.IntegracaoAsync(ct)));
    }

    [HttpGet]
    public IActionResult Novo()
    {
        ViewData["ModuloAtivo"] = "pacotes";
        return View("Formulario", new PacoteRequisicao());
    }

    [HttpGet]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "pacotes";
        var pacote = await servico.ObterParaEdicaoAsync(id, ct);
        return pacote is null ? NotFound() : View("Formulario", pacote);
    }

    [HttpPost]
    public async Task<IActionResult> Salvar(PacoteRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.SalvarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "pacotes";
            ModelState.AdicionarErro(resultado);
            return View("Formulario", requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }
}
