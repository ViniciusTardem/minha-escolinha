using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Turmas (BL-M04-01), composição e ocupação de vagas (BL-M04-03) e o alerta de conflito do professor e do local ao marcar
/// treino (BL-M04-09 e P31). Ver: gestor todas, professor as dele (BL-M08-05). Criar e alterar: configuração da escolinha (gestor).
/// </summary>
[Permissao(Recurso.CadastroAlunosResponsaveis)]
public class TurmasController(ServicoTurmas servico) : Controller
{
    private const string Modulo = "turmas";

    [HttpGet]
    public async Task<IActionResult> Index(bool encerradas, Guid? modalidade, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Encerradas"] = encerradas;
        ViewData["Modalidade"] = modalidade;
        ViewData["Opcoes"] = await servico.OpcoesAsync(ct);
        return View(await servico.ListarAsync(encerradas, modalidade, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Ficha(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var ficha = await servico.ObterFichaAsync(id, ct);
        return ficha is null ? NotFound() : View(ficha);
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Nova(CancellationToken ct) => await FormularioAsync(await servico.NovaAsync(ct), ct);

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Nova(TurmaRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(requisicao, ct);
        if (!resultado.Sucesso) return await DevolverAsync(resultado, requisicao, ct);
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id = resultado.Dados });
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        var requisicao = await servico.ParaEdicaoAsync(id, ct);
        return requisicao is null ? NotFound() : await FormularioAsync(requisicao, ct);
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, TurmaRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = id;
        var resultado = await servico.EditarAsync(requisicao, ct);
        if (!resultado.Sucesso) return await DevolverAsync(resultado, requisicao, ct);
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var ficha = await servico.ObterFichaAsync(id, ct);
        if (ficha is null) return NotFound();
        ViewData["Ficha"] = ficha;
        return View(new EncerrarTurmaRequisicao { TurmaId = id });
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarTurmaRequisicao requisicao, CancellationToken ct)
    {
        requisicao.TurmaId = id;
        var resultado = await servico.EncerrarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ModelState.AdicionarErro(resultado);
            var ficha = await servico.ObterFichaAsync(id, ct);
            if (ficha is null) return NotFound();
            ViewData["Ficha"] = ficha;
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    private async Task<IActionResult> DevolverAsync(ResultadoOperacao resultado, TurmaRequisicao requisicao, CancellationToken ct)
    {
        ModelState.AdicionarErro(resultado);
        if (resultado.Dados is ConflitosTurma conflitos) ViewData["Conflitos"] = conflitos.Conflitos;
        return await FormularioAsync(requisicao, ct);
    }

    /// <summary>O formulário com as listas dos selects e sempre uma linha de horário em branco no fim (sem JS, é por ela que se acrescenta).</summary>
    private async Task<IActionResult> FormularioAsync(TurmaRequisicao requisicao, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Opcoes"] = await servico.OpcoesAsync(ct);
        if (requisicao.Horarios.Count == 0 || !requisicao.Horarios[^1].Vazio) requisicao.Horarios.Add(new HorarioRequisicao());
        return View("Formulario", requisicao);
    }
}
