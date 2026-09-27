using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// A fila de vínculos a confirmar (BL-M01-06): quem ativou o acesso só vê o aluno depois que o gestor confirma que é mesmo
/// o responsável dele. Recusar exige motivo.
/// </summary>
[Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
public class VinculosController(ServicoVerificacaoVinculos servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!servico.PodeVerificar) return Forbid();
        ViewData["ModuloAtivo"] = "convites";
        return View(await servico.PendentesAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken ct)
    {
        var resultado = await servico.ConfirmarAsync(id, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Recusar(Guid id, string? motivo, CancellationToken ct)
    {
        var resultado = await servico.RecusarAsync(id, motivo, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }
}
