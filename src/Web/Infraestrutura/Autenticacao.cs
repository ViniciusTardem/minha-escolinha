using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MinhaEscolinha.Application.Acesso;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>Nomes das políticas de autorização.</summary>
public static class Politicas
{
    /// <summary>Equipe da plataforma (LDNA): criar escolinhas, conceder créditos.</summary>
    public const string Plataforma = nameof(Plataforma);

    /// <summary>Painel do Hangfire (/jobs): equipe da plataforma sem troca de senha pendente.</summary>
    public const string Jobs = nameof(Jobs);
}

public static class ClaimsEscolinha
{
    public const string Esquema = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string NomeCookie = ".MinhaEscolinha.Auth";

    public const string Login = "esc:login";
    public const string TrocarSenha = "esc:trocar_senha";
    public const string CarimboSeguranca = "esc:carimbo";
    public const string ValidadoEm = "esc:validado_em";
    public const string AdministradorPlataforma = "esc:plataforma";

    /// <summary>A escolinha escolhida para esta sessão. Os papéis nela são relidos a cada requisição.</summary>
    public const string Escolinha = "esc:escolinha";
    public const string EscolinhaNome = "esc:escolinha_nome";

    public static ClaimsPrincipal CriarPrincipal(CredencialAutenticada c, DateTimeOffset validadoEm, EscolinhaDaPessoa? escolinha)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, c.PessoaId.ToString()),
            new(ClaimTypes.Name, c.Nome),
            new(Login, c.Login),
            new(CarimboSeguranca, c.CarimboSeguranca.ToString()),
            new(ValidadoEm, validadoEm.ToUnixTimeSeconds().ToString())
        };
        if (c.TrocarSenha) claims.Add(new Claim(TrocarSenha, "true"));
        if (c.AdministradorPlataforma) claims.Add(new Claim(AdministradorPlataforma, "true"));
        if (escolinha is not null)
        {
            claims.Add(new Claim(Escolinha, escolinha.EscolinhaId.ToString()));
            claims.Add(new Claim(EscolinhaNome, escolinha.Nome));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, Esquema, ClaimTypes.Name, ClaimTypes.Role));
    }

    public static Guid? PessoaId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid? EscolinhaId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(Escolinha), out var id) ? id : null;

    public static bool PrecisaTrocarSenha(this ClaimsPrincipal principal) => principal.HasClaim(TrocarSenha, "true");

    public static bool EhPlataforma(this ClaimsPrincipal principal) => principal.HasClaim(AdministradorPlataforma, "true");
}

/// <summary>
/// Eventos do cookie de login:
/// <list type="bullet">
/// <item>requisição que não é navegação de página (fetch, /api, /hubs) recebe <b>401/403 JSON</b> em vez do 302 para o login;</item>
/// <item>a cada <see cref="IntervaloRevalidacao"/> a credencial é conferida no banco — desativada ou com senha redefinida
/// derruba a sessão (carimbo de segurança). Os papéis não esperam: são relidos a cada requisição pelo contexto.</item>
/// </list>
/// Portado do AlternativaDepartamentoPessoal.
/// </summary>
public class EventosCookieEscolinha(IServicoAutenticacao autenticacao, TimeProvider relogio, ILogger<EventosCookieEscolinha> logger)
    : CookieAuthenticationEvents
{
    public static readonly TimeSpan IntervaloRevalidacao = TimeSpan.FromMinutes(5);

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        RespostaOuRedirecionamento(context, StatusCodes.Status401Unauthorized, "Sessão expirada ou ausente. Entre novamente.",
            () => base.RedirectToLogin(context));

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        RespostaOuRedirecionamento(context, StatusCodes.Status403Forbidden, "Você não tem acesso a este recurso.",
            () => base.RedirectToAccessDenied(context));

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null) return;

        var agora = relogio.GetUtcNow();
        if (long.TryParse(principal.FindFirst(ClaimsEscolinha.ValidadoEm)?.Value, out var segundos)
            && agora - DateTimeOffset.FromUnixTimeSeconds(segundos) < IntervaloRevalidacao)
            return;

        var id = principal.PessoaId();
        _ = Guid.TryParse(principal.FindFirst(ClaimsEscolinha.CarimboSeguranca)?.Value, out var carimbo);
        var credencial = id is null ? null : await autenticacao.ValidarSessaoAsync(id.Value, carimbo, context.HttpContext.RequestAborted);

        if (credencial is null)
        {
            logger.LogInformation("Sessão da pessoa {PessoaId} encerrada: credencial inativa ou alterada.", id);
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(ClaimsEscolinha.Esquema);
            return;
        }

        // Mantém a escolinha escolhida se a pessoa ainda atua nela (o nome pode ter mudado).
        var escolhida = principal.EscolinhaId() is { } escolinhaId
            ? credencial.Escolinhas.FirstOrDefault(e => e.EscolinhaId == escolinhaId)
            : null;
        context.ReplacePrincipal(ClaimsEscolinha.CriarPrincipal(credencial, agora, escolhida));
        context.ShouldRenew = true;
    }

    private static Task RespostaOuRedirecionamento(RedirectContext<CookieAuthenticationOptions> context, int status,
        string mensagem, Func<Task> redirecionar)
    {
        if (RequisicaoDePagina(context.Request)) return redirecionar();

        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { sucesso = false, mensagem });
    }

    /// <summary>Navegação de página (o navegador pede text/html). fetch() sem Accept explícito manda */* e cai no JSON.</summary>
    public static bool RequisicaoDePagina(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/hubs")) return false;
        if (request.Headers.XRequestedWith.Count > 0) return false;
        return request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
