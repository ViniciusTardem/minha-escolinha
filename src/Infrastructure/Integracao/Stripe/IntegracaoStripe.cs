using System.Net.Http.Headers;
using System.Net.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Integracao.Stripe;

namespace MinhaEscolinha.Infrastructure.Integracao.Stripe;

public static class IntegracaoStripe
{
    /// <summary>
    /// Lê a seção <c>Stripe</c>, passa pela trava de ambiente e registra o gateway do modo: o simulador (Simulado) ou o
    /// cliente HTTP com resiliência (Teste e Producao). As opções ficam como singleton (a trava pode desligar o módulo).
    /// </summary>
    public static IServiceCollection AddStripe(this IServiceCollection services, IConfiguration configuracao, string ambiente,
        Action<string>? aviso = null)
    {
        var opcoes = configuracao.GetSection(OpcoesStripe.Secao).Get<OpcoesStripe>() ?? new OpcoesStripe();
        GuardaStripe.Validar(ambiente, opcoes, aviso);
        services.AddSingleton(opcoes);
        services.AddSingleton(new ArmazemSimuladorStripe());

        if (opcoes.Simulado)
        {
            services.AddScoped<IGatewayStripe, SimuladorStripe>();
            services.AddHttpClient(SimuladorStripe.ClienteWebhook)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // O simulador só fala com o próprio sistema, em desenvolvimento, onde o certificado pode não ser confiável.
                    ServerCertificateCustomValidationCallback = (mensagem, _, _, erros) =>
                        erros == SslPolicyErrors.None || mensagem.RequestUri is { IsLoopback: true }
                });
            return services;
        }

        services.AddHttpClient<IGatewayStripe, StripeClienteHttp>(http =>
            {
                http.BaseAddress = new Uri(StripeClienteHttp.UrlApi);
                // Quem manda no tempo é a resiliência (tempo por tentativa e total).
                http.Timeout = Timeout.InfiniteTimeSpan;
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opcoes.ChaveSecreta);
                if (!string.IsNullOrWhiteSpace(opcoes.VersaoApi)) http.DefaultRequestHeaders.Add("Stripe-Version", opcoes.VersaoApi);
            })
            .AddStandardResilienceHandler(resiliencia =>
            {
                resiliencia.AttemptTimeout.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
                resiliencia.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos * 3);
                resiliencia.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, opcoes.TimeoutSegundos * 2 + 1));
                resiliencia.Retry.MaxRetryAttempts = 2;
            });
        return services;
    }
}
