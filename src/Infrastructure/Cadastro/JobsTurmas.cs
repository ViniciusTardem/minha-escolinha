using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Cadastro;

/// <summary>
/// O primeiro recorrente de negócio (BL-M04-04): todo dia, cada escolinha tem as sessões de treino geradas até o fim do
/// mês seguinte. Um escopo por escolinha (o contexto nunca troca dentro de um escopo); a geração é idempotente, então a
/// retentativa não duplica.
/// </summary>
public class JobsTurmas(IConsultasPlataforma consultas, IExecutorEscolinha executor, ILogger<JobsTurmas> logger)
{
    public const string IdGeracao = "sessoes-gerar-horizonte";

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [JobDisplayName("Sessões de treino — gerar o horizonte das escolinhas")]
    public async Task GerarSessoesAsync(CancellationToken ct)
    {
        var falhas = 0;
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            try
            {
                await executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<ServicoSessoes>().GerarHorizonteAsync(ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                logger.LogError(ex, "Falha ao gerar as sessões da escolinha {Escolinha}.", escolinhaId);
            }
        }

        // As outras escolinhas já foram geradas; a retentativa refaz só o que falta (idempotente).
        if (falhas > 0) throw new InvalidOperationException($"A geração de sessões falhou em {falhas} escolinha(s): veja o log.");
    }

    /// <summary>Todo dia às 03:15, no fuso de Brasília.</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes) =>
        recorrentes.AddOrUpdate<JobsTurmas>(IdGeracao, j => j.GerarSessoesAsync(CancellationToken.None), "15 3 * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
}
