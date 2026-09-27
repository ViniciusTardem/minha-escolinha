using System.Globalization;

namespace MinhaEscolinha.Domain.Configuracao.Parametros;

/// <summary>Resultado de validar um valor digitado: o texto normalizado (invariante) ou a mensagem de erro.</summary>
public sealed record ValorParametroValidado(bool Valido, string? Valor, string? Erro)
{
    public static ValorParametroValidado Ok(string? valor) => new(true, valor, null);
    public static ValorParametroValidado Falha(string erro) => new(false, null, erro);
}

/// <summary>
/// Valida e normaliza o valor de um parâmetro conforme o tipo e a faixa da definição.
/// Aceita a digitação brasileira (vírgula decimal, "sim"/"não") e grava sempre no formato invariante.
/// </summary>
public static class ValidadorParametro
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static ValorParametroValidado Validar(DefinicaoParametro definicao, string? entrada)
    {
        var texto = entrada?.Trim();
        if (string.IsNullOrEmpty(texto))
            return definicao.Obrigatorio && definicao.Escopo == EscopoParametro.Turma
                ? ValorParametroValidado.Ok(null) // sem padrão na escolinha: cada turma informa o seu
                : ValorParametroValidado.Falha($"Informe \"{definicao.Rotulo}\".");

        return definicao.Tipo switch
        {
            TipoParametro.Inteiro => ValidarInteiro(definicao, texto),
            TipoParametro.Percentual or TipoParametro.Moeda => ValidarDecimal(definicao, texto),
            TipoParametro.Booleano => ValidarBooleano(definicao, texto),
            TipoParametro.Hora => ValidarHora(definicao, texto),
            TipoParametro.Janela => ValidarJanela(definicao, texto),
            TipoParametro.ListaDias => ValidarListaDias(definicao, texto),
            TipoParametro.Escolha => ValidarEscolha(definicao, texto),
            _ => ValorParametroValidado.Falha($"Tipo de parâmetro não suportado: {definicao.Tipo}.")
        };
    }

    private static ValorParametroValidado ValidarInteiro(DefinicaoParametro d, string texto)
    {
        if (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero))
            return ValorParametroValidado.Falha($"\"{d.Rotulo}\" precisa ser um número inteiro.");
        var faixa = ConferirFaixa(d, numero);
        return faixa ?? ValorParametroValidado.Ok(numero.ToString(CultureInfo.InvariantCulture));
    }

    private static ValorParametroValidado ValidarDecimal(DefinicaoParametro d, string texto)
    {
        var limpo = texto.Replace("%", "").Replace("R$", "").Trim();
        // "2,5" (digitação brasileira) e "2.5" (valor já gravado) valem os dois.
        var cultura = limpo.Contains(',') ? PtBr : CultureInfo.InvariantCulture;
        if (!decimal.TryParse(limpo, NumberStyles.Number, cultura, out var numero))
            return ValorParametroValidado.Falha($"\"{d.Rotulo}\" precisa ser um número.");
        numero = Math.Round(numero, 2, MidpointRounding.AwayFromZero);
        var faixa = ConferirFaixa(d, numero);
        return faixa ?? ValorParametroValidado.Ok(numero.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static ValorParametroValidado ValidarBooleano(DefinicaoParametro d, string texto) =>
        texto.ToLowerInvariant() switch
        {
            "true" or "sim" or "1" or "on" => ValorParametroValidado.Ok("true"),
            "false" or "nao" or "não" or "0" or "off" => ValorParametroValidado.Ok("false"),
            _ => ValorParametroValidado.Falha($"\"{d.Rotulo}\" precisa ser sim ou não.")
        };

    private static ValorParametroValidado ValidarHora(DefinicaoParametro d, string texto) =>
        TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? ValorParametroValidado.Ok(hora.ToString("HH\\:mm", CultureInfo.InvariantCulture))
            : ValorParametroValidado.Falha($"\"{d.Rotulo}\" precisa ser uma hora no formato 00:00.");

    private static ValorParametroValidado ValidarJanela(DefinicaoParametro d, string texto)
    {
        if (!JanelaHorario.TentarLer(texto, out var janela))
            return ValorParametroValidado.Falha($"\"{d.Rotulo}\" precisa ter início e fim no formato 00:00-00:00.");
        if (janela.Inicio == janela.Fim)
            return ValorParametroValidado.Falha($"\"{d.Rotulo}\": início e fim não podem ser iguais.");
        return ValorParametroValidado.Ok(janela.ParaTexto());
    }

    private static ValorParametroValidado ValidarListaDias(DefinicaoParametro d, string texto)
    {
        var dias = new SortedSet<int>();
        foreach (var parte in texto.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(parte, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dia))
                return ValorParametroValidado.Falha($"\"{d.Rotulo}\": \"{parte}\" não é um número de dias.");
            var faixa = ConferirFaixa(d, dia);
            if (faixa is not null) return faixa;
            dias.Add(dia);
        }
        return dias.Count == 0
            ? ValorParametroValidado.Falha($"Informe ao menos um dia em \"{d.Rotulo}\".")
            : ValorParametroValidado.Ok(string.Join(",", dias));
    }

    private static ValorParametroValidado ValidarEscolha(DefinicaoParametro d, string texto)
    {
        var opcao = d.Opcoes?.FirstOrDefault(o => string.Equals(o.Valor, texto, StringComparison.OrdinalIgnoreCase));
        return opcao is null
            ? ValorParametroValidado.Falha($"\"{d.Rotulo}\": opção inválida.")
            : ValorParametroValidado.Ok(opcao.Valor);
    }

    private static ValorParametroValidado? ConferirFaixa(DefinicaoParametro d, decimal valor)
    {
        if (d.Minimo is { } minimo && valor < minimo)
            return ValorParametroValidado.Falha($"\"{d.Rotulo}\" precisa ser no mínimo {Exibir(minimo)}.");
        if (d.Maximo is { } maximo && valor > maximo)
            return ValorParametroValidado.Falha($"\"{d.Rotulo}\" pode ser no máximo {Exibir(maximo)}.");
        return null;
    }

    private static string Exibir(decimal valor) => valor.ToString("0.##", PtBr);
}
