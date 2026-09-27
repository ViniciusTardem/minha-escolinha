using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>Consentimento de tratamento de dados e de uso de imagem, e a revogação (BL-M03-09, R6).</summary>
[Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
public class ConsentimentosController(ServicoConsentimentos servico, ServicoAlunos alunos) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Registrar(Guid alunoId, FinalidadeConsentimento finalidade, CancellationToken ct)
    {
        var ficha = await alunos.ObterFichaAsync(alunoId, ct);
        if (ficha is null || !ficha.Pode.Consentimentos) return NotFound();
        return Formulario(ficha, new RegistrarConsentimentoRequisicao
        {
            AlunoId = alunoId, Finalidade = finalidade, ResponsavelId = ficha.Responsaveis.FirstOrDefault()?.ResponsavelId
        });
    }

    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarConsentimentoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.RegistrarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AdicionarErro(resultado);
            var ficha = await alunos.ObterFichaAsync(requisicao.AlunoId, ct);
            return ficha is null ? NotFound() : Formulario(ficha, requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{requisicao.AlunoId}?aba=consentimentos");
    }

    [HttpGet]
    public async Task<IActionResult> Revogar(Guid id, CancellationToken ct)
    {
        var consentimento = await servico.ObterAsync(id, ct);
        if (consentimento is not { } c) return NotFound();
        ViewData["ModuloAtivo"] = "alunos";
        ViewData["Consentimento"] = c;
        return View(new RevogarConsentimentoRequisicao { ConsentimentoId = id });
    }

    [HttpPost]
    public async Task<IActionResult> Revogar(Guid id, RevogarConsentimentoRequisicao requisicao, CancellationToken ct)
    {
        var consentimento = await servico.ObterAsync(id, ct);
        if (consentimento is not { } c) return NotFound();
        requisicao.ConsentimentoId = id;
        var resultado = await servico.RevogarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = "alunos";
            ViewData["Consentimento"] = c;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{c.Consentimento.Aluno_ID}?aba=consentimentos");
    }

    private IActionResult Formulario(FichaAluno ficha, RegistrarConsentimentoRequisicao requisicao)
    {
        ViewData["ModuloAtivo"] = "alunos";
        ViewData["Ficha"] = ficha;
        return View("Registrar", requisicao);
    }
}
