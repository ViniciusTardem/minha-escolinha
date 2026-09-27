using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Middleware;

/// <summary>
/// Registra toda exceção não tratada com o contexto da requisição (método, caminho, pessoa) e re-lança
/// para o handler que responde (<c>UseDeveloperExceptionPage</c> em dev, <c>UseExceptionHandler("/Erro")</c> fora).
/// Fica depois da autenticação para saber quem era a pessoa. Portado do AlternativaDepartamentoPessoal.
/// </summary>
public class ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cliente desistiu (fechou a aba, navegou): não é erro do sistema.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro não tratado em {Metodo} {Caminho} (pessoa {PessoaId}, rastreio {Rastreio}).",
                context.Request.Method, context.Request.Path, context.User.PessoaId(), context.TraceIdentifier);
            throw;
        }
    }
}
