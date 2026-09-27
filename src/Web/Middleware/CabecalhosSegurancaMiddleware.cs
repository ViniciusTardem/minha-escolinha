namespace MinhaEscolinha.Web.Middleware;

/// <summary>
/// Cabeçalhos de segurança + CSP. <c>script-src 'self'</c>: nenhum script inline — JS só em arquivo, eventos por
/// delegação (CLAUDE.md §6). <c>style-src</c> aceita inline porque o KTUI posiciona dropdowns/drawers por atributo style.
/// <c>worker-src</c>/<c>manifest-src</c> já preparam a chamada offline (service worker da quadra, S3).
/// Portado do AlternativaDepartamentoPessoal, sem as origens externas de mídia do CRM de lá.
/// </summary>
public class CabecalhosSegurancaMiddleware(RequestDelegate next)
{
    public const string Csp =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "worker-src 'self'; " +
        "manifest-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'self'; " +
        "form-action 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var cabecalhos = context.Response.Headers;
        cabecalhos.XFrameOptions = "SAMEORIGIN";
        cabecalhos.XContentTypeOptions = "nosniff";
        cabecalhos["Referrer-Policy"] = "strict-origin-when-cross-origin";
        cabecalhos["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        cabecalhos.ContentSecurityPolicy = Csp;
        return next(context);
    }
}
