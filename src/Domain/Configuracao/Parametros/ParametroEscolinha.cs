using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Configuracao.Parametros;

/// <summary>
/// Valor de um parâmetro de regra numa escolinha (R7). Semeado com o padrão do catálogo quando a escolinha nasce;
/// a reconciliação do startup acrescenta os parâmetros novos. Toda alteração vai para o <c>RegistroAlteracao</c>.
/// </summary>
public class ParametroEscolinha : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoChave = 80;
    public const int TamanhoValor = 200;

    public Guid ParametroEscolinha_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public string Chave { get; set; } = string.Empty;

    /// <summary>Texto invariante validado por <see cref="ValidadorParametro"/>. Nulo só em parâmetro de turma sem padrão.</summary>
    public string? Valor { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }
}
