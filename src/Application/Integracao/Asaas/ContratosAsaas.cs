using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Financeiro;

namespace MinhaEscolinha.Application.Integracao.Asaas;

/// <summary>
/// Asaas das mensalidades (seção <c>Asaas</c>): cada escolinha usa a própria conta (R1: o dinheiro cai na conta dela), com
/// a chave de API cifrada no banco (BL-M02-10). Aqui fica só o que é da instalação: o modo e o endereço público.
/// A trava de ambiente (<c>GuardaAsaas</c>) confere as combinações no startup.
/// </summary>
public sealed class OpcoesAsaas
{
    public const string Secao = "Asaas";

    /// <summary>Sem conta na Asaas: o <c>/dev/asaas</c> faz o papel dela. Nunca em produção.</summary>
    public const string ModoSimulado = "Simulado";

    /// <summary>A API da Asaas com a conta de cada escolinha (Sandbox ou Produção, conforme a conta vinculada).</summary>
    public const string ModoApi = "Api";

    public const string CaminhoWebhook = "/api/v1/webhooks/asaas";

    /// <summary>Desligado: nada é emitido, o webhook responde 503 e a varredura não roda.</summary>
    public bool Habilitado { get; set; } = true;

    public string Modo { get; set; } = ModoSimulado;

    /// <summary>Endereço público do sistema: vai no cadastro do webhook em cada conta Asaas.</summary>
    public string UrlPublica { get; set; } = "https://localhost:7186";

    /// <summary>E-mail que a Asaas avisa quando a fila de webhooks da conta é interrompida (opcional).</summary>
    public string EmailAvisosWebhook { get; set; } = "";

    /// <summary>Segunda chave para cobrar de verdade em produção (spec §2.3): o modo Api em Production só sobe com isto true.</summary>
    public bool ConfirmarProducao { get; set; }

    public int TimeoutSegundos { get; set; } = 20;

    public bool Simulado => string.Equals(Modo, ModoSimulado, StringComparison.OrdinalIgnoreCase);
    public bool Api => string.Equals(Modo, ModoApi, StringComparison.OrdinalIgnoreCase);

    public string UrlWebhook(Guid escolinhaId) => $"{UrlPublica.TrimEnd('/')}{CaminhoWebhook}/{escolinhaId}";

    public string Descricao => !Habilitado ? "Desligada"
        : Simulado ? "Simulador (sem conta na Asaas)"
        : "API da Asaas";
}

/// <summary>A conta Asaas da escolinha, com a chave aberta, para uma chamada (nunca vai para o log).</summary>
public sealed record ContaAsaasAcesso(Guid EscolinhaId, AmbienteAsaas Ambiente, string ChaveApi, Guid Identificador);

/// <param name="ResponsavelId">Vai como referência externa: o cliente é reencontrado em vez de duplicado.</param>
public sealed record ClienteAsaasPedido(Guid ResponsavelId, string Nome, string Cpf, string? Email, string? Celular);

/// <param name="CobrancaId">Vai como referência externa: a emissão repetida reencontra a cobrança em vez de criar outra.</param>
public sealed record CobrancaAsaasPedido(
    Guid CobrancaId,
    string ClienteId,
    decimal Valor,
    DateOnly Vencimento,
    string Descricao,
    decimal MultaPercentual,
    decimal JurosPercentualMes);

