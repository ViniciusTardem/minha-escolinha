using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Configuracao.Parametros;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>
/// Leitura dos parâmetros de regra da escolinha do contexto por qualquer módulo (R7). Lê uma vez por escopo:
/// a mudança feita na tela vale na próxima requisição ou job, sem reiniciar o app (CA-18).
/// </summary>
public interface IParametros
{
    Task<ValoresParametros> ObterAsync(CancellationToken ct = default);
}

public sealed class LeitorParametros(IConfiguracaoRepositorio repositorio, IContextoEscolinha contexto) : IParametros
{
    private ValoresParametros? _cache;
    private Guid? _escolinhaDoCache;

    public async Task<ValoresParametros> ObterAsync(CancellationToken ct = default)
    {
        var escolinhaId = contexto.EscolinhaId
            ?? throw new InvalidOperationException("Leitura de parâmetros sem escolinha no contexto.");
        if (_cache is not null && _escolinhaDoCache == escolinhaId) return _cache;

        var gravados = await repositorio.ListarParametrosAsync(ct);
        _cache = new ValoresParametros(gravados.ToDictionary(p => p.Chave, p => p.Valor, StringComparer.Ordinal));
        _escolinhaDoCache = escolinhaId;
        return _cache;
    }
}

/// <summary>Tela de parâmetros (BL-M02-06 a 09 e 12): lista por domínio, valida e grava em lote, com histórico.</summary>
public class ServicoParametros(IConfiguracaoRepositorio repositorio, RegistroDeAlteracoes registro)
{
    public const string Entidade = "ParametroEscolinha";

    public async Task<IReadOnlyList<ParametroResposta>> ListarAsync(CancellationToken ct = default)
    {
        var gravados = (await repositorio.ListarParametrosAsync(ct)).ToDictionary(p => p.Chave, StringComparer.Ordinal);
        var historico = await repositorio.ListarAlteracoesAsync(Entidade, null, 500, ct);
        var ultimaPorChave = historico
            .GroupBy(h => h.Campo, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Em).First(), StringComparer.Ordinal);

        return CatalogoParametros.Todos.Select(d =>
        {
            var valor = gravados.TryGetValue(d.Chave, out var p) ? p.Valor : d.ValorPadrao;
            return new ParametroResposta(d, valor, string.Equals(valor, d.ValorPadrao, StringComparison.Ordinal),
                ultimaPorChave.GetValueOrDefault(d.Chave));
        }).ToList();
    }

    public Task<IReadOnlyList<Domain.Auditoria.RegistroAlteracao>> HistoricoAsync(string? chave, CancellationToken ct = default) =>
        repositorio.ListarAlteracoesAsync(Entidade, chave, 200, ct);

    /// <summary>
    /// Valida tudo antes de gravar qualquer coisa: se um valor estiver errado, nada muda e a mensagem lista os problemas.
    /// Dados = quantidade de parâmetros alterados; no erro, as chaves com problema (a tela marca cada uma).
    /// </summary>
    public async Task<ResultadoOperacao> AlterarAsync(AlterarParametrosRequisicao requisicao, CancellationToken ct = default)
    {
        var erros = new List<string>();
        var chavesComErro = new List<string>();
        var validos = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (chave, texto) in requisicao.Valores)
        {
            var definicao = CatalogoParametros.Obter(chave);
            if (definicao is null)
            {
                erros.Add($"Parâmetro desconhecido: {chave}.");
                chavesComErro.Add(chave);
                continue;
            }
            var resultado = ValidadorParametro.Validar(definicao, texto);
            if (resultado.Valido) validos[chave] = resultado.Valor;
            else
            {
                erros.Add(resultado.Erro!);
                chavesComErro.Add(chave);
            }
        }
        if (erros.Count > 0)
            return new ResultadoOperacao { Mensagem = string.Join(" ", erros), Campo = chavesComErro[0], Dados = chavesComErro };

        var gravados = (await repositorio.ListarParametrosAsync(ct)).ToDictionary(p => p.Chave, StringComparer.Ordinal);
        var alterados = 0;
        foreach (var (chave, valor) in validos)
        {
            if (!gravados.TryGetValue(chave, out var parametro))
            {
                parametro = new ParametroEscolinha { Chave = chave, Valor = CatalogoParametros.ObterObrigatorio(chave).ValorPadrao };
                repositorio.AdicionarParametro(parametro);
            }
            if (registro.SeMudou(Entidade, null, chave, parametro.Valor, valor))
            {
                parametro.Valor = valor;
                alterados++;
            }
        }

        if (alterados == 0) return ResultadoOperacao.Ok("Nada mudou.", 0);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok(alterados == 1 ? "1 parâmetro alterado." : $"{alterados} parâmetros alterados.", alterados);
    }

    /// <summary>Grava o padrão dos parâmetros que a escolinha ainda não tem (parâmetro novo no catálogo). Idempotente.</summary>
    public async Task<int> CompletarPadroesAsync(CancellationToken ct = default)
    {
        var existentes = (await repositorio.ListarParametrosAsync(ct)).Select(p => p.Chave).ToHashSet(StringComparer.Ordinal);
        var novos = CatalogoParametros.Todos.Where(d => !existentes.Contains(d.Chave)).ToList();
        foreach (var definicao in novos)
        {
            repositorio.AdicionarParametro(new ParametroEscolinha { Chave = definicao.Chave, Valor = definicao.ValorPadrao });
            registro.Registrar(Entidade, null, definicao.Chave, null, definicao.ValorPadrao, "Parâmetro novo: valor padrão do catálogo.");
        }
        if (novos.Count > 0) await repositorio.SalvarAsync(ct);
        return novos.Count;
    }
}

/// <summary>Reconciliação do startup: toda escolinha passa a ter todos os parâmetros do catálogo, cada uma no próprio escopo.</summary>
public class ReconciliadorParametros(IConsultasPlataforma consultas, IExecutorEscolinha executor, ILogger<ReconciliadorParametros> logger)
{
    public async Task<int> ReconciliarAsync(CancellationToken ct = default)
    {
        var total = 0;
        foreach (var escolinhaId in await consultas.IdsDasEscolinhasAsync(ct))
        {
            var novos = await executor.ExecutarAsync(escolinhaId,
                sp => sp.GetRequiredService<ServicoParametros>().CompletarPadroesAsync(ct), ct);
            if (novos > 0) logger.LogInformation("Escolinha {Escolinha}: {Novos} parâmetro(s) novo(s) com o valor padrão.", escolinhaId, novos);
            total += novos;
        }
        return total;
    }
}
