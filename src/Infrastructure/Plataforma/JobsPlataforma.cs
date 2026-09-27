using Hangfire;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Plataforma;

/// <summary>Os jobs do pagamento dos pacotes de créditos. O trabalho mora no <see cref="ProcessadorPagamentosStripe"/>.</summary>
public class JobsPlataforma(ProcessadorPagamentosStripe processador)
{
    public const string IdVarredura = "stripe-varredura-compras";

    [Queue(HangfireEscolinhaExtensions.FilaWebhooks)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("Stripe — processar o webhook {0}")]
    public Task ProcessarWebhookStripeAsync(Guid webhookId, CancellationToken ct) => processador.ProcessarWebhookAsync(webhookId, ct);

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("Stripe — conferir a compra {1}")]
    public Task ConferirCompraAsync(Guid escolinhaId, Guid compraId, CancellationToken ct) =>
        processador.ConferirCompraPorJobAsync(escolinhaId, compraId, ct);

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [JobDisplayName("Stripe — varredura das compras pendentes")]
    public async Task VarrerComprasPendentesAsync(CancellationToken ct) => await processador.VarrerPendentesAsync(ct);

    /// <summary>Varredura a cada 20 minutos, no fuso de Brasília. Com o Stripe desligado, o recorrente sai.</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes, OpcoesStripe opcoes)
    {
        if (!opcoes.Habilitado)
        {
            recorrentes.RemoveIfExists(IdVarredura);
            return;
        }
        recorrentes.AddOrUpdate<JobsPlataforma>(IdVarredura, j => j.VarrerComprasPendentesAsync(CancellationToken.None),
            "*/20 * * * *", new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
    }
}

/// <summary><see cref="IAgendadorPlataforma"/> sobre o Hangfire.</summary>
public class AgendadorPlataformaHangfire(IBackgroundJobClient jobs) : IAgendadorPlataforma
{
    public void ProcessarWebhookStripe(Guid webhookId) =>
        jobs.Enqueue<JobsPlataforma>(j => j.ProcessarWebhookStripeAsync(webhookId, CancellationToken.None));

    public void ConferirCompraCreditos(Guid escolinhaId, Guid compraId) =>
        jobs.Enqueue<JobsPlataforma>(j => j.ConferirCompraAsync(escolinhaId, compraId, CancellationToken.None));
}
