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

    /// <summary>
    /// O nome da versão aprovada anterior, quando a equipe trocou a versão (P55). A mensagem que já estava na fila com esse
    /// nome ainda sai por ele: o template antigo continua aprovado na Meta.
    /// </summary>
    public string? NomeAnterior { get; set; }

    /// <summary>O envio com este nome sai por este template: o nome atual, se aprovado, ou o da versão aprovada anterior.</summary>
    public bool AprovadoParaEnvio(string nome) =>
        nome == NomeNaMeta ? Situacao == SituacaoAprovacaoTemplate.Aprovado : nome == NomeAnterior;

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
/// Os textos iniciais do catálogo, gravados no startup quando faltam (a equipe edita e submete à Meta depois). Todo
/// gatilho tem as duas variações de tom (P02): a escolinha escolhe o tom dos avisos nos parâmetros, as faltas têm o tom
/// próprio e a régua da mensalidade vencida sobe de cordial para direto.
/// <para>
/// O envio sai do número oficial da plataforma, não do número da escolinha (P02): todo texto diz de qual escolinha é a
/// mensagem, com o nome como sujeito ("{{2}} informa"), sem artigo ("a escolinha Escolinha X" repetiria a palavra). As
/// regras da Meta: nenhum texto começa ou termina numa variável, e há texto fixo bastante entre elas.
/// </para>
/// </summary>
public static class TemplatesPadrao
{
    public sealed record Padrao(GatilhoMensagem Gatilho, TomMensagem Tom, string NomeNaMeta, string Corpo, string Variaveis, string? Botoes = null);

    private const string Duvidas = " Qualquer dúvida, fale com a equipe da escolinha.";
    private const string DuvidasDireto = " Dúvidas, fale com a equipe da escolinha.";
    private const string SeJaPagou = " Se já pagou, desconsidere esta mensagem.";

