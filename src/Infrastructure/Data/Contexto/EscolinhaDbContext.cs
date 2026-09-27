using System.Reflection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Infrastructure.Data.Contexto;

/// <summary>
/// Banco da Minha Escolinha (<c>escolinhaesporte</c>) — o único banco em que este projeto escreve e o único com migrations.
/// Guarda também as chaves de Data Protection (o cookie de login sobrevive à reciclagem do app).
/// <para>
/// Multi-escolinha: toda entidade <see cref="IEntidadeDaEscolinha"/> (e a própria <see cref="Escolinha"/>) tem o filtro
/// global de escolinha, fechado por padrão — sem escolinha no contexto, nenhuma linha. Ignorar o filtro só nos arquivos
/// da lista permitida (teste de arquitetura em <c>RegrasDeOuroTestes</c>).
/// </para>
/// <para>
/// EF Core 9: cada entidade tem UM filtro, e <c>IgnoreQueryFilters()</c> ignora todos. Se entrar outro filtro (ex.:
/// exclusão lógica), ele precisa ser combinado neste mesmo, na mesma expressão.
/// </para>
/// </summary>
public class EscolinhaDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly IContextoEscolinha? _contexto;

    public EscolinhaDbContext(DbContextOptions<EscolinhaDbContext> options, IContextoEscolinha? contexto = null) : base(options)
    {
        _contexto = contexto;
    }

    /// <summary>Lido a cada consulta (o EF parametriza): a escolinha do contexto no momento da execução.</summary>
    public Guid? EscolinhaAtualId => _contexto?.EscolinhaId;

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Credencial> Credenciais => Set<Credencial>();
    public DbSet<VinculoAtuacao> VinculosAtuacao => Set<VinculoAtuacao>();
    public DbSet<Escolinha> Escolinhas => Set<Escolinha>();
    public DbSet<Modalidade> Modalidades => Set<Modalidade>();
    public DbSet<ParametroEscolinha> Parametros => Set<ParametroEscolinha>();
    public DbSet<ContaAsaasEscolinha> ContasAsaas => Set<ContaAsaasEscolinha>();
    public DbSet<ContaCreditos> ContasCreditos => Set<ContaCreditos>();
    public DbSet<MovimentoCredito> MovimentosCredito => Set<MovimentoCredito>();
    public DbSet<RegistroAlteracao> RegistrosAlteracao => Set<RegistroAlteracao>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EscolinhaDbContext).Assembly);

        modelBuilder.Entity<Escolinha>().HasQueryFilter(e => e.Escolinha_ID == EscolinhaAtualId);

        var aplicar = typeof(EscolinhaDbContext).GetMethod(nameof(AplicarFiltroEscolinha), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var tipo in modelBuilder.Model.GetEntityTypes()
                     .Select(t => t.ClrType)
                     .Where(t => typeof(IEntidadeDaEscolinha).IsAssignableFrom(t))
                     .ToList())
        {
            aplicar.MakeGenericMethod(tipo).Invoke(this, [modelBuilder]);
        }
    }

    private void AplicarFiltroEscolinha<T>(ModelBuilder modelBuilder) where T : class, IEntidadeDaEscolinha =>
        modelBuilder.Entity<T>().HasQueryFilter(e => e.Escolinha_ID == EscolinhaAtualId);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Datas em horário de Brasília com milissegundos (CLAUDE.md §5).
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(3)");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("datetime2(3)");
    }
}
