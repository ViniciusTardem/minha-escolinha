namespace MinhaEscolinha.Domain.Comum;

/// <summary>
/// Dado que pertence a uma escolinha (multi-escolinha, desvio 12.3 registrado em docs/DECISOES-EM-ABERTO.md).
/// Toda entidade que implementa esta interface:
/// <list type="bullet">
/// <item>passa pelo filtro global do EF (só aparece a escolinha do contexto; sem contexto, nenhuma linha);</item>
/// <item>tem o <see cref="Escolinha_ID"/> carimbado e conferido pelo interceptor ao salvar — nunca muda depois de gravado.</item>
/// </list>
/// A própria <c>Escolinha</c> não implementa a interface (é a raiz), mas tem o mesmo filtro.
/// </summary>
public interface IEntidadeDaEscolinha
{
    Guid Escolinha_ID { get; set; }
}
