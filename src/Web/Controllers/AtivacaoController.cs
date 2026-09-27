using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Ativação do acesso pelo link do convite (BL-M01-05), sem login: 1) o link abre sem mostrar dado nenhum; 2) a pessoa
/// confere o CPF; 3) cria a senha (ou entra com a que já tem) e já sai logada — o responsável no portal, o professor no painel.
/// </summary>
[AllowAnonymous]
[Route("Convite/{token}")]
public class AtivacaoController(ServicoAtivacaoConvite servico, IServicoAutenticacao autenticacao, TimeProvider relogio) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        var (estado, _) = await servico.EstadoAsync(token, ct);
        return View(new AtivacaoViewModel(token, estado, null, new IdentificacaoRequisicao(), null));
    }

    [HttpPost("Identificar")]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> Identificar(string token, IdentificacaoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.IdentificarAsync(token, requisicao, ct);
        if (!resultado.Sucesso || resultado.Dados is not IdentificacaoConvite identificacao)
        {
            ModelState.AdicionarErro(resultado);
            var (estado, _) = await servico.EstadoAsync(token, ct);
            return View("Index", new AtivacaoViewModel(token, estado, null, requisicao, null));
        }
        return View("Senha", new AtivacaoViewModel(token, EstadoConvite.Valido, identificacao, requisicao,
            new AtivacaoRequisicao { Cpf = requisicao.Cpf }));
    }

    [HttpPost("Ativar")]
    [EnableRateLimiting(PoliticasLimite.Login)]
    public async Task<IActionResult> Ativar(string token, AtivacaoRequisicao requisicao, CancellationToken ct)
    {
        var resultado = await servico.AtivarAsync(token, requisicao, ct);
        if (!resultado.Sucesso || resultado.Dados is not AcessoAtivado ativado)
        {
            ModelState.AdicionarErro(resultado);
            requisicao.Senha = requisicao.ConfirmacaoSenha = string.Empty;
            // A identificação é refeita a cada passo (sem sessão): a tela volta com os dados que já estavam conferidos.
            var identificacao = await servico.IdentificarAsync(token, new IdentificacaoRequisicao { Cpf = requisicao.Cpf }, ct);
            if (identificacao.Dados is not IdentificacaoConvite conferida)
            {
                var (estado, _) = await servico.EstadoAsync(token, ct);
                return View("Index", new AtivacaoViewModel(token, estado, null, new IdentificacaoRequisicao { Cpf = requisicao.Cpf }, null));
            }
            return View("Senha", new AtivacaoViewModel(token, EstadoConvite.Valido, conferida,
                new IdentificacaoRequisicao { Cpf = requisicao.Cpf }, requisicao));
        }

        var credencial = await autenticacao.ValidarSessaoAsync(ativado.PessoaId, ativado.Carimbo, ct);
        var escolinha = credencial?.Escolinhas.FirstOrDefault(e => e.EscolinhaId == ativado.EscolinhaId);
        if (credencial is null || escolinha is null)
        {
            TempData[ChavesTempData.Sucesso] = "Acesso ativado. Entre com o seu login e senha.";
            return RedirectToAction("Login", "Conta");
        }

        await HttpContext.SignInAsync(ClaimsEscolinha.Esquema, ClaimsEscolinha.CriarPrincipal(credencial, relogio.GetUtcNow(), escolinha),
            new AuthenticationProperties { IsPersistent = false });
        TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
        return ativado.Tipo == TipoConvite.Responsavel ? RedirectToAction("Index", "Portal") : RedirectToAction("Index", "Painel");
    }
}
