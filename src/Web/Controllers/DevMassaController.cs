using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Infraestrutura.Desenvolvimento;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Massa de demonstração (/dev/massa): só em Development, na escolinha do contexto, pelo gestor. Gera o cenário do
/// piloto (3 turmas, 60 alunos, irmãos, dois responsáveis) pelos serviços de verdade.
/// </summary>
[Route("dev/massa")]
[Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
[Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
public class DevMassaController(MassaDemonstracao massa, IWebHostEnvironment ambiente) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => ambiente.IsDevelopment() ? View() : NotFound();

    [HttpPost("")]
    public async Task<IActionResult> Gerar(CancellationToken ct)
    {
        if (!ambiente.IsDevelopment()) return NotFound();
        var resultado = await massa.GerarAsync(ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return resultado.Sucesso ? Redirect("/Alunos") : RedirectToAction(nameof(Index));
    }
}
