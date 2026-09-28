using System.Net.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using MinhaEscolinha.Application.Integracao.Asaas;

namespace MinhaEscolinha.Infrastructure.Integracao.Asaas;

/// <summary>
/// Trava de ambiente da Asaas, no padrão do DP: roda no startup e torna o erro impossível (spec §2.3 — cobrança real em
/// produção só com aprovação humana). Em Production não existe simulador; o modo Api em Production exige a confirmação
/// explícita. Fora de Production, a conta de Produção já é recusada ao vincular (S0): lá só a Sandbox cobra.
/// </summary>
public static class GuardaAsaas
{
    /// <exception cref="InvalidOperationException">Combinação proibida: o site não sobe.</exception>
    public static void Validar(string ambiente, OpcoesAsaas opcoes, Action<string>? aviso = null)
    {
        if (!opcoes.Habilitado) return;
        var producao = string.Equals(ambiente, "Production", StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(opcoes.UrlPublica, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttps && !url.IsLoopback))
            throw new InvalidOperationException($"Asaas:UrlPublica inválida ({opcoes.UrlPublica}): use o endereço https do sistema.");

        if (opcoes.Simulado)
        {
            if (producao)
            {
                opcoes.Habilitado = false;
                aviso?.Invoke("Asaas:Modo = Simulado em Production: nenhuma mensalidade é emitida até a Asaas ser configurada.");
            }
            return;
        }

        if (!opcoes.Api) throw new InvalidOperationException($"Asaas:Modo desconhecido: {opcoes.Modo} (use Simulado ou Api).");
        if (producao && !opcoes.ConfirmarProducao)
            throw new InvalidOperationException("Asaas:ConfirmarProducao precisa ser true para emitir mensalidades de verdade em produção.");
        if (!producao) aviso?.Invoke($"Asaas pela API em {ambiente}: só contas Sandbox emitem (a de produção é recusada ao vincular).");
    }
}

public static class IntegracaoAsaas
{
    /// <summary>
    /// Lê a seção <c>Asaas</c>, passa pela trava de ambiente e registra o gateway do modo: o simulador (Simulado) ou o
    /// cliente HTTP com resiliência (Api). As opções ficam como singleton (a trava pode desligar a integração).
    /// </summary>
    public static IServiceCollection AddAsaas(this IServiceCollection services, IConfiguration configuracao, string ambiente,
        Action<string>? aviso = null)
    {
        var opcoes = configuracao.GetSection(OpcoesAsaas.Secao).Get<OpcoesAsaas>() ?? new OpcoesAsaas();
        GuardaAsaas.Validar(ambiente, opcoes, aviso);
        services.AddSingleton(opcoes);
        services.AddSingleton(new ArmazemSimuladorAsaas());

        if (opcoes.Simulado)
        {
            services.AddScoped<IGatewayAsaas, SimuladorAsaas>();
            services.AddHttpClient(SimuladorAsaas.ClienteWebhook)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // O simulador só fala com o próprio sistema, em desenvolvimento, onde o certificado pode não ser confiável.
                    ServerCertificateCustomValidationCallback = (mensagem, _, _, erros) =>
                        erros == SslPolicyErrors.None || mensagem.RequestUri is { IsLoopback: true }
                });
            return services;
        }

        services.AddHttpClient<IGatewayAsaas, AsaasClienteHttp>(http => http.Timeout = Timeout.InfiniteTimeSpan)
            .AddStandardResilienceHandler(resiliencia =>
            {
                resiliencia.AttemptTimeout.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
                resiliencia.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos * 3);
                resiliencia.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, opcoes.TimeoutSegundos * 2 + 1));
                // Criar cobrança não é idempotente na Asaas: quem repete é o job, depois de procurar pela referência.
                resiliencia.Retry.DisableForUnsafeHttpMethods();
            });
        return services;
    }
}
