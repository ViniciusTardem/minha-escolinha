using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Hangfire saudável = storage acessível e, quando o servidor roda no processo, algum servidor com heartbeat recente.
/// Sem heartbeat, jobs, lembretes e a régua param em silêncio (hospedagem que dorme por ociosidade).
/// Portado do AlternativaDepartamentoPessoal.
/// </summary>
public class HangfireHealthCheck(JobStorage storage, IConfiguration configuracao, TimeProvider relogio) : IHealthCheck
{
    public static readonly TimeSpan HeartbeatMaximo = TimeSpan.FromMinutes(1);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var servidores = storage.GetMonitoringApi().Servers();
            if (!configuracao.GetValue("Hangfire:ServidorNoProcesso", true))
                return Task.FromResult(HealthCheckResult.Healthy("Storage acessível (servidor de jobs fora do processo)."));

            // Heartbeat do Hangfire é gravado em UTC.
            var limite = relogio.GetUtcNow().UtcDateTime - HeartbeatMaximo;
            var vivos = servidores.Count(s => s.Heartbeat >= limite);

            return Task.FromResult(vivos > 0
                ? HealthCheckResult.Healthy($"{vivos} servidor(es) de jobs ativo(s).")
                : HealthCheckResult.Unhealthy("Nenhum servidor de jobs com heartbeat no último minuto."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage do Hangfire inacessível.", ex));
        }
    }
}

public static class Saude
{
    /// <summary>Resposta enxuta do /health (anônimo): só status por verificação, sem detalhes de erro.</summary>
    public static Task EscreverRespostaAsync(HttpContext context, HealthReport relatorio)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var corpo = new
        {
            status = relatorio.Status.ToString(),
            verificacoes = relatorio.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(corpo));
    }
}