/// <summary>Uma cobrança como a Asaas devolve (só o que o sistema lê).</summary>
/// <param name="Status">PENDING, RECEIVED, CONFIRMED, OVERDUE, REFUNDED, RECEIVED_IN_CASH, REFUND_REQUESTED, CHARGEBACK_...</param>
/// <param name="Valor">O que foi (ou será) pago: no atraso, já com multa e juros.</param>
/// <param name="ValorOriginal">O valor original, quando a Asaas acrescentou multa e juros (vazio quando não mudou).</param>
/// <param name="PagaEm">Quando a família pagou (<c>clientPaymentDate</c>) ou, sem ela, quando a Asaas confirmou.</param>
/// <param name="Meio">BOLETO, PIX, CREDIT_CARD ou UNDEFINED.</param>
public sealed record PagamentoAsaas(
    string Id,
    string Status,
    decimal Valor,
    decimal? ValorOriginal,
    DateOnly? Vencimento,
    DateOnly? PagaEm,
    string? Meio,
    string? ReferenciaExterna,
    bool Excluida,
    string? UrlFatura)
{
    public decimal ValorDeReferencia => ValorOriginal ?? Valor;

    public bool Recebida => Status is StatusAsaas.Recebida or StatusAsaas.Confirmada or StatusAsaas.RecebidaEmDinheiro;

    public MeioPagamento MeioDoPagamento => Status == StatusAsaas.RecebidaEmDinheiro
        ? MeioPagamento.Dinheiro
        : Meio switch
        {
            "PIX" => MeioPagamento.Pix,
            "BOLETO" => MeioPagamento.Boleto,
            "CREDIT_CARD" or "DEBIT_CARD" => MeioPagamento.Cartao,
            _ => MeioPagamento.Indefinido
        };

    public Guid? CobrancaId => Guid.TryParse(ReferenciaExterna, out var id) ? id : null;
}

public static class StatusAsaas
{
    public const string Pendente = "PENDING";
    public const string Recebida = "RECEIVED";
    public const string Confirmada = "CONFIRMED";
    public const string Vencida = "OVERDUE";
    public const string Estornada = "REFUNDED";
    public const string RecebidaEmDinheiro = "RECEIVED_IN_CASH";
    public const string EstornoPedido = "REFUND_REQUESTED";
    public const string EstornoEmAndamento = "REFUND_IN_PROGRESS";
    public const string ChargebackPedido = "CHARGEBACK_REQUESTED";
    public const string ChargebackEmDisputa = "CHARGEBACK_DISPUTE";
    public const string AguardandoReversaoChargeback = "AWAITING_CHARGEBACK_REVERSAL";

    public static bool Estorno(string status) => status is Estornada or EstornoPedido or EstornoEmAndamento;
    public static bool Chargeback(string status) => status is ChargebackPedido or ChargebackEmDisputa or AguardandoReversaoChargeback;
}

/// <summary>
/// O que a Minha Escolinha usa da Asaas, sempre com a conta da escolinha (R1): cliente (o pagador), cobrança com a fatura
/// que aceita Pix, boleto e cartão, a situação dela, a exclusão, o recebimento em dinheiro e o cadastro do webhook.
/// Implementado pelo cliente HTTP (modo Api) e pelo simulador (modo Simulado), com a mesma interface (spec §8.1).
/// </summary>
public interface IGatewayAsaas
{
    /// <returns>O id do cliente com esta referência externa, ou <c>null</c>.</returns>
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<string?> BuscarClienteAsync(ContaAsaasAcesso conta, Guid responsavelId, CancellationToken ct = default);

    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<string> CriarClienteAsync(ContaAsaasAcesso conta, ClienteAsaasPedido pedido, CancellationToken ct = default);

    /// <returns>A cobrança com esta referência externa (a nossa), ou <c>null</c>.</returns>
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<PagamentoAsaas?> BuscarCobrancaAsync(ContaAsaasAcesso conta, Guid cobrancaId, CancellationToken ct = default);

    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<PagamentoAsaas> CriarCobrancaAsync(ContaAsaasAcesso conta, CobrancaAsaasPedido pedido, CancellationToken ct = default);

    /// <returns>A cobrança, ou <c>null</c> se ela não existe nesta conta.</returns>
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task<PagamentoAsaas?> ObterCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default);

    /// <summary>Exclui a cobrança pendente (a fatura deixa de valer). Cobrança que já não existe não é erro.</summary>
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task ExcluirCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default);

    /// <summary>Marca na Asaas o recebimento feito por fora (dinheiro), sem avisar a família pela Asaas.</summary>
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task ReceberEmDinheiroAsync(ContaAsaasAcesso conta, string id, DateOnly data, decimal valor, CancellationToken ct = default);

    /// <summary>Cadastra (ou atualiza) o webhook de cobranças da conta, apontando para a Minha Escolinha, com o token.</summary>
    /// <exception cref="Comum.FalhaIntegracaoException">Classificada (retentável ou definitiva).</exception>
    Task RegistrarWebhookAsync(ContaAsaasAcesso conta, string url, string token, string? email, CancellationToken ct = default);
}

