using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;

namespace MinhaEscolinha.Infrastructure.Seguranca;

public class HashSenhaBCrypt : IHashSenha
{
    private const int FatorTrabalho = 11;

    public string Gerar(string senha) => BCrypt.Net.BCrypt.HashPassword(senha, FatorTrabalho);

    public bool Verificar(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash corrompido/vazio no banco: nega o acesso em vez de derrubar o login.
            return false;
        }
    }
}

/// <summary>
/// Segredos de terceiros cifrados com ASP.NET Data Protection (propósito próprio: uma chave vazada de outro uso não abre
/// estes). As chaves do anel ficam no banco; em produção são cifradas por certificado (RUNBOOK) — sem o certificado,
/// quem copiar o banco decifra a chave Asaas. Nunca apague nem revogue chaves antigas: textos antigos deixariam de abrir.
/// </summary>
public class ProtetorSegredosDataProtection(IDataProtectionProvider provedor) : IProtetorSegredos
{
    private readonly IDataProtector _protetor = provedor.CreateProtector("MinhaEscolinha.Segredos.v1");

    public string Proteger(string segredo) => _protetor.Protect(segredo);

    public string? Desproteger(string protegido)
    {
        try
        {
            return _protetor.Unprotect(protegido);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

public class GuardaAmbienteOpcoes
{
    public const string Secao = "GuardaAmbiente";

    /// <summary>Bancos de produção. Fora de Production, o app recusa subir apontando para qualquer um deles.</summary>
    public List<BancoProducao> BancosProducao { get; set; } = [];
}

public class BancoProducao
{
    /// <summary>Servidor (Data Source). Vazio = qualquer servidor com o nome de banco abaixo.</summary>
    public string? Servidor { get; set; }

    public string Banco { get; set; } = string.Empty;
}

/// <summary>
/// Trava de ambiente: fora de Production, recusa subir se a <c>ConexaoPadrao</c> apontar para o banco de produção.
/// Existe para tornar impossível um teste local gravar/migrar a produção. Portado do AlternativaDepartamentoPessoal.
/// Não contorne — ajuste a connection string (User Secrets) ou o ambiente.
/// </summary>
public static class GuardaAmbienteBanco
{
    public const string AmbienteProducao = "Production";

    /// <exception cref="InvalidOperationException">Fora de Production apontando para banco de produção.</exception>
    public static void Verificar(string ambiente, string? connectionString, IEnumerable<BancoProducao> bancosProducao)
    {
        if (string.Equals(ambiente, AmbienteProducao, StringComparison.OrdinalIgnoreCase)) return;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        var cs = new SqlConnectionStringBuilder(connectionString);
        var servidor = NormalizarServidor(cs.DataSource);
        var banco = cs.InitialCatalog.Trim();

        foreach (var producao in bancosProducao)
        {
            if (string.IsNullOrWhiteSpace(producao.Banco)) continue;

            var mesmoBanco = string.Equals(banco, producao.Banco.Trim(), StringComparison.OrdinalIgnoreCase);
            var mesmoServidor = string.IsNullOrWhiteSpace(producao.Servidor)
                || string.Equals(servidor, NormalizarServidor(producao.Servidor), StringComparison.OrdinalIgnoreCase);

            if (mesmoBanco && mesmoServidor)
                throw new InvalidOperationException(
                    $"Trava de ambiente: o ambiente \"{ambiente}\" está configurado para o banco de PRODUÇÃO " +
                    $"\"{banco}\" em \"{cs.DataSource}\". Corrija a ConnectionStrings:ConexaoPadrao (User Secrets) " +
                    "ou rode em Production. O app não vai subir.");
        }
    }

    /// <summary><c>tcp:Servidor,1433</c> e <c>Servidor</c> são o mesmo servidor.</summary>
    internal static string NormalizarServidor(string? servidor)
    {
        var s = (servidor ?? string.Empty).Trim();
        if (s.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) s = s[4..];
        if (s.EndsWith(",1433", StringComparison.Ordinal)) s = s[..^5];
        return s.Trim();
    }
}
