using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Application.Plataforma;

namespace MinhaEscolinha.Testes.Infraestrutura;

/// <summary>
/// Stripe controlado pelo teste: cria sessões abertas e deixa o teste pagar, emitir boleto ou Pix, recusar e expirar.
/// A consulta devolve o estado que o teste montou (é o que a conferência lê antes de lançar créditos).
/// </summary>
public sealed class GatewayStripeFalso : IGatewayStripe
{
    public List<PedidoCheckout> Pedidos { get; } = [];
    public Dictionary<string, SessaoCheckoutConsultada> Sessoes { get; } = new(StringComparer.Ordinal);
    public int Consultas { get; private set; }
    public FalhaIntegracaoException? FalhaAoCriar { get; set; }
    public FalhaIntegracaoException? FalhaAoConsultar { get; set; }

    public Task<SessaoCheckoutCriada> CriarSessaoCheckoutAsync(PedidoCheckout pedido, CancellationToken ct = default)
    {
        if (FalhaAoCriar is { } falha) throw falha;
        Pedidos.Add(pedido);
        var id = $"cs_test_{pedido.CompraId:N}";
        Sessoes[id] = new SessaoCheckoutConsultada(id, "open", "unpaid", null, null, null, new Dictionary<string, string>
        {
            [MetadadosStripe.Origem] = MetadadosStripe.ValorOrigem,
            [MetadadosStripe.Escolinha] = pedido.EscolinhaId.ToString(),
            [MetadadosStripe.Compra] = pedido.CompraId.ToString()
        });
        return Task.FromResult(new SessaoCheckoutCriada(id, $"https://checkout.stripe.com/c/pay/{id}", new DateTime(2026, 9, 27, 15, 0, 0, DateTimeKind.Utc)));
    }

    public Task<SessaoCheckoutConsultada> ConsultarSessaoAsync(string sessaoId, CancellationToken ct = default)
    {
        Consultas++;
        if (FalhaAoConsultar is { } falha) throw falha;
        return Sessoes.TryGetValue(sessaoId, out var sessao)
            ? Task.FromResult(sessao)
            : throw new FalhaIntegracaoException(NaturezaFalha.Definitiva, $"Stripe 404: sessão {sessaoId} inexistente.", 404);
    }

    public string SessaoDa(Guid compraId) => $"cs_test_{compraId:N}";

    public void Pagar(string sessaoId, string meio = "card") => Sessoes[sessaoId] = Sessoes[sessaoId] with
    {
        Status = "complete", SituacaoPagamento = "paid", SituacaoIntencao = "succeeded", MeioPagamento = meio, PagamentoId = $"pi_{sessaoId}"
    };

    public void EmitirBoletoOuPix(string sessaoId, string meio) => Sessoes[sessaoId] = Sessoes[sessaoId] with
    {
        Status = "complete", SituacaoPagamento = "unpaid", SituacaoIntencao = "requires_action", MeioPagamento = meio, PagamentoId = $"pi_{sessaoId}"
    };

    public void Recusar(string sessaoId) => Sessoes[sessaoId] = Sessoes[sessaoId] with { SituacaoIntencao = "requires_payment_method" };

    public void Expirar(string sessaoId) => Sessoes[sessaoId] = Sessoes[sessaoId] with { Status = "expired" };
}

/// <summary>Fila em memória: o teste vê o que foi agendado e decide quando rodar.</summary>
public sealed class AgendadorPlataformaDeTeste : IAgendadorPlataforma
{
    public List<Guid> Webhooks { get; } = [];
    public List<(Guid EscolinhaId, Guid CompraId)> Conferencias { get; } = [];

    public void ProcessarWebhookStripe(Guid webhookId) => Webhooks.Add(webhookId);
    public void ConferirCompraCreditos(Guid escolinhaId, Guid compraId) => Conferencias.Add((escolinhaId, compraId));
}
