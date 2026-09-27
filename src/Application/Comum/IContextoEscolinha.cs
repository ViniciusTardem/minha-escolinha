using MinhaEscolinha.Domain.Acesso;

namespace MinhaEscolinha.Application.Comum;

/// <summary>
/// A escolinha em que a requisição (ou o job) está operando e os papéis ativos da pessoa nela.
/// Todo dado de escolinha é filtrado por aqui: sem contexto, as consultas não devolvem nenhuma linha e gravar lança exceção.
/// <para>
/// Na requisição, quem preenche é o middleware de contexto, relendo os vínculos a cada requisição (revogação imediata).
/// Fora dela (job, criação da escolinha pela plataforma, reconciliação do startup), o código chama <see cref="Definir"/>
/// explicitamente — nunca troque de escolinha no meio de um mesmo escopo.
/// </para>
/// </summary>
public interface IContextoEscolinha
{
    Guid? EscolinhaId { get; }

    /// <summary>Papéis ativos da pessoa na escolinha do contexto (vazio em job e na plataforma).</summary>
    IReadOnlySet<PapelAtuacao> Papeis { get; }

    /// <summary>Operador da plataforma (equipe LDNA): cria escolinhas e concede créditos.</summary>
    bool AdministradorPlataforma { get; }

    void Definir(Guid escolinhaId, IEnumerable<PapelAtuacao>? papeis = null, bool administradorPlataforma = false);

    void DefinirSemEscolinha(bool administradorPlataforma);
}

/// <summary>Implementação padrão (um objeto por escopo de DI).</summary>
public sealed class ContextoEscolinha : IContextoEscolinha
{
    private static readonly IReadOnlySet<PapelAtuacao> Nenhum = new HashSet<PapelAtuacao>();

    public Guid? EscolinhaId { get; private set; }
    public IReadOnlySet<PapelAtuacao> Papeis { get; private set; } = Nenhum;
    public bool AdministradorPlataforma { get; private set; }

    public void Definir(Guid escolinhaId, IEnumerable<PapelAtuacao>? papeis = null, bool administradorPlataforma = false)
    {
        if (escolinhaId == Guid.Empty) throw new ArgumentException("Escolinha vazia no contexto.", nameof(escolinhaId));
        if (EscolinhaId is { } atual && atual != escolinhaId)
            throw new InvalidOperationException("O contexto já está em outra escolinha: abra um escopo novo para trocar.");

        EscolinhaId = escolinhaId;
        Papeis = papeis is null ? Nenhum : new HashSet<PapelAtuacao>(papeis);
        AdministradorPlataforma = administradorPlataforma;
    }

    public void DefinirSemEscolinha(bool administradorPlataforma)
    {
        EscolinhaId = null;
        Papeis = Nenhum;
        AdministradorPlataforma = administradorPlataforma;
    }
}
