using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Infrastructure.Data.Interceptadores;

/// <summary>
/// Carimba "quem e quando" em toda <see cref="IEntidadeAuditavel"/> ao salvar (inclusão e alteração),
/// tanto em <c>SaveChanges</c> quanto em <c>SaveChangesAsync</c>. Portado do AlternativaDepartamentoPessoal.
/// </summary>
public class AuditoriaInterceptor(IRelogioBrasil relogio, IUsuarioAtual usuarioAtual) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Carimbar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Carimbar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Carimbar(DbContext? contexto)
    {
        if (contexto is null) return;

        var agora = relogio.Agora();
        var usuarioId = usuarioAtual.Id;

        foreach (var entrada in contexto.ChangeTracker.Entries<IEntidadeAuditavel>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.Entity.DataCadastro = agora;
                    entrada.Entity.UsuarioCadastro_ID = usuarioId;
                    break;

                case EntityState.Modified:
                    entrada.Entity.DataAtualizacao = agora;
                    entrada.Entity.UsuarioAtualizacao_ID = usuarioId;
                    // Inclusão nunca é reescrita por uma alteração.
                    entrada.Property(e => e.DataCadastro).IsModified = false;
                    entrada.Property(e => e.UsuarioCadastro_ID).IsModified = false;
                    break;
            }
        }
    }
}
