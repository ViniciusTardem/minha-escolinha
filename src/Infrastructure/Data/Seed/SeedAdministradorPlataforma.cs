using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Infrastructure.Data.Contexto;

namespace MinhaEscolinha.Infrastructure.Data.Seed;

/// <summary>
/// Cria o 1º administrador da plataforma (equipe LDNA) — só se não existir nenhum — a partir de
/// <c>Seed:Administrador:{Login,Nome,Senha}</c> (User Secrets em dev, variável de ambiente em produção).
/// Ele nasce com <c>TrocarSenha = true</c>: a senha do seed vale só para o primeiro acesso.
/// Não recebe vínculo com escolinha nenhuma: para ver dados de uma escolinha, a equipe se vincula como gestora (auditado).
/// </summary>
public static class SeedAdministradorPlataforma
{
    public const string Secao = "Seed:Administrador";

    public static async Task ExecutarAsync(EscolinhaDbContext contexto, IConfiguration configuracao, IHashSenha hashSenha,
        ILogger logger, CancellationToken ct = default)
    {
        if (await contexto.Credenciais.AnyAsync(c => c.AdministradorPlataforma, ct))
            return;

        var secao = configuracao.GetSection(Secao);
        var login = Credencial.NormalizarLogin(secao["Login"]);
        var nome = secao["Nome"]?.Trim();
        var senha = secao["Senha"];

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(senha))
        {
            logger.LogWarning(
                "Nenhum administrador da plataforma no banco e {Secao}:(Login, Nome, Senha) não configurado: ninguém consegue entrar. " +
                "Configure por User Secrets (dev) ou variável de ambiente (produção) e reinicie.", Secao);
            return;
        }

        if (await contexto.Credenciais.AnyAsync(c => c.Login == login, ct))
        {
            logger.LogWarning("Seed do administrador ignorado: o login {Login} já existe sem o papel de plataforma.", login);
            return;
        }

        var credencial = new Credencial { Login = login, Ativa = true, AdministradorPlataforma = true };
        credencial.DefinirSenha(hashSenha.Gerar(senha), trocarNoProximoAcesso: true);
        contexto.Pessoas.Add(new Pessoa { Nome = nome, Credencial = credencial });
        await contexto.SaveChangesAsync(ct);

        logger.LogInformation("Administrador da plataforma {Login} criado pelo seed (troca de senha obrigatória no 1º acesso).", login);
    }
}
