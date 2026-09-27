using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Convites de acesso (BL-M01-04) e em lote (BL-M12-07): quem tem ficha na escolinha, com a situação do acesso. O convite
/// vai pelo WhatsApp com um link de uso único; quem ainda não ativou continua recebendo tudo pelo WhatsApp (modo parcial).
/// </summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class ConvitesController(ServicoConvites convites, ServicoVerificacaoVinculos vinculos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? tipo, SituacaoAcesso? situacao, string? busca, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "convites";
        var todos = await convites.CandidatosAsync(ct);
        var filtrados = todos
            .Where(c => tipo switch
            {
                "responsaveis" => c.Tipo == TipoConvite.Responsavel,
                "professores" => c.Tipo == TipoConvite.Professor,
                _ => true
            })
            .Where(c => situacao is null || c.Situacao == situacao)
            .Where(c => string.IsNullOrWhiteSpace(busca) || c.Nome.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
        return View(new ConvitesViewModel(filtrados, todos, tipo, situacao, busca, await vinculos.ContarPendentesAsync(ct), convites.PodeConvidar));
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Convidar(TipoConvite tipo, Guid id, string? voltar, CancellationToken ct)
    {
        Avisar(await convites.ConvidarAsync(tipo, id, ct));
        return Voltar(voltar);
    }

    /// <summary>Os escolhidos na lista chegam como "tipo:id" (10 = responsável, 20 = professor).</summary>
    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Lote([FromForm(Name = "selecionados")] List<string>? selecionados, string? voltar, CancellationToken ct)
    {
        var fichas = (selecionados ?? [])
            .Select(s => s.Split(':'))
            .Where(p => p.Length == 2 && int.TryParse(p[0], out var t) && Enum.IsDefined((TipoConvite)t) && Guid.TryParse(p[1], out _))
            .Select(p => ((TipoConvite)int.Parse(p[0]), Guid.Parse(p[1])))
            .ToList();
        Avisar(await convites.ConvidarLoteAsync(fichas, ct));
        return Voltar(voltar);
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Reenviar(Guid id, string? voltar, CancellationToken ct)
    {
        Avisar(await convites.ReenviarAsync(id, ct));
        return Voltar(voltar);
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Cancelar(Guid id, string? motivo, string? voltar, CancellationToken ct)
    {
        Avisar(await convites.CancelarAsync(id, motivo, ct));
        return Voltar(voltar);
    }

    private void Avisar(Application.Comum.ResultadoOperacao resultado) =>
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;

    private IActionResult Voltar(string? voltar) => Url.IsLocalUrl(voltar) ? LocalRedirect(voltar) : RedirectToAction(nameof(Index));
}
