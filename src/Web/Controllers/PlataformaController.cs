using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Área da equipe da plataforma (LDNA): lista das escolinhas, criação da escolinha com o 1º gestor, créditos e o acesso
/// da equipe como gestora (auditado). Não mostra dado de aluno nenhum.
/// </summary>
[Plataforma]
public class PlataformaController(
    ServicoPlataforma plataforma,
    IConsultasPlataforma consultas,
    IServicoAutenticacao autenticacao,
    TimeProvider relogio) : Controller
{
    private const string ChaveCriada = "Plataforma.Criada";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "plataforma";
        return View(await consultas.ListarEscolinhasAsync(User.PessoaId(), ct));
    }

    [HttpGet]
    public IActionResult Nova()
    {
        ViewData["ModuloAtivo"] = "plataforma";
        return View(new NovaEscolinhaRequisicao());
    }

    [HttpPost]
    public async Task<IActionResult> Nova(NovaEscolinhaRequisicao requisicao, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "plataforma";
        var resultado = await plataforma.CriarEscolinhaComGestorAsync(requisicao, ct);
        if (!resultado.Sucesso || resultado.Dados is not EscolinhaCriada criada)
        {
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        // A senha temporária vai uma única vez para a tela seguinte (TempData cifrado e lido uma vez).
        TempData[ChaveCriada] = System.Text.Json.JsonSerializer.Serialize(criada);
        return RedirectToAction(nameof(Criada));
    }

    [HttpGet]
    public IActionResult Criada()
    {
        ViewData["ModuloAtivo"] = "plataforma";
        if (TempData[ChaveCriada] is not string json) return RedirectToAction(nameof(Index));
        return View(System.Text.Json.JsonSerializer.Deserialize<EscolinhaCriada>(json));
    }

    [HttpGet]
    public async Task<IActionResult> Creditos(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "plataforma";
        var escolinha = await consultas.ObterEscolinhaAsync(id, User.PessoaId(), ct);
        if (escolinha is null) return NotFound();
        ViewData["Escolinha"] = escolinha;
        return View(new ConcederCreditosRequisicao { EscolinhaId = id });
    }

    [HttpPost]
    public async Task<IActionResult> Creditos(ConcederCreditosRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await plataforma.ConcederCreditosAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "plataforma";
            ViewData["Escolinha"] = await consultas.ObterEscolinhaAsync(requisicao.EscolinhaId, User.PessoaId(), ct);
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>A pessoa da equipe passa a atuar como gestora da escolinha (auditado) e a sessão entra nela.</summary>
    [HttpPost]
    public async Task<IActionResult> EntrarComoEquipe(Guid id, CancellationToken ct)
    {
        var resultado = await plataforma.VincularEquipeComoGestorAsync(id, ct);
        if (!resultado.Sucesso)
        {
            TempData[ChavesTempData.Erro] = resultado.Mensagem;
            return RedirectToAction(nameof(Index));
        }

        if (User.PessoaId() is { } pessoaId)
        {
            _ = Guid.TryParse(User.FindFirstValue(ClaimsEscolinha.CarimboSeguranca), out var carimbo);
            var credencial = await autenticacao.ValidarSessaoAsync(pessoaId, carimbo, ct);
            var escolhida = credencial?.Escolinhas.FirstOrDefault(e => e.EscolinhaId == id);
            if (credencial is not null && escolhida is not null)
            {
                var persistente = (await HttpContext.AuthenticateAsync(ClaimsEscolinha.Esquema)).Properties?.IsPersistent ?? false;
                await HttpContext.SignInAsync(ClaimsEscolinha.Esquema,
                    ClaimsEscolinha.CriarPrincipal(credencial, relogio.GetUtcNow(), escolhida),
                    new AuthenticationProperties { IsPersistent = persistente });
            }
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction("Index", "Painel");
    }
}
