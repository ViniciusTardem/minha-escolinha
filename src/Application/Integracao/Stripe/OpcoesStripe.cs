namespace MinhaEscolinha.Application.Integracao.Stripe;

/// <summary>
/// Stripe da LDNA: a plataforma cobra das escolinhas os pacotes de créditos (seção <c>Stripe</c>). As mensalidades dos
/// alunos continuam na Asaas (R1). Chave secreta e segredo do webhook só em user-secrets ou variável de ambiente.
/// A trava de ambiente (<c>GuardaStripe</c>) confere as combinações no startup.
/// </summary>
public sealed class OpcoesStripe
{
    public const string Secao = "Stripe";

    /// <summary>Sem conta: o <c>/dev/stripe</c> faz o papel do Stripe. Nunca em produção.</summary>
    public const string ModoSimulado = "Simulado";

    /// <summary>Modo de teste do Stripe (chave <c>sk_test_</c> ou <c>rk_test_</c>): cartões de teste, nada é cobrado.</summary>
    public const string ModoTeste = "Teste";

    /// <summary>Cobrança de verdade (chave <c>sk_live_</c> ou <c>rk_live_</c>). Só em Production, com confirmação.</summary>
    public const string ModoProducao = "Producao";

    /// <summary>Assina os webhooks que o simulador manda. Não vale fora do modo Simulado.</summary>
    public const string SegredoWebhookSimulador = "whsec_simulador_minha_escolinha";

    /// <summary>Desligado: a tela não oferece a compra, o webhook responde 503 e a varredura não roda.</summary>
    public bool Habilitado { get; set; } = true;

    public string Modo { get; set; } = ModoSimulado;

    /// <summary>Endereço público do sistema: volta do pagamento (sucesso e desistência) e URL do webhook.</summary>
    public string UrlPublica { get; set; } = "https://localhost:7186";

    public string ChaveSecreta { get; set; } = "";

    /// <summary>Segredo de assinatura do endpoint de webhook no Stripe (<c>whsec_...</c>).</summary>
    public string SegredoWebhook { get; set; } = "";

    /// <summary>Segunda chave para cobrar de verdade: o modo Producao só sobe com isto true.</summary>
    public bool ConfirmarProducao { get; set; }

    /// <summary>Versão da API do Stripe (cabeçalho Stripe-Version). Vazio = a versão padrão da conta.</summary>
    public string VersaoApi { get; set; } = "";

    /// <summary>Meios aceitos (card, boleto, pix). Vazio = os que estiverem ligados no painel do Stripe.</summary>
    public string[] MeiosPagamento { get; set; } = [];

    /// <summary>Dias para vencer o boleto (vazio = o padrão do Stripe, 3 dias).</summary>
    public int? DiasVencimentoBoleto { get; set; }

    public int TimeoutSegundos { get; set; } = 20;

    public bool Simulado => string.Equals(Modo, ModoSimulado, StringComparison.OrdinalIgnoreCase);
    public bool Teste => string.Equals(Modo, ModoTeste, StringComparison.OrdinalIgnoreCase);

    public string SegredoWebhookEfetivo =>
        Simulado && string.IsNullOrWhiteSpace(SegredoWebhook) ? SegredoWebhookSimulador : SegredoWebhook;

    public const string CaminhoWebhook = "/api/v1/webhooks/stripe";

    public string UrlWebhook => $"{UrlPublica.TrimEnd('/')}{CaminhoWebhook}";

    public string Descricao => !Habilitado ? "Desligado"
        : Simulado ? "Simulador (sem conta Stripe)"
        : Teste ? "Stripe — modo de teste"
        : "Stripe — produção";
}
