using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Domain.Turmas;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Carga;

/// <summary>Os lotes da carga inicial e a base da escolinha que a conferência compara com a planilha.</summary>
public sealed class CargaRepositorio(EscolinhaDbContext contexto) : ICargaRepositorio
{
    public async Task<IReadOnlyList<LoteNaLista>> ListarAsync(int limite, CancellationToken ct = default) =>
        await Resumos(contexto.ImportacaoLotes.OrderByDescending(l => l.EnviadoEm).Take(limite)).ToListAsync(ct);

    public Task<LoteNaLista?> ResumoAsync(Guid id, CancellationToken ct = default) =>
        Resumos(contexto.ImportacaoLotes.Where(l => l.ImportacaoLote_ID == id)).SingleOrDefaultAsync(ct);

    public Task<ImportacaoLote?> ObterAsync(Guid id, CancellationToken ct = default) =>
        contexto.ImportacaoLotes.SingleOrDefaultAsync(l => l.ImportacaoLote_ID == id, ct);

    public async Task<IReadOnlyList<ImportacaoLote>> AbertosAntesDeAsync(DateTime limite, CancellationToken ct = default) =>
        await contexto.ImportacaoLotes.Where(l => l.Situacao == SituacaoImportacao.Aberta && l.EnviadoEm < limite).ToListAsync(ct);

    public void Adicionar(ImportacaoLote lote) => contexto.ImportacaoLotes.Add(lote);

    public async Task<BaseExistente> BaseAsync(CancellationToken ct = default)
    {
        var alunos = await contexto.Alunos.AsNoTracking()
            .Select(a => new AlunoExistente(a.Aluno_ID, a.Nome, a.NomeBusca, a.DataNascimento, a.Cpf))
            .ToListAsync(ct);
        var responsaveis = await contexto.Responsaveis.AsNoTracking()
            .Select(r => new ResponsavelExistente(r.Responsavel_ID, r.Nome, r.NomeBusca, r.Cpf, r.Telefone, r.Pessoa_ID))
            .ToListAsync(ct);
        var ocupacao = await contexto.Matriculas.Where(m => m.Situacao == SituacaoMatricula.Ativa)
            .GroupBy(m => m.Turma_ID)
            .Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        var turmas = (await contexto.Turmas.AsNoTracking().Where(t => t.Situacao == SituacaoTurma.Ativa)
                .Select(t => new { t.Turma_ID, t.Nome, t.Mensalidade, t.Capacidade })
                .ToListAsync(ct))
            .Select(t => new TurmaExistente(t.Turma_ID, t.Nome, t.Mensalidade, t.Capacidade, ocupacao.GetValueOrDefault(t.Turma_ID)))
            .OrderBy(t => t.Nome)
            .ToList();
        return new BaseExistente(alunos, responsaveis, turmas);
    }

    public async Task SalvarAsync(CancellationToken ct = default)
    {
        try
        {
            await contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflitoDeConcorrenciaException("O lote mudou em outra tela; nada desta gravação ficou.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Um CPF que a conferência não viu (outro cadastro gravado no meio): a importação inteira volta.
            throw new ConflitoDeConcorrenciaException("Um cadastro igual foi gravado ao mesmo tempo; nada desta gravação ficou.", ex);
        }
    }

    /// <summary>O resumo sem o conteúdo. Filtro e ordem vêm antes: o EF não traduz condição sobre o record projetado.</summary>
    private static IQueryable<LoteNaLista> Resumos(IQueryable<ImportacaoLote> lotes) =>
        lotes.AsNoTracking().Select(l => new LoteNaLista(l.ImportacaoLote_ID, l.NomeArquivo, l.Situacao, l.Linhas,
            l.EnviadoEm, l.EnviadoPorNome, l.EncerradoEm, l.EncerradoPorNome, l.AlunosCriados, l.ResponsaveisCriados,
            l.ResponsaveisReaproveitados, l.LinhasNaoImportadas));
}
