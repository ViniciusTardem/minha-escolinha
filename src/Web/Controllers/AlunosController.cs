using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Alunos (BL-M03-02, BL-M03-05, BL-M03-07, BL-M03-08, BL-M03-11): a lista por turma e geral, o cadastro em etapas com
/// responsáveis sem duplicidade, a ficha com o dado sensível filtrado e a situação. Ver: gestor todos, professor os das
/// turmas dele. Cadastrar e mudar a situação: gestor (mexe na cobrança).
/// </summary>
[Permissao(Recurso.CadastroAlunosResponsaveis)]
public class AlunosController(ServicoAlunos servico, ServicoTurmas turmas, ServicoResponsaveis responsaveis, IParametros parametros,
    ServicoPresenca presenca, Application.Financeiro.ServicoMensalidades mensalidades, IRelogioBrasil relogio) : Controller
{
    private const string Modulo = "alunos";

    [HttpGet]
    public async Task<IActionResult> Index(FiltroAlunos filtro, int pagina, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var lista = await servico.ListarAsync(filtro, pagina, ct);
        var turmasVisiveis = await turmas.ListarAsync(false, null, ct);
        ViewData["Risco"] = (await presenca.AlunosEmRiscoAsync(ct)).ToDictionary(r => r.AlunoId);
        return View(new AlunosViewModel(filtro, lista, turmasVisiveis, DateOnly.FromDateTime(relogio.Hoje())));
    }

    [HttpGet]
    public async Task<IActionResult> Ficha(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var ficha = await servico.ObterFichaAsync(id, ct);
        if (ficha is null) return NotFound();
        ViewData["Presenca"] = await presenca.DoAlunoAsync(id, ct);
        // Só para quem vê o financeiro (o gestor, BL-M06-24): para os demais a aba nem existe.
        ViewData["Financeiro"] = await mensalidades.DoAlunoAsync(id, ct);
        ViewData["FinanceiroAlterar"] = mensalidades.PodeAlterar;
        return View(ficha);
    }

    [HttpGet]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Novo(Guid? turmaId, CancellationToken ct)
    {
        var requisicao = NovoAlunoRequisicao.Vazia(DateOnly.FromDateTime(relogio.Hoje()));
        requisicao.TurmaId = turmaId;
        return await FormularioNovoAsync(requisicao, ct);
    }

    [HttpPost]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Novo(NovoAlunoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.CriarAsync(requisicao, ct);
        if (resultado.Sucesso)
        {
            TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
            return RedirectToAction(nameof(Ficha), new { id = resultado.Dados });
        }

        ModelState.AdicionarErro(resultado);
        ViewData["Duplicidade"] = resultado.Dados;
        // A escolha "usar o cadastro existente" chega pelo botão: os campos digitados daquele responsável deixam de valer.
        foreach (var r in requisicao.Responsaveis.Where(r => r.ResponsavelId is not null))
        {
            r.Nome = r.Cpf = r.Telefone = r.Email = null;
        }
        return await FormularioNovoAsync(requisicao.Completar(), ct);
    }

    [HttpGet]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var requisicao = await servico.DadosParaEdicaoAsync(id, ct);
        return requisicao is null ? NotFound() : View(requisicao);
    }

    [HttpPost]
    [Permissao(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar)]
    public async Task<IActionResult> Editar(Guid id, DadosAlunoRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = id;
        var resultado = await servico.EditarDadosAsync(requisicao, ct);
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
    [Permissao(Recurso.DocumentosObservacoesMedicas, Operacao.Alterar)]
    public async Task<IActionResult> Saude(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        var requisicao = await servico.SaudeParaEdicaoAsync(id, ct);
        return requisicao is null ? NotFound() : View(requisicao);
    }

    [HttpPost]
    [Permissao(Recurso.DocumentosObservacoesMedicas, Operacao.Alterar)]
    public async Task<IActionResult> Saude(Guid id, SaudeAlunoRequisicao requisicao, CancellationToken ct)
    {
        requisicao.Id = id;
        var resultado = await servico.EditarSaudeAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ViewData["ModuloAtivo"] = Modulo;
            ModelState.AdicionarErro(resultado);
            return View(requisicao);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id, aba = "saude" });
    }

    /// <summary>Uma transição da situação (confirmar matrícula, pausar, reativar, desligar, rematricular) ou o prazo do trial.</summary>
    [HttpGet]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Situacao(Guid id, SituacaoAluno para, CancellationToken ct)
    {
        var ficha = await servico.ObterFichaAsync(id, ct);
        if (ficha is null) return NotFound();
        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        return await FormularioSituacaoAsync(ficha, new MudarSituacaoRequisicao
        {
            AlunoId = id, Para = para, Data = hoje, TrialAte = ficha.TrialAte
        }, ct);
    }

    [HttpPost]
    [Permissao(Recurso.FinanceiroEscolinha, Operacao.Alterar)]
    public async Task<IActionResult> Situacao(Guid id, MudarSituacaoRequisicao requisicao, CancellationToken ct)
    {
        requisicao.AlunoId = id;
        var resultado = await servico.MudarSituacaoAsync(requisicao, ct);
        if (!resultado.Sucesso)
        {
            ModelState.AdicionarErro(resultado);
            var ficha = await servico.ObterFichaAsync(id, ct);
            return ficha is null ? NotFound() : await FormularioSituacaoAsync(ficha, requisicao, ct);
        }

        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return RedirectToAction(nameof(Ficha), new { id });
    }

    private async Task<IActionResult> FormularioNovoAsync(NovoAlunoRequisicao requisicao, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        // O modelo é a própria requisição (os campos ficam na raiz, como o serviço aponta os erros); as turmas vão no ViewData.
        ViewData["Turmas"] = await turmas.ListarAsync(false, null, ct);
        ViewData["DiasAvisoTrial"] = (await parametros.ObterAsync(ct)).Matricula.TrialDiasAvisoAntes;
        ViewData["Escolhidos"] = await responsaveis.EscolhidosAsync(
            requisicao.Responsaveis.Where(r => r.ResponsavelId is not null).Select(r => r.ResponsavelId!.Value), ct);
        return View("Novo", requisicao);
    }

    private async Task<IActionResult> FormularioSituacaoAsync(FichaAluno ficha, MudarSituacaoRequisicao requisicao, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = Modulo;
        ViewData["Ficha"] = ficha;
        ViewData["Turmas"] = requisicao.Para == SituacaoAluno.Ativo && ficha.Situacao == SituacaoAluno.Desistente
            ? await turmas.ListarAsync(false, null, ct)
            : Array.Empty<TurmaResumo>();
        return View("Situacao", requisicao);
    }
}
