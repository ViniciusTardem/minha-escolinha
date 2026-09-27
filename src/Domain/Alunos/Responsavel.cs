using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Alunos;

/// <summary>
/// Responsável (BL-M03-01): cadastro único por pessoa na escolinha, reaproveitado entre irmãos — um responsável com dois
/// filhos é um cadastro com dois vínculos (CA-1). O CPF deduplica sozinho (único na escolinha); telefone e nome só
/// sugerem, porque celular compartilhado é comum.
/// <para>
/// O telefone é o WhatsApp da família, o canal de piso (R2). O e-mail serve para recuperação de acesso e recibo,
/// <b>não</b> é canal de notificação (spec §5). A permissão de receber cobrança ou notificação mora no vínculo, não aqui.
/// </para>
/// <see cref="Pessoa_ID"/> é a identidade com acesso (portal): ligada na ativação do convite (S2), com o CPF conferido.
/// </summary>
public class Responsavel : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 150;
    public const int TamanhoEmail = 150;
    public const int TamanhoTelefone = 20;

    public Guid Responsavel_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    public string Nome { get; private set; } = string.Empty;
    public string NomeBusca { get; private set; } = string.Empty;
    public string? Cpf { get; set; }

    /// <summary>Celular (WhatsApp) em E.164.</summary>
    public string? Telefone { get; set; }

    public string? Email { get; set; }
    public Endereco Endereco { get; set; } = new();
    public Guid? Pessoa_ID { get; set; }

    public List<VinculoAlunoResponsavel> Vinculos { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public void DefinirNome(string nome)
    {
        Nome = nome.Trim();
        NomeBusca = TextoHelper.NormalizarParaBusca(Nome);
    }
}

public enum Parentesco
{
    Mae = 10,
    Pai = 20,
    Avo = 30,
    Tutor = 40,
    Outro = 90
}

/// <summary>
/// Vínculo aluno ↔ responsável (BL-M03-03): é aqui que mora a permissão, não no responsável.
/// <list type="bullet">
/// <item><see cref="RecebeCobranca"/>: é responsável financeiro (recebe a régua de cobrança, S4);</item>
/// <item><see cref="Pagador"/>: o responsável financeiro em nome de quem a cobrança é emitida na Asaas (um por aluno);</item>
/// <item><see cref="RecebeNotificacao"/>: recebe alertas de falta, convocações e avisos de treino.</item>
/// </list>
/// A verificação do vínculo para o portal (BL-M01-06) entra no S2, com o convite.
/// </summary>
public class VinculoAlunoResponsavel : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public Guid VinculoAlunoResponsavel_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public Aluno? Aluno { get; set; }
    public Guid Responsavel_ID { get; set; }
    public Responsavel? Responsavel { get; set; }

    public Parentesco Parentesco { get; set; }
    public bool Pagador { get; private set; }
    public bool RecebeCobranca { get; private set; }
    public bool RecebeNotificacao { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>O pagador sempre recebe a cobrança; quem não recebe cobrança não pode ser o pagador.</summary>
    public void DefinirFinanceiro(bool pagador, bool recebeCobranca)
    {
        Pagador = pagador;
        RecebeCobranca = recebeCobranca || pagador;
    }

    public static string Rotulo(Parentesco parentesco) => parentesco switch
    {
        Parentesco.Mae => "Mãe",
        Parentesco.Pai => "Pai",
        Parentesco.Avo => "Avó ou avô",
        Parentesco.Tutor => "Tutor legal",
        _ => "Outro"
    };
}
