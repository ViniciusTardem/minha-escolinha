using System.Text.RegularExpressions;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Mensageria;

/// <summary>Variação de texto do mesmo gatilho (RF-MSG-003). Os mesmos nomes da opção de tom dos parâmetros.</summary>
public enum TomMensagem
{
    Cordial = 10,
    Direto = 20
}

/// <summary>Situação do template na Meta: só o aprovado pode ser enviado pela API oficial.</summary>
public enum SituacaoAprovacaoTemplate
{
    Rascunho = 10,
    EmAnalise = 20,
    Aprovado = 30,
    Rejeitado = 40
}

/// <summary>
/// Template de WhatsApp do catálogo (BL-M09-03): um por gatilho e tom, com a categoria da política do WhatsApp e a
/// situação da aprovação na Meta. É da plataforma, não da escolinha: os templates pertencem à conta WhatsApp Business da
/// LDNA (P14) e só mudam com nova aprovação. O corpo usa {{1}}, {{2}}..., na ordem de <see cref="Variaveis"/>.
/// </summary>
public class TemplateMensagem : IEntidadeAuditavel
{
    public const int TamanhoNome = 120;
    public const int TamanhoIdioma = 10;
    public const int TamanhoCorpo = 1024;
    public const int TamanhoVariaveis = 300;
    public const int TamanhoBotoes = 200;
    public const int TamanhoObservacao = 500;

    public Guid TemplateMensagem_ID { get; set; }
    public GatilhoMensagem Gatilho { get; set; }
    public TomMensagem Tom { get; set; }
    public CategoriaTemplate Categoria { get; set; }

    /// <summary>Nome do template na Meta (minúsculas e sublinhado), com a versão: um texto novo é um template novo.</summary>
    public string NomeNaMeta { get; set; } = string.Empty;

    public string Idioma { get; set; } = "pt_BR";
    public string Corpo { get; set; } = string.Empty;

    /// <summary>Os nomes dos parâmetros, separados por vírgula, na ordem dos {{n}} (ex.: "responsavel,aluno,turma").</summary>
    public string Variaveis { get; set; } = string.Empty;

    /// <summary>Botões de resposta rápida, separados por "|" (ex.: "Vai|Não vai"). Vazio = sem botão.</summary>
    public string? Botoes { get; set; }

    public SituacaoAprovacaoTemplate Situacao { get; set; } = SituacaoAprovacaoTemplate.Rascunho;

