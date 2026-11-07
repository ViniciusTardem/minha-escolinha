using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Alunos;

/// <summary>O pedido de aceite dos termos como a ficha do aluno mostra (D111). O link só vem enquanto vale, para a gestão.</summary>
/// <param name="Email">A situação do e-mail no motor (nula: sem e-mail, a gestão repassa o link) e o motivo, se não saiu.</param>
public sealed record PedidoTermosDaFicha(
    Guid Id,
    SituacaoPedidoTermos Situacao,
    bool Vencido,
    string Responsavel,
    string? EnviadoPara,
    SituacaoMensagem? Email,
    string? MotivoEmail,
    DateTime CriadoEm,
    string CriadoPorNome,
    DateTime ExpiraEm,
    string? Link,
    bool PedeDados,
    bool PedeImagem,
    DateTime? RespondidoEm,
    bool? AceitouDados,
    bool? AceitouImagem);

/// <summary>A aba Consentimentos da ficha: o último pedido e se a gestão pode enviar (de novo).</summary>
public sealed record TermosDaFicha(PedidoTermosDaFicha? Ultimo, bool PodeEnviar, string? MotivoNaoEnvia);

/// <summary>
/// Monta o pedido de aceite dos termos (D111) sem gravar: quem chama grava, na mesma transação da mudança de negócio (o
/// cadastro do aluno ou o reenvio). O pedido anterior que ainda vale é cancelado, e o e-mail dele, se ainda está na fila,
/// não sai (regra de ouro 5). Com e-mail, o motor manda o link; sem e-mail, a gestão copia o link na ficha.
/// </summary>
public class EmissaoTermos(
    IAlunosRepositorio repositorio,
    EmissorGatilhos emissor,
    IProtetorSegredos protetor,
    OpcoesEmail opcoesEmail,
    IParametros parametros,
    RegistroDeAlteracoes registro,
    IUsuarioAtual usuarioAtual,
    IRelogioBrasil relogio)
{
    /// <returns>Se o link foi por e-mail (ou fica com a gestão).</returns>
    public async Task<bool> PrepararAsync(Guid alunoId, string aluno, Responsavel responsavel, bool pedeDados, bool pedeImagem,
        CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        foreach (var anterior in (await repositorio.PedidosTermosDoAlunoAsync(alunoId, ct)).Where(p => p.Situacao == SituacaoPedidoTermos.Aguardando))
        {
            anterior.Cancelar("Substituído por um envio novo.", agora);
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemTermos, anterior.PedidoTermos_ID, "Os termos foram enviados de novo.", ct);
        }

        var email = opcoesEmail.Habilitado ? Cadastros.Vazio(responsavel.Email) : null;
        var validade = (await parametros.ObterAsync(ct)).Matricula.TermosValidadeDias;
        var (pedido, token) = PedidoTermos.Novo(alunoId, responsavel.Responsavel_ID, pedeDados, pedeImagem, agora, validade,
            usuarioAtual.Nome ?? "Gestão", email);
        pedido.TokenProtegido = protetor.Proteger(token);
        repositorio.AdicionarPedidoTermos(pedido);

        if (email is not null)
            await emissor.TermosConsentimentoAsync(pedido.PedidoTermos_ID, responsavel.Responsavel_ID, responsavel.Nome, email, alunoId, aluno,
                Termos(pedeDados, pedeImagem), opcoesEmail.LinkTermos(token), pedido.ExpiraEm, ct);

        registro.Registrar(ServicoAlunos.Entidade, alunoId, "Termos de consentimento", null,
            $"Link para {responsavel.Nome} responder ({Termos(pedeDados, pedeImagem)}), " +
            (email is null ? "para a gestão repassar" : $"por e-mail para {email}") + $", vale até {pedido.ExpiraEm:dd/MM/yyyy}");
        return email is not null;
    }

    public static string Termos(bool dados, bool imagem) => (dados, imagem) switch
    {
        (true, true) => "tratamento de dados e uso de imagem",
        (true, false) => "tratamento de dados",
        _ => "uso de imagem"
    };
}

