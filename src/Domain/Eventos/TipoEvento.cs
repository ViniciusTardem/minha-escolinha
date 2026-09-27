using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Domain.Eventos;

/// <summary>
/// Tipo do evento (BL-M02-03, RF-MOD-001). O núcleo trata todo compromisso datado além do treino como um evento com
/// tipo, sem pressupor adversário, placar ou classificação. Esta versão entrega só <see cref="Jogo"/>.
/// </summary>
public enum TipoEvento
{
    Jogo = 10
}

/// <summary>O que um evento de certo tipo pode usar numa modalidade — decidido pelos interruptores, não pelo tipo.</summary>
public sealed record RecursosDoEvento(bool PermiteAdversario, bool PermitePlacar, bool PermiteConvocacao);

/// <summary>
/// Regra genérica do núcleo (R8): o perfil da modalidade diz quais tipos de evento existem e os interruptores dizem
/// o que cada evento pode ter. Nada aqui assume "jogo": um perfil novo só acrescenta os próprios tipos.
/// </summary>
public static class PoliticaPerfil
{
    private static readonly IReadOnlyDictionary<PerfilModalidade, IReadOnlyList<TipoEvento>> TiposPorPerfil =
        new Dictionary<PerfilModalidade, IReadOnlyList<TipoEvento>>
        {
            [PerfilModalidade.Coletiva] = [TipoEvento.Jogo]
        };

    public static IReadOnlyList<TipoEvento> TiposDisponiveis(PerfilModalidade perfil) =>
        TiposPorPerfil.TryGetValue(perfil, out var tipos) ? tipos : [];

    public static bool TipoSuportado(PerfilModalidade perfil, TipoEvento tipo) =>
        Enum.IsDefined(tipo) && TiposDisponiveis(perfil).Contains(tipo);

    /// <summary>O que o evento pode ter nesta modalidade. Adversário, quando permitido, continua opcional.</summary>
    public static RecursosDoEvento Recursos(TipoEvento tipo, RecursosModalidade recursos)
    {
        if (!Enum.IsDefined(tipo)) throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de evento desconhecido.");
        return new RecursosDoEvento(recursos.Adversario, recursos.PlacarSumula, recursos.Convocacao);
    }

    public static string Rotulo(TipoEvento tipo) => tipo switch
    {
        TipoEvento.Jogo => "Jogo",
        _ => tipo.ToString()
    };
}
