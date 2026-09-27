using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Exige o recurso da matriz de permissões (§7) na escolinha do contexto. É a camada "ação" do motor de permissões;
/// o escopo (turmas do professor, filhos do responsável) é aplicado pelas consultas e pelo <c>IAutorizacao.PodeAsync</c>.
/// Sem escolinha no contexto: página → escolher escolinha; fetch → 403 JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissaoAttribute(Recurso recurso, Operacao operacao = Operacao.Ler) : Attribute, IAsyncAuthorizationFilter
{
    public Recurso Recurso { get; } = recurso;
    public Operacao Operacao { get; } = operacao;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var usuario = context.HttpContext.User;
        if (usuario.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return Task.CompletedTask;
        }

        var autorizacao = context.HttpContext.RequestServices.GetRequiredService<IAutorizacao>();
        if (autorizacao.EscolinhaId is null)
        {
            context.Result = EventosCookieEscolinha.RequisicaoDePagina(context.HttpContext.Request)
                ? new RedirectToActionResult("EscolherEscolinha", "Conta", null)
                : new ObjectResult(new { sucesso = false, mensagem = "Escolha a escolinha antes de continuar." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            return Task.CompletedTask;
        }

        if (!autorizacao.Pode(Recurso, Operacao)) context.Result = new ForbidResult();
        return Task.CompletedTask;
    }
}

/// <summary>Só a equipe da plataforma (LDNA).</summary>
public sealed class PlataformaAttribute() : AuthorizeAttribute(Politicas.Plataforma);

/// <summary>
/// Basta estar logado (painel inicial, trocar senha, escolher escolinha). Marca explícita: o teste de arquitetura exige
/// que toda action declare o acesso — <see cref="PermissaoAttribute"/>, <see cref="PlataformaAttribute"/>, este ou
/// <see cref="AllowAnonymousAttribute"/>. Nada fica aberto por esquecimento.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SomenteAutenticadoAttribute : Attribute;
