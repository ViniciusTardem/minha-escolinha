using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Mensageria;

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

    /// <summary>Login pelo CPF (responsável sem e-mail): a credencial da pessoa com este CPF (11 dígitos).</summary>
    Task<Credencial?> ObterCredencialPorCpfAsync(string cpf, CancellationToken ct = default);

    Task<bool> LoginEmUsoAsync(string login, CancellationToken ct = default);

    /// <summary>Pessoa com este CPF (11 dígitos), se houver — para recusar uma segunda pessoa com o mesmo CPF.</summary>
    Task<Pessoa?> ObterPessoaPorCpfAsync(string cpf, CancellationToken ct = default);

    /// <summary>As escolinhas em que a pessoa tem vínculo ativo, com os papéis (atravessa escolinhas).</summary>
    Task<IReadOnlyList<EscolinhaDaPessoa>> EscolinhasDaPessoaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>Papéis ativos da pessoa numa escolinha (relido a cada requisição: revogação imediata).</summary>
    Task<IReadOnlyList<PapelAtuacao>> PapeisAtivosAsync(Guid pessoaId, Guid escolinhaId, CancellationToken ct = default);

    /// <summary>Vínculos da pessoa na escolinha do contexto (ativos e revogados).</summary>
    Task<IReadOnlyList<VinculoAtuacao>> VinculosNaEscolinhaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>As pessoas com vínculo ativo na escolinha do contexto, com os papéis (para reaproveitar o cadastro, BL-M08-01).</summary>
    Task<IReadOnlyList<PessoaDaEscolinha>> PessoasDaEscolinhaAsync(CancellationToken ct = default);

    void AdicionarPessoa(Pessoa pessoa);

    /// <summary>Credencial para uma pessoa que já existe sem login.</summary>
    void AdicionarCredencial(Credencial credencial);

    /// <summary>
    /// O convite pelo hash do segredo do link, em qualquer escolinha (a ativação é anônima: a escolinha sai do convite).
    /// Por isso mora aqui, no arquivo autorizado a ignorar o filtro de escolinha.
    /// </summary>
    Task<ConviteAcesso?> ConvitePorHashAsync(string tokenHash, CancellationToken ct = default);

    void AdicionarVinculo(VinculoAtuacao vinculo);

    /// <summary>
    /// O pedido de aceite dos termos pelo hash do segredo do link, em qualquer escolinha (a página é anônima: a escolinha
    /// sai do pedido, D111). Mora aqui, no arquivo autorizado a ignorar o filtro, como o convite.
    /// </summary>
    Task<Domain.Alunos.PedidoTermos?> PedidoTermosPorHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Pedido de nova senha (BL-M01-09): global, como a credencial.</summary>
    void AdicionarRecuperacao(RecuperacaoSenha recuperacao);

    Task<RecuperacaoSenha?> ObterRecuperacaoAsync(Guid id, CancellationToken ct = default);

    Task<RecuperacaoSenha?> RecuperacaoPorHashAsync(string tokenHash, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}
