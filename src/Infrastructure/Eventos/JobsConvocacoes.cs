using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Eventos;

/// <summary>
/// A rotina da convocação (S5), a cada 15 minutos, uma escolinha por vez: o lembrete a quem não respondeu, na janela
/// antes do evento (BL-M07-11), e a convocação que ficou sem resposta quando o evento começou (P19).
/// </summary>
public class JobsConvocacoes(IConsultasPlataforma consultas, IExecutorEscolinha executor, ILogger<JobsConvocacoes> logger)
{
    public const string IdRotina = "convocacoes-rotina";

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [JobDisplayName("Convocações — lembretes e sem resposta")]
    public async Task RotinaAsync(CancellationToken ct)
    {
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            try
            {
                await executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<RotinaConvocacoes>().ExecutarAsync(ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A próxima rodada (15 minutos) refaz: tudo é idempotente.
                logger.LogError(ex, "Falha na rotina das convocações da escolinha {Escolinha}.", escolinhaId);
            }
        }
    }

    /// <summary>A cada 15 minutos, no fuso de Brasília.</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes) =>
        recorrentes.AddOrUpdate<JobsConvocacoes>(IdRotina, j => j.RotinaAsync(CancellationToken.None), "*/15 * * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
}

/// <summary>
/// O tempo real do painel de confirmações (BL-M07-09, CA-12), no padrão do DP: depois da gravação que mexeu num evento ou
/// nas convocações dele (disparo, resposta pelo WhatsApp, registro por fora, cancelamento, rotina), avisa as telas
/// abertas com o id do evento. Registrado depois do interceptor de escolinha: a escolinha já está carimbada.
/// </summary>
public class TempoRealConvocacoesInterceptor(INotificadorConvocacoes notificador) : SaveChangesInterceptor
{
    private readonly HashSet<(Guid Escolinha, Guid Evento)> _alterados = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Coletar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Coletar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Avisar();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Avisar();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _alterados.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _alterados.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Coletar(DbContext? contexto)
    {
        if (contexto is null) return;
        foreach (var entrada in contexto.ChangeTracker.Entries<Convocacao>())
            if (entrada.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                _alterados.Add((entrada.Entity.Escolinha_ID, entrada.Entity.Evento_ID));
        foreach (var entrada in contexto.ChangeTracker.Entries<Evento>())
            if (entrada.State is EntityState.Added or EntityState.Modified)
                _alterados.Add((entrada.Entity.Escolinha_ID, entrada.Entity.Evento_ID));
    }

    private void Avisar()
    {
        if (_alterados.Count == 0) return;
        notificador.EventosAlterados(_alterados.ToList());
        _alterados.Clear();
    }
}
