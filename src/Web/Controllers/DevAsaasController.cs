using System.Text;
using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Application.Integracao.Asaas;
using MinhaEscolinha.Infrastructure.Integracao.Asaas;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// A Asaas de mentira (/dev/asaas): só em Development e no modo Simulado. A lista mostra as cobranças da escolinha; a
/// fatura simulada é o que a família abriria pelo link. Pagar (no prazo, em atraso, com outro valor), vencer, estornar e
/// excluir mandam o webhook pelo endpoint de verdade, com o token da escolinha: o caminho inteiro roda sem conta na Asaas.
/// </summary>
[SomenteAutenticado]
[Route("dev/asaas")]
public class DevAsaasController(
    ArmazemSimuladorAsaas armazem,
    OpcoesAsaas opcoes,
    IContextoEscolinha contexto,
    IExecutorEscolinha executor,
    IHttpClientFactory fabrica,
    IWebHostEnvironment ambiente,
    IRelogioBrasil relogio,
    TimeProvider tempo,
    ILogger<DevAsaasController> logger) : Controller
{
    private bool Disponivel => ambiente.IsDevelopment() && opcoes is { Habilitado: true, Simulado: true };

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (!Disponivel || contexto.EscolinhaId is not { } escolinhaId) return NotFound();
        ViewData["ModuloAtivo"] = MensalidadesController.Modulo;
        var dados = await armazem.LerAsync();
        return View(dados.Cobrancas.Where(c => c.EscolinhaId == escolinhaId).OrderByDescending(c => c.CriadaEmUtc).ToList());
    }

    /// <summary>A fatura que a família abre pelo link da mensagem: paga com Pix, boleto ou cartão.</summary>
    [HttpGet("fatura/{id}")]
    public async Task<IActionResult> Fatura(string id)
    {
        if (!Disponivel || (await armazem.LerAsync()).Cobrancas.FirstOrDefault(c => c.Id == id) is not { } cobranca) return NotFound();
        ViewData["Hoje"] = DateOnly.FromDateTime(relogio.Hoje());
        return View(cobranca);
    }

    /// <summary>Paga o valor integral; depois do vencimento, com a multa e os juros da cobrança (o valor original fica guardado).</summary>
    [HttpPost("{id}/pagar")]
    public Task<IActionResult> Pagar(string id, string? meio, string? voltar, CancellationToken ct) =>
        MudarAsync(id, EventoAsaas.PagamentoRecebido, c => !c.Excluida && !SimuladorAsaas.StatusPago(c.Status) && !StatusAsaas.Estorno(c.Status), c =>
        {
            var hoje = DateOnly.FromDateTime(relogio.Hoje());
            c.Meio = meio is "PIX" or "BOLETO" or "CREDIT_CARD" ? meio : "PIX";
            c.Status = c.Meio == "CREDIT_CARD" ? StatusAsaas.Confirmada : StatusAsaas.Recebida;
            c.PagaEm = hoje;
            if (hoje > c.Vencimento)
            {
                var original = c.ValorOriginal ?? c.Valor;
                var meses = Math.Max(1, (hoje.DayNumber - c.Vencimento.DayNumber) / 30m);
                c.ValorOriginal = original;
                c.Valor = Math.Round(original * (1 + c.Multa / 100m + c.Juros / 100m * meses), 2);
            }
        }, voltar, ct);

    /// <summary>Um pagamento com valor diferente do cobrado (boleto pago com outro valor): vira divergência, não conciliação.</summary>
    [HttpPost("{id}/pagar-diferente")]
    public Task<IActionResult> PagarDiferente(string id, decimal valor, string? voltar, CancellationToken ct) =>
        MudarAsync(id, EventoAsaas.PagamentoRecebido, c => !c.Excluida && !SimuladorAsaas.StatusPago(c.Status) && valor > 0, c =>
        {
            c.Meio = "BOLETO";
            c.Status = StatusAsaas.Recebida;
            c.PagaEm = DateOnly.FromDateTime(relogio.Hoje());
            c.ValorOriginal = null;
            c.Valor = Math.Round(valor, 2);
        }, voltar, ct);

    [HttpPost("{id}/vencer")]
    public Task<IActionResult> Vencer(string id, string? voltar, CancellationToken ct) =>
        MudarAsync(id, EventoAsaas.PagamentoVencido, c => !c.Excluida && c.Status == StatusAsaas.Pendente, c => c.Status = StatusAsaas.Vencida,
            voltar, ct);

    [HttpPost("{id}/estornar")]
    public Task<IActionResult> Estornar(string id, string? voltar, CancellationToken ct) =>
        MudarAsync(id, EventoAsaas.PagamentoEstornado, c => SimuladorAsaas.StatusPago(c.Status), c => c.Status = StatusAsaas.Estornada, voltar, ct);

    [HttpPost("{id}/excluir")]
    public Task<IActionResult> Excluir(string id, string? voltar, CancellationToken ct) =>
        MudarAsync(id, EventoAsaas.PagamentoExcluido, c => !c.Excluida && !SimuladorAsaas.StatusPago(c.Status), c => c.Excluida = true, voltar, ct);

    private async Task<IActionResult> MudarAsync(string id, string evento, Func<ArmazemSimuladorAsaas.CobrancaSimulada, bool> pode,
        Action<ArmazemSimuladorAsaas.CobrancaSimulada> mudanca, string? voltar, CancellationToken ct)
    {
        if (!Disponivel) return NotFound();
        var mudou = false;
        var cobranca = await armazem.AlterarAsync(d =>
        {
            var c = d.Cobrancas.FirstOrDefault(x => x.Id == id);
            if (c is null || !pode(c)) return c;
            mudanca(c);
            mudou = true;
            return c;
        });
        if (cobranca is null) return NotFound();
        if (mudou) await EnviarWebhookAsync(evento, cobranca, ct);
        else TempData[ChavesTempData.Erro] = "Essa ação não vale para a cobrança no estado em que ela está.";
        return Url.IsLocalUrl(voltar) ? LocalRedirect(voltar) : RedirectToAction(nameof(Index));
    }

    /// <summary>Manda o evento para o endpoint de verdade da escolinha dona da cobrança, com o token dela.</summary>
    private async Task EnviarWebhookAsync(string evento, ArmazemSimuladorAsaas.CobrancaSimulada cobranca, CancellationToken ct)
    {
        var token = await executor.ExecutarAsync(cobranca.EscolinhaId,
            sp => sp.GetRequiredService<ServicoAcessoAsaas>().TokenWebhookAsync(criarSeFaltar: true, ct), ct);
        if (token is null)
        {
            TempData[ChavesTempData.Erro] = "Simulador: a escolinha não tem conta Asaas vinculada (sem token de webhook).";
            return;
        }

        var corpo = ArmazemSimuladorAsaas.MontarEvento(evento, cobranca, tempo.GetUtcNow().UtcDateTime);
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, opcoes.UrlWebhook(cobranca.EscolinhaId))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json")
        };
        requisicao.Headers.Add(EventoAsaas.CabecalhoToken, token);
        try
        {
            using var resposta = await fabrica.CreateClient(SimuladorAsaas.ClienteWebhook).SendAsync(requisicao, ct);
            if (resposta.IsSuccessStatusCode)
            {
                TempData[ChavesTempData.Sucesso] = $"Simulador: {evento} enviado ao sistema.";
                return;
            }
            logger.LogWarning("Simulador da Asaas: o webhook {Evento} voltou {Codigo}.", evento, (int)resposta.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Simulador da Asaas: o webhook {Evento} não foi entregue.", evento);
        }
        TempData[ChavesTempData.Erro] = "Simulador: o webhook não foi entregue. A varredura (de hora em hora) confere a cobrança.";
    }
}
