using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Domain.Auditoria;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Web.Infraestrutura;

namespace MinhaEscolinha.Web.Models;

/// <summary>Rodapé de paginação das listas (Views/Shared/_Paginacao.cshtml). <see cref="Parametros"/> mantém a busca nos links.</summary>
public record PaginacaoViewModel(
    int Numero,
    int TotalPaginas,
    int PrimeiroItem,
    int UltimoItem,
    int Total,
    string Rotulo,
    IReadOnlyDictionary<string, string?> Parametros)
{
    public static PaginacaoViewModel De<T>(Pagina<T> pagina, string rotulo, IReadOnlyDictionary<string, string?> parametros) =>
        new(pagina.Numero, pagina.TotalPaginas, pagina.PrimeiroItem, pagina.UltimoItem, pagina.Total, rotulo, parametros);
}

/// <summary>Um passo da configuração da escolinha no painel inicial.</summary>
public record PassoConfiguracao(string Titulo, string Descricao, string Icone, bool Feito, string Url, string Acao);

public record PainelViewModel(
    string NomeEscolinha,
    string Papeis,
    bool VeConfiguracao,
    IReadOnlyList<PassoConfiguracao> Passos,
    IReadOnlyList<Modulo> ProximasEntregas);

public record ParametrosViewModel(
    DominioParametro Dominio,
    IReadOnlyList<DominioParametro> Dominios,
    IReadOnlyList<ParametroResposta> Parametros,
    bool PodeAlterar,
    IReadOnlyCollection<string> ChavesComErro);

public record HistoricoParametrosViewModel(string? Chave, IReadOnlyList<RegistroAlteracao> Registros);
