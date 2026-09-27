using FluentValidation;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Acesso;

/// <summary>O acesso de uma ficha (responsável ou professor) ao sistema.</summary>
public enum SituacaoAcesso
{
    SemAcesso = 0,
    Convidado = 10,
    ConviteExpirado = 20,
    Ativo = 30
}

/// <summary>Uma ficha que pode ser convidada, com a situação do acesso e o último convite.</summary>
/// <param name="Detalhe">Os alunos (responsável) ou as turmas (professor), para reconhecer a pessoa na lista.</param>
public sealed record CandidatoAcesso(
    TipoConvite Tipo,
    Guid FichaId,
    string Nome,
    string? Telefone,
    string? Detalhe,
    SituacaoAcesso Situacao,
    Guid? ConviteId,
    DateTime? ConvidadoEm,
    DateTime? ExpiraEm);

/// <summary>Um vínculo aluno ↔ responsável esperando a confirmação do gestor (BL-M01-06).</summary>
public sealed record VinculoParaVerificar(
    Guid VinculoId,
    Guid ResponsavelId,
    string Responsavel,
    string? Cpf,
    string? Telefone,
    Guid AlunoId,
    string Aluno,
    Parentesco Parentesco,
    DateTime? SolicitadoEm);

/// <summary>Convites, fichas convidadas e vínculos a confirmar, na escolinha do contexto.</summary>
public interface IConvitesRepositorio
{
    void AdicionarConvite(ConviteAcesso convite);
    Task<ConviteAcesso?> ObterConviteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ConviteAcesso>> PendentesDaFichaAsync(TipoConvite tipo, Guid fichaId, CancellationToken ct = default);

    Task<IReadOnlyList<CandidatoAcesso>> CandidatosAsync(DateTime agora, CancellationToken ct = default);

    Task<IReadOnlyList<VinculoParaVerificar>> VinculosParaVerificarAsync(CancellationToken ct = default);
    Task<int> ContarVinculosParaVerificarAsync(CancellationToken ct = default);

    /// <summary>O vínculo para confirmar ou recusar (rastreado, com aluno e responsável).</summary>
    Task<VinculoAlunoResponsavel?> ObterVinculoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Os vínculos do responsável (rastreados): a ativação pede a confirmação de todos.</summary>
    Task<IReadOnlyList<VinculoAlunoResponsavel>> VinculosDoResponsavelAsync(Guid responsavelId, CancellationToken ct = default);

    Task<Responsavel?> ObterResponsavelAsync(Guid id, CancellationToken ct = default);
    Task<Professor?> ObterProfessorAsync(Guid id, CancellationToken ct = default);

    Task<bool> CpfDeOutraFichaAsync(TipoConvite tipo, string cpf, Guid fichaId, CancellationToken ct = default);

    /// <summary>Os alunos ligados à pessoa (pelas fichas de responsável dela nesta escolinha), com a verificação de cada vínculo.</summary>
    Task<IReadOnlyList<FilhoDaPessoa>> FilhosDaPessoaAsync(Guid pessoaId, CancellationToken ct = default);

    /// <summary>A pessoa tem o papel ativo na escolinha do contexto.</summary>
    Task<bool> AcessoAtivoAsync(Guid pessoaId, PapelAtuacao papel, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}

// ── Ativação pelo link do convite (anônima) ──

public enum EstadoConvite
{
    Invalido = 0,
    Valido = 10,
    Expirado = 20,
    JaUsado = 30,
    Cancelado = 40
}

public class IdentificacaoRequisicao
{
    public string Cpf { get; set; } = string.Empty;
}

/// <summary>O passo da senha: nova conta (e-mail opcional) ou a senha da conta que a pessoa já tem.</summary>
public class AtivacaoRequisicao
{
    public string Cpf { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Senha { get; set; } = string.Empty;
    public string ConfirmacaoSenha { get; set; } = string.Empty;
}

/// <summary>Depois do CPF conferido: quem é e o que falta (nada de dado antes disso, BL-M01-05).</summary>
/// <param name="ContaExistente">A pessoa já tem acesso ao sistema (outra escolinha ou outro papel): entra com a senha dela.</param>
public sealed record IdentificacaoConvite(string Nome, string Escolinha, TipoConvite Tipo, bool ContaExistente, string? Login);

public sealed class IdentificacaoRequisicaoValidador : AbstractValidator<IdentificacaoRequisicao>
{
    public IdentificacaoRequisicaoValidador()
    {
        RuleFor(x => x.Cpf).NotEmpty().WithMessage("Informe o seu CPF.")
            .Must(c => Cpf.Normalizar(c) is not null).WithMessage("CPF inválido.");
    }
}

public sealed class AtivacaoRequisicaoValidador : AbstractValidator<AtivacaoRequisicao>
{
    public AtivacaoRequisicaoValidador()
    {
        RuleFor(x => x.Cpf).Must(c => Cpf.Normalizar(c) is not null).WithMessage("CPF inválido.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("E-mail inválido.")
            .MaximumLength(Credencial.TamanhoLogin);
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}
