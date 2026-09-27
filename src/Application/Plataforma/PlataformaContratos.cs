using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Application.Plataforma;

/// <summary>
/// Dados da relação comercial LDNA ↔ escolinha. Pacotes e webhooks são globais; as compras são da escolinha do
/// contexto (filtro de escolinha). Um DbContext por escopo: um <see cref="SalvarAsync"/> grava tudo o que mudou.
/// </summary>
public interface IPlataformaRepositorio
{
    Task<IReadOnlyList<PacoteCreditos>> ListarPacotesAsync(bool somenteAtivos, CancellationToken ct = default);
    Task<PacoteCreditos?> ObterPacoteAsync(Guid id, CancellationToken ct = default);
    void AdicionarPacote(PacoteCreditos pacote);

    void AdicionarCompra(CompraCreditos compra);
    Task<CompraCreditos?> ObterCompraAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CompraCreditos>> ListarComprasAsync(int limite, CancellationToken ct = default);

    /// <summary>Compras ainda sem desfecho (aguardando pagamento ou confirmação): o que a varredura confere.</summary>
    Task<IReadOnlyList<Guid>> ListarComprasPendentesAsync(CancellationToken ct = default);

    /// <summary>Grava o webhook bruto. Evento repetido (mesma origem e id) não grava de novo: <c>Novo</c> = false.</summary>
    Task<(bool Novo, Guid Id)> GravarWebhookAsync(WebhookRecebido webhook, CancellationToken ct = default);

    Task<WebhookRecebido?> ObterWebhookAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookRecebido>> ListarWebhooksAsync(string origem, int limite, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}

/// <summary>
/// O que o módulo manda fazer em segundo plano (Hangfire no app; fila em memória nos testes). A linha do banco é a fonte
/// da verdade e o job é uma dica: a varredura reenfileira o que se perdeu.
/// </summary>
public interface IAgendadorPlataforma
{
    void ProcessarWebhookStripe(Guid webhookId);
    void ConferirCompraCreditos(Guid escolinhaId, Guid compraId);
}
