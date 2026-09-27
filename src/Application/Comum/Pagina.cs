namespace MinhaEscolinha.Application.Comum;

/// <summary>Uma página de uma lista (telas com busca e paginação).</summary>
public record Pagina<T>(IReadOnlyList<T> Itens, int Total, int Numero, int Tamanho)
{
    public const int TamanhoPadrao = 25;

    public int TotalPaginas => Total == 0 ? 1 : (Total + Tamanho - 1) / Tamanho;
    public bool TemAnterior => Numero > 1;
    public bool TemProxima => Numero < TotalPaginas;
    public int PrimeiroItem => Total == 0 ? 0 : (Numero - 1) * Tamanho + 1;
    public int UltimoItem => Math.Min(Numero * Tamanho, Total);

    /// <summary>Página pedida dentro dos limites (1 quando vier zero, negativa ou lixo).</summary>
    public static int Normalizar(int numero) => numero < 1 ? 1 : numero;
}