/// <summary>
/// Webhook da Asaas: <c>{ id: evt_..., event: PAYMENT_RECEIVED, payment: {...} }</c>, com o token da conta no cabeçalho
/// <c>asaas-access-token</c>. O id do evento é a chave de idempotência; a ordem dos eventos não é garantida.
/// </summary>
public sealed record EventoAsaas(string Id, string Tipo, PagamentoAsaas? Pagamento)
{
    public const string CabecalhoToken = "asaas-access-token";

    public const string PagamentoRecebido = "PAYMENT_RECEIVED";
    public const string PagamentoConfirmado = "PAYMENT_CONFIRMED";
    public const string PagamentoVencido = "PAYMENT_OVERDUE";
    public const string PagamentoExcluido = "PAYMENT_DELETED";
    public const string PagamentoEstornado = "PAYMENT_REFUNDED";
    public const string PagamentoAtualizado = "PAYMENT_UPDATED";
    public const string PagamentoCriado = "PAYMENT_CREATED";
    public const string RecebimentoEmDinheiroDesfeito = "PAYMENT_RECEIVED_IN_CASH_UNDONE";
    public const string ChargebackPedido = "PAYMENT_CHARGEBACK_REQUESTED";
    public const string ChargebackEmDisputa = "PAYMENT_CHARGEBACK_DISPUTE";
    public const string AguardandoReversaoChargeback = "PAYMENT_AWAITING_CHARGEBACK_REVERSAL";

    /// <summary>Os eventos que o cadastro do webhook assina na conta; outros que chegarem são gravados e ignorados.</summary>
    public static readonly string[] Assinados =
    [
        PagamentoCriado, PagamentoAtualizado, PagamentoConfirmado, PagamentoRecebido, PagamentoVencido, PagamentoExcluido,
        PagamentoEstornado, RecebimentoEmDinheiroDesfeito, ChargebackPedido, ChargebackEmDisputa, AguardandoReversaoChargeback
    ];

    /// <summary><c>null</c> se o corpo não é um evento da Asaas legível.</summary>
    public static EventoAsaas? Ler(string corpo)
    {
        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || Texto(raiz, "event") is not { } tipo) return null;
            var id = Texto(raiz, "id") ?? $"sem-id-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(corpo)))[..32]}";
            var pagamento = raiz.TryGetProperty("payment", out var p) && p.ValueKind == JsonValueKind.Object ? LerPagamento(p) : null;
            return new EventoAsaas(id, tipo, pagamento);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static PagamentoAsaas? LerPagamento(JsonElement p)
    {
        if (Texto(p, "id") is not { } id) return null;
        return new PagamentoAsaas(
            id,
            Texto(p, "status") ?? string.Empty,
            Numero(p, "value") ?? 0m,
            Numero(p, "originalValue"),
            Data(p, "dueDate"),
            Data(p, "clientPaymentDate") ?? Data(p, "paymentDate") ?? Data(p, "confirmedDate"),
            Texto(p, "billingType"),
            Texto(p, "externalReference"),
            p.TryGetProperty("deleted", out var d) && d.ValueKind == JsonValueKind.True,
            Texto(p, "invoiceUrl"));
    }

    private static string? Texto(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static decimal? Numero(JsonElement elemento, string nome) =>
        elemento.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero)
            ? numero
            : null;

    private static DateOnly? Data(JsonElement elemento, string nome) =>
        Texto(elemento, nome) is { Length: >= 10 } texto
        && DateOnly.TryParseExact(texto[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data
            : null;
}

/// <summary>O token do webhook: comparação em tempo constante (o cabeçalho é a única prova de que veio da Asaas).</summary>
public static class TokenWebhookAsaas
{
    public static string Gerar() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool Confere(string? recebido, string? esperado) =>
        !string.IsNullOrEmpty(recebido) && !string.IsNullOrEmpty(esperado)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebido), Encoding.UTF8.GetBytes(esperado));
}
