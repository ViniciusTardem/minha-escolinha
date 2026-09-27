namespace MinhaEscolinha.Application.Comum;

public enum NaturezaFalha
{
    /// <summary>5xx, timeout, 429, rede: tentar de novo mais tarde.</summary>
    Retentavel,

    /// <summary>4xx de validação, recurso inexistente, chave errada: tentar de novo não resolve.</summary>
    Definitiva
}

/// <summary>
/// Falha numa chamada externa, já classificada (regra de ouro 8). O job só relança a retentável; a definitiva é
/// registrada e o job termina. Portado do AlternativaDepartamentoPessoal.
/// </summary>
public class FalhaIntegracaoException(NaturezaFalha natureza, string mensagem, int? codigoHttp = null, Exception? interna = null)
    : Exception(mensagem, interna)
{
    public NaturezaFalha Natureza { get; } = natureza;
    public int? CodigoHttp { get; } = codigoHttp;
    public bool Retentavel => Natureza == NaturezaFalha.Retentavel;

    public static NaturezaFalha Classificar(int codigoHttp) =>
        codigoHttp is 408 or 429 || codigoHttp >= 500 ? NaturezaFalha.Retentavel : NaturezaFalha.Definitiva;
}
