using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Plataforma;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Plataforma;

public class PlataformaRepositorio(EscolinhaDbContext contexto) : IPlataformaRepositorio
{
    public async Task<IReadOnlyList<PacoteCreditos>> ListarPacotesAsync(bool somenteAtivos, CancellationToken ct = default) =>
        await contexto.PacotesCreditos
            .Where(p => !somenteAtivos || p.Ativo)
            .OrderBy(p => p.Ordem).ThenBy(p => p.Preco)
            .ToListAsync(ct);

    public Task<PacoteCreditos?> ObterPacoteAsync(Guid id, CancellationToken ct = default) =>
        contexto.PacotesCreditos.FirstOrDefaultAsync(p => p.PacoteCreditos_ID == id, ct);

    public void AdicionarPacote(PacoteCreditos pacote) => contexto.PacotesCreditos.Add(pacote);

    public void AdicionarCompra(CompraCreditos compra) => contexto.ComprasCreditos.Add(compra);

    public Task<CompraCreditos?> ObterCompraAsync(Guid id, CancellationToken ct = default) =>
        contexto.ComprasCreditos.FirstOrDefaultAsync(c => c.CompraCreditos_ID == id, ct);

    public async Task<IReadOnlyList<CompraCreditos>> ListarComprasAsync(int limite, CancellationToken ct = default) =>
        await contexto.ComprasCreditos.AsNoTracking()
            .OrderByDescending(c => c.IniciadaEm)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListarComprasPendentesAsync(CancellationToken ct = default) =>
        await contexto.ComprasCreditos
            .Where(c => c.Situacao == SituacaoCompraCreditos.AguardandoPagamento || c.Situacao == SituacaoCompraCreditos.AguardandoConfirmacao)
            .OrderBy(c => c.IniciadaEm)
            .Select(c => c.CompraCreditos_ID)
            .ToListAsync(ct);

    /// <summary>
    /// Grava na hora (é o único método que salva sozinho): o índice único (origem, evento) decide se é repetido, mesmo
    /// com dois reenvios chegando juntos.
    /// </summary>
    public async Task<(bool Novo, Guid Id)> GravarWebhookAsync(WebhookRecebido webhook, CancellationToken ct = default)
    {
        if (await IdDoWebhookAsync(webhook.Origem, webhook.EventoExterno, ct) is { } existente) return (false, existente);

        contexto.WebhooksRecebidos.Add(webhook);
        try
        {
            await contexto.SaveChangesAsync(ct);
            return (true, webhook.WebhookRecebido_ID);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            contexto.Entry(webhook).State = EntityState.Detached;
            return (false, await IdDoWebhookAsync(webhook.Origem, webhook.EventoExterno, ct) ?? Guid.Empty);
        }
    }

    public Task<WebhookRecebido?> ObterWebhookAsync(Guid id, CancellationToken ct = default) =>
        contexto.WebhooksRecebidos.FirstOrDefaultAsync(w => w.WebhookRecebido_ID == id, ct);

    public async Task<IReadOnlyList<WebhookRecebido>> ListarWebhooksAsync(string origem, int limite, CancellationToken ct = default) =>
        await contexto.WebhooksRecebidos.AsNoTracking()
            .Where(w => w.Origem == origem)
            .OrderByDescending(w => w.RecebidoEm)
            .Take(limite)
            .ToListAsync(ct);

    public async Task SalvarAsync(CancellationToken ct = default)
    {
        try
        {
            await contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeConcorrenciaException("Outra operação gravou o mesmo registro antes; nada desta gravação ficou.", ex);
        }
    }

    private async Task<Guid?> IdDoWebhookAsync(string origem, string evento, CancellationToken ct) =>
        await contexto.WebhooksRecebidos
            .Where(w => w.Origem == origem && w.EventoExterno == evento)
            .Select(w => (Guid?)w.WebhookRecebido_ID)
            .FirstOrDefaultAsync(ct);
}
