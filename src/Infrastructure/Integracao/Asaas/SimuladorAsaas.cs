using System.Globalization;
using System.Text.Json;
using MinhaEscolinha.Application.Integracao.Asaas;

namespace MinhaEscolinha.Infrastructure.Integracao.Asaas;

/// <summary>
/// A Asaas de mentira do modo Simulado: guarda clientes e cobranças num arquivo (sobrevive ao reinício do site), por
/// escolinha. O <c>/dev/asaas</c> e a fatura simulada pagam (Pix, boleto, cartão, com valor diferente, em atraso), vencem,
/// estornam e excluem; cada ação manda o webhook pelo endpoint de verdade, com o token da escolinha.
/// </summary>
public sealed class ArmazemSimuladorAsaas
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public string Arquivo { get; }

    public ArmazemSimuladorAsaas(string? arquivo = null)
    {
        Arquivo = arquivo ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinhaEscolinha", "simulador-asaas.json");
    }

    public sealed class Dados
    {
        public List<ClienteSimulado> Clientes { get; set; } = [];
        public List<CobrancaSimulada> Cobrancas { get; set; } = [];
    }

    public sealed class ClienteSimulado
    {
        public string Id { get; set; } = string.Empty;
        public Guid EscolinhaId { get; set; }
        public string ReferenciaExterna { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
    }

    public sealed class CobrancaSimulada
    {
        public string Id { get; set; } = string.Empty;
        public Guid EscolinhaId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string ClienteNome { get; set; } = string.Empty;
        public string ReferenciaExterna { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public decimal? ValorOriginal { get; set; }
        public decimal Multa { get; set; }
        public decimal Juros { get; set; }
        public DateOnly Vencimento { get; set; }
        public string Status { get; set; } = StatusAsaas.Pendente;
        public string? Meio { get; set; }
        public DateOnly? PagaEm { get; set; }
        public bool Excluida { get; set; }
        public DateTime CriadaEmUtc { get; set; }
        public string UrlFatura { get; set; } = string.Empty;

        public PagamentoAsaas ParaPagamento() =>
            new(Id, Status, Valor, ValorOriginal, Vencimento, PagaEm, Meio ?? "UNDEFINED", ReferenciaExterna, Excluida, UrlFatura);
    }

    public Task<T> AlterarAsync<T>(Func<Dados, T> alteracao) => ComTravaAsync(async () =>
    {
        var dados = await LerSemTravaAsync();
        var resultado = alteracao(dados);
        Directory.CreateDirectory(Path.GetDirectoryName(Arquivo)!);
        await File.WriteAllTextAsync(Arquivo, JsonSerializer.Serialize(dados, Json));
        return resultado;
    });

    public Task<Dados> LerAsync() => ComTravaAsync(LerSemTravaAsync);

    /// <summary>O evento como a Asaas manda (só o que o sistema lê), para o webhook do simulador.</summary>
    public static string MontarEvento(string tipo, CobrancaSimulada c, DateTime agoraUtc) => JsonSerializer.Serialize(new
    {
        id = $"evt_sim_{Guid.NewGuid():N}",
        @event = tipo,
        dateCreated = agoraUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        payment = new
        {
            @object = "payment",
            id = c.Id,
            customer = c.Cliente,
            value = c.Valor,
            originalValue = c.ValorOriginal,
            status = c.Status,
            billingType = c.Meio ?? "UNDEFINED",
            dueDate = c.Vencimento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            clientPaymentDate = c.PagaEm?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            paymentDate = c.PagaEm?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            externalReference = c.ReferenciaExterna,
            invoiceUrl = c.UrlFatura,
            deleted = c.Excluida
        }
    });

    private static async Task<T> ComTravaAsync<T>(Func<Task<T>> acao)
    {
        await Trava.WaitAsync();
        try
        {
            return await acao();
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task<Dados> LerSemTravaAsync()
    {
        if (!File.Exists(Arquivo)) return new Dados();
        try
        {
            return JsonSerializer.Deserialize<Dados>(await File.ReadAllTextAsync(Arquivo)) ?? new Dados();
        }
        catch (JsonException)
        {
            return new Dados();
        }
    }
}

/// <summary>O <see cref="IGatewayAsaas"/> do modo Simulado: a fatura é a página <c>/dev/asaas/fatura</c>.</summary>
public sealed class SimuladorAsaas(ArmazemSimuladorAsaas armazem, OpcoesAsaas opcoes, TimeProvider tempo) : IGatewayAsaas
{
    /// <summary>HttpClient nomeado que o <c>/dev/asaas</c> usa para mandar o webhook ao próprio sistema.</summary>
    public const string ClienteWebhook = "simulador-asaas-webhook";

    public Task<string?> BuscarClienteAsync(ContaAsaasAcesso conta, Guid responsavelId, CancellationToken ct = default) =>
        armazem.AlterarAsync(d => d.Clientes.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId
                                                                 && c.ReferenciaExterna == responsavelId.ToString())?.Id);

    public Task<string> CriarClienteAsync(ContaAsaasAcesso conta, ClienteAsaasPedido pedido, CancellationToken ct = default) =>
        armazem.AlterarAsync(d =>
        {
            var cliente = new ArmazemSimuladorAsaas.ClienteSimulado
            {
                Id = $"cus_sim_{Guid.NewGuid():N}"[..20],
                EscolinhaId = conta.EscolinhaId,
                ReferenciaExterna = pedido.ResponsavelId.ToString(),
                Nome = pedido.Nome,
                Cpf = pedido.Cpf
            };
            d.Clientes.Add(cliente);
            return cliente.Id;
        });

    public Task<PagamentoAsaas?> BuscarCobrancaAsync(ContaAsaasAcesso conta, Guid cobrancaId, CancellationToken ct = default) =>
        armazem.AlterarAsync(d => d.Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && !c.Excluida
                                                                  && c.ReferenciaExterna == cobrancaId.ToString())?.ParaPagamento());

    public Task<PagamentoAsaas> CriarCobrancaAsync(ContaAsaasAcesso conta, CobrancaAsaasPedido pedido, CancellationToken ct = default) =>
        armazem.AlterarAsync(d =>
        {
            var id = $"pay_sim_{Guid.NewGuid():N}"[..20];
            var cobranca = new ArmazemSimuladorAsaas.CobrancaSimulada
            {
                Id = id,
                EscolinhaId = conta.EscolinhaId,
                Cliente = pedido.ClienteId,
                ClienteNome = d.Clientes.FirstOrDefault(c => c.Id == pedido.ClienteId)?.Nome ?? "—",
                ReferenciaExterna = pedido.CobrancaId.ToString(),
                Descricao = pedido.Descricao,
                Valor = pedido.Valor,
                Multa = pedido.MultaPercentual,
                Juros = pedido.JurosPercentualMes,
                Vencimento = pedido.Vencimento,
                CriadaEmUtc = tempo.GetUtcNow().UtcDateTime,
                UrlFatura = $"{opcoes.UrlPublica.TrimEnd('/')}/dev/asaas/fatura/{id}"
            };
            d.Cobrancas.Add(cobranca);
            return cobranca.ParaPagamento();
        });

    public Task<PagamentoAsaas?> ObterCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default) =>
        armazem.AlterarAsync(d => d.Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Id == id)?.ParaPagamento());

    /// <summary>Um copia e cola com a forma do Pix (não paga nada): para pagar no simulador, use a fatura.</summary>
    public Task<PixAsaas?> ObterPixAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default) =>
        armazem.AlterarAsync(d => d.Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Id == id) is { Excluida: false } c
                                  && !StatusPago(c.Status)
            ? new PixAsaas(PixSimulado(c), null)
            : null);

    public static string PixSimulado(ArmazemSimuladorAsaas.CobrancaSimulada c)
    {
        var valor = c.Valor.ToString("0.00", CultureInfo.InvariantCulture);
        var chave = $"simulador.minhaescolinha/{c.Id}";
        return $"00020126{26 + chave.Length:D2}0014br.gov.bcb.pix25{chave.Length:D2}{chave}52040000530398654{valor.Length:D2}{valor}" +
               "5802BR5909SIMULADOR6008LONDRINA62070503***6304SIMU";
    }

    public Task ExcluirCobrancaAsync(ContaAsaasAcesso conta, string id, CancellationToken ct = default) =>
        armazem.AlterarAsync(d =>
        {
            if (d.Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Id == id) is { Status: StatusAsaas.Pendente or StatusAsaas.Vencida } c)
                c.Excluida = true;
            return true;
        });

    public Task ReceberEmDinheiroAsync(ContaAsaasAcesso conta, string id, DateOnly data, decimal valor, CancellationToken ct = default) =>
        armazem.AlterarAsync(d =>
        {
            if (d.Cobrancas.FirstOrDefault(c => c.EscolinhaId == conta.EscolinhaId && c.Id == id) is { } c && !StatusPago(c.Status))
            {
                c.Status = StatusAsaas.RecebidaEmDinheiro;
                c.PagaEm = data;
                c.Valor = valor;
            }
            return true;
        });

    public Task RegistrarWebhookAsync(ContaAsaasAcesso conta, string url, string token, string? email, CancellationToken ct = default) =>
        Task.CompletedTask;

    public static bool StatusPago(string status) =>
        status is StatusAsaas.Recebida or StatusAsaas.Confirmada or StatusAsaas.RecebidaEmDinheiro;
}
