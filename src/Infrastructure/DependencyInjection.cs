using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Application.Portal;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Infrastructure.Acesso;
using MinhaEscolinha.Infrastructure.Cadastro;
using MinhaEscolinha.Infrastructure.Carga;
using MinhaEscolinha.Infrastructure.Comum;
using MinhaEscolinha.Infrastructure.Configuracao;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Data.Interceptadores;
using MinhaEscolinha.Infrastructure.Eventos;
using MinhaEscolinha.Infrastructure.Financeiro;
using MinhaEscolinha.Infrastructure.Hangfire;
using MinhaEscolinha.Infrastructure.Integracao;
using MinhaEscolinha.Infrastructure.Integracao.Asaas;
using MinhaEscolinha.Infrastructure.Integracao.Stripe;
using MinhaEscolinha.Infrastructure.Integracao.WhatsApp;
using MinhaEscolinha.Infrastructure.Mensageria;
using MinhaEscolinha.Infrastructure.Plataforma;
using MinhaEscolinha.Infrastructure.Portal;
using MinhaEscolinha.Infrastructure.Presencas;
using MinhaEscolinha.Infrastructure.Seguranca;
using MinhaEscolinha.Infrastructure.Tempo;

namespace MinhaEscolinha.Infrastructure;

public static class DependencyInjection
{
    public const string ConexaoPadrao = "ConexaoPadrao";

    /// <summary>SQL Server 2019 (Express no desenvolvimento): o EF gera SQL compatível com o nível 150.</summary>
    public const int NivelCompatibilidadeSql = 150;

    /// <param name="ambiente">Nome do ambiente (Development, Homologacao, Production): as travas de integração dependem dele.</param>
    /// <param name="aviso">Para onde vão os avisos do startup (o log ainda é o de bootstrap).</param>
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuracao, string ambiente,
        Action<string>? aviso = null)
    {
        var connectionString = configuracao.GetConnectionString(ConexaoPadrao);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConexaoPadrao} não configurada. Em dev: dotnet user-secrets set \"ConnectionStrings:{ConexaoPadrao}\" \"...\" --project src/Web");

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRelogioBrasil, RelogioBrasil>();
        services.AddSingleton<IHashSenha, HashSenhaBCrypt>();
        services.AddSingleton<IProtetorSegredos, ProtetorSegredosDataProtection>();

        services.AddScoped<AuditoriaInterceptor>();
        services.AddScoped<EscolinhaInterceptor>();
        services.AddScoped<DespachoMensagensInterceptor>();
        services.AddScoped<TempoRealConvocacoesInterceptor>();
        // A ordem importa: o despacho do outbox e o tempo real leem a escolinha que o interceptor de escolinha carimbou.
        services.AddDbContext<EscolinhaDbContext>((sp, opcoes) => ConfigurarBanco(opcoes, connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>(), sp.GetRequiredService<EscolinhaInterceptor>(),
                sp.GetRequiredService<DespachoMensagensInterceptor>(), sp.GetRequiredService<TempoRealConvocacoesInterceptor>()));

        services.AddScoped<IExecutorEscolinha, ExecutorEscolinha>();
        services.AddScoped<IAcessoRepositorio, AcessoRepositorio>();
        services.AddScoped<IConfiguracaoRepositorio, ConfiguracaoRepositorio>();
        services.AddScoped<IConsultasPlataforma, ConsultasPlataforma>();

        // S1 — cadastro
        services.AddScoped<IEquipeRepositorio, EquipeRepositorio>();
        services.AddScoped<ITurmasRepositorio, TurmasRepositorio>();
        services.AddScoped<IAlunosRepositorio, AlunosRepositorio>();
        services.AddScoped<IConsultasEscopo, ConsultasEscopo>();
        services.AddScoped<JobsTurmas>();

        // S2 — motor único de mensagens, WhatsApp (Cloud API ou simulador), convites
        services.AddScoped<IMensageriaRepositorio, MensageriaRepositorio>();
        services.AddScoped<IConvitesRepositorio, ConvitesRepositorio>();
        services.AddScoped<IAgendadorMensageria, AgendadorMensageriaHangfire>();
        services.AddScoped<JobsMensageria>();
        services.AddWhatsApp(configuracao, ambiente, aviso);

        // S3 — presença e alerta de faltas
        services.AddScoped<IPresencaRepositorio, PresencaRepositorio>();

        // S4 — mensalidades na Asaas da escolinha (API ou simulador)
        services.AddScoped<IFinanceiroRepositorio, FinanceiroRepositorio>();
        services.AddScoped<IAgendadorFinanceiro, AgendadorFinanceiroHangfire>();
        services.AddScoped<JobsFinanceiro>();
        services.AddAsaas(configuracao, ambiente, aviso);

        // S5 — eventos e convocações; S6 — portal do responsável
        services.AddScoped<IEventosRepositorio, EventosRepositorio>();
        services.AddScoped<IPortalRepositorio, PortalRepositorio>();
        services.AddScoped<JobsConvocacoes>();

        // S7 — carga inicial por planilha
        services.AddScoped<ICargaRepositorio, CargaRepositorio>();
        services.AddSingleton<IPlanilhas, PlanilhasClosedXml>();
        services.AddScoped<JobsCarga>();

        // A guarda do corpo bruto dos webhooks (P22)
        services.AddScoped<JobsIntegracao>();

        // Plataforma: pacotes de créditos vendidos pelo Stripe da LDNA
        services.AddScoped<IPlataformaRepositorio, PlataformaRepositorio>();
        services.AddScoped<IAgendadorPlataforma, AgendadorPlataformaHangfire>();
        services.AddScoped<JobsPlataforma>();
        services.AddStripe(configuracao, ambiente, aviso);

        services.AddHangfireEscolinha(configuracao, connectionString);

        return services;
    }

    /// <summary>Opções do EF usadas pelo app e pelos testes (mesmo SQL gerado nos dois).</summary>
    public static DbContextOptionsBuilder ConfigurarBanco(DbContextOptionsBuilder opcoes, string connectionString) =>
        opcoes
            .UseSqlServer(connectionString, sql => sql
                .MigrationsAssembly(typeof(EscolinhaDbContext).Assembly.GetName().Name)
                .UseCompatibilityLevel(NivelCompatibilidadeSql))
            // Filtro de escolinha dos dois lados de toda relação (a escolinha e o dado dela têm o mesmo filtro):
            // o aviso de "principal filtrado" não se aplica.
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
}