/// <summary>
/// A gestão e os termos de consentimento pela ficha do aluno (D111): ver o último pedido (com o link enquanto vale, para
/// quem não tem e-mail) e enviar de novo. O link vai para o 1º responsável: o pagador, ou o primeiro vinculado.
/// </summary>
public class ServicoTermos(
    IAlunosRepositorio repositorio,
    IMensageriaRepositorio mensagens,
    IAutorizacao autorizacao,
    EmissaoTermos emissao,
    IProtetorSegredos protetor,
    OpcoesEmail opcoesEmail,
    IRelogioBrasil relogio)
{
    public async Task<TermosDaFicha?> DaFichaAsync(Guid alunoId, CancellationToken ct = default)
    {
        if (!await PodeAsync(alunoId, ct)) return null;
        var agora = relogio.Agora();
        var (dados, imagem) = await PendentesAsync(alunoId, ct);
        var motivo = !dados && !imagem ? "Os dois termos já têm resposta registrada." : null;

        PedidoTermosDaFicha? ultimo = null;
        if ((await repositorio.PedidosTermosDoAlunoAsync(alunoId, ct)).FirstOrDefault() is { } p)
        {
            var email = (await mensagens.MensagensDasOrigensAsync(EmissorGatilhos.OrigemTermos, [p.PedidoTermos_ID], ct))
                .Where(m => m.Canal == CanalMensagem.Email).OrderByDescending(m => m.CriadaEm).FirstOrDefault();
            var link = p.Valido(agora) && p.TokenProtegido is { } protegido && protetor.Desproteger(protegido) is { } token
                ? opcoesEmail.LinkTermos(token)
                : null;
            ultimo = new PedidoTermosDaFicha(p.PedidoTermos_ID, p.Situacao, p.Vencido(agora), p.Responsavel?.Nome ?? "—", p.EnviadoPara,
                email?.Situacao, email?.Motivo, p.CriadoEm, p.CriadoPorNome, p.ExpiraEm, link, p.PedeDados, p.PedeImagem, p.RespondidoEm,
                p.AceitouDados, p.AceitouImagem);
        }
        return new TermosDaFicha(ultimo, motivo is null, motivo);
    }

    public async Task<ResultadoOperacao> EnviarAsync(Guid alunoId, CancellationToken ct = default)
    {
        if (!await PodeAsync(alunoId, ct) || await repositorio.ObterAlunoAsync(alunoId, ct) is not { } aluno)
            return ResultadoOperacao.Erro("Aluno não encontrado.");
        var (dados, imagem) = await PendentesAsync(alunoId, ct);
        if (!dados && !imagem) return ResultadoOperacao.Erro("Os dois termos já têm resposta registrada.");

        var vinculos = await repositorio.VinculosDoAlunoAsync(alunoId, ct);
        if ((vinculos.FirstOrDefault(v => v.Pagador) ?? vinculos.OrderBy(v => v.DataCadastro).FirstOrDefault())?.Responsavel is not { } responsavel)
            return ResultadoOperacao.Erro("O aluno não tem responsável.");

        var porEmail = await emissao.PrepararAsync(alunoId, aluno.Nome, responsavel, dados, imagem, ct);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok(porEmail
            ? $"Termos enviados por e-mail para {responsavel.Nome}. O link anterior, se havia, deixou de valer."
            : $"{responsavel.Nome} não tem e-mail: copie o link abaixo e repasse. O link anterior, se havia, deixou de valer.");
    }

    /// <summary>Os termos que ainda não têm registro nenhum: só eles vão para o link (a recusa e a revogação ficam como estão).</summary>
    private async Task<(bool Dados, bool Imagem)> PendentesAsync(Guid alunoId, CancellationToken ct)
    {
        var registros = await repositorio.ConsentimentosDoAlunoAsync(alunoId, ct);
        return (ConsentimentoAluno.EstadoAtual(registros, FinalidadeConsentimento.TratamentoDados) == EstadoConsentimento.Pendente,
            ConsentimentoAluno.EstadoAtual(registros, FinalidadeConsentimento.UsoImagem) == EstadoConsentimento.Pendente);
    }

    private Task<bool> PodeAsync(Guid alunoId, CancellationToken ct) =>
        autorizacao.PodeAsync(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar, new AlvoEscopo(TipoAlvoEscopo.Aluno, alunoId), ct);
}

/// <summary>O e-mail dos termos só sai se o pedido ainda espera resposta e vale (regra de ouro 4).</summary>
public class ValidadorOrigemTermos(IAlunosRepositorio repositorio, IRelogioBrasil relogio) : IValidadorOrigemMensagem
{
    public string TipoOrigem => EmissorGatilhos.OrigemTermos;

