using FluentValidation.Results;

namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// Retorno de caso de uso: regra de negócio violada é <see cref="Erro"/>, nunca exceção.
/// Portado do AlternativaDepartamentoPessoal (que veio da Plataforma de Vagas); novo aqui: <see cref="Campo"/>.
/// </summary>
public class ResultadoOperacao
{
    public bool Sucesso { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public object? Dados { get; set; }

    /// <summary>
    /// Propriedade da requisição a que o erro se refere (opcional). A tela marca o campo e o formulário em etapas
    /// abre na etapa dele, em vez de voltar para a primeira.
    /// </summary>
    public string? Campo { get; set; }

    public static ResultadoOperacao Ok(string mensagem = "Operação realizada com sucesso.", object? dados = null)
        => new() { Sucesso = true, Mensagem = mensagem, Dados = dados };

    public static ResultadoOperacao Erro(string mensagem, string? campo = null)
        => new() { Sucesso = false, Mensagem = mensagem, Campo = campo };

    /// <summary>A primeira falha da validação, com o campo dela.</summary>
    public static ResultadoOperacao Invalido(ValidationResult validacao)
        => Erro(validacao.Errors[0].ErrorMessage, validacao.Errors[0].PropertyName);
}
