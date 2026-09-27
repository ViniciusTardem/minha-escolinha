using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Configuracao;

/// <summary>
/// ARQUIVO NA LISTA PERMITIDA do <c>IgnoreQueryFilters</c>: a lista da equipe da plataforma e a reconciliação do
/// startup atravessam escolinhas. Só lê dados de cadastro da conta (nome, cidade, gestores, saldo) — nada de aluno.
/// </summary>
public class ConsultasPlataforma(EscolinhaDbContext contexto) : IConsultasPlataforma
{
    public async Task<IReadOnlyList<EscolinhaNaPlataforma>> ListarEscolinhasAsync(Guid? pessoaId, CancellationToken ct = default) =>
        await Consultar(null, pessoaId, ct);

    public async Task<EscolinhaNaPlataforma?> ObterEscolinhaAsync(Guid escolinhaId, Guid? pessoaId, CancellationToken ct = default) =>
        (await Consultar(escolinhaId, pessoaId, ct)).FirstOrDefault();

    public async Task<IReadOnlyList<Guid>> IdsDasEscolinhasAsync(CancellationToken ct = default) =>
        await contexto.Escolinhas.IgnoreQueryFilters().Select(e => e.Escolinha_ID).ToListAsync(ct);

    private async Task<IReadOnlyList<EscolinhaNaPlataforma>> Consultar(Guid? escolinhaId, Guid? pessoaId, CancellationToken ct)
    {
        var escolinhas = await contexto.Escolinhas.IgnoreQueryFilters().AsNoTracking()
            .Where(e => escolinhaId == null || e.Escolinha_ID == escolinhaId)
            .OrderBy(e => e.Nome)
            .Select(e => new { e.Escolinha_ID, e.Nome, e.Cnpj, e.Endereco.Cidade, e.Endereco.Uf, e.DataCadastro })
            .ToListAsync(ct);
        var ids = escolinhas.Select(e => e.Escolinha_ID).ToList();

        var gestores = await contexto.VinculosAtuacao.IgnoreQueryFilters().AsNoTracking()
            .Where(v => ids.Contains(v.Escolinha_ID) && v.Papel == PapelAtuacao.Gestor && v.Situacao == SituacaoVinculo.Ativo)
            .Select(v => new { v.Escolinha_ID, v.Pessoa_ID, v.Pessoa!.Nome })
            .ToListAsync(ct);
        var saldos = await contexto.ContasCreditos.IgnoreQueryFilters().AsNoTracking()
            .Where(c => ids.Contains(c.Escolinha_ID))
            .ToDictionaryAsync(c => c.Escolinha_ID, c => c.Saldo, ct);
        var comConta = (await contexto.ContasAsaas.IgnoreQueryFilters().AsNoTracking()
            .Where(c => ids.Contains(c.Escolinha_ID))
            .Select(c => c.Escolinha_ID)
            .ToListAsync(ct)).ToHashSet();

        return escolinhas.Select(e => new EscolinhaNaPlataforma(
            e.Escolinha_ID,
            e.Nome,
            e.Cnpj,
            e.Cidade,
            e.Uf,
            gestores.Where(g => g.Escolinha_ID == e.Escolinha_ID).Select(g => g.Nome).OrderBy(n => n).ToList(),
            saldos.GetValueOrDefault(e.Escolinha_ID),
            comConta.Contains(e.Escolinha_ID),
            e.DataCadastro,
            pessoaId is { } p && gestores.Any(g => g.Escolinha_ID == e.Escolinha_ID && g.Pessoa_ID == p))).ToList();
    }
}
