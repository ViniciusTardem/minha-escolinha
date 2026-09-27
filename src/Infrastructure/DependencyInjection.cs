using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Infrastructure.Acesso;
using MinhaEscolinha.Infrastructure.Comum;
using MinhaEscolinha.Infrastructure.Configuracao;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Data.Interceptadores;
using MinhaEscolinha.Infrastructure.Hangfire;
using MinhaEscolinha.Infrastructure.Integracao.Stripe;
using MinhaEscolinha.Infrastructure.Plataforma;
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
        services.AddDbContext<EscolinhaDbContext>((sp, opcoes) => ConfigurarBanco(opcoes, connectionString)
            .AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>(), sp.GetRequiredService<EscolinhaInterceptor>()));

        services.AddScoped<IExecutorEscolinha, ExecutorEscolinha>();
        services.AddScoped<IAcessoRepositorio, AcessoRepositorio>();
        services.AddScoped<IConfiguracaoRepositorio, ConfiguracaoRepositorio>();
        services.AddScoped<IConsultasPlataforma, ConsultasPlataforma>();

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
