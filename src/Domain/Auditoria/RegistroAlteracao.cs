using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Auditoria;

/// <summary>
/// Trilha de "quem alterou o quê" das ações sensíveis da escolinha (spec §7: alteração de mensalidade, exclusão de aluno,
/// isenção, cancelamento de cobrança — e, desde o S0, parâmetros de regra, dados da escolinha e conta de recebimento).
/// Uma tabela só para todas as entidades (decisão 12.1): uma consulta mostra a auditoria inteira da escolinha.
/// Valor secreto (chave de API) nunca entra aqui — só "alterada".
/// </summary>
public class RegistroAlteracao : IEntidadeDaEscolinha
{
    public const int TamanhoEntidade = 60;
    public const int TamanhoCampo = 100;
    public const int TamanhoValor = 400;
    public const int TamanhoMotivo = 300;
    public const int TamanhoAutor = 150;
    public const int TamanhoIp = 45;

    public Guid RegistroAlteracao_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    /// <summary>Nome da entidade alterada ("ParametroEscolinha", "Escolinha", "ContaAsaasEscolinha"...).</summary>
    public string Entidade { get; set; } = string.Empty;

    /// <summary>Id do registro alterado, quando houver.</summary>
    public Guid? Registro_ID { get; set; }

    /// <summary>Campo ou chave alterada (a chave do parâmetro, o nome da propriedade).</summary>
    public string Campo { get; set; } = string.Empty;

    public string? ValorAnterior { get; set; }
    public string? ValorNovo { get; set; }
    public string? Motivo { get; set; }
    public Guid? Autor_ID { get; set; }
    public string AutorNome { get; set; } = string.Empty;
    public DateTime Em { get; set; }
    public string? Ip { get; set; }

    public static RegistroAlteracao Novo(Guid escolinhaId, string entidade, Guid? registroId, string campo,
        string? antes, string? depois, DateTime em, Guid? autorId, string? autorNome, string? ip, string? motivo = null) =>
        new()
        {
            Escolinha_ID = escolinhaId,
            Entidade = TextoHelper.Truncar(entidade, TamanhoEntidade) ?? string.Empty,
            Registro_ID = registroId,
            Campo = TextoHelper.Truncar(campo, TamanhoCampo) ?? string.Empty,
            ValorAnterior = TextoHelper.Truncar(antes, TamanhoValor),
            ValorNovo = TextoHelper.Truncar(depois, TamanhoValor),
            Motivo = TextoHelper.Truncar(motivo, TamanhoMotivo),
            Em = em,
            Autor_ID = autorId,
            AutorNome = TextoHelper.Truncar(autorNome ?? "Sistema", TamanhoAutor) ?? "Sistema",
            Ip = TextoHelper.Truncar(ip, TamanhoIp)
        };
}
