using System.Security.Cryptography;
using FluentValidation;

namespace MinhaEscolinha.Application.Acesso;

public interface IHashSenha
{
    string Gerar(string senha);
    bool Verificar(string senha, string hash);
}

/// <summary>
/// Senha temporária (1º gestor, redefinição): 12 caracteres sem os ambíguos (0/O, 1/l/I), sempre com letra e número —
/// atende à <see cref="RegrasSenha"/>. Portado do AlternativaDepartamentoPessoal.
/// </summary>
public static class GeradorSenhaTemporaria
{
    private const string Letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digitos = "23456789";
    private const int Tamanho = 12;

    public static string Gerar()
    {
        var todos = Letras + Digitos;
        var chars = new char[Tamanho];
        chars[0] = Letras[RandomNumberGenerator.GetInt32(Letras.Length)];
        chars[1] = Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)];
        for (var i = 2; i < Tamanho; i++)
            chars[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}

public static class RegrasSenha
{
    public const int TamanhoMinimo = 8;
    public const int TamanhoMaximo = 100;

    public static IRuleBuilderOptions<T, string> SenhaForte<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("Informe a nova senha.")
            .MinimumLength(TamanhoMinimo).WithMessage($"A senha precisa ter pelo menos {TamanhoMinimo} caracteres.")
            .MaximumLength(TamanhoMaximo).WithMessage($"A senha pode ter no máximo {TamanhoMaximo} caracteres.")
            .Must(s => s.Any(char.IsLetter) && s.Any(char.IsDigit))
            .WithMessage("A senha precisa ter letras e números.");
}
