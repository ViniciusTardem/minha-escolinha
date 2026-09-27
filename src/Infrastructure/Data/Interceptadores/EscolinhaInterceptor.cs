using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Infrastructure.Data.Interceptadores;

/// <summary>
/// Guarda do multi-escolinha na gravação (a leitura é o filtro global do DbContext):
/// <list type="bullet">
/// <item>inclusão de dado de escolinha sem escolinha no contexto → exceção;</item>
/// <item>inclusão sem <c>Escolinha_ID</c> → recebe a do contexto; com outra escolinha → exceção;</item>
/// <item>alteração ou exclusão de dado de outra escolinha, ou troca de <c>Escolinha_ID</c> → exceção.</item>
/// </list>
/// É exceção (e não <c>ResultadoOperacao</c>) de propósito: chegar aqui é defeito de código, não regra de negócio.
/// </summary>
public class EscolinhaInterceptor(IContextoEscolinha contexto) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Conferir(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Conferir(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Conferir(DbContext? db)
    {
        if (db is null) return;

        foreach (var entrada in db.ChangeTracker.Entries<IEntidadeDaEscolinha>())
        {
            if (entrada.State is EntityState.Unchanged or EntityState.Detached) continue;

            var atual = contexto.EscolinhaId ?? throw new InvalidOperationException(
                $"Gravação de {entrada.Metadata.ClrType.Name} sem escolinha no contexto.");

            if (entrada.State == EntityState.Added)
            {
                if (entrada.Entity.Escolinha_ID == Guid.Empty) entrada.Entity.Escolinha_ID = atual;
                else if (entrada.Entity.Escolinha_ID != atual)
                    throw new InvalidOperationException(
                        $"Tentativa de incluir {entrada.Metadata.ClrType.Name} da escolinha {entrada.Entity.Escolinha_ID} no contexto da escolinha {atual}.");
                continue;
            }

            var propriedade = entrada.Property(e => e.Escolinha_ID);
            if (propriedade.IsModified && !Equals(propriedade.OriginalValue, propriedade.CurrentValue))
                throw new InvalidOperationException($"{entrada.Metadata.ClrType.Name} não pode mudar de escolinha.");
            if (propriedade.OriginalValue != atual)
                throw new InvalidOperationException(
                    $"Tentativa de alterar {entrada.Metadata.ClrType.Name} da escolinha {propriedade.OriginalValue} no contexto da escolinha {atual}.");
        }

        foreach (var entrada in db.ChangeTracker.Entries<Escolinha>())
        {
            if (entrada.State is EntityState.Unchanged or EntityState.Detached) continue;
            if (contexto.EscolinhaId != entrada.Entity.Escolinha_ID)
                throw new InvalidOperationException("A escolinha só é gravada com o contexto nela mesma.");
        }
    }
}
