using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Acesso;

/// <summary>A conta pronta para entrar: a pessoa e o carimbo de segurança (o controller monta a sessão).</summary>
public sealed record AcessoAtivado(Guid PessoaId, Guid Carimbo, Guid EscolinhaId, TipoConvite Tipo, string Nome);

/// <summary>
/// Ativação pelo link do convite (BL-M01-05), anônima: o link leva ao convite pelo hash do segredo, sem dado nenhum
/// antes da identificação. A pessoa confere o CPF com o da ficha (ou informa o CPF, se a ficha não tem) e então cria a
/// senha — ou, se já tem conta no sistema, entra com a senha dela. O trabalho de verdade roda na escolinha do convite.
/// </summary>
public class ServicoAtivacaoConvite(IAcessoRepositorio acesso, IExecutorEscolinha executor, IRelogioBrasil relogio)
{
    public async Task<(EstadoConvite Estado, ConviteAcesso? Convite)> EstadoAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return (EstadoConvite.Invalido, null);
        var convite = await acesso.ConvitePorHashAsync(ConviteAcesso.Hash(token), ct);
        var estado = convite switch
        {
            null => EstadoConvite.Invalido,
            { Situacao: SituacaoConvite.Aceito } => EstadoConvite.JaUsado,
            { Situacao: SituacaoConvite.Cancelado or SituacaoConvite.Substituido } => EstadoConvite.Cancelado,
            _ when convite.Expirado(relogio.Agora()) => EstadoConvite.Expirado,
            _ => EstadoConvite.Valido
        };
        return (estado, convite);
    }

    public async Task<ResultadoOperacao> IdentificarAsync(string? token, IdentificacaoRequisicao r, CancellationToken ct = default)
    {
        var (estado, convite) = await EstadoAsync(token, ct);
        if (estado != EstadoConvite.Valido) return ResultadoOperacao.Erro(MensagemEstado(estado));
        return await executor.ExecutarAsync(convite!.Escolinha_ID,
            sp => sp.GetRequiredService<AtivacaoNaEscolinha>().IdentificarAsync(convite.ConviteAcesso_ID, r, ct), ct);
    }

    public async Task<ResultadoOperacao> AtivarAsync(string? token, AtivacaoRequisicao r, CancellationToken ct = default)
    {
        var (estado, convite) = await EstadoAsync(token, ct);
        if (estado != EstadoConvite.Valido) return ResultadoOperacao.Erro(MensagemEstado(estado));
        return await executor.ExecutarAsync(convite!.Escolinha_ID,
            sp => sp.GetRequiredService<AtivacaoNaEscolinha>().AtivarAsync(convite.ConviteAcesso_ID, r, ct), ct);
    }

    public static string MensagemEstado(EstadoConvite estado) => estado switch
    {
        EstadoConvite.Expirado => "Este convite venceu. Peça um novo à escolinha.",
        EstadoConvite.JaUsado => "Este convite já foi usado. Entre com o seu login e senha.",
        EstadoConvite.Cancelado => "Este convite não vale mais. Peça um novo à escolinha.",
        EstadoConvite.Valido => string.Empty,
        _ => "Convite não encontrado. Confira o link que chegou pelo WhatsApp."
    };
}

