using System.Net.Http.Headers;
using System.Net.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using MinhaEscolinha.Application.Mensageria;

namespace MinhaEscolinha.Infrastructure.Integracao.WhatsApp;

/// <summary>
/// Trava de ambiente do WhatsApp, no padrão do DP: roda no startup e torna o erro impossível (spec §2.3 — WhatsApp real só
/// para número de teste sem aprovação). Em Production não existe simulador; fora de Production, a API oficial só manda
/// para os <see cref="OpcoesWhatsApp.NumerosTeste"/>.
/// </summary>
public static class GuardaWhatsApp
{
    /// <exception cref="InvalidOperationException">Combinação proibida: o site não sobe.</exception>
    public static void Validar(string ambiente, OpcoesWhatsApp opcoes, Action<string>? aviso = null)
    {
        if (!opcoes.Habilitado) return;
        var producao = string.Equals(ambiente, "Production", StringComparison.OrdinalIgnoreCase);

        if (!Uri.TryCreate(opcoes.UrlPublica, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttps && !url.IsLoopback))
            throw new InvalidOperationException($"WhatsApp:UrlPublica inválida ({opcoes.UrlPublica}): use o endereço https do sistema.");

        if (opcoes.Simulado)
        {
            if (producao)
            {
                opcoes.Habilitado = false;
                aviso?.Invoke("WhatsApp:Modo = Simulado em Production: o WhatsApp fica desligado (essenciais retidas) até a API oficial ser configurada.");
            }
            return;
        }

        if (!opcoes.Api) throw new InvalidOperationException($"WhatsApp:Modo desconhecido: {opcoes.Modo} (use Simulado ou Api).");

        foreach (var (valor, chave) in new[]
                 {
                     (opcoes.IdNumero, "IdNumero"), (opcoes.TokenAcesso, "TokenAcesso"), (opcoes.SegredoApp, "SegredoApp"),
                     (opcoes.TokenVerificacao, "TokenVerificacao")
                 })
            if (string.IsNullOrWhiteSpace(valor))
                throw new InvalidOperationException($"WhatsApp:{chave} é obrigatório no modo Api (user-secrets ou variável de ambiente).");

        if (producao)
        {
            if (!opcoes.ConfirmarProducao)
                throw new InvalidOperationException("WhatsApp:ConfirmarProducao precisa ser true para mandar mensagem às famílias em produção.");
            return;
        }

        if (opcoes.NumerosTeste.Length == 0)
            throw new InvalidOperationException(
                $"WhatsApp:Modo = Api no ambiente {ambiente}: informe WhatsApp:NumerosTeste (fora de produção, só números de teste recebem).");
        aviso?.Invoke($"WhatsApp pela API oficial em {ambiente}: só {opcoes.NumerosTeste.Length} número(s) de teste recebem.");
    }
}

public static class IntegracaoWhatsApp
{
    /// <summary>
    /// Lê a seção <c>WhatsApp</c>, passa pela trava de ambiente e registra o gateway do modo: o simulador (Simulado) ou o
    /// cliente HTTP da Cloud API com resiliência (Api). As opções ficam como singleton (a trava pode desligar o canal).
    /// </summary>
    public static IServiceCollection AddWhatsApp(this IServiceCollection services, IConfiguration configuracao, string ambiente,
        Action<string>? aviso = null)
    {
        var opcoes = configuracao.GetSection(OpcoesWhatsApp.Secao).Get<OpcoesWhatsApp>() ?? new OpcoesWhatsApp();
        GuardaWhatsApp.Validar(ambiente, opcoes, aviso);
        services.AddSingleton(opcoes);
        services.AddSingleton(new ArmazemSimuladorWhatsApp());

        if (opcoes.Simulado)
        {
            services.AddScoped<IGatewayWhatsApp, SimuladorWhatsApp>();
            services.AddHttpClient(SimuladorWhatsApp.ClienteWebhook)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // O simulador só fala com o próprio sistema, em desenvolvimento, onde o certificado pode não ser confiável.
                    ServerCertificateCustomValidationCallback = (mensagem, _, _, erros) =>
                        erros == SslPolicyErrors.None || mensagem.RequestUri is { IsLoopback: true }
                });
            return services;
        }

        services.AddHttpClient<IGatewayWhatsApp, WhatsAppClienteHttp>(http =>
            {
                http.BaseAddress = new Uri(WhatsAppClienteHttp.UrlApi);
                http.Timeout = Timeout.InfiniteTimeSpan;
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opcoes.TokenAcesso);
            })
            .AddStandardResilienceHandler(resiliencia =>
            {
                resiliencia.AttemptTimeout.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos);
                resiliencia.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSegundos * 2);
                resiliencia.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(30, opcoes.TimeoutSegundos * 2 + 1));
                // O POST de mensagem não tem chave de idempotência na Meta: repetir aqui poderia mandar duas vezes.
                resiliencia.Retry.DisableForUnsafeHttpMethods();
            });
        return services;
    }
}
