using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Filters;

/// <summary>Libera a action para quem ainda precisa trocar a senha (a própria tela de troca, sair, erro).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PermitirSemTrocarSenhaAttribute : Attribute;

/// <summary>
/// Global: credencial com <c>TrocarSenha</c> (1º acesso ou senha redefinida) só usa o sistema depois de trocar a senha.
/// Página → redireciona para a troca; fetch → 403 JSON. Portado do AlternativaDepartamentoPessoal.
/// </summary>
public class ExigirTrocaSenhaFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var usuario = context.HttpContext.User;
        if (usuario.Identity?.IsAuthenticated != true || !usuario.PrecisaTrocarSenha()) return;
        if (context.ActionDescriptor.EndpointMetadata.OfType<PermitirSemTrocarSenhaAttribute>().Any()) return;

        context.Result = EventosCookieEscolinha.RequisicaoDePagina(context.HttpContext.Request)
            ? new RedirectToActionResult("TrocarSenha", "Conta", null)
            : new ObjectResult(new { sucesso = false, mensagem = "Troque a sua senha antes de continuar." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
