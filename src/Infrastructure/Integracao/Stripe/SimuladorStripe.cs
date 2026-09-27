using System.Text.Json;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;

namespace MinhaEscolinha.Infrastructure.Integracao.Stripe;

/// <summary>
/// O Stripe de mentira do modo Simulado: guarda as sessões num arquivo (sobrevive ao reinício do site) e deixa o
/// <c>/dev/stripe</c> pagar com cartão, emitir boleto ou Pix, confirmar, recusar e expirar. Cada ação manda o webhook
/// pelo endpoint de verdade, assinado: o caminho do pagamento roda inteiro, sem conta no Stripe.
/// </summary>
public sealed class ArmazemSimuladorStripe
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Arquivo { get; }

    public ArmazemSimuladorStripe(string? arquivo = null)
    {
        Arquivo = arquivo ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinhaEscolinha", "simulador-stripe.json");
    }

    public sealed class SessaoSimulada
    {
        public string Id { get; set; } = string.Empty;
        public Guid CompraId { get; set; }
        public Guid EscolinhaId { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public long ValorCentavos { get; set; }
        public string Status { get; set; } = "open";
        public string SituacaoPagamento { get; set; } = "unpaid";
        public string? SituacaoIntencao { get; set; }
        public string? MeioPagamento { get; set; }
        public string? PagamentoId { get; set; }
        public DateTime CriadaEmUtc { get; set; }
        public DateTime ExpiraEmUtc { get; set; }
        public string UrlSucesso { get; set; } = string.Empty;
        public string UrlCancelamento { get; set; } = string.Empty;
        public string? Email { get; set; }

        public Dictionary<string, string> Metadados() => new(StringComparer.Ordinal)
        {
            [MetadadosStripe.Origem] = MetadadosStripe.ValorOrigem,
            [MetadadosStripe.Escolinha] = EscolinhaId.ToString(),
            [MetadadosStripe.Compra] = CompraId.ToString()
        };
    }

    /// <summary>Como a chave de idempotência do Stripe: a mesma compra devolve a mesma sessão.</summary>
    public Task<SessaoSimulada> CriarAsync(PedidoCheckout pedido, DateTime agoraUtc) => AlterarTodasAsync(sessoes =>
    {
        if (sessoes.FirstOrDefault(s => s.CompraId == pedido.CompraId) is { } existente) return existente;
        var nova = new SessaoSimulada
        {
            Id = $"cs_sim_{Guid.NewGuid():N}",
            CompraId = pedido.CompraId,
            EscolinhaId = pedido.EscolinhaId,
            Descricao = pedido.Descricao,
            ValorCentavos = pedido.ValorCentavos,
            CriadaEmUtc = agoraUtc,
            ExpiraEmUtc = agoraUtc.AddHours(24),
            UrlSucesso = pedido.UrlSucesso,
            UrlCancelamento = pedido.UrlCancelamento,
            Email = pedido.EmailCliente
        };
        sessoes.Add(nova);
        return nova;
    });

    public async Task<SessaoSimulada?> ObterAsync(string id) => (await LerAsync()).FirstOrDefault(s => s.Id == id);

    public async Task<IReadOnlyList<SessaoSimulada>> ListarAsync() =>
        (await LerAsync()).OrderByDescending(s => s.CriadaEmUtc).ToList();

    /// <summary>Aplica a mudança e grava. <c>null</c> se a sessão não existe.</summary>
    public Task<SessaoSimulada?> AlterarAsync(string id, Action<SessaoSimulada> mudanca) => AlterarTodasAsync(sessoes =>
    {
        var sessao = sessoes.FirstOrDefault(s => s.Id == id);
        if (sessao is not null) mudanca(sessao);
        return sessao;
    });

    /// <summary>O evento como o Stripe manda (só o que o sistema lê), para o webhook do simulador.</summary>
    public static string MontarEvento(string tipo, SessaoSimulada sessao, DateTimeOffset agora) => JsonSerializer.Serialize(new
    {
        id = $"evt_sim_{Guid.NewGuid():N}",
        @object = "event",
        type = tipo,
        created = agora.ToUnixTimeSeconds(),
        livemode = false,
        data = new
        {
            @object = new
            {
                id = sessao.Id,
                @object = "checkout.session",
                client_reference_id = sessao.CompraId.ToString(),
                status = sessao.Status,
                payment_status = sessao.SituacaoPagamento,
                metadata = sessao.Metadados()
            }
        }
    });

    private async Task<T> AlterarTodasAsync<T>(Func<List<SessaoSimulada>, T> alteracao)
    {
        await Trava.WaitAsync();
        try
        {
            var sessoes = await LerSemTravaAsync();
            var resultado = alteracao(sessoes);
            Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
            await File.WriteAllTextAsync(Arquivo, JsonSerializer.Serialize(sessoes, Json));
            return resultado;
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task<List<SessaoSimulada>> LerAsync()
    {
        await Trava.WaitAsync();
        try
        {
            return await LerSemTravaAsync();
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task<List<SessaoSimulada>> LerSemTravaAsync()
    {
        if (!File.Exists(Arquivo)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<SessaoSimulada>>(await File.ReadAllTextAsync(Arquivo)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>O <see cref="IGatewayStripe"/> do modo Simulado: a URL de pagamento é a página <c>/dev/stripe/checkout</c>.</summary>
public sealed class SimuladorStripe(ArmazemSimuladorStripe armazem, OpcoesStripe opcoes, TimeProvider tempo) : IGatewayStripe
{
    /// <summary>HttpClient nomeado que o <c>/dev/stripe</c> usa para mandar o webhook ao próprio sistema.</summary>
    public const string ClienteWebhook = "simulador-stripe-webhook";

    public async Task<SessaoCheckoutCriada> CriarSessaoCheckoutAsync(PedidoCheckout pedido, CancellationToken ct = default)
    {
        var sessao = await armazem.CriarAsync(pedido, tempo.GetUtcNow().UtcDateTime);
        return new SessaoCheckoutCriada(sessao.Id, $"{opcoes.UrlPublica.TrimEnd('/')}/dev/stripe/checkout/{sessao.Id}", sessao.ExpiraEmUtc);
    }

    public async Task<SessaoCheckoutConsultada> ConsultarSessaoAsync(string sessaoId, CancellationToken ct = default)
    {
        var sessao = await armazem.ObterAsync(sessaoId)
            ?? throw new FalhaIntegracaoException(NaturezaFalha.Definitiva, $"Stripe 404: a sessão {sessaoId} não existe no simulador.", 404);

        // Como o Stripe: a sessão aberta vence sozinha no prazo.
        if (sessao.Status == "open" && tempo.GetUtcNow().UtcDateTime >= sessao.ExpiraEmUtc)
            sessao = await armazem.AlterarAsync(sessaoId, s => s.Status = "expired") ?? sessao;

        return new SessaoCheckoutConsultada(sessao.Id, sessao.Status, sessao.SituacaoPagamento, sessao.PagamentoId,
            sessao.SituacaoIntencao, sessao.MeioPagamento, sessao.Metadados());
    }
}
