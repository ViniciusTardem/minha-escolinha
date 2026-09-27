using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Mensageria;

/// <summary>
/// Os jobs do motor único: o envio de cada mensagem (na escolinha dela), o processamento do webhook do WhatsApp e a
/// varredura de 5 em 5 minutos, que recolhe o que ficou para trás e libera as retidas quando o saldo volta.
/// </summary>
public class JobsMensageria(
    IConsultasPlataforma consultas,
    IExecutorEscolinha executor,
    ProcessadorWebhooksWhatsApp processador,
    ILogger<JobsMensageria> logger)
{
    public const string IdVarredura = "mensagens-varredura";
    public const int TentativasNoConflito = 3;

    [Queue(HangfireEscolinhaExtensions.FilaMensagens)]
    [AutomaticRetry(Attempts = 5)]
    [JobDisplayName("Mensagem — enviar {1}")]
    public async Task EnviarAsync(Guid escolinhaId, Guid mensagemId, CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<ServicoEnvioMensagens>().EnviarAsync(mensagemId, ct), ct);
                return;
            }
            catch (ConflitoDeConcorrenciaException) when (tentativa < TentativasNoConflito)
            {
                // Outra gravação passou na frente (o crédito de outra mensagem, ou outro trabalho com esta): relê num escopo novo.
            }
            catch (ConflitoDeConcorrenciaException)
            {
                logger.LogInformation("Mensagem {Mensagem} disputada por outro trabalho: a varredura retoma se ela ficou na fila.", mensagemId);
                return;
            }
        }
    }

    [Queue(HangfireEscolinhaExtensions.FilaWebhooks)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("WhatsApp — processar o webhook {0}")]
    public Task ProcessarWebhookWhatsAppAsync(Guid webhookId, CancellationToken ct) => processador.ProcessarAsync(webhookId, ct);

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [JobDisplayName("Mensagens — varredura da fila")]
    public async Task VarrerAsync(CancellationToken ct)
    {
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            try
            {
                await executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<VarreduraMensagens>().VarrerAsync(ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na varredura de mensagens da escolinha {Escolinha}.", escolinhaId);
            }
        }
    }

    /// <summary>A cada 5 minutos, no fuso de Brasília.</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes) =>
        recorrentes.AddOrUpdate<JobsMensageria>(IdVarredura, j => j.VarrerAsync(CancellationToken.None), "*/5 * * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
}

/// <summary><see cref="IAgendadorMensageria"/> sobre o Hangfire.</summary>
public class AgendadorMensageriaHangfire(IBackgroundJobClient jobs) : IAgendadorMensageria
{
    public void Enviar(Guid escolinhaId, Guid mensagemId, DateTime? quando = null)
    {
        if (quando is { } horario)
            jobs.Schedule<JobsMensageria>(j => j.EnviarAsync(escolinhaId, mensagemId, CancellationToken.None),
                new DateTimeOffset(DataHoraBrasil.ConverterParaUtc(horario), TimeSpan.Zero));
        else
            jobs.Enqueue<JobsMensageria>(j => j.EnviarAsync(escolinhaId, mensagemId, CancellationToken.None));
    }

    public void ProcessarWebhookWhatsApp(Guid webhookId) =>
        jobs.Enqueue<JobsMensageria>(j => j.ProcessarWebhookWhatsAppAsync(webhookId, CancellationToken.None));
}

/// <summary>
/// O despacho do outbox: depois que a gravação da mudança de negócio (com as mensagens na fila) confirma, cada mensagem
/// de WhatsApp nova vira um job de envio. Se o processo cair entre a gravação e o agendamento, a varredura recolhe.
/// </summary>
public class DespachoMensagensInterceptor(IAgendadorMensageria agendador) : SaveChangesInterceptor
{
    private readonly List<(Guid Escolinha, Guid Mensagem)> _novas = [];

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
        Despachar();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Despachar();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _novas.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _novas.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Coletar(DbContext? contexto)
    {
        if (contexto is null) return;
        // Chamado depois do interceptor de escolinha (a ordem de registro): a escolinha já está carimbada.
        foreach (var entrada in contexto.ChangeTracker.Entries<Mensagem>())
            if (entrada.State == EntityState.Added && entrada.Entity is { Canal: CanalMensagem.WhatsApp, Situacao: SituacaoMensagem.Pendente })
                _novas.Add((entrada.Entity.Escolinha_ID, entrada.Entity.Mensagem_ID));
    }

    private void Despachar()
    {
        foreach (var (escolinha, mensagem) in _novas) agendador.Enviar(escolinha, mensagem);
        _novas.Clear();
    }
}
