using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Modalidades, perfil e interruptores de recurso (BL-M02-02, BL-M02-03).</summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class ModalidadesController(ServicoModalidades servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "modalidades";
        return View(await servico.ListarAsync(ct));
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public IActionResult Nova()
    {
        ViewData["ModuloAtivo"] = "modalidades";
        return View("Formulario", ModalidadeRequisicao.NovaDoPerfil(PerfilModalidade.Coletiva));
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Nova(ModalidadeRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = null;
        var resultado = await servico.CriarAsync(requisicao, ct);
        return Concluir(resultado, requisicao);
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "modalidades";
        var m = await servico.ObterAsync(id, ct);
        if (m is null) return NotFound();
        return View("Formulario", new ModalidadeRequisicao
        {
            Id = m.Id,
            Nome = m.Nome,
            Perfil = m.Perfil,
            TemAdversario = m.Recursos.Adversario,
            TemPlacarSumula = m.Recursos.PlacarSumula,
            TemClassificacao = m.Recursos.Classificacao,
            TemConvocacao = m.Recursos.Convocacao,
            TemGraduacao = m.Recursos.Graduacao,
            TemApresentacao = m.Recursos.Apresentacao,
            DuracaoPadraoEventoMinutos = m.DuracaoPadraoEventoMinutos
        });
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, ModalidadeRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = id;
        var resultado = await servico.EditarAsync(requisicao, ct);
        return Concluir(resultado, requisicao);
    }

    private IActionResult Concluir(Application.Comum.ResultadoOperacao resultado, ModalidadeRequisicao requisicao)
    {
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "modalidades";
            ModelState.AdicionarErro(resultado);
            return View("Formulario", requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }
}
