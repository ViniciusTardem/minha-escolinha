namespace MinhaEscolinha.Domain.Comum;

/// <summary>Endereço postal (tipo complexo: gravado em colunas da própria tabela dona).</summary>
public class Endereco
{
    public const int TamanhoLogradouro = 150;
    public const int TamanhoNumero = 20;
    public const int TamanhoComplemento = 80;
    public const int TamanhoBairro = 80;
    public const int TamanhoCidade = 80;
    public const int TamanhoUf = 2;
    public const int TamanhoCep = 8;

    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    /// <summary>Só os 8 dígitos.</summary>
    public string? Cep { get; set; }

    /// <summary>"Rua X, 10 — Centro, Londrina/PR" (partes vazias somem). Usado no link de mapa e nas telas.</summary>
    public string Resumo()
    {
        var rua = string.Join(", ", new[] { Logradouro, Numero }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var cidade = string.Join("/", new[] { Cidade, Uf }.Where(p => !string.IsNullOrWhiteSpace(p)));
        var partes = new[] { rua, Bairro, cidade }.Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" — ", partes);
    }

    /// <summary>
    /// O link de mapa da convocação (CA-11): uma busca no Google Maps pelo endereço (ou pelo nome do lugar, sem endereço).
    /// É um link montado aqui, e não uma URL de terceiro guardada (regra de ouro 3). <c>null</c> sem nada para buscar.
    /// </summary>
    public string? LinkMapa(string? nomeDoLugar)
    {
        var busca = Resumo().Replace(" — ", ", ");
        if (string.IsNullOrWhiteSpace(Logradouro)) busca = string.Join(", ", new[] { nomeDoLugar, busca }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(busca) ? null : $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(busca)}";
    }
}
