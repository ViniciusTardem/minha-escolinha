using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Integracao.Asaas;

namespace MinhaEscolinha.Testes.Infraestrutura;

/// <summary>
/// Asaas controlada pelo teste: clientes e cobranças em memória, por escolinha. O teste paga, vence, exclui e estorna, e
/// pode fazer a próxima criação "perder a resposta" (a cobrança nasce, mas a chamada falha como timeout) para provar que a
/// emissão repetida reencontra a cobrança em vez de criar outra.
/// </summary>
public sealed class GatewayAsaasFalso : IGatewayAsaas
{
    public sealed class Cobranca
    {
        public required string Id { get; init; }
        public required Guid EscolinhaId { get; init; }
        public required CobrancaAsaasPedido Pedido { get; init; }
        public string Status { get; set; } = StatusAsaas.Pendente;
        public decimal Valor { get; set; }
        public decimal? ValorOriginal { get; set; }
        public DateOnly? PagaEm { get; set; }
        public string? Meio { get; set; }
        public bool Excluida { get; set; }

        public PagamentoAsaas Pagamento() => new(Id, Status, Valor, ValorOriginal, Pedido.Vencimento, PagaEm, Meio ?? "UNDEFINED",
            Pedido.CobrancaId.ToString(), Excluida, $"https://sandbox.asaas.com/i/{Id}");
    }

    public List<(Guid EscolinhaId, ClienteAsaasPedido Pedido, string Id)> Clientes { get; } = [];
    public List<Cobranca> Cobrancas { get; } = [];
    public List<string> Excluidas { get; } = [];
    public List<(string Id, DateOnly Data, decimal Valor)> RecebidasEmDinheiro { get; } = [];
    public List<(string Url, string Token)> Webhooks { get; } = [];
    public int Consultas { get; private set; }

    /// <summary>A próxima criação de cobrança acontece, mas a resposta se perde (timeout): a falha é retentável.</summary>
    public bool PerderRespostaDaProximaCriacao { get; set; }

    /// <summary>A próxima chamada que falar com a Asaas falha com isto.</summary>
    public FalhaIntegracaoException? Falha { get; set; }

    public Task<string?> BuscarClienteAsync(ContaAsaasAcesso conta, Guid responsavelId, CancellationToken ct = default)
    {
        Falhar();
        var cliente = Clientes.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Pedido.ResponsavelId == responsavelId);
        return Task.FromResult<string?>(cliente == default ? null : cliente.Id);
    }

    public Task<string> CriarClienteAsync(ContaAsaasAcesso conta, ClienteAsaasPedido pedido, CancellationToken ct = default)
    {
        Falhar();
        var id = $"cus_teste_{Clientes.Count + 1:D4}";
        Clientes.Add((conta.EscolinhaId, pedido, id));
        return Task.FromResult(id);
    }

    public List<(string IdAsaas, ClienteAsaasPedido Pedido)> ClientesAtualizados { get; } = [];

    public Task AtualizarClienteAsync(ContaAsaasAcesso conta, string idAsaas, ClienteAsaasPedido pedido, CancellationToken ct = default)
    {
        Falhar();
        ClientesAtualizados.Add((idAsaas, pedido));
        return Task.CompletedTask;
    }

    public Task<PagamentoAsaas?> BuscarCobrancaAsync(ContaAsaasAcesso conta, Guid cobrancaId, CancellationToken ct = default)
    {
        Falhar();
        return Task.FromResult(Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && !c.Excluida
                                                             && c.Pedido.CobrancaId == cobrancaId)?.Pagamento());
    }

    public Task<PagamentoAsaas> CriarCobrancaAsync(ContaAsaasAcesso conta, CobrancaAsaasPedido pedido, CancellationToken ct = default)
    {
        Falhar();
        var cobranca = new Cobranca { Id = $"pay_teste_{Cobrancas.Count + 1:D4}", EscolinhaId = conta.EscolinhaId, Pedido = pedido, Valor = pedido.Valor };
        Cobrancas.Add(cobranca);
        if (PerderRespostaDaProximaCriacao)
        {
            PerderRespostaDaProximaCriacao = false;
            throw new FalhaIntegracaoException(NaturezaFalha.Retentavel, "A Asaas demorou demais para responder.");
        }
        return Task.FromResult(cobranca.Pagamento());
    }

    public Task<PagamentoAsaas?> ObterCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default)
    {
        Consultas++;
        Falhar();
        return Task.FromResult(Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Id == id)?.Pagamento());
    }

    /// <summary>A conta não tem chave Pix: a Asaas não devolve o copia e cola.</summary>
    public bool SemPix { get; set; }

    /// <summary>A validade que a Asaas informa para o próximo Pix (vazio: sem validade).</summary>
    public DateTime? ValidadeDoPix { get; set; }

    public List<string> PixConsultados { get; } = [];

    /// <summary>Um PNG mínimo em base64: o suficiente para conferir que a imagem do QR code é guardada e mostrada (P49).</summary>
    public const string ImagemDoPix = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    public static string PixDe(string id) => $"00020126580014br.gov.bcb.pix0136teste-{id}5204000053039865802BR6304ABCD";

    public Task<PixAsaas?> ObterPixAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default)
    {
        Falhar();
        PixConsultados.Add(id);
        var cobranca = Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Id == id);
        var emAberto = cobranca is { Excluida: false, Status: StatusAsaas.Pendente or StatusAsaas.Vencida };
        return Task.FromResult(SemPix || !emAberto ? null : new PixAsaas(PixDe(id), ValidadeDoPix, ImagemDoPix));
    }

    public Task ExcluirCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default)
    {
        Falhar();
        Excluidas.Add(id);
        if (Cobrancas.FirstOrDefault(c => c.Id == id) is { } c) c.Excluida = true;
        return Task.CompletedTask;
    }

    public Task ReceberEmDinheiroAsync(ContaAsaasAcesso conta, string id, DateOnly data, decimal valor, CancellationToken ct = default)
    {
        Falhar();
        RecebidasEmDinheiro.Add((id, data, valor));
        return Task.CompletedTask;
    }

    public Task RegistrarWebhookAsync(ContaAsaasAcesso conta, string url, string token, string? email, CancellationToken ct = default)
    {
        Falhar();
        Webhooks.Add((url, token));
        return Task.CompletedTask;
    }

    public Cobranca Da(Guid cobrancaId) => Cobrancas.Single(c => c.Pedido.CobrancaId == cobrancaId && !c.Excluida);

    /// <summary>A família pagou o valor cobrado; depois do vencimento, com acréscimo (o original fica guardado, como na Asaas).</summary>
    public Cobranca Pagar(Guid cobrancaId, DateOnly quando, string meio = "PIX", decimal acrescimo = 0)
    {
        var c = Da(cobrancaId);
        c.Status = meio == "CREDIT_CARD" ? StatusAsaas.Confirmada : StatusAsaas.Recebida;
        c.Meio = meio;
        c.PagaEm = quando;
        if (acrescimo > 0)
        {
            c.ValorOriginal = c.Pedido.Valor;
            c.Valor = c.Pedido.Valor + acrescimo;
        }
        return c;
    }

    private void Falhar()
    {
        if (Falha is not { } falha) return;
        Falha = null;
        throw falha;
    }
}

