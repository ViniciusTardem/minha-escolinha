using Microsoft.EntityFrameworkCore;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Acesso;

/// <summary>
/// ARQUIVO NA LISTA PERMITIDA do <c>IgnoreQueryFilters</c>: o login e a montagem do contexto leem os vínculos da pessoa
/// antes de existir escolinha escolhida, e a ativação anônima acha o convite pelo hash do link. Toda leitura que ignora o
/// filtro aqui filtra explicitamente pela pessoa (e pela escolinha, quando informada) ou pelo hash do convite.
/// </summary>
public class AcessoRepositorio(EscolinhaDbContext contexto) : IAcessoRepositorio
{
    public Task<Credencial?> ObterCredencialPorLoginAsync(string login, CancellationToken ct = default) =>
        contexto.Credenciais.Include(c => c.Pessoa).FirstOrDefaultAsync(c => c.Login == login, ct);

    public Task<Credencial?> ObterCredencialAsync(Guid pessoaId, CancellationToken ct = default) =>
        contexto.Credenciais.Include(c => c.Pessoa).FirstOrDefaultAsync(c => c.Pessoa_ID == pessoaId, ct);

    public Task<Credencial?> ObterCredencialPorCpfAsync(string cpf, CancellationToken ct = default) =>
        contexto.Credenciais.Include(c => c.Pessoa).FirstOrDefaultAsync(c => c.Pessoa!.Cpf == cpf, ct);

    public Task<bool> LoginEmUsoAsync(string login, CancellationToken ct = default) =>
        contexto.Credenciais.AnyAsync(c => c.Login == login, ct);

    public Task<Pessoa?> ObterPessoaPorCpfAsync(string cpf, CancellationToken ct = default) =>
        contexto.Pessoas.FirstOrDefaultAsync(p => p.Cpf == cpf, ct);

    public async Task<IReadOnlyList<EscolinhaDaPessoa>> EscolinhasDaPessoaAsync(Guid pessoaId, CancellationToken ct = default)
    {
        var linhas = await contexto.VinculosAtuacao.IgnoreQueryFilters()
            .Where(v => v.Pessoa_ID == pessoaId && v.Situacao == SituacaoVinculo.Ativo)
            .Join(contexto.Escolinhas.IgnoreQueryFilters(),
                v => v.Escolinha_ID, e => e.Escolinha_ID, (v, e) => new { e.Escolinha_ID, e.Nome, v.Papel })
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .GroupBy(l => new { l.Escolinha_ID, l.Nome })
            .OrderBy(g => g.Key.Nome)
            .Select(g => new EscolinhaDaPessoa(g.Key.Escolinha_ID, g.Key.Nome, g.Select(l => l.Papel).Distinct().OrderBy(p => p).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<PapelAtuacao>> PapeisAtivosAsync(Guid pessoaId, Guid escolinhaId, CancellationToken ct = default) =>
        await contexto.VinculosAtuacao.IgnoreQueryFilters()
            .Where(v => v.Pessoa_ID == pessoaId && v.Escolinha_ID == escolinhaId && v.Situacao == SituacaoVinculo.Ativo)
            .Select(v => v.Papel)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VinculoAtuacao>> VinculosNaEscolinhaAsync(Guid pessoaId, CancellationToken ct = default) =>
        await contexto.VinculosAtuacao.Where(v => v.Pessoa_ID == pessoaId).ToListAsync(ct);

    public async Task<IReadOnlyList<PessoaDaEscolinha>> PessoasDaEscolinhaAsync(CancellationToken ct = default)
    {
        // Os vínculos passam pelo filtro (só a escolinha do contexto); a Pessoa é global e vem pela junção.
        var linhas = await contexto.VinculosAtuacao.AsNoTracking()
            .Where(v => v.Situacao == SituacaoVinculo.Ativo)
            .Select(v => new { v.Pessoa_ID, v.Pessoa!.Nome, v.Pessoa.Cpf, v.Papel })
            .ToListAsync(ct);

        return linhas
            .GroupBy(l => new { l.Pessoa_ID, l.Nome, l.Cpf })
            .OrderBy(g => g.Key.Nome)
            .Select(g => new PessoaDaEscolinha(g.Key.Pessoa_ID, g.Key.Nome, g.Key.Cpf, g.Select(l => l.Papel).Distinct().OrderBy(p => p).ToList()))
            .ToList();
    }

    public void AdicionarPessoa(Pessoa pessoa) => contexto.Pessoas.Add(pessoa);

    public void AdicionarCredencial(Credencial credencial) => contexto.Credenciais.Add(credencial);

    public Task<ConviteAcesso?> ConvitePorHashAsync(string tokenHash, CancellationToken ct = default) =>
        contexto.Convites.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.TokenHash == tokenHash, ct);

    public void AdicionarVinculo(VinculoAtuacao vinculo) => contexto.VinculosAtuacao.Add(vinculo);

    public Task SalvarAsync(CancellationToken ct = default) => contexto.SaveChangesAsync(ct);
}
