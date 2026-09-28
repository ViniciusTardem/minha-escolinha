using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// A chamada na quadra (S3, BL-M05-01): o professor no celular, em pé, com sinal ruim ou sem sinal (R10, CA-21).
/// As páginas ficam guardadas no aparelho pelo service worker (<c>/sw-chamada.js</c>, escopo <c>/Chamada</c>), e cada toque
/// vai para uma fila que sincroniza quando há sinal. A gestão usa a mesma tela para corrigir uma chamada.
/// </summary>
public class ChamadaController(ServicoChamada servico, IAntiforgery antiforgery, IUsuarioAtual usuarioAtual) : Controller
{
    public const string Modulo = "chamada";

    [HttpGet]
    [Permissao(Recurso.RegistroPresenca, Operacao.Alterar)]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        return View(await servico.HojeAsync(ct));
    }

    [HttpGet]
    [Permissao(Recurso.RegistroPresenca, Operacao.Alterar)]
    public async Task<IActionResult> Sessao(Guid id, CancellationToken ct)
    {
        var chamada = await servico.ChamadaAsync(id, ct);
        return chamada is null ? NotFound() : View(chamada);
    }

    /// <summary>
    /// O token antiforgery na hora do envio (a página guardada no aparelho pode ter um token antigo) e quem está logado:
    /// no aparelho compartilhado, a fila só envia as marcações de quem está logado (P38).
    /// </summary>
    [HttpGet]
    [Permissao(Recurso.RegistroPresenca, Operacao.Alterar)]
    public IActionResult Token()
    {
        // Os mesmos cabeçalhos que o antiforgery põe: com outros, ele sobrescreve e avisa no log.
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Pragma = "no-cache";
        return Json(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken, pessoa = usuarioAtual.Id });
    }

    [HttpPost]
    [Permissao(Recurso.RegistroPresenca, Operacao.Alterar)]
    public async Task<IActionResult> Sincronizar([FromBody] LoteMarcacoes? lote, CancellationToken ct)
    {
        try
        {
            var resultado = await servico.SincronizarAsync(lote ?? new LoteMarcacoes(), ct);
            return resultado.Erro is { } erro ? BadRequest(new { erro }) : Json(resultado);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro aparelho gravou a mesma presença ao mesmo tempo: nada foi gravado, e a fila do aparelho manda de novo.
            return Conflict(new { erro = "Outra marcação chegou ao mesmo tempo. A fila manda de novo em instantes." });
        }
    }

    /// <summary>O que o service worker mostra sem sinal quando a chamada ainda não foi aberta neste aparelho.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult SemSinal() => View();
}
