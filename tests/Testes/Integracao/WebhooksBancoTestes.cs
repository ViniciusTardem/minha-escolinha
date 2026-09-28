using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Infrastructure.Data.Contexto;
using MinhaEscolinha.Infrastructure.Integracao;
using MinhaEscolinha.Testes.Infraestrutura;

namespace MinhaEscolinha.Testes.Integracao;

/// <summary>
/// A guarda do corpo bruto dos webhooks (P22): o do encerrado sai depois de 90 dias, e a linha fica. O relógio começa no
/// sábado, 26/09/2026, 12:00 de Brasília.
/// </summary>
[Collection(BancoDeTestes.Colecao)]
public sealed class WebhooksBancoTestes(BancoDeTestes banco) : IAsyncLifetime, IDisposable
{
    private static readonly DateTime Agora = new(2026, 9, 26, 12, 0, 0);
    private readonly AmbienteAplicacao _app = new();

    public Task InitializeAsync() => BancoDeTestes.Indisponivel is null ? banco.LimparAsync() : Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() => _app.Dispose();

    private static WebhookRecebido Webhook(string evento, int diasAtras, Action<WebhookRecebido>? encerrar = null)
    {
        var webhook = new WebhookRecebido
        {
            Origem = WebhookRecebido.OrigemAsaas, EventoExterno = evento, Tipo = "PAYMENT_RECEIVED",
            Corpo = "{\"id\":\"" + evento + "\",\"payment\":{\"customer\":\"Maria Lima\"}}", RecebidoEm = Agora.AddDays(-diasAtras)
        };
        encerrar?.Invoke(webhook);
        return webhook;
    }

    private Task LimparAsync() => _app.SemContextoAsync(async sp =>
    {
        await new JobsIntegracao(sp.GetRequiredService<EscolinhaDbContext>(), sp.GetRequiredService<IRelogioBrasil>(),
            NullLogger<JobsIntegracao>.Instance).LimparCorposAsync(CancellationToken.None);
        return 0;
    });

    [FatoComBanco]
    public async Task O_corpo_bruto_do_webhook_encerrado_sai_depois_de_90_dias_e_a_linha_fica()
    {
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            db.WebhooksRecebidos.AddRange(
                Webhook("evt_processado_91", 91, w => w.MarcarProcessado(Agora.AddDays(-91))),
                Webhook("evt_processado_89", 89, w => w.MarcarProcessado(Agora.AddDays(-89))),
                Webhook("evt_falhou_100", 100, w => w.Falhar(Agora.AddDays(-100), "Recusado pela conferência.")),
                Webhook("evt_ignorado_95", 95, w => w.Ignorar(Agora.AddDays(-95), "Evento de outra conta.")),
                Webhook("evt_pendente_120", 120));
            await db.SaveChangesAsync();
        }

        await LimparAsync();

        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            var webhooks = await db.WebhooksRecebidos.ToDictionaryAsync(w => w.EventoExterno);
            Assert.Equal(5, webhooks.Count); // nenhuma linha sai
            foreach (var apagado in new[] { "evt_processado_91", "evt_falhou_100", "evt_ignorado_95" })
                Assert.Equal((string.Empty, (DateTime?)Agora), (webhooks[apagado].Corpo, webhooks[apagado].CorpoApagadoEm));
            // O recente e o que ainda espera processamento guardam o corpo, com a situação e a observação intactas.
            foreach (var guardado in new[] { "evt_processado_89", "evt_pendente_120" })
                Assert.Equal(((DateTime?)null, true), (webhooks[guardado].CorpoApagadoEm, webhooks[guardado].Corpo.Contains("Maria Lima")));
            Assert.Equal((SituacaoWebhook.Falhou, "Recusado pela conferência."), (webhooks["evt_falhou_100"].Situacao, webhooks["evt_falhou_100"].Observacao));
        }

        // A rodada seguinte não mexe no que já foi apagado.
        _app.Tempo.Advance(TimeSpan.FromDays(1));
        await LimparAsync();
        await using (var db = BancoDeTestes.NovoContextoSemEscolinha())
        {
            Assert.Equal(Agora, (await db.WebhooksRecebidos.SingleAsync(w => w.EventoExterno == "evt_processado_91")).CorpoApagadoEm);
            // O de 89 dias passou para 90: ainda fica (só sai o de mais de 90).
            Assert.Null((await db.WebhooksRecebidos.SingleAsync(w => w.EventoExterno == "evt_processado_89")).CorpoApagadoEm);
        }
    }
}
