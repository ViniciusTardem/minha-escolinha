using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Integracao;

/// <summary>
/// A guarda do corpo bruto dos webhooks (P22): todo dia, o corpo do webhook já encerrado (processado, ignorado ou com
/// falha definitiva) e recebido há mais de <see cref="WebhookRecebido.DiasDoCorpo"/> dias é apagado. A linha fica, com a
/// origem, o id do evento (que continua barrando o reenvio), a situação e a observação. O que ainda espera processamento
/// nunca perde o corpo.
/// </summary>
public class JobsIntegracao(EscolinhaDbContext contexto, IRelogioBrasil relogio, ILogger<JobsIntegracao> logger)
{
    public const string IdLimpeza = "webhooks-limpar-corpo";

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [JobDisplayName("Webhooks — apagar o corpo bruto dos encerrados há mais de 90 dias")]
    public async Task LimparCorposAsync(CancellationToken ct)
    {
        var agora = relogio.Agora();
        var limite = agora.AddDays(-WebhookRecebido.DiasDoCorpo);
        var apagados = await contexto.WebhooksRecebidos
            .Where(w => w.CorpoApagadoEm == null && w.Situacao != SituacaoWebhook.Recebido && w.RecebidoEm < limite)
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.Corpo, string.Empty).SetProperty(w => w.CorpoApagadoEm, agora), ct);
        if (apagados > 0)
            logger.LogInformation("Webhooks: corpo bruto apagado de {Apagados} chamada(s) encerrada(s) recebida(s) antes de {Limite:dd/MM/yyyy}.",
                apagados, limite);
    }

    /// <summary>Todo dia às 04:30, no fuso de Brasília (depois das sessões e antes da rotina das mensalidades).</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes) =>
        recorrentes.AddOrUpdate<JobsIntegracao>(IdLimpeza, j => j.LimparCorposAsync(CancellationToken.None), "30 4 * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
}
