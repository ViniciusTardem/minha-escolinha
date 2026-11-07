using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Alunos;

/// <summary>As finalidades do consentimento (R6): tratamento de dados separado do uso de imagem.</summary>
public enum FinalidadeConsentimento
{
    TratamentoDados = 10,
    UsoImagem = 20
}

public enum OrigemConsentimento
{
    /// <summary>Termo assinado em papel, guardado pela escolinha.</summary>
    TermoEmPapel = 10,

    /// <summary>O responsável autorizou por mensagem ou pessoalmente, e o gestor registrou.</summary>
    DeclaracaoAoGestor = 20,

    /// <summary>Pelo portal do responsável (S6).</summary>
    Portal = 30,

    /// <summary>Declaração do gestor na carga inicial (S7).</summary>
    Importacao = 40,

    /// <summary>O próprio responsável respondeu pelo link de aceite (D111), com o CPF conferido.</summary>
    LinkDoResponsavel = 50
}

/// <summary>O estado de uma finalidade para um aluno, lido do último registro.</summary>
public enum EstadoConsentimento
{
    /// <summary>Nenhum registro ainda.</summary>
    Pendente = 0,
    Concedido = 10,
    Recusado = 20,
    Revogado = 30
}

/// <summary>
/// Consentimento registrado na matrícula e a revogação dele (BL-M03-09, R6, RF-LGP-001/002): finalidade, texto (versão),
/// quem autorizou, como, data e hora, e quem registrou. Nada é apagado: conceder de novo depois de revogar é outro registro.
/// A revogação do uso de imagem não para a operação essencial — cobrança, presença e convocação não consultam essa finalidade.
/// </summary>
public class ConsentimentoAluno : IEntidadeDaEscolinha
{
    public const int TamanhoVersao = 20;
    public const int TamanhoAutor = 150;
    public const int TamanhoMotivo = 300;

    public Guid ConsentimentoAluno_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Aluno_ID { get; set; }
    public FinalidadeConsentimento Finalidade { get; set; }
    public bool Concedido { get; set; }

    /// <summary>A versão do texto do termo que foi apresentado (<see cref="TermosConsentimento"/>).</summary>
    public string VersaoTermo { get; set; } = string.Empty;

    /// <summary>Quem autorizou (ou recusou).</summary>
    public Guid Responsavel_ID { get; set; }
    public Responsavel? Responsavel { get; set; }

    public OrigemConsentimento Origem { get; set; }

    public DateTime RegistradoEm { get; set; }
    public Guid? RegistradoPor_ID { get; set; }
    public string RegistradoPorNome { get; set; } = string.Empty;

    public DateTime? RevogadoEm { get; private set; }
    public Guid? RevogadoPor_ID { get; private set; }
    public string? RevogadoPorNome { get; private set; }
    public string? MotivoRevogacao { get; private set; }

    public EstadoConsentimento Estado =>
        !Concedido ? EstadoConsentimento.Recusado
        : RevogadoEm is null ? EstadoConsentimento.Concedido
        : EstadoConsentimento.Revogado;

    public void Revogar(DateTime agora, Guid? autorId, string? autorNome, string? motivo)
    {
        if (Estado != EstadoConsentimento.Concedido) throw new InvalidOperationException("Só um consentimento concedido pode ser revogado.");
        RevogadoEm = agora;
        RevogadoPor_ID = autorId;
        RevogadoPorNome = TextoHelper.Truncar(autorNome ?? "Sistema", TamanhoAutor);
        MotivoRevogacao = TextoHelper.Truncar(motivo, TamanhoMotivo);
    }

    /// <summary>O estado atual de cada finalidade: o registro mais recente manda.</summary>
    public static EstadoConsentimento EstadoAtual(IEnumerable<ConsentimentoAluno> registros, FinalidadeConsentimento finalidade) =>
        registros.Where(r => r.Finalidade == finalidade).OrderByDescending(r => r.RegistradoEm).FirstOrDefault()?.Estado
        ?? EstadoConsentimento.Pendente;
}

/// <summary>Um termo de consentimento: o texto apresentado ao responsável e a versão dele.</summary>
public sealed record TermoConsentimento(FinalidadeConsentimento Finalidade, string Titulo, string Versao, string Texto);

/// <summary>
/// Os textos dos termos, versionados. <b>Provisórios</b> (pendência P29): precisam da revisão jurídica da LDNA antes do piloto.
/// Mudar um texto = subir a versão; o registro guarda a versão que o responsável viu.
/// </summary>
public static class TermosConsentimento
{
    public static readonly IReadOnlyList<TermoConsentimento> Todos =
    [
        new(FinalidadeConsentimento.TratamentoDados, "Tratamento de dados", "1-provisorio",
            "Autorizo a escolinha a tratar os dados pessoais do aluno — identificação, contato dos responsáveis, frequência " +
            "e as informações de saúde necessárias à prática esportiva — para matrícula, treinos, jogos, comunicação com a " +
            "família e cobrança da mensalidade, nos termos da LGPD. As informações de saúde ficam restritas ao gestor, ao " +
            "professor da turma e aos responsáveis do aluno."),
        new(FinalidadeConsentimento.UsoImagem, "Uso de imagem", "1-provisorio",
            "Autorizo a escolinha a usar a imagem do aluno (fotos e vídeos de treinos e jogos) nos seus canais de comunicação. " +
            "Posso revogar a qualquer momento; a revogação vale dali em diante e não afeta matrícula, cobrança, presença nem convocação.")
    ];

    public static TermoConsentimento Obter(FinalidadeConsentimento finalidade) => Todos.First(t => t.Finalidade == finalidade);

    public static string Rotulo(FinalidadeConsentimento finalidade) => Obter(finalidade).Titulo;

    public static string Rotulo(OrigemConsentimento origem) => origem switch
    {
        OrigemConsentimento.TermoEmPapel => "Termo assinado",
        OrigemConsentimento.DeclaracaoAoGestor => "Autorização registrada pelo gestor",
        OrigemConsentimento.Portal => "Portal do responsável",
        OrigemConsentimento.Importacao => "Carga inicial",
        OrigemConsentimento.LinkDoResponsavel => "Aceite pelo link do responsável",
        _ => origem.ToString()
    };
}
