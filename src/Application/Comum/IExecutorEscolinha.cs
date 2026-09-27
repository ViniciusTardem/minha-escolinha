namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// Roda uma ação num escopo novo de DI com o contexto posto na escolinha indicada — o jeito explícito de operar
/// numa escolinha fora da requisição dela: job, criação da escolinha pela plataforma, reconciliação do startup.
/// Cada chamada tem o próprio DbContext: nunca se troca de escolinha dentro do mesmo escopo.
/// </summary>
public interface IExecutorEscolinha
{
    Task<T> ExecutarAsync<T>(Guid escolinhaId, Func<IServiceProvider, Task<T>> acao, CancellationToken ct = default);
}