    public async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default) =>
        await repositorio.ObterPedidoTermosAsync(mensagem.Origem_ID, ct) switch
        {
            null => "O pedido dos termos não existe mais.",
            { Situacao: SituacaoPedidoTermos.Respondido } => "Os termos já foram respondidos.",
            { Situacao: SituacaoPedidoTermos.Cancelado } => "Os termos foram enviados de novo: vale o link novo.",
            var p when !p.Valido(relogio.Agora()) => "O link dos termos venceu antes do envio.",
            _ => null
        };
}

// ── A página pública de aceite (anônima) ──

/// <summary>O que a página mostra. Antes do CPF, só o nome da escolinha (nenhum dado do aluno sem a identificação).</summary>
public sealed record TermosParaResponder(string Escolinha, string Responsavel, string Aluno, bool PedeCpf, bool Identificado,
    IReadOnlyList<TermoConsentimento> Termos);

public class RespostaTermosRequisicao
{
    public string? Cpf { get; set; }

    /// <summary>Tratamento de dados: true aceita, false não aceita; nulo quando o termo não foi pedido (ou falta a resposta).</summary>
    public bool? Dados { get; set; }

    public bool? Imagem { get; set; }
}

/// <summary>
/// A página pública dos termos (D111), anônima como a do convite: o link acha o pedido pelo hash do segredo, e o
/// trabalho roda na escolinha do pedido. Link respondido, cancelado ou vencido é "indisponível", sem dizer mais nada.
/// </summary>
public class ServicoRespostaTermos(IAcessoRepositorio acesso, IExecutorEscolinha executor, IRelogioBrasil relogio)
{
    public const string MensagemIndisponivel = "Este link não está mais disponível. Se ainda precisa responder aos termos, peça um link novo à escolinha.";

    public async Task<PedidoTermos?> PedidoValidoAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100) return null;
        var pedido = await acesso.PedidoTermosPorHashAsync(PedidoTermos.Hash(token), ct);
        return pedido is not null && pedido.Valido(relogio.Agora()) ? pedido : null;
    }

    /// <param name="cpf">Nulo na abertura; na identificação, o CPF digitado.</param>
    public async Task<ResultadoOperacao> AbrirAsync(string? token, string? cpf, CancellationToken ct = default)
    {
        if (await PedidoValidoAsync(token, ct) is not { } pedido) return ResultadoOperacao.Erro(MensagemIndisponivel);
        return await executor.ExecutarAsync(pedido.Escolinha_ID,
            sp => sp.GetRequiredService<RespostaTermosNaEscolinha>().AbrirAsync(pedido.PedidoTermos_ID, cpf, ct), ct);
    }

    public async Task<ResultadoOperacao> ResponderAsync(string? token, RespostaTermosRequisicao r, string? ip, CancellationToken ct = default)
    {
        if (await PedidoValidoAsync(token, ct) is not { } pedido) return ResultadoOperacao.Erro(MensagemIndisponivel);
        return await executor.ExecutarAsync(pedido.Escolinha_ID,
            sp => sp.GetRequiredService<RespostaTermosNaEscolinha>().ResponderAsync(pedido.PedidoTermos_ID, r, ip, ct), ct);
    }
}

