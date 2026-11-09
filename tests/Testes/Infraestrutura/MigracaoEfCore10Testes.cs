using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Testes.Infraestrutura;

/// <summary>
/// Guardas da migração para o .NET 10 / EF Core 10 (D113): o modelo continua igual ao das migrations, e o
/// <c>Contains</c> com lista grande segue funcionando (modo de parâmetros que mantém o OPENJSON) e respeitando o filtro de escolinha.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class MigracaoEfCore10Testes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    /// <summary>
    /// O snapshot das migrations descreve o modelo atual: o EF 10 não enxerga mudança pendente (nada de migration nova por causa da troca de versão).
    /// Não abre conexão: compara o modelo do código com o snapshot.
    /// </summary>
    [Fact]
    public void O_modelo_nao_tem_mudanca_pendente_em_relacao_as_migrations()
    {
        using var contexto = BancoDeTestes.NovoContextoSemEscolinha();

        Assert.False(contexto.Database.HasPendingModelChanges(),
            "O modelo difere do snapshot das migrations. Gere a migration (dotnet ef migrations add) e revise o diff antes de seguir.");
    }

    [FatoComBanco]
    public async Task Contains_com_mais_de_2100_ids_funciona_e_respeita_o_filtro_da_escolinha()
    {
        var a = await CriarEscolinhaAsync("Escolinha A", "gestor.a@escolinha.com");
        var b = await CriarEscolinhaAsync("Escolinha B", "gestor.b@escolinha.com");

        // Os ids das duas escolinhas no meio de milhares de ids que não existem (acima do limite de 2.100 parâmetros do SQL Server).
        var idsDeA = await IdsDasModalidadesAsync(a);
        var idsDeB = await IdsDasModalidadesAsync(b);
        var ids = Enumerable.Range(0, 2500).Select(_ => Guid.NewGuid()).Concat(idsDeA).Concat(idsDeB).ToList();
        Assert.True(ids.Count > 2100);

        var vistosPorA = await _app.NaEscolinhaAsync(a, [PapelAtuacao.Gestor], sp =>
            sp.GetRequiredService<EscolinhaDbContext>().Modalidades.Where(m => ids.Contains(m.Modalidade_ID)).Select(m => m.Modalidade_ID).ToListAsync());

        Assert.Equal(idsDeA.Order(), vistosPorA.Order());
        Assert.DoesNotContain(vistosPorA, id => idsDeB.Contains(id));
    }

    private async Task<Guid> CriarEscolinhaAsync(string nome, string email)
    {
        var resultado = await _app.ComoPlataformaAsync(sp => sp.GetRequiredService<MinhaEscolinha.Application.Configuracao.ServicoPlataforma>()
            .CriarEscolinhaComGestorAsync(new MinhaEscolinha.Application.Configuracao.NovaEscolinhaRequisicao
            {
                Nome = nome, Cidade = "Londrina", Uf = "PR", NomeModalidade = "Futsal", GestorNome = $"Gestor {nome}", GestorEmail = email
            }));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return Assert.IsType<MinhaEscolinha.Application.Configuracao.EscolinhaCriada>(resultado.Dados).EscolinhaId;
    }

    private static async Task<List<Guid>> IdsDasModalidadesAsync(Guid escolinhaId)
    {
        await using var db = BancoDeTestes.NovoContextoSemEscolinha();
        return await db.Modalidades.IgnoreQueryFilters().Where(m => m.Escolinha_ID == escolinhaId).Select(m => m.Modalidade_ID).ToListAsync();
    }
}
