using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Integracao.Stripe;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Plataforma;

namespace MinhaEscolinha.Application.Plataforma;

/// <summary>
/// O gestor compra um pacote de créditos: a compra nasce aqui, o pagamento é no Checkout do Stripe (cartão, boleto ou
/// Pix) e os créditos entram quando o Stripe confirma (<see cref="ConferenciaCompraCreditos"/>), nunca antes.
/// </summary>
public class ServicoCompraCreditos(
    IPlataformaRepositorio repositorio,
    IGatewayStripe gateway,
    IAgendadorPlataforma agendador,
    IContextoEscolinha contexto,
    IUsuarioAtual usuarioAtual,
    IRelogioBrasil relogio,
    OpcoesStripe opcoes,
    ILogger<ServicoCompraCreditos> logger)
{
    public async Task<TelaCompraCreditos> ObterTelaAsync(CancellationToken ct = default)
    {
        var pacotes = opcoes.Habilitado ? await repositorio.ListarPacotesAsync(somenteAtivos: true, ct) : [];
        var compras = await repositorio.ListarComprasAsync(20, ct);
        return new TelaCompraCreditos(opcoes.Habilitado, opcoes.Descricao,
            pacotes.Select(PacoteResposta.De).ToList(), compras.Select(CompraResposta.De).ToList());
    }

    /// <summary>
    /// Dados = a URL do Checkout, para onde a pessoa é levada. Única chamada externa dentro de uma requisição no sistema
    /// (exceção documentada à regra de ouro 2, DECISOES D26): a pessoa está esperando para ir ao pagamento. A compra é
    /// gravada antes, e a chamada leva a compra como chave de idempotência.
    /// </summary>
    public async Task<ResultadoOperacao> IniciarCompraAsync(Guid pacoteId, CancellationToken ct = default)
    {
        if (!opcoes.Habilitado) return ResultadoOperacao.Erro("A compra de créditos está desligada no momento. Fale com a equipe da LDNA.");
        if (contexto.EscolinhaId is not { } escolinhaId) return ResultadoOperacao.Erro("Entre numa escolinha para comprar créditos.");

        if (await repositorio.ObterPacoteAsync(pacoteId, ct) is not { Ativo: true } pacote)
            return ResultadoOperacao.Erro("Esse pacote não está mais à venda. Escolha outro.");

        var compra = CompraCreditos.Nova(pacote, relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome ?? "Gestor");
        repositorio.AdicionarCompra(compra);
        await repositorio.SalvarAsync(ct);

        SessaoCheckoutCriada sessao;
        try
        {
            sessao = await gateway.CriarSessaoCheckoutAsync(new PedidoCheckout(
                compra.CompraCreditos_ID, escolinhaId, pacote.Descricao(), Centavos(pacote.Preco),
                UrlDaCompra(compra.CompraCreditos_ID), $"{UrlDaCompra(compra.CompraCreditos_ID)}?cancelada=true",
                usuarioAtual.Login), ct);
        }
        catch (FalhaIntegracaoException ex)
        {
            compra.Falhar(relogio.Agora(), ex.Message);
            await repositorio.SalvarAsync(ct);
            logger.LogWarning("Stripe: sessão de pagamento não criada para a compra {Compra} ({Natureza}): {Mensagem}",
                compra.CompraCreditos_ID, ex.Natureza, ex.Message);
            return ResultadoOperacao.Erro(ex.Retentavel
                ? "O Stripe não respondeu agora. Tente de novo em alguns minutos."
                : "O Stripe recusou abrir o pagamento. Avise a equipe da LDNA.");
        }

        compra.SessaoCheckoutId = sessao.SessaoId;
        compra.ExpiraEm = sessao.ExpiraEmUtc is { } utc ? DataHoraBrasil.ConverterParaBrasilia(utc) : null;
        await repositorio.SalvarAsync(ct);
        logger.LogInformation("Compra {Compra} do pacote {Pacote} aberta no Stripe (sessão {Sessao}).",
            compra.CompraCreditos_ID, pacote.Nome, sessao.SessaoId);
        return ResultadoOperacao.Ok("Abrindo o pagamento.", sessao.Url);
    }

    public async Task<CompraResposta?> ObterCompraAsync(Guid id, CancellationToken ct = default) =>
        await repositorio.ObterCompraAsync(id, ct) is { } compra ? CompraResposta.De(compra) : null;

    /// <summary>Volta do pagamento: pede uma conferência já (job), sem esperar o webhook nem a varredura.</summary>
    public async Task SolicitarConferenciaAsync(Guid id, CancellationToken ct = default)
    {
        if (contexto.EscolinhaId is not { } escolinhaId) return;
        if (await repositorio.ObterCompraAsync(id, ct) is { Final: false, SessaoCheckoutId: not null })
            agendador.ConferirCompraCreditos(escolinhaId, id);
    }

    public static long Centavos(decimal valor) => (long)Math.Round(valor * 100m, MidpointRounding.AwayFromZero);

    private string UrlDaCompra(Guid compraId) => $"{opcoes.UrlPublica.TrimEnd('/')}/Creditos/Compra/{compraId}";
}
