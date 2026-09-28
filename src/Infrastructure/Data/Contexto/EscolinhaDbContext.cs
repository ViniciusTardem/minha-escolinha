using System.Reflection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Plataforma;
using MinhaEscolinha.Domain.Turmas;

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
    public DbSet<PacoteCreditos> PacotesCreditos => Set<PacoteCreditos>();
    public DbSet<CompraCreditos> ComprasCreditos => Set<CompraCreditos>();
    public DbSet<WebhookRecebido> WebhooksRecebidos => Set<WebhookRecebido>();
    public DbSet<Professor> Professores => Set<Professor>();
    public DbSet<Local> Locais => Set<Local>();
    public DbSet<Turma> Turmas => Set<Turma>();
    public DbSet<HorarioTurma> HorariosTurma => Set<HorarioTurma>();
    public DbSet<SessaoTreino> Sessoes => Set<SessaoTreino>();
    public DbSet<Aluno> Alunos => Set<Aluno>();
    public DbSet<Responsavel> Responsaveis => Set<Responsavel>();
    public DbSet<VinculoAlunoResponsavel> VinculosResponsavel => Set<VinculoAlunoResponsavel>();
    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<ConsentimentoAluno> Consentimentos => Set<ConsentimentoAluno>();
    public DbSet<Mensagem> Mensagens => Set<Mensagem>();
    public DbSet<EventoMensagem> EventosMensagem => Set<EventoMensagem>();
    public DbSet<RespostaMensagem> RespostasMensagem => Set<RespostaMensagem>();
    public DbSet<TemplateMensagem> TemplatesMensagem => Set<TemplateMensagem>();
    public DbSet<ConviteAcesso> Convites => Set<ConviteAcesso>();
    public DbSet<RastreioWhatsApp> RastreiosWhatsApp => Set<RastreioWhatsApp>();
    public DbSet<Presenca> Presencas => Set<Presenca>();
    public DbSet<MarcacaoPresenca> MarcacoesPresenca => Set<MarcacaoPresenca>();
    public DbSet<AlertaFaltas> AlertasFaltas => Set<AlertaFaltas>();
    public DbSet<Cobranca> Cobrancas => Set<Cobranca>();
    public DbSet<ClienteAsaas> ClientesAsaas => Set<ClienteAsaas>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<EventoTurma> EventosTurma => Set<EventoTurma>();
    public DbSet<Convocacao> Convocacoes => Set<Convocacao>();
    public DbSet<HistoricoConvocacao> HistoricosConvocacao => Set<HistoricoConvocacao>();
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
        // Horário de treino e faixa de disponibilidade: hora e minuto.
        configurationBuilder.Properties<TimeOnly>().HaveColumnType("time(0)");
    }
}
