using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>
/// Configuração da escolinha do contexto (todas as consultas passam pelo filtro de escolinha).
/// Gravar sem contexto ou em outra escolinha lança exceção no interceptor.
/// </summary>
public interface IConfiguracaoRepositorio
{
    /// <summary>A escolinha do contexto (nula sem contexto).</summary>
    Task<Escolinha?> ObterEscolinhaAsync(CancellationToken ct = default);

    void AdicionarEscolinha(Escolinha escolinha);

    Task<IReadOnlyList<Modalidade>> ListarModalidadesAsync(CancellationToken ct = default);
    Task<Modalidade?> ObterModalidadeAsync(Guid id, CancellationToken ct = default);
    Task<bool> NomeModalidadeEmUsoAsync(string nome, Guid? excetoId, CancellationToken ct = default);
    void AdicionarModalidade(Modalidade modalidade);

    Task<IReadOnlyList<ParametroEscolinha>> ListarParametrosAsync(CancellationToken ct = default);
    void AdicionarParametro(ParametroEscolinha parametro);

    Task<ContaAsaasEscolinha?> ObterContaAsaasAsync(CancellationToken ct = default);
    void AdicionarContaAsaas(ContaAsaasEscolinha conta);

    Task<ContaCreditos?> ObterContaCreditosAsync(CancellationToken ct = default);
    void AdicionarContaCreditos(ContaCreditos conta);
    void AdicionarMovimentoCredito(MovimentoCredito movimento);
    Task<Pagina<MovimentoCredito>> ListarMovimentosCreditoAsync(int pagina, int tamanho, CancellationToken ct = default);

    void Registrar(RegistroAlteracao registro);

    /// <summary>Alterações mais recentes primeiro, de uma entidade (e, opcionalmente, de um campo).</summary>
    Task<IReadOnlyList<RegistroAlteracao>> ListarAlteracoesAsync(string entidade, string? campo, int limite, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}

/// <summary>
/// Leituras da plataforma: atravessam escolinhas de propósito (lista da equipe LDNA, reconciliação do startup).
/// A implementação está na lista de arquivos autorizados a ignorar o filtro de escolinha.
/// </summary>
public interface IConsultasPlataforma
{
    /// <param name="pessoaId">Quem está olhando — para marcar as escolinhas em que já é gestor.</param>
    Task<IReadOnlyList<EscolinhaNaPlataforma>> ListarEscolinhasAsync(Guid? pessoaId, CancellationToken ct = default);

    Task<EscolinhaNaPlataforma?> ObterEscolinhaAsync(Guid escolinhaId, Guid? pessoaId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> IdsDasEscolinhasAsync(CancellationToken ct = default);
}
