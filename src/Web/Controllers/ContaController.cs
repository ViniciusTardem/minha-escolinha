using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Web.Filters;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Entrar, escolher a escolinha da sessão, trocar senha e sair.</summary>
public class ContaController(IServicoAutenticacao autenticacao, TimeProvider relogio) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Painel");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginRequisicao());
    }

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> Login(LoginRequisicao requisicao, string? returnUrl = null, CancellationToken ct = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        var resultado = await autenticacao.AutenticarAsync(requisicao, ct);
        if (!resultado.Sucesso || resultado.Dados is not CredencialAutenticada credencial)
        {
            ModelState.AdicionarErro(resultado);
            requisicao.Senha = string.Empty;
            return View(requisicao);
        }

        // Uma escolinha só: a sessão já nasce nela. Mais de uma (ou só a plataforma): a pessoa escolhe.
        var unica = credencial.Escolinhas.Count == 1 ? credencial.Escolinhas[0] : null;
        await EntrarAsync(credencial, unica, requisicao.Lembrarme);

        if (credencial.TrocarSenha) return RedirectToAction(nameof(TrocarSenha));
        if (unica is null) return DestinoSemEscolinha(credencial);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Painel");
    }

    [HttpGet]
    [SomenteAutenticado]
    public async Task<IActionResult> EscolherEscolinha(CancellationToken ct)
    {
        var escolinhas = await autenticacao.EscolinhasDisponiveisAsync(ct);
        if (escolinhas.Count == 0 && !User.EhPlataforma()) return RedirectToAction(nameof(AcessoNegado));
        return View(escolinhas);
    }

    [HttpPost]
    [SomenteAutenticado]
    public async Task<IActionResult> EscolherEscolinha(Guid escolinhaId, CancellationToken ct)
    {
        var credencial = await CredencialDaSessaoAsync(ct);
        if (credencial is null) return await SairEVoltarAoLoginAsync();

        var escolhida = credencial.Escolinhas.FirstOrDefault(e => e.EscolinhaId == escolinhaId);
        if (escolhida is null)
        {
            TempData[ChavesTempData.Erro] = "Você não atua nessa escolinha.";
            return RedirectToAction(nameof(EscolherEscolinha));
        }

        await EntrarAsync(credencial, escolhida, await PersistenteAsync());
        return RedirectToAction("Index", "Painel");
    }

    /// <summary>Equipe da plataforma: sai da escolinha da sessão e vai para a área da plataforma.</summary>
    [HttpPost]
    [Plataforma]
    public async Task<IActionResult> UsarPlataforma(CancellationToken ct)
    {
        var credencial = await CredencialDaSessaoAsync(ct);
        if (credencial is null) return await SairEVoltarAoLoginAsync();

        await EntrarAsync(credencial, null, await PersistenteAsync());
        return RedirectToAction("Index", "Plataforma");
    }

    [HttpPost]
    [SomenteAutenticado]
    [PermitirSemTrocarSenha]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(ClaimsEscolinha.Esquema);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [SomenteAutenticado]
    [PermitirSemTrocarSenha]
    public IActionResult TrocarSenha() => View(new TrocarSenhaRequisicao());

    [HttpPost]
    [SomenteAutenticado]
    [PermitirSemTrocarSenha]
    public async Task<IActionResult> TrocarSenha(TrocarSenhaRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await autenticacao.TrocarPropriaSenhaAsync(requisicao, ct);
        if (!resultado.Sucesso || resultado.Dados is not CredencialAutenticada credencial)
        {
            ModelState.AdicionarErro(resultado);
            return View(LimparSenhas(requisicao));
        }

        // Novo carimbo de segurança: renova o cookie desta sessão (as outras sessões caem na próxima revalidação).
        var escolhida = User.EscolinhaId() is { } atual ? credencial.Escolinhas.FirstOrDefault(e => e.EscolinhaId == atual) : null;
        escolhida ??= credencial.Escolinhas.Count == 1 ? credencial.Escolinhas[0] : null;
        await EntrarAsync(credencial, escolhida, await PersistenteAsync());

        TempData[ChavesTempData.Sucesso] = "Senha alterada.";
        return escolhida is null ? DestinoSemEscolinha(credencial) : RedirectToAction("Index", "Painel");
    }

    // Recuperação de senha (BL-M01-09): o pedido por e-mail e a tela do link de uso único (e-mail ou gerado pela gestão).

    [AllowAnonymous]
    [HttpGet]
    public IActionResult EsqueciSenha() => View(new EsqueciSenhaRequisicao());

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> EsqueciSenha(EsqueciSenhaRequisicao requisicao, [FromServices] ServicoRecuperacaoSenha recuperacao,
        CancellationToken ct)
    {
        var resultado = await recuperacao.PedirPorEmailAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(EsqueciSenha));
    }

    [AllowAnonymous]
    [PermitirSemTrocarSenha]
    [HttpGet("/Conta/NovaSenha/{token}")]
    public async Task<IActionResult> NovaSenha(string token, [FromServices] ServicoRecuperacaoSenha recuperacao, CancellationToken ct)
    {
        ViewData["Nome"] = await recuperacao.ValidarAsync(token, ct);
        return View(new NovaSenhaRequisicao());
    }

    [AllowAnonymous]
    [PermitirSemTrocarSenha]
    [HttpPost("/Conta/NovaSenha/{token}")]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> NovaSenha(string token, NovaSenhaRequisicao requisicao, [FromServices] ServicoRecuperacaoSenha recuperacao,
        CancellationToken ct)
    {
        var resultado = await recuperacao.DefinirNovaSenhaAsync(token, requisicao, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AdicionarErro(resultado);
            ViewData["Nome"] = await recuperacao.ValidarAsync(token, ct);
            return View(new NovaSenhaRequisicao());
        }

        // A sessão aberta neste aparelho (se houver) é de antes da senha nova: sai, e a pessoa entra de novo.
        if (User.Identity?.IsAuthenticated == true) await HttpContext.SignOutAsync(ClaimsEscolinha.Esquema);
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [PermitirSemTrocarSenha]
    public IActionResult AcessoNegado()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    /// <summary>Sessão sem escolinha: a equipe da plataforma sem vínculo vai para a área dela; os demais escolhem a escolinha.</summary>
    private RedirectToActionResult DestinoSemEscolinha(CredencialAutenticada credencial) =>
        credencial.Escolinhas.Count == 0 && credencial.AdministradorPlataforma
            ? RedirectToAction("Index", "Plataforma")
            : RedirectToAction(nameof(EscolherEscolinha));

    private async Task<CredencialAutenticada?> CredencialDaSessaoAsync(CancellationToken ct)
    {
        if (User.PessoaId() is not { } pessoaId) return null;
        _ = Guid.TryParse(User.FindFirstValue(ClaimsEscolinha.CarimboSeguranca), out var carimbo);
        return await autenticacao.ValidarSessaoAsync(pessoaId, carimbo, ct);
    }

    private async Task<IActionResult> SairEVoltarAoLoginAsync()
    {
        await HttpContext.SignOutAsync(ClaimsEscolinha.Esquema);
        return RedirectToAction(nameof(Login));
    }

    private async Task<bool> PersistenteAsync() =>
        (await HttpContext.AuthenticateAsync(ClaimsEscolinha.Esquema)).Properties?.IsPersistent ?? false;

    private Task EntrarAsync(CredencialAutenticada credencial, EscolinhaDaPessoa? escolinha, bool persistente) =>
        HttpContext.SignInAsync(ClaimsEscolinha.Esquema, ClaimsEscolinha.CriarPrincipal(credencial, relogio.GetUtcNow(), escolinha),
            new AuthenticationProperties { IsPersistent = persistente });

    private static TrocarSenhaRequisicao LimparSenhas(TrocarSenhaRequisicao r)
    {
        r.SenhaAtual = r.NovaSenha = r.ConfirmacaoSenha = string.Empty;
        return r;
    }
}
