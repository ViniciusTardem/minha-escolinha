using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Acesso;

/// <summary>
/// Identidade única de uma pessoa no sistema (BL-M01-01): a mesma pessoa pode ser gestora de uma escolinha,
/// professora de outra e responsável por um aluno — sem contas separadas.
/// <para>
/// É global de propósito, e por isso guarda só a identidade (nome e CPF). Contato, endereço e dados de cadastro
/// ficam na ficha de cada escolinha (fatia S1): a escolinha B nunca enxerga o que a escolinha A digitou.
/// A ficha de uma escolinha só se liga a uma <see cref="Pessoa"/> existente na ativação do convite (S2) ou por
/// decisão explícita da equipe da plataforma — nunca por coincidência de CPF no cadastro.
/// </para>
/// </summary>
public class Pessoa : IEntidadeAuditavel
{
    public const int TamanhoNome = 150;

    public Guid Pessoa_ID { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Só os 11 dígitos; opcional (nem toda pessoa informa). Único quando preenchido.</summary>
    public string? Cpf { get; set; }

    public Credencial? Credencial { get; set; }

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }
}