/// <summary>A resposta aos termos na escolinha do pedido: os consentimentos e o pedido respondido numa gravação só.</summary>
public class RespostaTermosNaEscolinha(
    IAlunosRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    EmissorGatilhos emissor,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio,
    ILogger<RespostaTermosNaEscolinha> logger)
{
    public const string MensagemCpf = "O CPF não confere com o do responsável cadastrado na escolinha.";

    public async Task<ResultadoOperacao> AbrirAsync(Guid pedidoId, string? cpf, CancellationToken ct = default)
    {
        if (await CarregarAsync(pedidoId, ct) is not { } carregado) return ResultadoOperacao.Erro(ServicoRespostaTermos.MensagemIndisponivel);
        var (pedido, responsavel, aluno) = carregado;
        var escolinha = (await configuracao.ObterEscolinhaAsync(ct))?.Nome ?? "A escolinha";
        var pedeCpf = responsavel.Cpf is not null;
        if (pedeCpf && cpf is null)
            return ResultadoOperacao.Ok("Confirme o CPF.", new TermosParaResponder(escolinha, string.Empty, string.Empty, true, false, []));
        if (pedeCpf && Cpf.Normalizar(cpf) != responsavel.Cpf)
            return ResultadoOperacao.Erro(MensagemCpf, nameof(RespostaTermosRequisicao.Cpf));

        var termos = TermosConsentimento.Todos
            .Where(t => (t.Finalidade == FinalidadeConsentimento.TratamentoDados && pedido.PedeDados)
                        || (t.Finalidade == FinalidadeConsentimento.UsoImagem && pedido.PedeImagem))
            .ToList();
        return ResultadoOperacao.Ok("Termos.", new TermosParaResponder(escolinha, EmissorGatilhos.PrimeiroNome(responsavel.Nome), aluno,
            pedeCpf, true, termos));
    }

    public async Task<ResultadoOperacao> ResponderAsync(Guid pedidoId, RespostaTermosRequisicao r, string? ip, CancellationToken ct = default)
    {
        if (await CarregarAsync(pedidoId, ct) is not { } carregado) return ResultadoOperacao.Erro(ServicoRespostaTermos.MensagemIndisponivel);
        var (pedido, responsavel, aluno) = carregado;
        if (responsavel.Cpf is not null && Cpf.Normalizar(r.Cpf) != responsavel.Cpf)
            return ResultadoOperacao.Erro(MensagemCpf, nameof(RespostaTermosRequisicao.Cpf));
        if (pedido.PedeDados && r.Dados is null)
            return ResultadoOperacao.Erro("Responda ao termo de tratamento de dados.", nameof(RespostaTermosRequisicao.Dados));
        if (pedido.PedeImagem && r.Imagem is null)
            return ResultadoOperacao.Erro("Responda ao termo de uso de imagem.", nameof(RespostaTermosRequisicao.Imagem));

        var agora = relogio.Agora();
        pedido.Responder(r.Dados, r.Imagem, agora, ip);
        var respostas = new List<string>();
        foreach (var (finalidade, pedida, aceitou) in new[]
                 {
                     (FinalidadeConsentimento.TratamentoDados, pedido.PedeDados, r.Dados),
                     (FinalidadeConsentimento.UsoImagem, pedido.PedeImagem, r.Imagem)
                 })
        {
            if (!pedida) continue;
            repositorio.AdicionarConsentimento(new ConsentimentoAluno
            {
                ConsentimentoAluno_ID = Guid.NewGuid(),
                Aluno_ID = pedido.Aluno_ID,
                Finalidade = finalidade,
                Concedido = aceitou!.Value,
                VersaoTermo = TermosConsentimento.Obter(finalidade).Versao,
                Responsavel_ID = responsavel.Responsavel_ID,
                Origem = OrigemConsentimento.LinkDoResponsavel,
                RegistradoEm = agora,
                RegistradoPorNome = TextoHelper.Truncar(responsavel.Nome, ConsentimentoAluno.TamanhoAutor)!
            });
            respostas.Add($"{TermosConsentimento.Rotulo(finalidade).ToLowerInvariant()}: {(aceitou.Value ? "aceito" : "não aceito")}");
        }
        await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemTermos, pedido.PedidoTermos_ID, "Os termos já foram respondidos.", ct);
        registro.Registrar(ServicoAlunos.Entidade, pedido.Aluno_ID, "Termos de consentimento", "Aguardando resposta pelo link",
            $"Respondido por {responsavel.Nome} pelo link ({string.Join("; ", respostas)})");

        try
        {
            await repositorio.SalvarAsync(ct);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Duas respostas ao mesmo link ao mesmo tempo: a primeira vale.
            return ResultadoOperacao.Erro(ServicoRespostaTermos.MensagemIndisponivel);
        }
        logger.LogInformation("Termos do pedido {Pedido} respondidos pelo link ({Respostas}).", pedido.PedidoTermos_ID, string.Join("; ", respostas));
        return ResultadoOperacao.Ok($"Resposta registrada, {EmissorGatilhos.PrimeiroNome(responsavel.Nome)}. {aluno}: {string.Join("; ", respostas)}. " +
                                    "Este link não vale mais. Para mudar a resposta, fale com a escolinha.");
    }

    private async Task<(PedidoTermos Pedido, Responsavel Responsavel, string Aluno)?> CarregarAsync(Guid pedidoId, CancellationToken ct)
    {
        if (await repositorio.ObterPedidoTermosAsync(pedidoId, ct) is not { Responsavel: { } responsavel } pedido
            || !pedido.Valido(relogio.Agora()))
            return null;
        var aluno = (await repositorio.ObterAlunoAsync(pedido.Aluno_ID, ct))?.Nome ?? "—";
        return (pedido, responsavel, EmissorGatilhos.PrimeiroNome(aluno));
    }
}
