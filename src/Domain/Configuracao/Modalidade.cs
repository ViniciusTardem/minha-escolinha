using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Configuracao;

/// <summary>
/// Perfil da modalidade (R8). Esta versão entrega só <see cref="Coletiva"/>; judô, natação e ballet entram como novos
/// perfis sem reescrever o núcleo, porque quem decide os recursos são os interruptores da <see cref="Modalidade"/>.
/// </summary>
public enum PerfilModalidade
{
    /// <summary>Esporte coletivo (futsal, futebol): jogos com adversário e convocação.</summary>
    Coletiva = 10
}

/// <summary>Os interruptores de recurso de uma modalidade (RF-MOD-002).</summary>
public sealed record RecursosModalidade(
    bool Adversario,
    bool PlacarSumula,
    bool Classificacao,
    bool Convocacao,
    bool Graduacao,
    bool Apresentacao);

/// <summary>Valores iniciais dos interruptores para cada perfil — a escolinha pode mudar depois.</summary>
public static class ModelosPerfil
{
    public static RecursosModalidade Padrao(PerfilModalidade perfil) => perfil switch
    {
        PerfilModalidade.Coletiva => new RecursosModalidade(
            Adversario: true, PlacarSumula: true, Classificacao: true, Convocacao: true, Graduacao: false, Apresentacao: false),
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "Perfil de modalidade desconhecido.")
    };

    public static string Rotulo(PerfilModalidade perfil) => perfil switch
    {
        PerfilModalidade.Coletiva => "Coletiva",
        _ => perfil.ToString()
    };
}

/// <summary>
/// Modalidade oferecida pela escolinha (BL-M02-02), com o perfil e os interruptores que dizem se existe adversário,
/// placar, classificação, convocação, graduação ou apresentação. Referenciada pela turma (S1) e pelo evento (S5).
/// </summary>
public class Modalidade : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = 80;

    /// <summary>
    /// Duração padrão do evento quando a modalidade não diz outra (pendência 12.2: "duração padrão do evento").
    /// Valor provisório, editável por modalidade.
    /// </summary>
    public const int DuracaoPadraoEventoProvisoria = 60;

    public Guid Modalidade_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public string Nome { get; set; } = string.Empty;
    public PerfilModalidade Perfil { get; set; } = PerfilModalidade.Coletiva;

    public bool TemAdversario { get; set; }
    public bool TemPlacarSumula { get; set; }
    public bool TemClassificacao { get; set; }
    public bool TemConvocacao { get; set; }
    public bool TemGraduacao { get; set; }
    public bool TemApresentacao { get; set; }

    /// <summary>Em minutos; necessária para os alertas de conflito de agenda.</summary>
    public int DuracaoPadraoEventoMinutos { get; set; } = DuracaoPadraoEventoProvisoria;

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public RecursosModalidade Recursos =>
        new(TemAdversario, TemPlacarSumula, TemClassificacao, TemConvocacao, TemGraduacao, TemApresentacao);

    public void AplicarRecursos(RecursosModalidade recursos)
    {
        TemAdversario = recursos.Adversario;
        TemPlacarSumula = recursos.PlacarSumula;
        TemClassificacao = recursos.Classificacao;
        TemConvocacao = recursos.Convocacao;
        TemGraduacao = recursos.Graduacao;
        TemApresentacao = recursos.Apresentacao;
    }

    /// <summary>Nova modalidade com os interruptores iniciais do perfil.</summary>
    public static Modalidade Nova(string nome, PerfilModalidade perfil)
    {
        var modalidade = new Modalidade { Nome = nome.Trim(), Perfil = perfil };
        modalidade.AplicarRecursos(ModelosPerfil.Padrao(perfil));
        return modalidade;
    }
}
