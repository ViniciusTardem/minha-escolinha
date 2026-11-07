using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// A página pública de aceite dos termos (D111), sem login e fora dos buscadores: 1) o link abre sem mostrar dado nenhum;
/// 2) o responsável confere o CPF (quando o cadastro tem CPF); 3) lê os termos e responde cada um. Respondido, o link
/// deixa de existir.
/// </summary>
[AllowAnonymous]
[Route("Termos/{token}")]
public class TermosController(ServicoRespostaTermos servico) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        NaoIndexar();
        var resultado = await servico.AbrirAsync(token, null, ct);
        if (!resultado.Sucesso || resultado.Dados is not TermosParaResponder termos) return View("Indisponivel");
        return View(termos.Identificado ? "Responder" : "Index", new TermosViewModel(token, termos, new RespostaTermosRequisicao()));
    }

    [HttpPost("Identificar")]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> Identificar(string token, RespostaTermosRequisicao requisicao, CancellationToken ct)
    {
        NaoIndexar();
        var resultado = await servico.AbrirAsync(token, requisicao.Cpf ?? string.Empty, ct);
        if (resultado.Sucesso && resultado.Dados is TermosParaResponder termos)
            return View("Responder", new TermosViewModel(token, termos, new RespostaTermosRequisicao { Cpf = requisicao.Cpf }));
        if (resultado.Campo is null) return View("Indisponivel");

        ModelState.AdicionarErro(resultado);
        var abertura = await servico.AbrirAsync(token, null, ct);
        return abertura.Dados is TermosParaResponder inicio
            ? View("Index", new TermosViewModel(token, inicio, requisicao))
            : View("Indisponivel");
    }

    [HttpPost("Responder")]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> Responder(string token, RespostaTermosRequisicao requisicao, CancellationToken ct)
    {
        NaoIndexar();
        var resultado = await servico.ResponderAsync(token, requisicao, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (resultado.Sucesso)
        {
            TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
            return RedirectToAction(nameof(Respondido), new { token });
        }
        if (resultado.Campo is null) return View("Indisponivel");

        ModelState.AdicionarErro(resultado);
        var abertura = await servico.AbrirAsync(token, requisicao.Cpf ?? string.Empty, ct);
        if (abertura.Dados is TermosParaResponder termos) return View("Responder", new TermosViewModel(token, termos, requisicao));
        // O CPF digitado não confere: volta para a confirmação.
        var inicio = await servico.AbrirAsync(token, null, ct);
        return inicio.Dados is TermosParaResponder t ? View("Index", new TermosViewModel(token, t, requisicao)) : View("Indisponivel");
    }

    [HttpGet("Respondido")]
    public IActionResult Respondido()
    {
        NaoIndexar();
        return View();
    }

    /// <summary>O link é não listado: nenhum buscador guarda a página.</summary>
    private void NaoIndexar() => Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
}
