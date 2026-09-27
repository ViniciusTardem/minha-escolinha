using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Comum;

namespace MinhaEscolinha.Infrastructure.Comum;

/// <summary>
/// Abre um escopo novo de DI (DbContext novo) com o contexto na escolinha indicada. Mantém o sinal de "equipe da
/// plataforma" de quem chamou — o usuário atual continua o mesmo (o autor da auditoria é quem pediu a ação).
/// </summary>
public class ExecutorEscolinha(IServiceScopeFactory fabrica, IContextoEscolinha contextoAtual) : IExecutorEscolinha
{
    public async Task<T> ExecutarAsync<T>(Guid escolinhaId, Func<IServiceProvider, Task<T>> acao, CancellationToken ct = default)
    {
        await using var escopo = fabrica.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<IContextoEscolinha>()
            .Definir(escolinhaId, papeis: null, administradorPlataforma: contextoAtual.AdministradorPlataforma);
        ct.ThrowIfCancellationRequested();
        return await acao(escopo.ServiceProvider);
    }
}