/// <summary>Os jobs do financeiro em memória: o teste vê o que foi agendado e decide quando rodar.</summary>
public sealed class AgendadorFinanceiroDeTeste : IAgendadorFinanceiro
{
    public List<Guid> Projecoes { get; } = [];
    public List<(Guid EscolinhaId, Guid CobrancaId)> Emissoes { get; } = [];
    public List<(Guid EscolinhaId, Guid CobrancaId)> Conferencias { get; } = [];
    public List<(Guid EscolinhaId, Guid CobrancaId)> Exclusoes { get; } = [];
    public List<(Guid EscolinhaId, Guid CobrancaId)> RecebimentosEmDinheiro { get; } = [];
    public List<Guid> Webhooks { get; } = [];
    public List<Guid> RegistrosDeWebhook { get; } = [];
    public List<(Guid EscolinhaId, Guid CobrancaId)> BuscasDePix { get; } = [];
    public List<(Guid EscolinhaId, Guid ResponsavelId)> AtualizacoesDeCliente { get; } = [];
    public void AtualizarClienteAsaas(Guid escolinhaId, Guid responsavelId) => AtualizacoesDeCliente.Add((escolinhaId, responsavelId));

    public void Projetar(Guid escolinhaId) => Projecoes.Add(escolinhaId);
    public void ObterPix(Guid escolinhaId, Guid cobrancaId) => BuscasDePix.Add((escolinhaId, cobrancaId));
    public void Emitir(Guid escolinhaId, Guid cobrancaId) => Emissoes.Add((escolinhaId, cobrancaId));
    public void Conferir(Guid escolinhaId, Guid cobrancaId) => Conferencias.Add((escolinhaId, cobrancaId));
    public void ExcluirNaAsaas(Guid escolinhaId, Guid cobrancaId) => Exclusoes.Add((escolinhaId, cobrancaId));
    public void ReceberEmDinheiroNaAsaas(Guid escolinhaId, Guid cobrancaId) => RecebimentosEmDinheiro.Add((escolinhaId, cobrancaId));
    public void ProcessarWebhookAsaas(Guid webhookId) => Webhooks.Add(webhookId);
    public void RegistrarWebhook(Guid escolinhaId) => RegistrosDeWebhook.Add(escolinhaId);
}
