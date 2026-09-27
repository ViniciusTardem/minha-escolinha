using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MinhaEscolinha.Infrastructure.Hangfire;

public static class HangfireEscolinhaExtensions
{
    /// <summary>Tudo que não tem fila própria (geração de sessões, reconciliações).</summary>
    public const string FilaPadrao = "esc_padrao";

    /// <summary>Motor de mensagens: montagem, elegibilidade e despacho (fatia S2).</summary>
    public const string FilaMensagens = "esc_mensagens";

    /// <summary>Processamento dos webhooks gravados (Asaas e WhatsApp): o request só grava o bruto e responde 200.</summary>
    public const string FilaWebhooks = "esc_webhooks";

    /// <summary>Chamadas para fora (Asaas, WhatsApp) — servidor próprio: um parceiro fora do ar não atrasa o resto.</summary>
    public const string FilaIntegracoes = "esc_integracoes";

    public static readonly string[] Filas = [FilaMensagens, FilaWebhooks, FilaPadrao];

    /// <summary>
    /// Hangfire com storage no banco da Minha Escolinha (schema <c>HangFire</c> padrão). O servidor de jobs roda no
    /// próprio processo do site quando <c>Hangfire:ServidorNoProcesso</c> = true (padrão). Portado do AlternativaDepartamentoPessoal.
    /// </summary>
    public static IServiceCollection AddHangfireEscolinha(this IServiceCollection services, IConfiguration configuracao, string connectionString)
    {
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            // Job sem [Queue] cairia na fila "default", que nenhum servidor daqui escuta: vai para esc_padrao.
            // [Queue] no método tem precedência sobre este filtro global.
            .UseFilter(new QueueAttribute(FilaPadrao))
            .UseSqlServerStorage(() => new SqlConnection(connectionString), new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        if (configuracao.GetValue("Hangfire:ServidorNoProcesso", true))
        {
            services.AddHangfireServer(opcoes =>
            {
                opcoes.ServerName = $"{Environment.MachineName}:esc";
                opcoes.WorkerCount = configuracao.GetValue("Hangfire:WorkerCount", 2);
                opcoes.Queues = Filas;
            });

            services.AddHangfireServer(opcoes =>
            {
                opcoes.ServerName = $"{Environment.MachineName}:esc-integracoes";
                opcoes.WorkerCount = configuracao.GetValue("Hangfire:WorkerCountIntegracoes", 2);
                opcoes.Queues = [FilaIntegracoes];
            });
        }

        return services;
    }
}
