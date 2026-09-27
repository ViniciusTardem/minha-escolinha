using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Agenda (BL-M04-11): as sessões de treino por turma, professor ou quadra, com período, modalidade e situação — e a
/// ocupação de agenda com os alertas de conflito do professor e do local (BL-M04-08, BL-M04-09 e P31). O professor vê as dele.
/// </summary>
[Permissao(Recurso.RegistroPresenca)]
public class AgendaController(ServicoAgenda agenda) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(FiltroAgenda filtro, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "agenda";
        return View(await agenda.ConsultarAsync(filtro, ct));
    }
}
