using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Carga inicial por planilha (S7): enviar (BL-M12-01), mapear as colunas, conferir as linhas classificadas e os
/// responsáveis sem duplicidade (BL-M12-02, BL-M12-03), decidir o que a planilha não resolve e confirmar a gravação
/// (BL-M12-04). A saúde não aparece em tela nenhuma daqui (BL-M12-05). O serviço confere que é a gestão.
/// </summary>
[Permissao(Recurso.ConfiguracaoEscolinha, Operacao.Alterar)]
public class ImportacaoController(ServicoImportacao servico) : Controller
{
    /// <summary>O .xlsx de até 2 MB, com folga para o envelope do formulário.</summary>
    private const long LimiteEnvio = ImportacaoLote.MaximoBytes + 512 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "importacao";
        return View(new ImportacaoViewModel(await servico.ListarAsync(ct), servico.PodeImportar));
    }

    [HttpGet]
    public async Task<IActionResult> Modelo(CancellationToken ct) =>
        await servico.ModeloAsync(ct) is { } arquivo
            ? File(arquivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "modelo-importacao-minha-escolinha.xlsx")
            : Forbid();

    [HttpPost]
    [RequestSizeLimit(LimiteEnvio)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteEnvio)]
    public async Task<IActionResult> Enviar(IFormFile? arquivo, CancellationToken ct)
    {
        if (arquivo is null)
        {
            TempData[ChavesTempData.Erro] = "Escolha a planilha (.xlsx).";
            return RedirectToAction(nameof(Index));
        }
        await using var conteudo = arquivo.OpenReadStream();
        var resultado = await servico.EnviarAsync(conteudo, arquivo.FileName, arquivo.Length, ct);
        Avisar(resultado);
        return resultado.Sucesso && resultado.Dados is Guid id ? RedirectToAction(nameof(Mapear), new { id }) : RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Mapear(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "importacao";
        return await servico.MapeamentoAsync(id, ct) is { } mapeamento ? View(mapeamento) : await NaoAbertoAsync(id, ct);
    }

    /// <summary>Um campo por coluna, na ordem das colunas ("" = a coluna fica de fora).</summary>
    [HttpPost]
    public async Task<IActionResult> Mapear(Guid id, [FromForm(Name = "campos")] List<string?>? campos, CancellationToken ct)
    {
        var escolhas = (campos ?? [])
            .Select((valor, coluna) => (coluna, ok: Enum.TryParse<CampoImportacao>(valor, out var campo) && Enum.IsDefined(campo), campo))
            .Where(x => x.ok)
            .ToDictionary(x => x.coluna, x => x.campo);
        var resultado = await servico.MapearAsync(id, escolhas, ct);
        Avisar(resultado);
        return resultado.Sucesso ? RedirectToAction(nameof(Conferir), new { id }) : RedirectToAction(nameof(Mapear), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Conferir(Guid id, string? ver, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "importacao";
        return await servico.ConferenciaAsync(id, ct) is { } conferencia
            ? View(new ConferenciaViewModel(conferencia, ver))
            : await NaoAbertoAsync(id, ct);
    }

    /// <summary>As escolhas chegam em listas paralelas: a chave e a escolha de cada turma e de cada responsável parecido.</summary>
    [HttpPost]
    public async Task<IActionResult> Decidir(Guid id,
        [FromForm(Name = "turmaChave")] List<string>? turmaChave, [FromForm(Name = "turmaEscolhida")] List<string?>? turmaEscolhida,
        [FromForm(Name = "responsavelChave")] List<string>? responsavelChave, [FromForm(Name = "responsavelEscolha")] List<string?>? responsavelEscolha,
        [FromForm(Name = "alunosNovos")] List<int>? alunosNovos, CancellationToken ct)
    {
        var decisoes = new DecisoesImportacao { AlunosNovos = alunosNovos ?? [] };
        foreach (var (chave, escolha) in (turmaChave ?? []).Zip(turmaEscolhida ?? []))
            if (Guid.TryParse(escolha, out var turma)) decisoes.Turmas[chave] = turma;
        foreach (var (chave, escolha) in (responsavelChave ?? []).Zip(responsavelEscolha ?? []))
            decisoes.Responsaveis[chave] = Guid.TryParse(escolha, out var existente) ? existente : null;
        Avisar(await servico.DecidirAsync(id, decisoes, ct));
        return RedirectToAction(nameof(Conferir), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Confirmar(Guid id, string? assinatura, bool declaracao, CancellationToken ct)
    {
        var resultado = await servico.ConfirmarAsync(id, assinatura, declaracao, ct);
        Avisar(resultado);
        return resultado.Sucesso ? RedirectToAction(nameof(Lote), new { id }) : RedirectToAction(nameof(Conferir), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Descartar(Guid id, CancellationToken ct)
    {
        Avisar(await servico.DescartarAsync(id, ct));
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Lote(Guid id, CancellationToken ct)
    {
        ViewData["ModuloAtivo"] = "importacao";
        return await servico.LoteAsync(id, ct) is { } lote ? View(lote) : NotFound();
    }

    /// <summary>O lote encerrado volta para o resumo dele; o que não existe (ou é de outra escolinha) dá 404.</summary>
    private async Task<IActionResult> NaoAbertoAsync(Guid id, CancellationToken ct) =>
        await servico.LoteAsync(id, ct) is { Situacao: not SituacaoImportacao.Aberta } ? RedirectToAction(nameof(Lote), new { id }) : NotFound();

    private void Avisar(ResultadoOperacao resultado) =>
        TempData[resultado.Sucesso ? ChavesTempData.Sucesso : ChavesTempData.Erro] = resultado.Mensagem;
}
