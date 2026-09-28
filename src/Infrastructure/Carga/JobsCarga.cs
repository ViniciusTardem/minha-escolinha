using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Carga;

/// <summary>
/// A guarda da planilha de importação (P53): todo dia, uma escolinha por vez, o lote aberto e esquecido é descartado, e a
/// planilha cifrada sai do banco. Antes, isso só acontecia no envio seguinte da mesma escolinha.
/// </summary>
public class JobsCarga(IConsultasPlataforma consultas, IExecutorEscolinha executor, ILogger<JobsCarga> logger)
{
    public const string IdDescarte = "importacao-descartar-esquecidos";

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [JobDisplayName("Importação — descartar os lotes esquecidos")]
    public async Task DescartarEsquecidosAsync(CancellationToken ct)
    {
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            try
            {
                var descartados = await executor.ExecutarAsync(escolinhaId,
                    sp => sp.GetRequiredService<ServicoImportacao>().DescartarEsquecidosAsync(ct), ct);
                if (descartados > 0)
                    logger.LogInformation("Importação: {Descartados} lote(s) esquecido(s) descartado(s) na escolinha {Escolinha}.", descartados, escolinhaId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A rodada do dia seguinte refaz: o descarte é idempotente.
                logger.LogError(ex, "Falha no descarte dos lotes de importação da escolinha {Escolinha}.", escolinhaId);
            }
        }
    }

    /// <summary>Todo dia às 04:45, no fuso de Brasília (depois da guarda dos webhooks).</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes) =>
        recorrentes.AddOrUpdate<JobsCarga>(IdDescarte, j => j.DescartarEsquecidosAsync(CancellationToken.None), "45 4 * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
}
