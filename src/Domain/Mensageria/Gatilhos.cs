namespace MinhaEscolinha.Domain.Mensageria;

/// <summary>
/// O catálogo fechado de disparos automáticos desta versão (spec §8.3, BL-M09-14). Nenhum outro envio automático
/// existe (R3, CA-20): os módulos só emitem estes gatilhos, e um motor único envia. Valores gravados no banco.
/// </summary>
public enum GatilhoMensagem
{
    /// <summary>Regra de faltas atingida (S3).</summary>
    FaltasAtingidas = 10,

    /// <summary>Treino cancelado (S1, avisado pelo motor desde o S2).</summary>
    TreinoCancelado = 20,

    /// <summary>Treino remarcado: a remarcação de uma sessão (BL-M04-07) e a mudança do horário da turma (P33).</summary>
    TreinoRemarcado = 25,

    /// <summary>Mensalidade a vencer (S4).</summary>
    MensalidadeAVencer = 30,

    /// <summary>Mensalidade vencida, conforme a régua (S4).</summary>
    MensalidadeVencida = 40,

    /// <summary>Pagamento confirmado na conciliação (S4).</summary>
    PagamentoConfirmado = 50,

    /// <summary>Convocação criada (S5): espera sim/não.</summary>
    ConvocacaoCriada = 60,

    /// <summary>Convocação sem resposta, antes do evento (S5): espera sim/não.</summary>
    ConvocacaoSemResposta = 70,

    /// <summary>Evento alterado (S5).</summary>
    EventoAlterado = 80,

    /// <summary>Evento cancelado (S5).</summary>
    EventoCancelado = 85,

    /// <summary>Convite de acesso ao responsável ou ao professor (S2).</summary>
    ConviteAcesso = 90,

    /// <summary>Os termos de consentimento para o 1º responsável aceitar pelo link (D111): só por e-mail.</summary>
    TermosConsentimento = 95
}

/// <summary>Quem recebe, como a §8.3 diz. Nunca o aluno (R4): o aluno nem tem telefone no cadastro.</summary>
public enum DestinatarioGatilho
{
    ResponsaveisDoAluno = 10,
    ResponsaveisDaTurma = 20,
    ResponsavelFinanceiro = 30,
    ResponsaveisDosConvocados = 40,
    ResponsaveisPendentes = 50,
    Convidado = 60,
    PrimeiroResponsavel = 70
}

/// <summary>As categorias de template aceitas pela política do WhatsApp (spec §8.2).</summary>
public enum CategoriaTemplate
{
    Utilidade = 10,
    Autenticacao = 20,
    Marketing = 30
}

/// <summary>Uma linha do catálogo: a regra de um gatilho.</summary>
/// <param name="Essencial">Retida na fila quando os créditos acabam, em vez de suprimida (spec §8.2: cobrança, convocação, cancelamento).</param>
/// <param name="PodeSerUrgente">Escapa da janela de silêncio quando o compromisso é nas próximas 24 horas (spec §8.2).</param>
/// <param name="AvisoNoPortal">Também vira aviso no portal para quem ativou o acesso (modo parcial, BL-M12-11).</param>
/// <param name="SoPorEmail">Sai só por e-mail, sem template da Meta nem WhatsApp (os termos de consentimento, D111).</param>
public sealed record RegraGatilho(
    GatilhoMensagem Gatilho,
    string Nome,
    string Evento,
    DestinatarioGatilho Destinatario,
    string Quando,
    bool EsperaResposta,
    string Fase,
    CategoriaTemplate Categoria,
    bool Essencial,
    bool PodeSerUrgente,
    bool AvisoNoPortal,
    bool SoPorEmail = false)
{
    public string RotuloDestinatario => Destinatario switch
    {
        DestinatarioGatilho.ResponsaveisDoAluno => "Responsáveis do aluno",
        DestinatarioGatilho.ResponsaveisDaTurma => "Responsáveis da turma",
        DestinatarioGatilho.ResponsavelFinanceiro => "Responsável financeiro",
        DestinatarioGatilho.ResponsaveisDosConvocados => "Responsáveis dos convocados",
        DestinatarioGatilho.ResponsaveisPendentes => "Responsáveis que não responderam",
        DestinatarioGatilho.Convidado => "Responsável ou professor convidado",
        DestinatarioGatilho.PrimeiroResponsavel => "1º responsável do aluno",
        _ => Destinatario.ToString()
    };
}

