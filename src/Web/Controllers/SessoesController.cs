using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Cancelamento de uma sessão com motivo (BL-M04-06): o gestor, qualquer uma; o professor, as dele. O aviso às famílias
/// da turma é o gatilho "Treino cancelado" e sai pelo motor único de mensagens (S2).
/// </summary>
[Permissao(Recurso.RegistroPresenca, Operacao.Alterar)]
public class SessoesController(ServicoSessoes servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "agenda";
        var sessao = await servico.ObterAsync(id, ct);
        if (sessao is null) return NotFound();
        ViewData["Sessao"] = sessao;
        return View(new CancelarSessaoRequisicao { SessaoId = id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancelar(Guid id, CancelarSessaoRequisicao requisicao, string? voltar, CancellationToken ct)
    {
        var sessao = await servico.ObterAsync(id, ct);
        if (sessao is null) return NotFound();
        requisicao.SessaoId = id;
        var resultado = await servico.CancelarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "agenda";
            ViewData["Sessao"] = sessao;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Url.IsLocalUrl(voltar) ? LocalRedirect(voltar) : Redirect($"/Turmas/Ficha/{sessao.TurmaId}");
    }
}
