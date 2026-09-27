using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Dados institucionais da escolinha (BL-M02-01).</summary>
[Permissao(Recurso.ConfiguracaoEscolinha)]
public class EscolinhaController(ServicoEscolinha servico) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "escolinha";
        var dados = await servico.ObterAsync(ct);
        return dados is null ? NotFound() : View(dados);
    }

    [HttpGet]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "escolinha";
        var d = await servico.ObterAsync(ct);
        if (d is null) return NotFound();
        return View(new DadosEscolinhaRequisicao
        {
            Nome = d.Nome,
            RazaoSocial = d.RazaoSocial,
            Cnpj = Formatacao.Cnpj(d.Cnpj),
            Telefone = Formatacao.Telefone(d.Telefone),
            Email = d.Email,
            Logradouro = d.Endereco.Logradouro,
            Numero = d.Endereco.Numero,
            Complemento = d.Endereco.Complemento,
            Bairro = d.Endereco.Bairro,
            Cidade = d.Endereco.Cidade,
            Uf = d.Endereco.Uf,
            Cep = Formatacao.Cep(d.Endereco.Cep)
        });
    }

    [HttpPost]
    [Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Editar(DadosEscolinhaRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.EditarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "escolinha";
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Index));
    }
}