/// <summary>A ativação no escopo da escolinha do convite: tudo numa gravação só (pessoa, credencial, ficha, papel e convite).</summary>
public class AtivacaoNaEscolinha(
    IConvitesRepositorio convites,
    IAcessoRepositorio acesso,
    IConfiguracaoRepositorio configuracao,
    IHashSenha hashSenha,
    RegistroDeAlteracoes registro,
    IValidator<IdentificacaoRequisicao> validadorIdentificacao,
    IValidator<AtivacaoRequisicao> validadorAtivacao,
    IRelogioBrasil relogio)
{
    private sealed record Ficha(string Nome, string? Cpf, Guid? PessoaId, Action<Guid, string> Ligar);

    public async Task<ResultadoOperacao> IdentificarAsync(Guid conviteId, IdentificacaoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorIdentificacao.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var (erro, convite, ficha, cpf, credencial) = await ConferirAsync(conviteId, r.Cpf, ct);
        if (erro is not null) return erro;

        var escolinha = (await configuracao.ObterEscolinhaAsync(ct))?.Nome ?? "sua escolinha";
        return ResultadoOperacao.Ok("Identificação conferida.",
            new IdentificacaoConvite(ficha!.Nome, escolinha, convite!.Tipo, credencial is not null, credencial?.Login));
    }

    public async Task<ResultadoOperacao> AtivarAsync(Guid conviteId, AtivacaoRequisicao r, CancellationToken ct = default)
    {
        var validacao = await validadorAtivacao.ValidateAsync(r, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);

        var (erro, convite, ficha, cpf, credencial) = await ConferirAsync(conviteId, r.Cpf, ct);
        if (erro is not null) return erro;
        var agora = relogio.Agora();

        Pessoa pessoa;
        if (credencial is not null)
        {
            // Já tem conta: entra com a senha dela (o bloqueio por tentativas da credencial vale aqui também).
            if (credencial.EstaBloqueado(agora) || !credencial.Ativa)
                return ResultadoOperacao.Erro("Esta conta está bloqueada no momento. Tente de novo em alguns minutos.");
            if (!hashSenha.Verificar(r.Senha, credencial.SenhaHash))
            {
                credencial.RegistrarFalhaLogin(agora);
                await convites.SalvarAsync(ct);
                return ResultadoOperacao.Erro("Senha incorreta. Use a senha que você já usa no Minha Escolinha.", nameof(AtivacaoRequisicao.Senha));
            }
            pessoa = credencial.Pessoa!;
        }
        else
        {
            if (Senha(r) is { } erroSenha) return erroSenha;
            var login = string.IsNullOrWhiteSpace(r.Email) ? cpf! : Credencial.NormalizarLogin(r.Email);
            if (await acesso.LoginEmUsoAsync(login, ct))
                return ResultadoOperacao.Erro("Este e-mail já é usado por outra conta. Use outro ou deixe em branco para entrar pelo CPF.",
                    nameof(AtivacaoRequisicao.Email));

            // A pessoa pode já existir sem login (cadastrada pela plataforma): ganha a credencial agora.
            var semLogin = await acesso.ObterPessoaPorCpfAsync(cpf!, ct);
            pessoa = semLogin ?? new Pessoa { Pessoa_ID = Guid.NewGuid(), Nome = ficha!.Nome, Cpf = cpf };
            var nova = new Credencial { Pessoa_ID = pessoa.Pessoa_ID, Login = login };
            nova.DefinirSenha(hashSenha.Gerar(r.Senha), trocarNoProximoAcesso: false);
            if (semLogin is null)
            {
                pessoa.Credencial = nova;
                acesso.AdicionarPessoa(pessoa);
            }
            else acesso.AdicionarCredencial(nova);
            credencial = nova;
        }

        ficha!.Ligar(pessoa.Pessoa_ID, cpf!);
        var papel = convite!.Tipo == TipoConvite.Responsavel ? PapelAtuacao.Responsavel : PapelAtuacao.Professor;
        var vinculos = await acesso.VinculosNaEscolinhaAsync(pessoa.Pessoa_ID, ct);
        if (vinculos.FirstOrDefault(v => v.Papel == papel) is { } existente) existente.Reativar();
        else acesso.AdicionarVinculo(new VinculoAtuacao { VinculoAtuacao_ID = Guid.NewGuid(), Pessoa_ID = pessoa.Pessoa_ID, Papel = papel });

        convite.Aceitar(pessoa.Pessoa_ID, agora);
        if (convite.Tipo == TipoConvite.Responsavel)
            foreach (var vinculo in await convites.VinculosDoResponsavelAsync(convite.FichaId, ct))
                vinculo.SolicitarVerificacao(agora);

        var entidade = convite.Tipo == TipoConvite.Responsavel ? ServicoResponsaveis.Entidade : ServicoProfessores.Entidade;
        registro.Registrar(entidade, convite.FichaId, "Acesso", "Convidado", "Ativo", $"Ativado pelo convite, com o CPF conferido ({ficha.Nome}).");
        await convites.SalvarAsync(ct);

        return ResultadoOperacao.Ok(convite.Tipo == TipoConvite.Responsavel
                ? "Acesso ativado. A escolinha confirma os seus vínculos com os alunos; até lá, o portal não mostra os dados deles."
                : "Acesso ativado.",
            new AcessoAtivado(pessoa.Pessoa_ID, credencial.CarimboSeguranca, convite.Escolinha_ID, convite.Tipo, ficha.Nome));
    }

    private async Task<(ResultadoOperacao? Erro, ConviteAcesso? Convite, Ficha? Ficha, string? Cpf, Credencial? Credencial)> ConferirAsync(
        Guid conviteId, string cpfTexto, CancellationToken ct)
    {
        var agora = relogio.Agora();
        if (await convites.ObterConviteAsync(conviteId, ct) is not { } convite || !convite.Valido(agora))
            return (ResultadoOperacao.Erro(ServicoAtivacaoConvite.MensagemEstado(EstadoConvite.Invalido)), null, null, null, null);

        var cpf = Cpf.Normalizar(cpfTexto)!;
        var ficha = await FichaAsync(convite, ct);
        if (ficha is null) return (ResultadoOperacao.Erro("O cadastro deste convite não existe mais. Fale com a escolinha."), null, null, null, null);

        if (ficha.Cpf is not null && ficha.Cpf != cpf)
        {
            convite.RegistrarTentativaErrada(agora);
            await convites.SalvarAsync(ct);
            var restantes = ConviteAcesso.MaxTentativasIdentificacao - convite.TentativasIdentificacao;
            return (ResultadoOperacao.Erro(restantes > 0
                ? $"O CPF não confere com o cadastro da escolinha. Restam {restantes} tentativa(s)."
                : "O CPF não confere. O convite foi cancelado por segurança: peça um novo à escolinha.", nameof(IdentificacaoRequisicao.Cpf)),
                null, null, null, null);
        }
        if (ficha.Cpf is null && await convites.CpfDeOutraFichaAsync(convite.Tipo, cpf, convite.FichaId, ct))
            return (ResultadoOperacao.Erro("Este CPF já está em outro cadastro da escolinha. Fale com a escolinha.", nameof(IdentificacaoRequisicao.Cpf)),
                null, null, null, null);

        var pessoa = await acesso.ObterPessoaPorCpfAsync(cpf, ct);
        if (ficha.PessoaId is { } ligada && pessoa?.Pessoa_ID != ligada)
            return (ResultadoOperacao.Erro("Este cadastro já está ligado a outra pessoa. Fale com a escolinha."), null, null, null, null);

        var credencial = pessoa is null ? null : await acesso.ObterCredencialAsync(pessoa.Pessoa_ID, ct);
        return (null, convite, ficha, cpf, credencial);
    }

    private async Task<Ficha?> FichaAsync(ConviteAcesso convite, CancellationToken ct)
    {
        if (convite.Tipo == TipoConvite.Responsavel)
            return await convites.ObterResponsavelAsync(convite.FichaId, ct) is { } responsavel
                ? new Ficha(responsavel.Nome, responsavel.Cpf, responsavel.Pessoa_ID, (pessoaId, cpf) =>
                {
                    responsavel.Pessoa_ID = pessoaId;
                    responsavel.Cpf ??= cpf;
                })
                : null;

        return await convites.ObterProfessorAsync(convite.FichaId, ct) is { } professor
            ? new Ficha(professor.Nome, professor.Cpf, professor.Pessoa_ID, (pessoaId, cpf) =>
            {
                professor.Pessoa_ID = pessoaId;
                professor.Cpf ??= cpf;
            })
            : null;
    }

    private static ResultadoOperacao? Senha(AtivacaoRequisicao r)
    {
        var validador = new InlineValidator<AtivacaoRequisicao>();
        validador.RuleFor(x => x.Senha).SenhaForte();
        var resultado = validador.Validate(r);
        if (!resultado.IsValid) return ResultadoOperacao.Invalido(resultado);
        return r.Senha != r.ConfirmacaoSenha
            ? ResultadoOperacao.Erro("A confirmação não é igual à senha.", nameof(AtivacaoRequisicao.ConfirmacaoSenha))
            : null;
    }
}
