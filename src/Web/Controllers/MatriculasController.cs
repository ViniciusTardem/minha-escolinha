using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Matrícula em turma e em mais de uma turma (BL-M03-04): nova, encerrar e o valor próprio. Mexe na cobrança: só o gestor.
/// </summary>
[Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
[Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
public class MatriculasController(ServicoMatriculas servico, ServicoAlunos alunos, ServicoTurmas turmas, IRelogioBrasil relogio) : Controller
{
    private const string Modulo = "alunos";

    [HttpGet]
    public async Task<IActionResult> Nova(Guid alunoId, CancellationToken ct)
    {
        var ficha = await alunos.ObterFichaAsync(alunoId, ct);
        if (ficha is null) return NotFound();
        return await FormularioNovaAsync(ficha, new NovaMatriculaRequisicao { AlunoId = alunoId, DataInicio = DateOnly.FromDateTime(relogio.Hoje()) }, ct);
    }

    [HttpPost]
    public async Task<IActionResult> Nova(NovaMatriculaRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.NovaAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AdicionarErro(resultado);
            var ficha = await alunos.ObterFichaAsync(requisicao.AlunoId, ct);
            return ficha is null ? NotFound() : await FormularioNovaAsync(ficha, requisicao, ct);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{requisicao.AlunoId}?aba=matriculas");
    }

    [HttpGet]
    public async Task<IActionResult> Encerrar(Guid id, CancellationToken ct)
    {
        var matricula = await servico.ObterAsync(id, ct);
        if (matricula is not { } m) return NotFound();
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Matricula"] = m;
        return View(new EncerrarMatriculaRequisicao { MatriculaId = id, Data = DateOnly.FromDateTime(relogio.Hoje()) });
    }

    [HttpPost]
    public async Task<IActionResult> Encerrar(Guid id, EncerrarMatriculaRequisicao requisicao, CancellationToken ct)
    {
        var matricula = await servico.ObterAsync(id, ct);
        if (matricula is not { } m) return NotFound();
        requisicao.MatriculaId = id;
        var resultado = await servico.EncerrarAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ViewData["Matricula"] = m;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{m.Matricula.Aluno_ID}?aba=matriculas");
    }

    [HttpGet]
    public async Task<IActionResult> Mensalidade(Guid id, CancellationToken ct)
    {
        var matricula = await servico.ObterAsync(id, ct);
        var requisicao = await servico.MensalidadeParaEdicaoAsync(id, ct);
        if (matricula is not { } m || requisicao is null) return NotFound();
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Matricula"] = m;
        return View(requisicao);
    }

    [HttpPost]
    public async Task<IActionResult> Mensalidade(Guid id, MensalidadeMatriculaRequisicao requisicao, CancellationToken ct)
    {
        var matricula = await servico.ObterAsync(id, ct);
        if (matricula is not { } m) return NotFound();
        requisicao.MatriculaId = id;
        var resultado = await servico.AlterarMensalidadeAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ViewData["Matricula"] = m;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return Redirect($"/Alunos/Ficha/{m.Matricula.Aluno_ID}?aba=matriculas");
    }

    private async Task<IActionResult> FormularioNovaAsync(FichaAluno ficha, NovaMatriculaRequisicao requisicao, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Ficha"] = ficha;
        var jaMatriculado = ficha.Matriculas.Where(x => x.Situacao == Domain.Alunos.SituacaoMatricula.Ativa).Select(x => x.TurmaId).ToHashSet();
        ViewData["Turmas"] = (await turmas.ListarAsync(false, null, ct)).Where(t => !jaMatriculado.Contains(t.Id)).ToList();
        return View("Nova", requisicao);
    }
}
