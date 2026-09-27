namespace MinhaEscolinha.Application.Integracao.Stripe;

/// <summary>
/// O que a Minha Escolinha usa do Stripe: abrir uma sessão de pagamento (Checkout) e conferir a situação dela.
/// Implementado pelo cliente HTTP (modos Teste e Producao) e pelo simulador (modo Simulado).
/// </summary>
public interface IGatewayStripe
{
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<SessaoCheckoutCriada> CriarSessaoCheckoutAsync(PedidoCheckout pedido, CancellationToken ct = default);

    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<SessaoCheckoutConsultada> ConsultarSessaoAsync(string sessaoId, CancellationToken ct = default);
}

/// <summary>Um pagamento avulso em reais. <see cref="CompraId"/> vai como referência e, com a escolinha, nos metadados.</summary>
public sealed record PedidoCheckout(
    Guid CompraId,
    Guid EscolinhaId,
    string Descricao,
    long ValorCentavos,
    string UrlSucesso,
    string UrlCancelamento,
    string? EmailCliente);

public sealed record SessaoCheckoutCriada(string SessaoId, string Url, DateTime? ExpiraEmUtc);

/// <param name="Status">open · complete · expired (sessão).</param>
/// <param name="SituacaoPagamento">paid · unpaid · no_payment_required (sessão).</param>
/// <param name="SituacaoIntencao">status do PaymentIntent (succeeded, requires_action, requires_payment_method, canceled...).</param>
/// <param name="MeioPagamento">card · boleto · pix, quando o pagamento já tem meio.</param>
public sealed record SessaoCheckoutConsultada(
    string SessaoId,
    string Status,
    string SituacaoPagamento,
    string? PagamentoId,
    string? SituacaoIntencao,
    string? MeioPagamento,
    IReadOnlyDictionary<string, string> Metadados)
{
    public bool Paga => SituacaoPagamento is "paid" or "no_payment_required";
    public bool Concluida => Status == "complete";
    public bool Expirada => Status == "expired";

    /// <summary>A página foi concluída (boleto ou Pix emitido), mas o pagamento não veio: venceu ou foi recusado.</summary>
    public bool PagamentoRecusado => Concluida && !Paga && SituacaoIntencao is "requires_payment_method" or "canceled";
}

/// <summary>Metadados que marcam a sessão como nossa: a conta Stripe da LDNA também é usada para outras coisas.</summary>
public static class MetadadosStripe
{
    public const string Origem = "origem";
    public const string ValorOrigem = "minha-escolinha";
    public const string Escolinha = "escolinha_id";
    public const string Compra = "compra_id";
}

public static class EventosStripe
{
    public const string SessaoConcluida = "checkout.session.completed";
    public const string PagamentoAssincronoConfirmado = "checkout.session.async_payment_succeeded";
    public const string PagamentoAssincronoRecusado = "checkout.session.async_payment_failed";
    public const string SessaoExpirada = "checkout.session.expired";

    /// <summary>Os eventos a assinar no endpoint do Stripe; qualquer outro é gravado e ignorado.</summary>
    public static readonly string[] Tratados = [SessaoConcluida, PagamentoAssincronoConfirmado, PagamentoAssincronoRecusado, SessaoExpirada];
}