    public static readonly IReadOnlyList<Padrao> Todos =
    [
        new(GatilhoMensagem.FaltasAtingidas, TomMensagem.Cordial, "faltas_atingidas_cordial_v1",
            "Olá, {{1}}! {{2}} informa: {{3}} teve {{4}} em {{5}} nos treinos da turma {{6}}. Está tudo bem? " +
            "Se precisar, fale com a equipe da escolinha.",
            "responsavel,escolinha,aluno,faltas,periodo,turma"),
        new(GatilhoMensagem.FaltasAtingidas, TomMensagem.Direto, "faltas_atingidas_direto_v1",
            "Atenção, {{1}}: {{2}} informa que {{3}} já soma {{4}} em {{5}} na turma {{6}}. Por favor, entre em contato com a " +
            "equipe da escolinha.",
            "responsavel,escolinha,aluno,faltas,periodo,turma"),

        new(GatilhoMensagem.TreinoCancelado, TomMensagem.Cordial, "treino_cancelado_v1",
            "Olá, {{1}}! {{2}} informa: o treino da turma {{3}} marcado para {{4}} foi cancelado. Motivo: {{5}}. " +
            "Este aviso é sobre {{6}}." + Duvidas,
            "responsavel,escolinha,turma,quando,motivo,alunos"),
        new(GatilhoMensagem.TreinoCancelado, TomMensagem.Direto, "treino_cancelado_direto_v1",
            "Atenção, {{1}}: {{2}} cancelou o treino da turma {{3}} de {{4}}. Motivo: {{5}}. Aviso sobre {{6}}." + DuvidasDireto,
            "responsavel,escolinha,turma,quando,motivo,alunos"),

        new(GatilhoMensagem.TreinoRemarcado, TomMensagem.Cordial, "treino_remarcado_v1",
            "Olá, {{1}}! {{2}} informa que o treino da turma {{3}} mudou de horário. Antes: {{4}}. Agora: {{5}}. " +
            "Este aviso é sobre {{6}}." + Duvidas,
            "responsavel,escolinha,turma,de,para,alunos"),
        new(GatilhoMensagem.TreinoRemarcado, TomMensagem.Direto, "treino_remarcado_direto_v1",
            "Atenção, {{1}}: {{2}} mudou o horário do treino da turma {{3}}. Antes: {{4}}. Agora: {{5}}. Aviso sobre {{6}}." +
            DuvidasDireto,
            "responsavel,escolinha,turma,de,para,alunos"),

        new(GatilhoMensagem.MensalidadeAVencer, TomMensagem.Cordial, "mensalidade_a_vencer_v1",
            "Olá, {{1}}! {{2}} lembra que a mensalidade de {{3}}, no valor de {{4}}, vence em {{5}}. Para pagar por Pix, boleto " +
            "ou cartão, acesse a fatura: {{6}}." + SeJaPagou,
            "responsavel,escolinha,aluno,valor,vencimento,link"),
        new(GatilhoMensagem.MensalidadeAVencer, TomMensagem.Direto, "mensalidade_a_vencer_direto_v1",
            "Atenção, {{1}}: {{2}} informa que a mensalidade de {{3}}, de {{4}}, vence em {{5}}. Fatura para pagamento: {{6}}." +
            SeJaPagou,
            "responsavel,escolinha,aluno,valor,vencimento,link"),

        new(GatilhoMensagem.MensalidadeVencida, TomMensagem.Cordial, "mensalidade_vencida_cordial_v1",
            "Olá, {{1}}! {{2}} informa que a mensalidade de {{3}}, no valor de {{4}}, com vencimento em {{5}}, ainda consta em " +
            "aberto. Para pagar por Pix, boleto ou cartão, acesse a fatura: {{6}}." + SeJaPagou,
            "responsavel,escolinha,aluno,valor,vencimento,link"),
        new(GatilhoMensagem.MensalidadeVencida, TomMensagem.Direto, "mensalidade_vencida_direto_v1",
            "Atenção, {{1}}: {{2}} informa que a mensalidade de {{3}}, de {{4}}, está vencida desde {{5}}. Regularize pela " +
            "fatura: {{6}}." + SeJaPagou,
            "responsavel,escolinha,aluno,valor,vencimento,link"),

        new(GatilhoMensagem.PagamentoConfirmado, TomMensagem.Cordial, "pagamento_confirmado_v1",
            "Olá, {{1}}! {{2}} confirma o recebimento do pagamento da mensalidade de {{3}}, no valor de {{4}}. Agradecemos, e " +
            "até o próximo treino!",
            "responsavel,escolinha,aluno,valor"),
        new(GatilhoMensagem.PagamentoConfirmado, TomMensagem.Direto, "pagamento_confirmado_direto_v1",
            "Pagamento confirmado, {{1}}: {{2}} recebeu o pagamento da mensalidade de {{3}}, de {{4}}. Agradecemos.",
            "responsavel,escolinha,aluno,valor"),

        new(GatilhoMensagem.ConvocacaoCriada, TomMensagem.Cordial, "convocacao_v1",
            "Olá, {{1}}! {{2}} convocou {{3}} para o {{4}}, {{5}}, no local {{6}}. Como chegar: {{7}}. Toque em Vai ou Não vai " +
            "para confirmar a presença.",
            "responsavel,escolinha,aluno,jogo,quando,local,mapa", "Vai|Não vai"),
        new(GatilhoMensagem.ConvocacaoCriada, TomMensagem.Direto, "convocacao_direto_v1",
            "Convocação, {{1}}: {{2}} chamou {{3}} para o {{4}}, {{5}}, no local {{6}}. Como chegar: {{7}}. Responda pelos " +
            "botões: Vai ou Não vai.",
            "responsavel,escolinha,aluno,jogo,quando,local,mapa", "Vai|Não vai"),

        new(GatilhoMensagem.ConvocacaoSemResposta, TomMensagem.Cordial, "convocacao_lembrete_v1",
            "Olá, {{1}}! {{2}} ainda aguarda a resposta da convocação de {{3}} para o {{4}}, {{5}}. Toque em Vai ou Não vai para " +
            "confirmar a presença.",
            "responsavel,escolinha,aluno,jogo,quando", "Vai|Não vai"),
        new(GatilhoMensagem.ConvocacaoSemResposta, TomMensagem.Direto, "convocacao_lembrete_direto_v1",
            "Lembrete, {{1}}: {{2}} precisa da resposta da convocação de {{3}} para o {{4}}, {{5}}. Responda pelos botões: Vai " +
            "ou Não vai.",
            "responsavel,escolinha,aluno,jogo,quando", "Vai|Não vai"),

        new(GatilhoMensagem.EventoAlterado, TomMensagem.Cordial, "jogo_alterado_v1",
            "Atenção, {{1}}: {{2}} informa que o {{3}} mudou. Novo horário: {{4}}, no local {{5}}. Como chegar: {{6}}. " +
            "Este aviso é sobre {{7}}." + Duvidas,
            "responsavel,escolinha,jogo,quando,local,mapa,alunos"),
        new(GatilhoMensagem.EventoAlterado, TomMensagem.Direto, "jogo_alterado_direto_v1",
            "Mudança de jogo, {{1}}: {{2}} alterou o {{3}}. Agora: {{4}}, no local {{5}}. Como chegar: {{6}}. Aviso sobre {{7}}." +
            DuvidasDireto,
            "responsavel,escolinha,jogo,quando,local,mapa,alunos"),

        new(GatilhoMensagem.EventoCancelado, TomMensagem.Cordial, "jogo_cancelado_v1",
            "Atenção, {{1}}: {{2}} informa que o {{3}}, marcado para {{4}}, foi {{5}}. Motivo: {{6}}. Este aviso é sobre {{7}}." +
            Duvidas,
            "responsavel,escolinha,jogo,quando,situacao,motivo,alunos"),
        new(GatilhoMensagem.EventoCancelado, TomMensagem.Direto, "jogo_cancelado_direto_v1",
            "Aviso de jogo, {{1}}: {{2}} informa que o {{3}} de {{4}} foi {{5}}. Motivo: {{6}}. Aviso sobre {{7}}." + DuvidasDireto,
            "responsavel,escolinha,jogo,quando,situacao,motivo,alunos"),

        new(GatilhoMensagem.ConviteAcesso, TomMensagem.Cordial, "convite_acesso_v1",
            "Olá, {{1}}! {{2}} convidou você para acompanhar tudo pelo Minha Escolinha. Ative o seu acesso por este " +
            "link: {{3}} (vale até {{4}}). Se não reconhece este convite, ignore esta mensagem.",
            "nome,escolinha,link,validade"),
        new(GatilhoMensagem.ConviteAcesso, TomMensagem.Direto, "convite_acesso_direto_v1",
            "Convite de acesso, {{1}}: {{2}} liberou o seu acesso ao Minha Escolinha. Ative pelo link {{3}}, válido até {{4}}. " +
            "Se não reconhece este convite, ignore esta mensagem.",
            "nome,escolinha,link,validade")
    ];

    /// <summary>
    /// O texto padrão mudou e o template ainda é rascunho que nenhuma pessoa editou (a alteração do startup não tem
    /// autor): o startup troca pelo texto novo. Nada foi submetido à Meta, e ninguém perde uma edição.
    /// </summary>
    public static bool AtualizarRascunho(TemplateMensagem template, Padrao padrao)
    {
        if (template.Situacao != SituacaoAprovacaoTemplate.Rascunho || template.UsuarioAtualizacao_ID is not null) return false;
        if (template.NomeNaMeta == padrao.NomeNaMeta && template.Corpo == padrao.Corpo && template.Variaveis == padrao.Variaveis
            && template.Botoes == padrao.Botoes)
            return false;
        template.NomeNaMeta = padrao.NomeNaMeta;
        template.Corpo = padrao.Corpo;
        template.Variaveis = padrao.Variaveis;
        template.Botoes = padrao.Botoes;
        return true;
    }

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
