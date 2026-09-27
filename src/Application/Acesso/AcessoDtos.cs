using FluentValidation;
using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Application.Acesso;

public class LoginRequisicao
{
    public string Login { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public bool Lembrarme { get; set; }
}

public class TrocarSenhaRequisicao
{
    public string SenhaAtual { get; set; } = string.Empty;
    public string NovaSenha { get; set; } = string.Empty;
    public string ConfirmacaoSenha { get; set; } = string.Empty;
}

/// <summary>Uma escolinha em que a pessoa atua, com os papéis ativos nela.</summary>
public record EscolinhaDaPessoa(Guid EscolinhaId, string Nome, IReadOnlyList<PapelAtuacao> Papeis);

/// <summary>Uma pessoa com acesso à escolinha do contexto e os papéis ativos dela.</summary>
public record PessoaDaEscolinha(Guid PessoaId, string Nome, string? Cpf, IReadOnlyList<PapelAtuacao> Papeis);

/// <summary>O que o login precisa para montar a sessão (claims do cookie).</summary>
public record CredencialAutenticada(
    Guid PessoaId,
    string Nome,
    string Login,
    bool AdministradorPlataforma,
    bool TrocarSenha,
    Guid CarimboSeguranca,
    IReadOnlyList<EscolinhaDaPessoa> Escolinhas);

/// <summary>Senha temporária: mostrada uma única vez (criação do 1º gestor, redefinição).</summary>
public record SenhaTemporariaResposta(Guid PessoaId, string Nome, string Login, string SenhaTemporaria);

public class LoginRequisicaoValidador : AbstractValidator<LoginRequisicao>
{
    public LoginRequisicaoValidador()
    {
        RuleFor(x => x.Login).NotEmpty().WithMessage("Informe o login.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}

public class TrocarSenhaRequisicaoValidador : AbstractValidator<TrocarSenhaRequisicao>
{
    public TrocarSenhaRequisicaoValidador()
    {
        RuleFor(x => x.SenhaAtual).NotEmpty().WithMessage("Informe a senha atual.");
        RuleFor(x => x.NovaSenha).SenhaForte();
        RuleFor(x => x.ConfirmacaoSenha)
            .Equal(x => x.NovaSenha).WithMessage("A confirmação não confere com a nova senha.");
        RuleFor(x => x.NovaSenha)
            .NotEqual(x => x.SenhaAtual).WithMessage("A nova senha precisa ser diferente da atual.")
            .When(x => !string.IsNullOrEmpty(x.NovaSenha));
    }
}

public static class RotulosAcesso
{
    public static string Papel(PapelAtuacao papel) => papel switch
    {
        PapelAtuacao.Gestor => "Gestor",
        PapelAtuacao.Professor => "Professor",
        PapelAtuacao.Responsavel => "Responsável",
        _ => papel.ToString()
    };

    public static string Papeis(IEnumerable<PapelAtuacao> papeis) =>
        string.Join(" · ", papeis.OrderBy(p => p).Select(Papel));
}