/// <summary>A lista fechada da §8.3, uma regra por gatilho (teste 1:1 com o enum).</summary>
public static class CatalogoGatilhos
{
    public static readonly IReadOnlyList<RegraGatilho> Todas =
    [
        new(GatilhoMensagem.FaltasAtingidas, "Faltas", "Regra de faltas atingida", DestinatarioGatilho.ResponsaveisDoAluno,
            "No registro da falta que atinge a regra", false, "S3", CategoriaTemplate.Utilidade, false, false, true),
        new(GatilhoMensagem.TreinoCancelado, "Treino cancelado", "Treino cancelado", DestinatarioGatilho.ResponsaveisDaTurma,
            "Imediato", false, "S2", CategoriaTemplate.Utilidade, true, true, true),
        new(GatilhoMensagem.TreinoRemarcado, "Treino remarcado", "Treino remarcado", DestinatarioGatilho.ResponsaveisDaTurma,
            "Imediato", false, "S1 (BL-M04-07, P33)", CategoriaTemplate.Utilidade, true, true, true),
        new(GatilhoMensagem.MensalidadeAVencer, "Mensalidade a vencer", "Mensalidade a vencer", DestinatarioGatilho.ResponsavelFinanceiro,
            "Janela configurável antes do vencimento", false, "S4", CategoriaTemplate.Utilidade, true, false, true),
        new(GatilhoMensagem.MensalidadeVencida, "Mensalidade vencida", "Mensalidade vencida", DestinatarioGatilho.ResponsavelFinanceiro,
            "No vencimento e conforme a régua", false, "S4", CategoriaTemplate.Utilidade, true, false, true),
        new(GatilhoMensagem.PagamentoConfirmado, "Pagamento confirmado", "Pagamento confirmado", DestinatarioGatilho.ResponsavelFinanceiro,
            "Na conciliação", false, "S4", CategoriaTemplate.Utilidade, false, false, true),
        new(GatilhoMensagem.ConvocacaoCriada, "Convocação", "Convocação criada", DestinatarioGatilho.ResponsaveisDosConvocados,
            "No disparo ou na janela configurada", true, "S5", CategoriaTemplate.Utilidade, true, false, true),
        new(GatilhoMensagem.ConvocacaoSemResposta, "Convocação sem resposta", "Convocação sem resposta", DestinatarioGatilho.ResponsaveisPendentes,
            "Janela configurável antes do evento", true, "S5", CategoriaTemplate.Utilidade, true, false, true),
        new(GatilhoMensagem.EventoAlterado, "Jogo alterado", "Evento alterado", DestinatarioGatilho.ResponsaveisDosConvocados,
            "Imediato (urgente)", false, "S5", CategoriaTemplate.Utilidade, true, true, true),
        new(GatilhoMensagem.EventoCancelado, "Jogo cancelado", "Evento cancelado", DestinatarioGatilho.ResponsaveisDosConvocados,
            "Imediato (urgente)", false, "S5", CategoriaTemplate.Utilidade, true, true, true),
        new(GatilhoMensagem.ConviteAcesso, "Convite de acesso", "Convite de acesso", DestinatarioGatilho.Convidado,
            "No convite", false, "S2", CategoriaTemplate.Utilidade, false, false, false),
        new(GatilhoMensagem.TermosConsentimento, "Termos de consentimento", "Aluno cadastrado ou termos reenviados",
            DestinatarioGatilho.PrimeiroResponsavel, "No cadastro do aluno ou no reenvio", false, "Melhoria (D111)", CategoriaTemplate.Utilidade,
            false, false, false, SoPorEmail: true)
    ];

    private static readonly Dictionary<GatilhoMensagem, RegraGatilho> PorGatilho = Todas.ToDictionary(r => r.Gatilho);

    public static RegraGatilho Obter(GatilhoMensagem gatilho) =>
        PorGatilho.TryGetValue(gatilho, out var regra)
            ? regra
            : throw new InvalidOperationException($"Gatilho fora do catálogo: {gatilho}.");

    public static string Rotulo(GatilhoMensagem gatilho) => Obter(gatilho).Nome;
}

/// <summary>
/// O pedido para não receber mais avisos (P36): a resposta que é só uma destas palavras, sem acento e sem pontuação. A
/// família continua podendo escrever "não vou poder sair mais cedo": só a palavra sozinha conta.
/// </summary>
public static class Descadastro
{
    public static readonly IReadOnlyList<string> Palavras = ["sair", "parar", "stop", "descadastrar"];

    public static bool Pediu(string? texto)
    {
        var limpo = new string(Comum.TextoHelper.NormalizarParaBusca(texto).Where(c => char.IsLetter(c) || c == 32).ToArray()).Trim();
        return Palavras.Contains(limpo);
    }
}
