namespace MinhaEscolinha.Application.Mensageria;

/// <summary>
/// WhatsApp pela API oficial da Meta (Cloud API), seção <c>WhatsApp</c>. Uma conta WhatsApp Business da LDNA, com o nome
/// da escolinha no corpo das mensagens (P14). Token, segredo do app e token de verificação só em user-secrets ou variável
/// de ambiente. A trava de ambiente (<c>GuardaWhatsApp</c>) confere as combinações no startup.
/// </summary>
public sealed class OpcoesWhatsApp
{
    public const string Secao = "WhatsApp";

    /// <summary>Sem conta na Meta: o <c>/dev/whatsapp</c> faz o papel do celular das famílias. Nunca em produção.</summary>
    public const string ModoSimulado = "Simulado";

    /// <summary>API oficial. Fora de Production, só os <see cref="NumerosTeste"/> recebem (spec §2.3).</summary>
    public const string ModoApi = "Api";

    /// <summary>Assina os webhooks que o simulador manda. Não vale fora do modo Simulado.</summary>
    public const string SegredoAppSimulador = "segredo_simulador_minha_escolinha";

    public const string TokenVerificacaoSimulador = "verificacao_simulador_minha_escolinha";

    /// <summary>Desligado: o motor não manda WhatsApp (essenciais ficam retidas; as demais, suprimidas) e o webhook responde 503.</summary>
    public bool Habilitado { get; set; } = true;

    public string Modo { get; set; } = ModoSimulado;

    /// <summary>Endereço público do sistema: o link do convite e a URL do webhook.</summary>
    public string UrlPublica { get; set; } = "https://localhost:7186";

    /// <summary>Versão da Graph API (ex.: v21.0).</summary>
    public string VersaoApi { get; set; } = "v21.0";

    /// <summary>Id do número de telefone na Meta (phone_number_id).</summary>
    public string IdNumero { get; set; } = "";

    /// <summary>Token de acesso (usuário do sistema da Meta).</summary>
    public string TokenAcesso { get; set; } = "";

    /// <summary>Segredo do app na Meta: confere a assinatura X-Hub-Signature-256 dos webhooks.</summary>
    public string SegredoApp { get; set; } = "";

    /// <summary>Token que a Meta manda no handshake GET do webhook.</summary>
    public string TokenVerificacao { get; set; } = "";

    /// <summary>Fora de Production (modo Api): só estes números recebem; os demais ficam suprimidos, com o motivo.</summary>
    public string[] NumerosTeste { get; set; } = [];

    /// <summary>Segunda chave para mandar mensagem de verdade a qualquer número: o modo Api em Production só sobe com isto true.</summary>
    public bool ConfirmarProducao { get; set; }

    public int TimeoutSegundos { get; set; } = 20;

    public bool Simulado => string.Equals(Modo, ModoSimulado, StringComparison.OrdinalIgnoreCase);
    public bool Api => string.Equals(Modo, ModoApi, StringComparison.OrdinalIgnoreCase);

    public string SegredoAppEfetivo => Simulado && string.IsNullOrWhiteSpace(SegredoApp) ? SegredoAppSimulador : SegredoApp;

    public string TokenVerificacaoEfetivo =>
        Simulado && string.IsNullOrWhiteSpace(TokenVerificacao) ? TokenVerificacaoSimulador : TokenVerificacao;

    public const string CaminhoWebhook = "/api/v1/webhooks/whatsapp";

    public string UrlWebhook => $"{UrlPublica.TrimEnd('/')}{CaminhoWebhook}";

    public string LinkConvite(string token) => $"{UrlPublica.TrimEnd('/')}/Convite/{token}";

    public string Descricao => !Habilitado ? "Desligado"
        : Simulado ? "Simulador (sem conta na Meta)"
        : "API oficial do WhatsApp";
}
