using MinhaEscolinha.Domain.Comum;

namespace MinhaEscolinha.Domain.Acesso;

/// <summary>
/// Como a pessoa entra no sistema (login e senha). Uma por <see cref="Pessoa"/>, com a mesma chave.
/// As regras de bloqueio, troca obrigatória e carimbo de segurança vieram do <c>Usuario</c> do AlternativaDepartamentoPessoal.
/// </summary>
public class Credencial : IEntidadeAuditavel
{
    public const int MaxTentativasFalhas = 5;
    public static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);

    public const int TamanhoLogin = 150;

    /// <summary>Mesma chave da <see cref="Pessoa"/> (1:1).</summary>
    public Guid Pessoa_ID { get; set; }
    public Pessoa? Pessoa { get; set; }

    /// <summary>Identificador do login (o e-mail, para gestores e professores), sempre em minúsculas e sem espaços. Único.</summary>
    public string Login { get; set; } = string.Empty;

    public string SenhaHash { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;

    /// <summary>
    /// Equipe da LDNA que opera a plataforma: cria escolinhas e concede créditos. Não dá acesso aos dados de nenhuma
    /// escolinha — para isso precisa de um <see cref="VinculoAtuacao"/> explícito, que fica registrado.
    /// </summary>
    public bool AdministradorPlataforma { get; set; }

    /// <summary>Obriga a trocar a senha no próximo acesso (1º acesso e senha redefinida).</summary>
    public bool TrocarSenha { get; set; }

    public int TentativasFalhas { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    public DateTime? UltimoAcesso { get; set; }

    /// <summary>
    /// Muda sempre que a sessão precisa cair: senha trocada/redefinida, credencial desativada.
    /// O cookie carrega o carimbo e é revalidado periodicamente contra o banco.
    /// </summary>
    public Guid CarimboSeguranca { get; set; } = Guid.NewGuid();

    public DateTime DataCadastro { get; set; }
    public Guid? UsuarioCadastro_ID { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public Guid? UsuarioAtualizacao_ID { get; set; }

    public static string NormalizarLogin(string? login) =>
        (login ?? string.Empty).Trim().ToLowerInvariant();

    public bool EstaBloqueado(DateTime agora) => BloqueadoAte.HasValue && BloqueadoAte.Value > agora;

    /// <summary>Conta a falha; na 5ª seguida bloqueia por 15 minutos e zera a contagem.</summary>
    public void RegistrarFalhaLogin(DateTime agora)
    {
        TentativasFalhas++;
        if (TentativasFalhas >= MaxTentativasFalhas)
        {
            BloqueadoAte = agora.Add(DuracaoBloqueio);
            TentativasFalhas = 0;
        }
    }

    public void RegistrarLoginComSucesso(DateTime agora)
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
        UltimoAcesso = agora;
    }

    public void DefinirSenha(string senhaHash, bool trocarNoProximoAcesso)
    {
        SenhaHash = senhaHash;
        TrocarSenha = trocarNoProximoAcesso;
        TentativasFalhas = 0;
        BloqueadoAte = null;
        RenovarCarimboSeguranca();
    }

    public void RenovarCarimboSeguranca() => CarimboSeguranca = Guid.NewGuid();
}
