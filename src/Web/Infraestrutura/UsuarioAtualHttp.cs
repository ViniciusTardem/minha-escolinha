using System.Security.Claims;
using MinhaEscolinha.Application.Comum;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>Pessoa logada a partir dos claims do cookie. Nula fora de requisição HTTP (jobs) e em requisição anônima.</summary>
public class UsuarioAtualHttp(IHttpContextAccessor accessor) : IUsuarioAtual
{
    private ClaimsPrincipal? Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } usuario ? usuario : null;

    public Guid? Id => Principal?.PessoaId();
    public string? Nome => Principal?.FindFirstValue(ClaimTypes.Name);
    public string? Login => Principal?.FindFirstValue(ClaimsEscolinha.Login);
    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

/// <summary>Ambiente do host (Development, Homologacao, Production) para as travas da aplicação.</summary>
public class AmbienteExecucaoWeb(IWebHostEnvironment ambiente) : IAmbienteExecucao
{
    public string Nome => ambiente.EnvironmentName;
    public bool EhProducao => ambiente.IsProduction();
}
