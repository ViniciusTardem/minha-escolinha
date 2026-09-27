using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Parâmetros de regra da escolinha por domínio (R7; BL-M02-06 a 09 e 12), com histórico de alterações.</summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class ParametrosController(ServicoParametros servico, IAutorizacao autorizacao) : Controller
{
    private static readonly DominioParametro[] Dominios =
    [
        DominioParametro.Presenca, DominioParametro.Financeiro, DominioParametro.Convocacao,
        DominioParametro.Mensagem, DominioParametro.Matricula
    ];

    [HttpGet]
    public async Task<IActionResult> Index(DominioParametro? dominio, CancellationToken ct) =>
        View(await MontarAsync(dominio, null, null, ct));

    /// <summary>
    /// Grava o domínio da aba. Janelas chegam como início e fim separados (campos de hora) e viram "HH:mm-HH:mm".
    /// Checkbox desmarcado não é enviado: o campo oculto logo depois dele manda "false".
    /// Os dicionários têm nome explícito: sem ele, na aba sem janela o ASP.NET ligaria janelaInicio com prefixo vazio
    /// (todos os campos do formulário) e "dominio" chegaria ao serviço como parâmetro.
    /// </summary>
    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Salvar(DominioParametro dominio,
        [FromForm(Name = "valores")] Dictionary<string, string?> valores,
        [FromForm(Name = "janelaInicio")] Dictionary<string, string?> janelaInicio,
        [FromForm(Name = "janelaFim")] Dictionary<string, string?> janelaFim, CancellationToken ct)
    {
        foreach (var (chave, inicio) in janelaInicio)
            valores[chave] = $"{inicio}-{janelaFim.GetValueOrDefault(chave)}";

        var resultado = await servico.AlterarAsync(new AlterarParametrosRequisicao { Valores = valores }, ct);
        if (!resultado.Sucesso)
        {
            // Nada foi gravado: a aba volta com o que foi digitado e os campos com problema marcados.
            ModelState.AdicionarErro(resultado);
            return View(nameof(Index), await MontarAsync(dominio, valores, resultado.Dados as IReadOnlyList<string>, ct));
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index), new { dominio });
    }

    [HttpGet]
    public async Task<IActionResult> Historico(string? chave, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "parametros";
        return View(new HistoricoParametrosViewModel(chave, await servico.HistoricoAsync(chave, ct)));
    }

    /// <summary>A aba do domínio; <paramref name="digitados"/> (quando o salvar falhou) substitui os valores gravados.</summary>
    private async Task<ParametrosViewModel> MontarAsync(DominioParametro? dominio, IReadOnlyDictionary<string, string?>? digitados,
        IReadOnlyList<string>? chavesComErro, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "parametros";
        var atual = dominio is { } d && Dominios.Contains(d) ? d : DominioParametro.Presenca;
        var parametros = (await servico.ListarAsync(ct))
            .Where(p => p.Definicao.Dominio == atual)
            .Select(p => digitados is not null && digitados.TryGetValue(p.Definicao.Chave, out var valor) ? p with { Valor = valor } : p)
            .ToList();
        return new ParametrosViewModel(atual, Dominios, parametros,
            autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Alterar), chavesComErro ?? []);
    }
}
