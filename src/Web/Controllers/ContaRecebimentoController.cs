using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Conta de recebimento Asaas (BL-M02-10) — financeiro: só o gestor.</summary>
[Permissao(Recurso.FinanceiroEscolinha)]
public class ContaRecebimentoController(ServicoContaAsaas servico, IAmbienteExecucao ambiente) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "conta-recebimento";
        ViewData["Ambiente"] = ambiente;
        return View(await servico.ObterAsync(ct));
    }

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public IActionResult Vincular()
    {
        ViewData["ModuloAtivo"] = "conta-recebimento";
        ViewData["Ambiente"] = ambiente;
        return View(new VincularContaAsaasRequisicao());
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Vincular(VincularContaAsaasRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.VincularAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "conta-recebimento";
            ViewData["Ambiente"] = ambiente;
            ModelState.AdicionarErro(resultado);
            requisicao.ChaveApi = string.Empty;
            requisicao.SenhaAtual = string.Empty;
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }
}
