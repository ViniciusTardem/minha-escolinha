using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Equipe;

/// <summary>
/// Professor da escolinha (BL-M08-01): dados pessoais, contato e as turmas sob responsabilidade (a turma aponta para ele).
/// É a ficha da escolinha: a escolinha B nunca vê o que a A digitou.
/// <para>
/// <see cref="Pessoa_ID"/> é a identidade com acesso ao sistema. Fica preenchida quando o cadastro reaproveita uma
/// pessoa que já atua nesta escolinha (o gestor que também dá aula) ou, a partir do S2, na ativação do convite —
/// nunca por coincidência de CPF com outra escolinha. Sem ela, o professor existe no cadastro mas ainda não entra no sistema.
/// </para>
/// Tipo de vínculo, forma de remuneração e dados bancários são do BL-M08-02 (adiado no piloto).
/// </summary>
public class Professor : IEntidadeAuditavel, IEntidadeDaEscolinha
{
    public const int TamanhoNome = Pessoa.TamanhoNome;
    public const int TamanhoEmail = 150;
    public const int TamanhoTelefone = 20;

    public Guid Professor_ID { get; set; }
    public Guid Escolinha_ID { get; set; }

    public string Nome { get; private set; } = string.Empty;

    /// <summary>O nome sem acento e em minúsculas (o banco compara acento): é por aqui que a busca procura.</summary>
    public string NomeBusca { get; private set; } = string.Empty;

    /// <summary>Só os 11 dígitos; opcional, único na escolinha quando informado.</summary>
    public string? Cpf { get; set; }

    /// <summary>Celular em E.164 (é por ele que chega o convite de acesso, S2).</summary>
    public string? Telefone { get; set; }

    public string? Email { get; set; }

    public Guid? Pessoa_ID { get; set; }

    public List<FaixaDisponibilidade> Disponibilidade { get; set; } = [];
    public List<IndisponibilidadeProfessor> Indisponibilidades { get; set; } = [];

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public void DefinirNome(string nome)
    {
        Nome = nome.Trim();
        NomeBusca = TextoHelper.NormalizarParaBusca(Nome);
    }

    /// <summary>Sem faixa cadastrada, a disponibilidade é desconhecida: nenhum horário é "fora da disponibilidade".</summary>
    public bool TemDisponibilidadeDeclarada => Disponibilidade.Count > 0;
}

/// <summary>
/// Faixa recorrente em que o professor está disponível (BL-M08-04): "terças, das 17:00 às 21:00".
/// Treino marcado fora de todas as faixas do dia gera alerta (BL-M04-09), sem bloquear (R9).
/// </summary>
public class FaixaDisponibilidade : IEntidadeDaEscolinha
{
    public Guid FaixaDisponibilidade_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Professor_ID { get; set; }
    public DayOfWeek DiaSemana { get; set; }
    public TimeOnly Inicio { get; set; }
    public TimeOnly Fim { get; set; }

    public bool Cobre(DayOfWeek dia, TimeOnly inicio, TimeOnly fim) => DiaSemana == dia && Inicio <= inicio && fim <= Fim;
}

/// <summary>Indisponibilidade pontual (viagem, consulta, férias): treino ou jogo nesse intervalo gera alerta.</summary>
public class IndisponibilidadeProfessor : IEntidadeDaEscolinha
{
    public const int TamanhoMotivo = 200;

    public Guid IndisponibilidadeProfessor_ID { get; set; }
    public Guid Escolinha_ID { get; set; }
    public Guid Professor_ID { get; set; }

    /// <summary>Horário de Brasília.</summary>
    public DateTime Inicio { get; set; }

    public DateTime Fim { get; set; }
    public string? Motivo { get; set; }

    public DateTime RegistradaEm { get; set; }
    public string RegistradaPorNome { get; set; } = string.Empty;
}
