using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MinhaEscolinha.Application;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Infrastructure.Acesso;
using MinhaEscolinha.Infrastructure.Comum;
using MinhaEscolinha.Infrastructure.Configuracao;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Data.Interceptadores;
using MinhaEscolinha.Infrastructure.Plataforma;
using MinhaEscolinha.Infrastructure.Seguranca;
using MinhaEscolinha.Infrastructure.Tempo;

namespace MinhaEscolinha.Testes.Infraestrutura;

/// <summary>
/// Banco SQL Server só para testes (<c>escolinhaesporte_Testes</c>), recriado com as migrations no início da execução.
/// Conexão: variável de ambiente <c>ESC_TESTES_SQL</c> ou o SQL Express local com autenticação do Windows.
/// Trava: o nome do banco precisa terminar em "_Testes" — o fixture apaga dados e nunca pode apontar para o banco real.
/// Portado do AlternativaDepartamentoPessoal.
/// </summary>
public sealed class BancoDeTestes : IAsyncLifetime
{
    public const string Colecao = "Banco de testes (SQL Server)";

    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("ESC_TESTES_SQL")
        ?? @"Server=VINICIUSTARDEM\SQLEXPRESS;Database=escolinhaesporte_Testes;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Application Name=MinhaEscolinha.Testes";

    private static readonly Lazy<string?> MotivoIndisponivel = new(() =>
    {
        try
        {
            var cs = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master", ConnectTimeout = 5 };
            using var conexao = new SqlConnection(cs.ConnectionString);
            conexao.Open();
            return null;
        }
        catch (Exception ex)
        {
            return $"SQL Server de testes indisponível ({ex.Message}). Defina ESC_TESTES_SQL para rodar os testes com banco.";
        }
    });

    public static string? Indisponivel => MotivoIndisponivel.Value;

