using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Financeiro;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// O financeiro da escolinha (S4): o painel de pagamentos por mês (BL-M06-03), a fila de divergências (BL-M06-06) e as
/// ações da gestão. Tudo exige o financeiro da escolinha, que é só do gestor (§7, BL-M06-24): o professor recebe 403 em
/// qualquer caminho. Nenhuma ação fala com a Asaas no request: o que muda lá sai por job (regra de ouro 2).
/// </summary>
[Permissao(Recurso.FinanceiroEscolinha)]
public class MensalidadesController(ServicoMensalidades servico, IRelogioBrasil relogio) : Controller
{
    public const string Modulo = "mensalidades";

    [HttpGet]
    public async Task<IActionResult> Index(FiltroMensalidades filtro, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var painel = await servico.PainelAsync(filtro, ct);
        return painel is null ? Forbid() : View(new MensalidadesViewModel(filtro, painel, servico.PodeAlterar));
    }

    [HttpGet]
    public async Task<IActionResult> Divergencias(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var filtro = new FiltroMensalidades { SoDivergencias = true };
        var painel = await servico.PainelAsync(filtro, ct);
        return painel is null ? Forbid() : View(new MensalidadesViewModel(filtro, painel, servico.PodeAlterar));
    }

    /// <summary>Roda agora a rotina que roda toda madrugada: projeção, vencidas, régua e emissões.</summary>
    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Atualizar(string? voltar, CancellationToken ct) =>
        Voltar(await servico.AtualizarAsync(ct), voltar);

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken ct)
    {
        if (await servico.ParaAcaoAsync(id, ct) is not { PodeConfirmar: true } acao) return NotFound();
        return Formulario("Confirmar", acao, new ConfirmarMensalidadeRequisicao { Id = id, Valor = acao.Linha.Valor });
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Confirmar(ConfirmarMensalidadeRequisicao requisicao, string? voltar, CancellationToken ct)
    {
        var resultado = await servico.ConfirmarAsync(requisicao, ct);
        if (resultado.Sucesso) return Voltar(resultado, voltar);
        ModelState.AdicionarErro(resultado);
        return await ReexibirAsync("Confirmar", requisicao.Id, requisicao, ct);
    }

    /// <summary>A entrada de uma turma inteira no sistema: confirma as marcadas com o valor da matrícula.</summary>
    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> ConfirmarMarcadas([FromForm(Name = "selecionadas")] List<Guid>? selecionadas, string? voltar,
        CancellationToken ct) =>
        Voltar(await servico.ConfirmarVariasAsync(selecionadas ?? [], ct), voltar);

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Encerrar(Guid id, SituacaoCobranca para, CancellationToken ct)
    {
        if (para is not (SituacaoCobranca.Cancelada or SituacaoCobranca.Isenta)) return NotFound();
        if (await servico.ParaAcaoAsync(id, ct) is not { PodeCancelar: true } acao) return NotFound();
        return Formulario("Encerrar", acao, new EncerrarMensalidadeRequisicao { Id = id, Para = para });
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Encerrar(EncerrarMensalidadeRequisicao requisicao, string? voltar, CancellationToken ct)
    {
        var resultado = await servico.EncerrarAsync(requisicao, ct);
        if (resultado.Sucesso) return Voltar(resultado, voltar);
        ModelState.AdicionarErro(resultado);
        return await ReexibirAsync("Encerrar", requisicao.Id, requisicao, ct);
    }

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Receber(Guid id, CancellationToken ct)
    {
        if (await servico.ParaAcaoAsync(id, ct) is not { PodeReceberPorFora: true } acao) return NotFound();
        return Formulario("Receber", acao, new ReceberPorForaRequisicao { Id = id, Data = DateOnly.FromDateTime(relogio.Hoje()) });
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Receber(ReceberPorForaRequisicao requisicao, string? voltar, CancellationToken ct)
    {
        var resultado = await servico.ReceberPorForaAsync(requisicao, ct);
        if (resultado.Sucesso) return Voltar(resultado, voltar);
        ModelState.AdicionarErro(resultado);
        return await ReexibirAsync("Receber", requisicao.Id, requisicao, ct);
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Reenviar(Guid id, string? voltar, CancellationToken ct) =>
        Voltar(await servico.ReenviarAsync(id, ct), voltar);

    /// <summary>A recusada pela Asaas volta para a emissão, depois que a gestão corrigiu o que foi recusado.</summary>
    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> TentarDeNovo(Guid id, string? voltar, CancellationToken ct) =>
        Voltar(await servico.TentarDeNovoAsync(id, ct), voltar);

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Resolver(Guid id, CancellationToken ct)
    {
        if (await servico.ParaAcaoAsync(id, ct) is not { } acao || acao.Linha.Sinal == SinalCobranca.Nenhum) return NotFound();
        return Formulario("Resolver", acao, new ResolverDivergenciaRequisicao { Id = id, Resolucao = ResolucaoDivergencia.Ciencia });
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Resolver(ResolverDivergenciaRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.ResolverAsync(requisicao, ct);
        if (resultado.Sucesso) return Voltar(resultado, "/Mensalidades/Divergencias");
        ModelState.AdicionarErro(resultado);
        return await ReexibirAsync("Resolver", requisicao.Id, requisicao, ct);
    }

    private IActionResult Formulario(string view, CobrancaParaAcao acao, object modelo)
    {
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Cobranca"] = acao;
        return View(view, modelo);
    }

    private async Task<IActionResult> ReexibirAsync(string view, Guid id, object modelo, CancellationToken ct) =>
        await servico.ParaAcaoAsync(id, ct) is { } acao ? Formulario(view, acao, modelo) : NotFound();

    /// <summary>PRG + toast; volta para a tela de origem (lista ou ficha) quando ela é local.</summary>
    private IActionResult Voltar(ResultadoOperacao resultado, string? voltar)
    {
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return Url.IsLocalUrl(voltar) ? LocalRedirect(voltar) : RedirectToAction(nameof(Index));
    }
}
