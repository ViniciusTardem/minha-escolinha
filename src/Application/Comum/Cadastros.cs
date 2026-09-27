using FluentValidation;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Application.Comum;

/// <summary>Requisição com os campos de endereço (local, aluno, responsável).</summary>
public interface IComEndereco
{
    string? Logradouro { get; set; }
    string? Numero { get; set; }
    string? Complemento { get; set; }
    string? Bairro { get; set; }
    string? Cidade { get; set; }
    string? Uf { get; set; }
    string? Cep { get; set; }
}

/// <summary>Normalização e regras comuns dos cadastros (endereço, texto opcional, telefone, CPF).</summary>
public static class Cadastros
{
    public static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Endereço da requisição, com UF em maiúsculas e CEP só com os 8 dígitos.</summary>
    public static Endereco Endereco(IComEndereco r) => new()
    {
        Logradouro = Vazio(r.Logradouro),
        Numero = Vazio(r.Numero),
        Complemento = Vazio(r.Complemento),
        Bairro = Vazio(r.Bairro),
        Cidade = Vazio(r.Cidade),
        Uf = Vazio(r.Uf)?.ToUpperInvariant(),
        Cep = TextoHelper.SomenteDigitos(r.Cep) is { Length: 8 } cep ? cep : null
    };

    public static void PreencherEndereco(IComEndereco r, Endereco? e)
    {
        if (e is null) return;
        r.Logradouro = e.Logradouro;
        r.Numero = e.Numero;
        r.Complemento = e.Complemento;
        r.Bairro = e.Bairro;
        r.Cidade = e.Cidade;
        r.Uf = e.Uf;
        r.Cep = e.Cep;
    }

    public static void RegrasEndereco<T>(AbstractValidator<T> v) where T : IComEndereco
    {
        v.RuleFor(x => x.Logradouro).MaximumLength(Domain.Comum.Endereco.TamanhoLogradouro).WithMessage("O logradouro é longo demais.");
        v.RuleFor(x => x.Numero).MaximumLength(Domain.Comum.Endereco.TamanhoNumero).WithMessage("O número é longo demais.");
        v.RuleFor(x => x.Complemento).MaximumLength(Domain.Comum.Endereco.TamanhoComplemento).WithMessage("O complemento é longo demais.");
        v.RuleFor(x => x.Bairro).MaximumLength(Domain.Comum.Endereco.TamanhoBairro).WithMessage("O bairro é longo demais.");
        v.RuleFor(x => x.Cidade).MaximumLength(Domain.Comum.Endereco.TamanhoCidade).WithMessage("A cidade é longa demais.");
        v.RuleFor(x => x.Uf).Must(u => string.IsNullOrWhiteSpace(u) || u.Trim().Length == 2).WithMessage("UF com 2 letras.");
        v.RuleFor(x => x.Cep).Must(c => string.IsNullOrWhiteSpace(c) || TextoHelper.SomenteDigitos(c).Length == 8)
            .WithMessage("CEP com 8 dígitos.");
    }

    public static bool CpfValidoOuVazio(string? cpf) => string.IsNullOrWhiteSpace(cpf) || Cpf.Normalizar(cpf) is not null;

    public static bool TelefoneValidoOuVazio(string? telefone) =>
        string.IsNullOrWhiteSpace(telefone) || Telefone.ParaE164(telefone) is not null;

    /// <summary>"+5543987654321" → "(43) 98765-4321" para preencher um campo de edição.</summary>
    public static string? TelefoneParaCampo(string? e164)
    {
        if (string.IsNullOrEmpty(e164)) return null;
        var n = e164.StartsWith("+55", StringComparison.Ordinal) ? e164[3..] : e164;
        return n.Length switch
        {
            11 => $"({n[..2]}) {n[2..7]}-{n[7..]}",
            10 => $"({n[..2]}) {n[2..6]}-{n[6..]}",
            _ => e164
        };
    }

    /// <summary>Base64 da versão de linha, para o formulário devolver (conflito de edição = D27).</summary>
    public static string? Versao(byte[]? versao) => versao is { Length: > 0 } ? Convert.ToBase64String(versao) : null;

    public static byte[]? LerVersao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try { return Convert.FromBase64String(texto); }
        catch (FormatException) { return null; }
    }

    public const string MensagemConflito =
        "Outra pessoa alterou este cadastro enquanto você editava. Abra de novo para ver o que mudou e refaça a sua alteração.";
}