    public async Task InitializeAsync()
    {
        if (Indisponivel is not null) return;
        GarantirBancoDeTeste();

        await using var contexto = NovoContextoSemEscolinha();
        await contexto.Database.EnsureDeletedAsync();
        await contexto.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Apaga os dados (entre um teste e outro). Só no banco de teste.</summary>
    public async Task LimparAsync()
    {
        GarantirBancoDeTeste();
        await using var contexto = NovoContextoSemEscolinha();
        await contexto.Database.ExecuteSqlRawAsync("""
            DELETE FROM WebhookRecebido;
            DELETE FROM CompraCreditos;
            DELETE FROM PacoteCreditos;
            DELETE FROM RegistroAlteracao;
            DELETE FROM MovimentoCredito;
            DELETE FROM ContaCreditos;
            DELETE FROM ContaAsaasEscolinha;
            DELETE FROM ParametroEscolinha;
            DELETE FROM Modalidade;
            DELETE FROM VinculoAtuacao;
            DELETE FROM Escolinha;
            DELETE FROM Credencial;
            DELETE FROM Pessoa;
            """);
    }

    public static EscolinhaDbContext NovoContextoSemEscolinha()
    {
        var opcoes = new DbContextOptionsBuilder<EscolinhaDbContext>();
        MinhaEscolinha.Infrastructure.DependencyInjection.ConfigurarBanco(opcoes, ConnectionString);
        return new EscolinhaDbContext(opcoes.Options);
    }

    private static void GarantirBancoDeTeste()
    {
        var banco = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if (!banco.EndsWith("_Testes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Recusado: \"{banco}\" não é um banco de teste (o nome precisa terminar em _Testes).");
    }
}

[CollectionDefinition(BancoDeTestes.Colecao)]
public class ColecaoBancoDeTestes : ICollectionFixture<BancoDeTestes>;

/// <summary>Fato que só roda com o SQL Server de testes disponível (senão aparece como ignorado, com o motivo).</summary>
public sealed class FatoComBancoAttribute : FactAttribute
{
    public FatoComBancoAttribute()
    {
        if (BancoDeTestes.Indisponivel is { } motivo) Skip = motivo;
    }
}

/// <summary>Quem está "logado" (Id nulo = job ou seed).</summary>
public sealed class UsuarioDeTeste : IUsuarioAtual
{
    public Guid? Id { get; set; }
    public string? Nome { get; set; } = "Teste automatizado";
    public string? Login { get; set; } = "teste@teste.com";
    public string? Ip => "127.0.0.1";
}

public sealed class AmbienteDeTeste : IAmbienteExecucao
{
    public string Nome { get; set; } = "Development";
    public bool EhProducao => Nome == "Production";
}

/// <summary>
/// Monta a aplicação "de verdade" (serviços + EF + SQL Server de testes) com relógio falso e usuário controlado.
/// Cada ação roda num escopo novo, como uma requisição — com o contexto de escolinha que o teste pedir.
/// </summary>
public sealed class AmbienteAplicacao : IDisposable
{
    public FakeTimeProvider Tempo { get; } = new(new DateTimeOffset(2026, 9, 26, 15, 0, 0, TimeSpan.Zero)); // 12:00 em Brasília
    public UsuarioDeTeste Usuario { get; } = new();
    public AmbienteDeTeste Ambiente { get; } = new();

    /// <summary>Stripe em modo Teste, com segredo de webhook: o teste assina os eventos com <see cref="OpcoesStripe.SegredoWebhook"/>.</summary>
    public OpcoesStripe Stripe { get; } = new()
    {
        Habilitado = true,
        Modo = OpcoesStripe.ModoTeste,
        ChaveSecreta = "sk_test_testes",
        SegredoWebhook = "whsec_testes",
        UrlPublica = "https://localhost:7186"
    };

    public GatewayStripeFalso GatewayStripe { get; } = new();
    public AgendadorPlataformaDeTeste Agendador { get; } = new();

    private readonly ServiceProvider _servicos;

    public AmbienteAplicacao()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<TimeProvider>(Tempo);
        services.AddSingleton<IRelogioBrasil, RelogioBrasil>();
        services.AddSingleton<IUsuarioAtual>(Usuario);
        services.AddSingleton<IAmbienteExecucao>(Ambiente);
        services.AddSingleton<IHashSenha, HashSenhaBCrypt>();
        services.AddDataProtection().SetApplicationName("MinhaEscolinha.Testes").UseEphemeralDataProtectionProvider();
        services.AddSingleton<IProtetorSegredos, ProtetorSegredosDataProtection>();

        services.AddAplicacao();

        services.AddScoped<AuditoriaInterceptor>();
        services.AddScoped<EscolinhaInterceptor>();
        services.AddDbContext<EscolinhaDbContext>((sp, o) => MinhaEscolinha.Infrastructure.DependencyInjection
            .ConfigurarBanco(o, BancoDeTestes.ConnectionString)
            .AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>(), sp.GetRequiredService<EscolinhaInterceptor>()));
        services.AddScoped<IExecutorEscolinha, ExecutorEscolinha>();
        services.AddScoped<IAcessoRepositorio, AcessoRepositorio>();
        services.AddScoped<MinhaEscolinha.Application.Configuracao.IConfiguracaoRepositorio, ConfiguracaoRepositorio>();
        services.AddScoped<MinhaEscolinha.Application.Configuracao.IConsultasPlataforma, ConsultasPlataforma>();
        services.AddScoped<IPlataformaRepositorio, PlataformaRepositorio>();
        services.AddSingleton(Stripe);
        services.AddSingleton<IGatewayStripe>(GatewayStripe);
        services.AddSingleton<IAgendadorPlataforma>(Agendador);
        _servicos = services.BuildServiceProvider();
    }

    /// <summary>Roda a ação num escopo novo como a equipe da plataforma (sem escolinha no contexto).</summary>
    public Task<T> ComoPlataformaAsync<T>(Func<IServiceProvider, Task<T>> acao) =>
        NoEscopoAsync(c => c.DefinirSemEscolinha(administradorPlataforma: true), acao);

    /// <summary>Roda a ação num escopo novo na escolinha indicada, com os papéis indicados.</summary>
    public Task<T> NaEscolinhaAsync<T>(Guid escolinhaId, IEnumerable<Domain.Acesso.PapelAtuacao> papeis, Func<IServiceProvider, Task<T>> acao) =>
        NoEscopoAsync(c => c.Definir(escolinhaId, papeis), acao);

    /// <summary>Roda a ação num escopo novo sem contexto nenhum (ex.: login, job mal escrito).</summary>
    public Task<T> SemContextoAsync<T>(Func<IServiceProvider, Task<T>> acao) => NoEscopoAsync(_ => { }, acao);

    private async Task<T> NoEscopoAsync<T>(Action<IContextoEscolinha> definir, Func<IServiceProvider, Task<T>> acao)
    {
        await using var escopo = _servicos.CreateAsyncScope();
        definir(escopo.ServiceProvider.GetRequiredService<IContextoEscolinha>());
        return await acao(escopo.ServiceProvider);
    }

    public void Dispose() => _servicos.Dispose();
}
