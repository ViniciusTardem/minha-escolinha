using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Application.Acesso;

/// <summary>
/// Identidade, credencial e vínculos. As leituras de vínculos por pessoa atravessam escolinhas de propósito (login e
/// montagem do contexto, antes de existir uma escolinha escolhida) — por isso a implementação está na lista de arquivos
/// autorizados a ignorar o filtro de escolinha (teste de arquitetura).
/// </summary>
public interface IAcessoRepositorio
{
    /// <summary>Busca pelo login já normalizado (<see cref="Credencial.NormalizarLogin"/>), com a pessoa.</summary>
    Task<Credencial?> ObterCredencialPorLoginAsync(string login, CancellationToken ct = default);

    Task<Credencial?> ObterCredencialAsync(Guid pessoaId, CancellationToken ct = default);

    Task<bool> LoginEmUsoAsync(string login, CancellationToken ct = default);

    /// <summary>Pessoa com este CPF (11 dígitos), se houver — para recusar uma segunda pessoa com o mesmo CPF.</summary>
    Task<Pessoa?> ObterPessoaPorCpfAsync(string cpf, CancellationToken ct = default);

    /// <summary>As escolinhas em que a pessoa tem vínculo ativo, com os papéis (atravessa escolinhas).</summary>
    Task<IReadOnlyList<EscolinhaDaPessoa>> EscolinhasDaPessoaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>Papéis ativos da pessoa numa escolinha (relido a cada requisição: revogação imediata).</summary>
    Task<IReadOnlyList<PapelAtuacao>> PapeisAtivosAsync(Guid pessoaId, Guid escolinhaId, CancellationToken ct = default);

    /// <summary>Vínculos da pessoa na escolinha do contexto (ativos e revogados).</summary>
    Task<IReadOnlyList<VinculoAtuacao>> VinculosNaEscolinhaAsync(Guid pessoaId, CancellationToken ct = default);

    void AdicionarPessoa(Pessoa pessoa);

    void AdicionarVinculo(VinculoAtuacao vinculo);

    Task SalvarAsync(CancellationToken ct = default);
}
