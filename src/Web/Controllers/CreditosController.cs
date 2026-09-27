using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Plano, saldo e histórico de créditos de mensagem (BL-M02-11). Conceder é da plataforma.</summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class CreditosController(ServicoCreditos servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int pagina = 1, CancellationToken ct = default)
    {
        ViewData["ModuloAtivo"] = "creditos";
        var creditos = await servico.ObterAsync(pagina, ct);
        return creditos is null ? NotFound() : View(creditos);
    }
}
