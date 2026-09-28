using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O log de entrega do motor único (BL-M09-08), para o gestor: cada mensagem com a situação, o motivo de retenção ou
/// supressão, a linha do tempo, as respostas e os créditos consumidos.
/// </summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class MensagensController(ServicoMensagens servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(FiltroMensagens filtro, int pagina, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "mensagens";
        var resumo = await servico.ResumoAsync(30, ct);
        if (resumo is null) return Forbid();
        return View(new MensagensViewModel(filtro, await servico.ListarAsync(filtro, pagina, ct), resumo));
    }

    /// <summary>As respostas das famílias que nenhum módulo tratou (P35).</summary>
    [HttpGet]
    public async Task<IActionResult> Respostas(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "mensagens";
        return View(await servico.RespostasEmAbertoAsync(ct));
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> MarcarVista(Guid id, CancellationToken ct)
    {
        var resultado = await servico.MarcarRespostaVistaAsync(id, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return RedirectToAction(nameof(Respostas));
    }

    [HttpGet]
    public async Task<IActionResult> Detalhe(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "mensagens";
        var mensagem = await servico.DetalheAsync(id, ct);
        return mensagem is null ? NotFound() : View(mensagem);
    }
}
