using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Responsáveis (BL-M03-01) e vínculos com os alunos (BL-M03-03): um cadastro por pessoa, reaproveitado entre irmãos.
/// Ver e editar o cadastro: gestor, e o professor os das turmas dele. Vínculos (quem paga, quem recebe): gestor.
/// </summary>
[Permissao(Recurso.CadastroAlunosResponsaveis)]
public class ResponsaveisController(ServicoResponsaveis servico, ServicoAlunos alunos) : Controller
{
    private const string Modulo = "responsaveis";

    [HttpGet]
    public async Task<IActionResult> Index(string? busca, int pagina, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        return View(new ResponsaveisViewModel(busca, await servico.ListarAsync(busca, pagina, ct)));
    }

    [HttpGet]
    public async Task<IActionResult> Ficha(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var ficha = await servico.ObterFichaAsync(id, ct);
        return ficha is null ? NotFound() : View(ficha);
    }

    /// <summary>A busca do cadastro do aluno (JSON): nome, CPF ou celular; CPF mascarado.</summary>
    [HttpGet]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    public async Task<IActionResult> Buscar(string? termo, CancellationToken ct) => Json(await servico.BuscarAsync(termo, ct));

    [HttpGet]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var requisicao = await servico.ParaEdicaoAsync(id, ct);
        return requisicao is null ? NotFound() : View(requisicao);
    }

    [HttpPost]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, ResponsavelRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = id;
        var resultado = await servico.EditarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Vincular(Guid alunoId, CancellationToken ct)
    {
        var requisicao = new VincularResponsavelRequisicao { AlunoId = alunoId };
        _ = requisicao.Pedido;
        return await FormularioVincularAsync(requisicao, ct);
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Vincular(VincularResponsavelRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.VincularAsync(requisicao.AlunoId, requisicao.Pedido, ct);
        if (resultado.Sucesso)
        {
            TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
            return Redirect($"/Alunos/Ficha/{requisicao.AlunoId}?aba=responsaveis");
        }

        ModelState.AdicionarErro(resultado);
        ViewData["Duplicidade"] = resultado.Dados;
        if (requisicao.Pedido.ResponsavelId is not null) requisicao.Pedido.Nome = requisicao.Pedido.Cpf = requisicao.Pedido.Telefone = requisicao.Pedido.Email = null;
        return await FormularioVincularAsync(requisicao, ct);
    }

    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Vinculo(Guid id, CancellationToken ct)
    {
        var vinculo = await servico.VinculoParaEdicaoAsync(id, ct);
        if (vinculo is not { } v) return NotFound();
        ViewData["ModuloAtivo"] = "alunos";
        ViewData["Vinculo"] = v;
        return View(new VinculoRequisicao
        {
            VinculoId = id, Parentesco = v.Vinculo.Parentesco, Pagador = v.Vinculo.Pagador, RecebeCobranca = v.Vinculo.RecebeCobranca,
            RecebeNotificacao = v.Vinculo.RecebeNotificacao
        });
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Vinculo(Guid id, VinculoRequisicao requisicao, CancellationToken ct)
    {
        var vinculo = await servico.VinculoParaEdicaoAsync(id, ct);
        if (vinculo is not { } v) return NotFound();
        requisicao.VinculoId = id;
        var resultado = await servico.EditarVinculoAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "alunos";
            ViewData["Vinculo"] = v;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{v.Vinculo.Aluno_ID}?aba=responsaveis");
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Desvincular(Guid id, CancellationToken ct)
    {
        var vinculo = await servico.VinculoParaEdicaoAsync(id, ct);
        if (vinculo is not { } v) return NotFound();
        var resultado = await servico.DesvincularAsync(id, ct);
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{v.Vinculo.Aluno_ID}?aba=responsaveis");
    }

    private async Task<IActionResult> FormularioVincularAsync(VincularResponsavelRequisicao requisicao, CancellationToken ct)
    {
        var ficha = await alunos.ObterFichaAsync(requisicao.AlunoId, ct);
        if (ficha is null) return NotFound();
        ViewData["ModuloAtivo"] = "alunos";
        ViewData["Ficha"] = ficha;
        ViewData["Escolhidos"] = await servico.EscolhidosAsync(
            requisicao.Responsaveis.Where(r => r.ResponsavelId is not null).Select(r => r.ResponsavelId!.Value), ct);
        return View("Vincular", requisicao);
    }
}