    /// <summary>Motivo da rejeição da Meta ou nota da equipe.</summary>
    public string? Observacao { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public IReadOnlyList<string> NomesVariaveis =>
        Variaveis.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> ListaBotoes =>
        string.IsNullOrWhiteSpace(Botoes) ? [] : Botoes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>O texto com os valores no lugar dos {{n}}, na ordem das variáveis.</summary>
    public string Renderizar(IReadOnlyDictionary<string, string> valores) =>
        RenderizadorTemplate.Renderizar(Corpo, ValoresEmOrdem(valores));

    /// <summary>Os valores na ordem dos {{n}} (o que a API do WhatsApp recebe). Variável sem valor vira "—".</summary>
    public IReadOnlyList<string> ValoresEmOrdem(IReadOnlyDictionary<string, string> valores) =>
        NomesVariaveis.Select(n => valores.TryGetValue(n, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : "—").ToList();

    public static string Rotulo(SituacaoAprovacaoTemplate situacao) => situacao switch
    {
        SituacaoAprovacaoTemplate.Rascunho => "Rascunho",
        SituacaoAprovacaoTemplate.EmAnalise => "Em análise na Meta",
        SituacaoAprovacaoTemplate.Aprovado => "Aprovado",
        SituacaoAprovacaoTemplate.Rejeitado => "Rejeitado",
        _ => situacao.ToString()
    };

    public static string Rotulo(CategoriaTemplate categoria) => categoria switch
    {
        CategoriaTemplate.Utilidade => "Utilidade",
        CategoriaTemplate.Autenticacao => "Autenticação",
        CategoriaTemplate.Marketing => "Marketing",
        _ => categoria.ToString()
    };
}

public static partial class RenderizadorTemplate
{
    [GeneratedRegex(@"\{\{(\d+)\}\}")]
    private static partial Regex Marcador();

    /// <summary>Troca cada {{n}} pelo n-ésimo valor (1 = o primeiro). Marcador sem valor fica como está (defeito visível).</summary>
    public static string Renderizar(string corpo, IReadOnlyList<string> valores) =>
        Marcador().Replace(corpo, m =>
        {
            var indice = int.Parse(m.Groups[1].Value) - 1;
            return indice >= 0 && indice < valores.Count ? valores[indice] : m.Value;
        });

    /// <summary>Os números dos marcadores usados no corpo, sem repetição.</summary>
    public static IReadOnlyList<int> Marcadores(string corpo) =>
        Marcador().Matches(corpo).Select(m => int.Parse(m.Groups[1].Value)).Distinct().OrderBy(n => n).ToList();
}

/// <summary>
/// Os textos iniciais do catálogo, gravados no startup quando faltam (a equipe edita e submete à Meta depois).
/// <b>Provisórios</b> (P02): a categoria de cada um precisa da validação do responsável antes da submissão.
/// O nome da escolinha entra como sujeito ("{{2}} informa"), sem artigo: "a escolinha Escolinha X" repetiria a palavra.
/// </summary>
public static class TemplatesPadrao
{
    public sealed record Padrao(GatilhoMensagem Gatilho, TomMensagem Tom, string NomeNaMeta, string Corpo, string Variaveis, string? Botoes = null);

    public static readonly IReadOnlyList<Padrao> Todos =
    [
        new(GatilhoMensagem.FaltasAtingidas, TomMensagem.Cordial, "faltas_atingidas_cordial_v1",
            "Olá, {{1}}! {{2}} informa: {{3}} teve {{4}} em {{5}} nos treinos da turma {{6}}. Está tudo bem? " +
            "Se precisar, fale com a equipe.",
            "responsavel,escolinha,aluno,faltas,periodo,turma"),
        new(GatilhoMensagem.FaltasAtingidas, TomMensagem.Direto, "faltas_atingidas_direto_v1",
            "Atenção, {{1}}: {{2}} informa que {{3}} já soma {{4}} em {{5}} na turma {{6}}. Por favor, entre em contato com a equipe.",
            "responsavel,escolinha,aluno,faltas,periodo,turma"),
        new(GatilhoMensagem.TreinoCancelado, TomMensagem.Cordial, "treino_cancelado_v1",
            "Olá, {{1}}! {{2}} informa: o treino da turma {{3}} de {{4}} foi cancelado. Motivo: {{5}}. Aviso para: {{6}}.",
            "responsavel,escolinha,turma,quando,motivo,alunos"),
        new(GatilhoMensagem.TreinoRemarcado, TomMensagem.Cordial, "treino_remarcado_v1",
            "Olá, {{1}}! {{2}} informa: o treino da turma {{3}} de {{4}} mudou para {{5}}. Aviso para: {{6}}.",
            "responsavel,escolinha,turma,de,para,alunos"),
        new(GatilhoMensagem.MensalidadeAVencer, TomMensagem.Cordial, "mensalidade_a_vencer_v1",
            "Olá, {{1}}! A mensalidade de {{2}} ({{3}}) vence em {{4}}. Para pagar, acesse {{5}} e escolha Pix, boleto ou cartão.",
            "responsavel,aluno,valor,vencimento,link"),
        new(GatilhoMensagem.MensalidadeVencida, TomMensagem.Cordial, "mensalidade_vencida_cordial_v1",
            "Olá, {{1}}! A mensalidade de {{2}} ({{3}}), com vencimento em {{4}}, ainda está em aberto. " +
            "Para pagar, acesse {{5}} e escolha Pix, boleto ou cartão.",
            "responsavel,aluno,valor,vencimento,link"),
        new(GatilhoMensagem.MensalidadeVencida, TomMensagem.Direto, "mensalidade_vencida_direto_v1",
            "Atenção, {{1}}: a mensalidade de {{2}} ({{3}}) está vencida desde {{4}}. Regularize pelo link {{5}} o quanto antes.",
            "responsavel,aluno,valor,vencimento,link"),
        new(GatilhoMensagem.PagamentoConfirmado, TomMensagem.Cordial, "pagamento_confirmado_v1",
            "Olá, {{1}}! {{2}} confirma o pagamento da mensalidade de {{3}} ({{4}}). Agradecemos!",
            "responsavel,escolinha,aluno,valor"),
        new(GatilhoMensagem.ConvocacaoCriada, TomMensagem.Cordial, "convocacao_v1",
            "Olá, {{1}}! Convocação de {{2}}: {{3}}, {{4}}, no local {{5}} ({{6}}). Confirma a presença?",
            "responsavel,aluno,jogo,quando,local,mapa", "Vai|Não vai"),
        new(GatilhoMensagem.ConvocacaoSemResposta, TomMensagem.Cordial, "convocacao_lembrete_v1",
            "Olá, {{1}}! Ainda falta a resposta da convocação de {{2}} para {{3}}, {{4}}. Confirma a presença?",
            "responsavel,aluno,jogo,quando", "Vai|Não vai"),
        new(GatilhoMensagem.EventoAlterado, TomMensagem.Cordial, "jogo_alterado_v1",
            "Atenção, {{1}}: {{2}} mudou. Agora: {{3}}, no local {{4}} ({{5}}). Aviso para: {{6}}.",
            "responsavel,jogo,quando,local,mapa,alunos"),
        new(GatilhoMensagem.EventoCancelado, TomMensagem.Cordial, "jogo_cancelado_v1",
            "Atenção, {{1}}: {{2}} de {{3}} foi cancelado. Motivo: {{4}}. Aviso para: {{5}}.",
            "responsavel,jogo,quando,motivo,alunos"),
        new(GatilhoMensagem.ConviteAcesso, TomMensagem.Cordial, "convite_acesso_v1",
            "Olá, {{1}}! {{2}} convidou você para acompanhar tudo pelo Minha Escolinha. Ative o seu acesso por este " +
            "link: {{3}} (vale até {{4}}). Se não reconhece este convite, ignore esta mensagem.",
            "nome,escolinha,link,validade")
    ];

    public static TemplateMensagem Criar(Padrao p) => new()
    {
        TemplateMensagem_ID = Guid.NewGuid(),
        Gatilho = p.Gatilho,
        Tom = p.Tom,
        Categoria = CatalogoGatilhos.Obter(p.Gatilho).Categoria,
        NomeNaMeta = p.NomeNaMeta,
        Corpo = p.Corpo,
        Variaveis = p.Variaveis,
        Botoes = p.Botoes,
        Situacao = SituacaoAprovacaoTemplate.Rascunho
    };
}
