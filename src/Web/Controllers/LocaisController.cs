using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Quadras e locais de treino (spec §5): consumidos pela turma e, no S5, pelo evento.</summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class LocaisController(ServicoLocais servico) : Controller
{
    private const string Modulo = "locais";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        return View(await servico.ListarAsync(ct));
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public IActionResult Novo()
    {
        ViewData["ModuloAtivo"] = Modulo;
        return View("Formulario", ComLinhaEmBranco(new LocalRequisicao()));
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var local = await servico.ParaEdicaoAsync(id, ct);
        return local is null ? NotFound() : View("Formulario", ComLinhaEmBranco(local));
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Salvar(LocalRequisicao requisicao, string? voltar, CancellationToken ct)
    {
        var resultado = await servico.SalvarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ModelState.AdicionarErro(resultado);
            return View("Formulario", ComLinhaEmBranco(requisicao));
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Url.IsLocalUrl(voltar) ? LocalRedirect(voltar) : RedirectToAction(nameof(Index));
    }

    /// <summary>Sempre uma linha de horário em branco no fim: sem JS, é por ela que se acrescenta um horário (P31).</summary>
    private static LocalRequisicao ComLinhaEmBranco(LocalRequisicao requisicao)
    {
        if (requisicao.Horarios.Count == 0 || !requisicao.Horarios[^1].Vazia) requisicao.Horarios.Add(new Application.Equipe.FaixaRequisicao());
        return requisicao;
    }
}
