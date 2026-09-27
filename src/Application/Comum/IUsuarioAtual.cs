namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// Quem está executando a ação: a <c>Pessoa</c> dona da credencial logada (<see cref="Id"/> = <c>Pessoa_ID</c>).
/// Nulo em job, seed e requisição anônima.
/// </summary>
public interface IUsuarioAtual
{
    Guid? Id { get; }
    string? Nome { get; }
    string? Login { get; }

    /// <summary>IP de onde veio a ação (trilha de auditoria). Nulo fora de requisição HTTP.</summary>
    string? Ip => null;
}
