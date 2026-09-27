using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Configuracao;

/// <summary>
/// A conta principal (BL-M02-01): agrupa turmas, alunos, professores e finanças. Raiz de quase tudo — todo dado
/// de escolinha carrega o <see cref="Escolinha_ID"/> e passa pelo filtro de contexto.
/// Nasce junto com o primeiro gestor (ServicoPlataforma.CriarEscolinhaComGestorAsync), numa transação só.
/// </summary>
public class Escolinha : IEntidadeAuditavel
{
    public const int TamanhoNome = 150;
    public const int TamanhoRazaoSocial = 200;
    public const int TamanhoEmail = 150;
    public const int TamanhoTelefone = 20;

    public Guid Escolinha_ID { get; set; }

    /// <summary>Nome como a escolinha é conhecida (aparece nas mensagens às famílias).</summary>
    public string Nome { get; set; } = string.Empty;

    public string? RazaoSocial { get; set; }

    /// <summary>Só os 14 dígitos; opcional (escolinha informal ou MEI sem CNPJ à mão), validado quando informado.</summary>
    public string? Cnpj { get; set; }

    public Endereco Endereco { get; set; } = new();

    /// <summary>Telefone de contato em E.164.</summary>
    public string? Telefone { get; set; }

    public string? Email { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }
}
