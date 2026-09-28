using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Infrastructure.Hangfire;

namespace MinhaEscolinha.Infrastructure.Financeiro;

/// <summary>
/// Os jobs das mensalidades (S4): toda conversa com a Asaas sai daqui, nunca do request (regra de ouro 2).
/// <list type="bullet">
/// <item>a rotina diária de cada escolinha, às 06:30: projeção, vencidas, régua e emissões;</item>
/// <item>a varredura de hora em hora, que confere na Asaas o que o webhook não trouxe;</item>
/// <item>por cobrança: emitir (uma de cada vez: duas emissões da mesma cobrança nunca correm juntas), conferir, excluir e
/// registrar o recebimento por fora; por escolinha: refazer a projeção e cadastrar o webhook.</item>
/// </list>
/// </summary>
public class JobsFinanceiro(
    IConsultasPlataforma consultas,
    IExecutorEscolinha executor,
    ProcessadorWebhooksAsaas processador,
    OpcoesAsaas opcoes,
    ILogger<JobsFinanceiro> logger)
{
    public const string IdRotina = "mensalidades-rotina";
    public const string IdVarredura = "mensalidades-varredura";

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 900)]
    [JobDisplayName("Mensalidades — rotina diária das escolinhas")]
    public async Task RotinaAsync(CancellationToken ct)
    {
        var falhas = 0;
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            try
            {
                await executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<RotinaCobrancas>().ExecutarAsync(ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                logger.LogError(ex, "Falha na rotina das mensalidades da escolinha {Escolinha}.", escolinhaId);
            }
        }
        // As outras escolinhas já rodaram; a retentativa refaz só o que falta (tudo é idempotente).
        if (falhas > 0) throw new InvalidOperationException($"A rotina das mensalidades falhou em {falhas} escolinha(s): veja o log.");
    }

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [JobDisplayName("Mensalidades — varredura na Asaas")]
    public async Task VarrerAsync(CancellationToken ct)
    {
        if (!opcoes.Habilitado) return;
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            try
            {
                await executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<VarreduraCobrancas>().VarrerAsync(ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Falha na varredura das mensalidades da escolinha {Escolinha}.", escolinhaId);
            }
        }
    }

    [Queue(HangfireEscolinhaExtensions.FilaPadrao)]
    [AutomaticRetry(Attempts = 3)]
    [JobDisplayName("Mensalidades — refazer a projeção da escolinha {0}")]
    public Task ProjetarAsync(Guid escolinhaId, CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId, async sp =>
        {
            var rotina = sp.GetRequiredService<RotinaCobrancas>();
            await rotina.ProjetarAsync(ct);
            return await rotina.AgendarEmissoesAsync(ct);
        }, ct);

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 8)]
    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    [JobDisplayName("Mensalidade — emitir {1} na Asaas")]
    public Task EmitirAsync(Guid escolinhaId, Guid cobrancaId, CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<EmissaoCobrancas>().EmitirAsync(cobrancaId, ct), ct);

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 5)]
    [JobDisplayName("Mensalidade — conferir {1} na Asaas")]
    public Task ConferirAsync(Guid escolinhaId, Guid cobrancaId, CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<VarreduraCobrancas>().ConferirAsync(cobrancaId, ct), ct);

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("Mensalidade — excluir {1} na Asaas")]
    public Task ExcluirNaAsaasAsync(Guid escolinhaId, Guid cobrancaId, CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<OperacoesAsaas>().ExcluirAsync(cobrancaId, ct), ct);

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("Mensalidade — registrar {1} como recebida na Asaas")]
    public Task ReceberEmDinheiroNaAsaasAsync(Guid escolinhaId, Guid cobrancaId, CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<OperacoesAsaas>().ReceberEmDinheiroAsync(cobrancaId, ct), ct);

    [Queue(HangfireEscolinhaExtensions.FilaIntegracoes)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("Asaas — cadastrar o webhook da escolinha {0}")]
    public Task RegistrarWebhookAsync(Guid escolinhaId, CancellationToken ct) =>
        executor.ExecutarAsync(escolinhaId, sp => sp.GetRequiredService<OperacoesAsaas>().RegistrarWebhookAsync(ct), ct);

    [Queue(HangfireEscolinhaExtensions.FilaWebhooks)]
    [AutomaticRetry(Attempts = 8)]
    [JobDisplayName("Asaas — processar o webhook {0}")]
    public Task ProcessarWebhookAsaasAsync(Guid webhookId, CancellationToken ct) => processador.ProcessarAsync(webhookId, ct);

    /// <summary>A rotina às 06:30 (a régua sai depois da janela de silêncio) e a varredura de hora em hora, no fuso de Brasília.</summary>
    public static void RegistrarRecorrentes(IRecurringJobManager recorrentes)
    {
        recorrentes.AddOrUpdate<JobsFinanceiro>(IdRotina, j => j.RotinaAsync(CancellationToken.None), "30 6 * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
        recorrentes.AddOrUpdate<JobsFinanceiro>(IdVarredura, j => j.VarrerAsync(CancellationToken.None), "40 * * * *",
            new RecurringJobOptions { TimeZone = DataHoraBrasil.TimeZone });
    }
}

/// <summary><see cref="IAgendadorFinanceiro"/> sobre o Hangfire.</summary>
public class AgendadorFinanceiroHangfire(IBackgroundJobClient jobs) : IAgendadorFinanceiro
{
    public void Projetar(Guid escolinhaId) => jobs.Enqueue<JobsFinanceiro>(j => j.ProjetarAsync(escolinhaId, CancellationToken.None));
    public void Emitir(Guid escolinhaId, Guid cobrancaId) => jobs.Enqueue<JobsFinanceiro>(j => j.EmitirAsync(escolinhaId, cobrancaId, CancellationToken.None));
    public void Conferir(Guid escolinhaId, Guid cobrancaId) => jobs.Enqueue<JobsFinanceiro>(j => j.ConferirAsync(escolinhaId, cobrancaId, CancellationToken.None));

    public void ExcluirNaAsaas(Guid escolinhaId, Guid cobrancaId) =>
        jobs.Enqueue<JobsFinanceiro>(j => j.ExcluirNaAsaasAsync(escolinhaId, cobrancaId, CancellationToken.None));

    public void ReceberEmDinheiroNaAsaas(Guid escolinhaId, Guid cobrancaId) =>
        jobs.Enqueue<JobsFinanceiro>(j => j.ReceberEmDinheiroNaAsaasAsync(escolinhaId, cobrancaId, CancellationToken.None));

    public void ProcessarWebhookAsaas(Guid webhookId) => jobs.Enqueue<JobsFinanceiro>(j => j.ProcessarWebhookAsaasAsync(webhookId, CancellationToken.None));
    public void RegistrarWebhook(Guid escolinhaId) => jobs.Enqueue<JobsFinanceiro>(j => j.RegistrarWebhookAsync(escolinhaId, CancellationToken.None));
}
