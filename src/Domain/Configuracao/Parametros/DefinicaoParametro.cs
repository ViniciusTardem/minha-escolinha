namespace MinhaEscolinha.Domain.Configuracao.Parametros;

/// <summary>Domínio do parâmetro (R7: "agrupe por domínio e permita leitura por qualquer módulo").</summary>
public enum DominioParametro
{
    Presenca = 10,
    Financeiro = 20,
    Convocacao = 30,
    Mensagem = 40,
    Matricula = 50
}

public enum TipoParametro
{
    /// <summary>Número inteiro ("15").</summary>
    Inteiro = 10,

    /// <summary>Percentual com até 2 casas ("2.5" = 2,5%).</summary>
    Percentual = 20,

    /// <summary>"true" / "false".</summary>
    Booleano = 30,

    /// <summary>Hora do dia ("21:00").</summary>
    Hora = 40,

    /// <summary>Faixa de horário, pode virar a meia-noite ("21:00-07:00").</summary>
    Janela = 50,

    /// <summary>Lista de dias, crescente e sem repetição ("1,5,10") — os degraus da régua.</summary>
    ListaDias = 60,

    /// <summary>Uma das <see cref="DefinicaoParametro.Opcoes"/>.</summary>
    Escolha = 70,

    /// <summary>Valor em reais com 2 casas ("150.00").</summary>
    Moeda = 80
}

/// <summary>Onde o valor vale: na escolinha inteira ou em cada turma (o da escolinha é o valor inicial das turmas novas).</summary>
public enum EscopoParametro
{
    Escolinha = 10,
    Turma = 20
}

public sealed record OpcaoParametro(string Valor, string Rotulo);

/// <summary>
/// Definição de um parâmetro de regra de negócio (R7): configurável por escolinha, alterável pela operação sem nova
/// entrega de software. Valores gravados como texto invariante (ponto decimal) e validados por <see cref="ValidadorParametro"/>.
/// </summary>
/// <param name="Provisorio">
/// O valor padrão não vem da especificação — foi escolhido para o piloto e está registrado como pendência
/// (docs/DECISOES-EM-ABERTO.md). As faixas mínima e máxima são todas provisórias (pendência 12.2 "faixas").
/// </param>
/// <param name="Origem">Requisitos de origem (RF-/BL-), para rastrear de onde veio a regra.</param>
public sealed record DefinicaoParametro(
    string Chave,
    DominioParametro Dominio,
    TipoParametro Tipo,
    EscopoParametro Escopo,
    string Rotulo,
    string Descricao,
    string? ValorPadrao,
    decimal? Minimo,
    decimal? Maximo,
    string? Unidade,
    bool Provisorio,
    string Origem,
    IReadOnlyList<OpcaoParametro>? Opcoes = null)
{
    /// <summary>Sem valor padrão (ex.: mensalidade): precisa ser informado em cada turma.</summary>
    public bool Obrigatorio => ValorPadrao is null;
}
