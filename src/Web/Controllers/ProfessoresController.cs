using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Professores e equipe (BL-M08-01) e a escala de disponibilidade com as indisponibilidades (BL-M08-04).</summary>
[Permissao(Recurso.CadastroProfessores)]
public class ProfessoresController(ServicoProfessores servico, ServicoConvites convites) : Controller
{
    private const string Modulo = "professores";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        return View(await servico.ListarAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> Ficha(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var ficha = await servico.ObterFichaAsync(id, ct);
        if (ficha is null) return NotFound();
        ViewData["Acesso"] = await convites.SituacaoDaFichaAsync(Domain.Mensageria.TipoConvite.Professor, id, ct);
        return View(new FichaProfessorViewModel(ficha, new IndisponibilidadeRequisicao { ProfessorId = id }));
    }

    /// <summary>Professor novo; <paramref name="candidato"/> ("origem|id") traz o cadastro reaproveitado já preenchido.</summary>
    [HttpGet]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Novo(string? candidato, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var (origem, origemId) = LerCandidato(candidato);
        var requisicao = await servico.PrepararAsync(origem, origemId, ct) ?? new ProfessorRequisicao();
        return View("Formulario", await FormularioAsync(requisicao, ct));
    }

    [HttpPost]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Novo(ProfessorRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(requisicao, ct);
        if (!resultado.Sucesso) return await DevolverAsync(resultado, requisicao, ct);
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id = resultado.Dados });
    }

    [HttpGet]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var requisicao = await servico.ParaEdicaoAsync(id, ct);
        return requisicao is null ? NotFound() : View("Formulario", await FormularioAsync(requisicao, ct));
    }

    [HttpPost]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, ProfessorRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = id;
        var resultado = await servico.EditarAsync(requisicao, ct);
        if (!resultado.Sucesso) return await DevolverAsync(resultado, requisicao, ct);
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    [HttpGet]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Disponibilidade(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var requisicao = await servico.DisponibilidadeParaEdicaoAsync(id, ct);
        if (requisicao is null) return NotFound();
        return View(await DisponibilidadeAsync(requisicao, ct));
    }

    [HttpPost]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Disponibilidade(Guid id, DisponibilidadeRequisicao requisicao, CancellationToken ct)
    {
        requisicao.ProfessorId = id;
        var resultado = await servico.SalvarDisponibilidadeAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ModelState.AdicionarErro(resultado);
            return View(await DisponibilidadeAsync(requisicao, ct));
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    [HttpPost]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> Indisponibilidade(Guid id, [Bind(Prefix = "Nova")] IndisponibilidadeRequisicao requisicao, CancellationToken ct)
    {
        requisicao.ProfessorId = id;
        var resultado = await servico.AdicionarIndisponibilidadeAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ModelState.AdicionarErro(resultado, "Nova");
            var ficha = await servico.ObterFichaAsync(id, ct);
            return ficha is null ? NotFound() : View(nameof(Ficha), new FichaProfessorViewModel(ficha, requisicao));
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    [HttpPost]
    [Permissao(Recurso.CadastroProfessores, Operacao.Alterar)]
    public async Task<IActionResult> RemoverIndisponibilidade(Guid id, Guid indisponibilidadeId, CancellationToken ct)
    {
        var resultado = await servico.RemoverIndisponibilidadeAsync(id, indisponibilidadeId, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    private async Task<IActionResult> DevolverAsync(ResultadoOperacao resultado, ProfessorRequisicao requisicao, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        ModelState.AdicionarErro(resultado);
        return View("Formulario", await FormularioAsync(requisicao, ct));
    }

    /// <summary>O formulário tem a requisição como modelo (campos na raiz); os cadastros reaproveitáveis vão no ViewData.</summary>
    private async Task<ProfessorRequisicao> FormularioAsync(ProfessorRequisicao requisicao, CancellationToken ct)
    {
        ViewData["Candidatos"] = requisicao.Id is null ? await servico.CandidatosAsync(ct) : Array.Empty<CandidatoProfessor>();
        return requisicao;
    }

    private async Task<DisponibilidadeRequisicao> DisponibilidadeAsync(DisponibilidadeRequisicao requisicao, CancellationToken ct)
    {
        ViewData["Professor"] = (await servico.ObterFichaAsync(requisicao.ProfessorId, ct))?.Nome ?? "Professor";
        // Sempre uma linha em branco no fim: sem JS, é por ela que se acrescenta uma faixa.
        if (requisicao.Faixas.Count == 0 || !requisicao.Faixas[^1].Vazia) requisicao.Faixas.Add(new FaixaRequisicao());
        return requisicao;
    }

    private static (OrigemCadastroProfessor Origem, Guid? Id) LerCandidato(string? candidato)
    {
        var partes = candidato?.Split('|', 2);
        return partes is { Length: 2 } && Enum.TryParse<OrigemCadastroProfessor>(partes[0], out var origem) && Guid.TryParse(partes[1], out var id)
            ? (origem, id)
            : (OrigemCadastroProfessor.PessoaNova, null);
    }
}
