using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Acesso;

/// <summary>Papel com que a pessoa atua numa escolinha. Valores espaçados de 10 (gravados no banco).</summary>
public enum PapelAtuacao
{
    /// <summary>Gestor / dono da escolinha: enxerga tudo da própria escolinha.</summary>
    Gestor = 10,

    /// <summary>Professor: as próprias turmas (chamada, convocação).</summary>
    Professor = 20,

    /// <summary>Responsável: os próprios dados e os dos filhos com vínculo verificado.</summary>
    Responsavel = 30
}

public enum SituacaoVinculo
{
    Ativo = 10,

    /// <summary>Acesso revogado: nega já na próxima requisição e preserva o histórico do que a pessoa registrou.</summary>
    Revogado = 20
}

/// <summary>
/// "Esta pessoa atua nesta escolinha com este papel" (BL-M01-01). Uma pessoa acumula papéis sem contas separadas:
/// cada papel é um vínculo, e a permissão efetiva é a união dos papéis ativos na escolinha do contexto.
/// </summary>
public class VinculoAtuacao : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoMotivo = 300;

    public Guid VinculoAtuacao_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Pessoa_ID { get; set; }
    public Pessoa? Pessoa { get; set; }
    public PapelAtuacao Papel { get; set; }
    public SituacaoVinculo Situacao { get; set; } = SituacaoVinculo.Ativo;

    public DateTime? RevogadoEm { get; set; }
    public Guid? RevogadoPor_ID { get; set; }
    public string? MotivoRevogacao { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public bool Ativo => Situacao == SituacaoVinculo.Ativo;

    public void Revogar(DateTime agora, Guid? autorId, string motivo)
    {
        Situacao = SituacaoVinculo.Revogado;
        RevogadoEm = agora;
        RevogadoPor_ID = autorId;
        MotivoRevogacao = TextoHelper.Truncar(motivo, TamanhoMotivo);
    }

    public void Reativar()
    {
        Situacao = SituacaoVinculo.Ativo;
        RevogadoEm = null;
        RevogadoPor_ID = null;
        MotivoRevogacao = null;
    }
}
