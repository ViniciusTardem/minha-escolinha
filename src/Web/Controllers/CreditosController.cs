using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Plataforma;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Plano, saldo e histórico de créditos de mensagem (BL-M02-11) e a compra de pacotes pelo Stripe da LDNA. Comprar é
/// gastar dinheiro da escolinha: pede o financeiro (só o gestor). Conceder à mão continua sendo da plataforma.
/// </summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class CreditosController(ServicoCreditos servico, ServicoCompraCreditos compras, IAutorizacao autorizacao) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int pagina = 1, CancellationToken ct = default)
    {
        ViewData["ModuloAtivo"] = "creditos";
        var creditos = await servico.ObterAsync(pagina, ct);
        if (creditos is null) return NotFound();
        return View(new CreditosViewModel(creditos, await compras.ObterTelaAsync(ct),
            autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Alterar)));
    }

    /// <summary>Abre o pagamento no Stripe e leva a pessoa até lá.</summary>
    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Comprar(Guid pacoteId, CancellationToken ct)
    {
        var resultado = await compras.IniciarCompraAsync(pacoteId, ct);
        if (resultado is { Sucesso: true, Dados: string url }) return Redirect(url);

        TempData[ChavesTempData.Erro] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Volta do Stripe (pagou ou desistiu). Se a compra ainda não terminou, pede a conferência na hora.</summary>
    [HttpGet]
    public async Task<IActionResult> Compra(Guid id, bool cancelada = false, CancellationToken ct = default)
    {
        ViewData["ModuloAtivo"] = "creditos";
        var compra = await compras.ObterCompraAsync(id, ct);
        if (compra is null) return NotFound();
        if (!compra.Final) await compras.SolicitarConferenciaAsync(id, ct);
        var saldo = (await servico.ObterAsync(1, ct))?.Saldo;
        return View(new CompraCreditosViewModel(compra, cancelada, saldo));
    }

    /// <summary>Para a página da compra acompanhar sozinha (js/compra-creditos.js).</summary>
    [HttpGet]
    public async Task<IActionResult> SituacaoCompra(Guid id, CancellationToken ct)
    {
        var compra = await compras.ObterCompraAsync(id, ct);
        if (compra is null) return NotFound();
        return Json(new { situacao = compra.Situacao.ToString(), final = compra.Final });
    }
}
