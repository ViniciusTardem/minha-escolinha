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
/// <summary>
/// Verificação do vínculo (BL-M01-06): só vale para quem ativou o acesso. Enquanto pendente, o portal não mostra nada do
/// aluno (bloqueio total); o gestor confirma ou recusa. A comunicação pelo WhatsApp não depende dela: vai para o número
/// que a escolinha cadastrou.
/// </summary>
public enum VerificacaoVinculo
{
    /// <summary>O responsável ainda não tem acesso: não há o que verificar.</summary>
    NaoSolicitada = 0,

    Pendente = 10,
    Verificado = 20,
    Recusado = 30
}

public class VinculoAlunoResponsavel : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoMotivoRecusa = 300;

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

    public VerificacaoVinculo Verificacao { get; private set; } = VerificacaoVinculo.NaoSolicitada;
    public DateTime? VerificacaoSolicitadaEm { get; private set; }
    public DateTime? VerificadoEm { get; private set; }
    public Guid? VerificadoPor_ID { get; private set; }
    public string? VerificadoPorNome { get; private set; }
    public string? MotivoRecusa { get; private set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    /// <summary>O responsável ativou o acesso (ou já tinha, e o vínculo é novo): o gestor precisa confirmar.</summary>
    public void SolicitarVerificacao(DateTime agora)
    {
        if (Verificacao is VerificacaoVinculo.Pendente or VerificacaoVinculo.Verificado) return;
        Verificacao = VerificacaoVinculo.Pendente;
        VerificacaoSolicitadaEm = agora;
        MotivoRecusa = null;
    }

    public void Confirmar(DateTime agora, Guid? autorId, string autorNome)
    {
        if (Verificacao != VerificacaoVinculo.Pendente) throw new InvalidOperationException("Só o vínculo pendente é confirmado.");
        Verificacao = VerificacaoVinculo.Verificado;
        VerificadoEm = agora;
        VerificadoPor_ID = autorId;
        VerificadoPorNome = autorNome;
    }

    public void Recusar(DateTime agora, Guid? autorId, string autorNome, string motivo)
    {
        if (Verificacao != VerificacaoVinculo.Pendente) throw new InvalidOperationException("Só o vínculo pendente é recusado.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("A recusa exige motivo.", nameof(motivo));
        Verificacao = VerificacaoVinculo.Recusado;
        VerificadoEm = agora;
        VerificadoPor_ID = autorId;
        VerificadoPorNome = autorNome;
        MotivoRecusa = TextoHelper.Truncar(motivo, TamanhoMotivoRecusa);
    }

    /// <summary>
    /// Vínculo criado pelo gestor para quem já tem acesso: quem cria confirma (a verificação é sobre a pessoa que ativou
    /// o acesso, e o gestor está vinculando essa pessoa de propósito).
    /// </summary>
    public void ConfirmarNaCriacao(DateTime agora, Guid? autorId, string autorNome)
    {
        Verificacao = VerificacaoVinculo.Verificado;
        VerificacaoSolicitadaEm = agora;
        VerificadoEm = agora;
        VerificadoPor_ID = autorId;
        VerificadoPorNome = autorNome;
    }

    /// <summary>O portal mostra o aluno: vínculo confirmado pelo gestor.</summary>
    public bool Verificado => Verificacao == VerificacaoVinculo.Verificado;

    /// <summary>O pagador sempre recebe a cobrança; quem não recebe cobrança não pode ser o pagador.</summary>
    public void DefinirFinanceiro(bool pagador, bool recebeCobranca)
    {
        Pagador = pagador;
        RecebeCobranca = recebeCobranca || pagador;
    }

    public static string Rotulo(VerificacaoVinculo verificacao) => verificacao switch
    {
        VerificacaoVinculo.NaoSolicitada => "Sem acesso",
        VerificacaoVinculo.Pendente => "A confirmar",
        VerificacaoVinculo.Verificado => "Confirmado",
        VerificacaoVinculo.Recusado => "Recusado",
        _ => verificacao.ToString()
    };

    public static string Rotulo(Parentesco parentesco) => parentesco switch
    {
        Parentesco.Mae => "Mãe",
        Parentesco.Pai => "Pai",
        Parentesco.Avo => "Avó ou avô",
        Parentesco.Tutor => "Tutor legal",
        _ => "Outro"
    };
}
