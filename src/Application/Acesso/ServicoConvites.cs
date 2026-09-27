using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Acesso;

/// <summary>
/// Convites de acesso (BL-M01-04) e em lote (BL-M12-07), sobre fichas que já existem: responsável ou professor com
/// celular e sem acesso ativo. O convite vai pelo motor único (gatilho "Convite de acesso"), um por pessoa, identificado.
/// Reenviar troca o link (o anterior fica substituído); cancelar invalida o link.
/// </summary>
public class ServicoConvites(
    IConvitesRepositorio repositorio,
    EmissorGatilhos emissor,
    IAutorizacao autorizacao,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio)
{
    public const int MaximoLote = 100;

    public bool PodeConvidar => autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Alterar);

    public async Task<IReadOnlyList<CandidatoAcesso>> CandidatosAsync(CancellationToken ct = default) =>
        PodeConvidar ? await repositorio.CandidatosAsync(relogio.Agora(), ct) : [];

    /// <summary>A situação do acesso de uma ficha (para a ficha do responsável e a do professor).</summary>
    public async Task<CandidatoAcesso?> SituacaoDaFichaAsync(TipoConvite tipo, Guid fichaId, CancellationToken ct = default) =>
        PodeConvidar
            ? (await repositorio.CandidatosAsync(relogio.Agora(), ct)).FirstOrDefault(c => c.Tipo == tipo && c.FichaId == fichaId)
            : null;

    public async Task<ResultadoOperacao> ConvidarAsync(TipoConvite tipo, Guid fichaId, CancellationToken ct = default)
    {
        if (!PodeConvidar) return ResultadoOperacao.Erro("Só o gestor convida.");
        var resultado = await PrepararAsync(tipo, fichaId, ct);
        if (!resultado.Sucesso) return resultado;
        await repositorio.SalvarAsync(ct);
        return resultado;
    }

    /// <summary>Um convite por pessoa escolhida, num fluxo só (BL-M12-07). Quem não pode ser convidado fica no resumo, com o motivo.</summary>
    public async Task<ResultadoOperacao> ConvidarLoteAsync(IReadOnlyCollection<(TipoConvite Tipo, Guid FichaId)> fichas, CancellationToken ct = default)
    {
        if (!PodeConvidar) return ResultadoOperacao.Erro("Só o gestor convida.");
        if (fichas.Count == 0) return ResultadoOperacao.Erro("Escolha ao menos uma pessoa.");
        if (fichas.Count > MaximoLote) return ResultadoOperacao.Erro($"Convide no máximo {MaximoLote} pessoas de uma vez.");

        var enviados = 0;
        var recusas = new List<string>();
        foreach (var (tipo, fichaId) in fichas.Distinct())
        {
            var resultado = await PrepararAsync(tipo, fichaId, ct);
            if (resultado.Sucesso) enviados++;
            else recusas.Add(resultado.Mensagem);
        }
        if (enviados > 0) await repositorio.SalvarAsync(ct);

        var mensagem = enviados == 0 ? "Nenhum convite enviado." : $"{enviados} convite(s) na fila do WhatsApp.";
        if (recusas.Count > 0) mensagem += $" {recusas.Count} não enviado(s): {string.Join(" ", recusas.Take(5))}";
        return enviados == 0 ? ResultadoOperacao.Erro(mensagem) : ResultadoOperacao.Ok(mensagem);
    }

    public async Task<ResultadoOperacao> ReenviarAsync(Guid conviteId, CancellationToken ct = default)
    {
        if (!PodeConvidar) return ResultadoOperacao.Erro("Só o gestor convida.");
        if (await repositorio.ObterConviteAsync(conviteId, ct) is not { } convite) return ResultadoOperacao.Erro("Convite não encontrado.");
        return await ConvidarAsync(convite.Tipo, convite.FichaId, ct);
    }

    public async Task<ResultadoOperacao> CancelarAsync(Guid conviteId, string? motivo, CancellationToken ct = default)
    {
        if (!PodeConvidar) return ResultadoOperacao.Erro("Só o gestor cancela convite.");
        if (await repositorio.ObterConviteAsync(conviteId, ct) is not { } convite) return ResultadoOperacao.Erro("Convite não encontrado.");
        if (convite.Situacao != SituacaoConvite.Pendente) return ResultadoOperacao.Erro("O convite não está mais em aberto.");

        var texto = string.IsNullOrWhiteSpace(motivo) ? "Cancelado pelo gestor." : motivo.Trim();
        convite.Encerrar(SituacaoConvite.Cancelado, relogio.Agora(), texto);
        await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvite, convite.ConviteAcesso_ID, "O convite foi cancelado antes do envio.", ct);
        registro.Registrar(Entidade(convite.Tipo), convite.FichaId, "Convite de acesso", "Enviado", "Cancelado", texto);
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Convite de {convite.Nome} cancelado: o link deixou de valer.");
    }

    /// <summary>Confere a ficha, substitui o convite em aberto e emite o novo com a mensagem (sem gravar: quem chama grava).</summary>
    private async Task<ResultadoOperacao> PrepararAsync(TipoConvite tipo, Guid fichaId, CancellationToken ct)
    {
        string nome;
        string? telefone;
        Guid? pessoaId;
        if (tipo == TipoConvite.Responsavel)
        {
            if (await repositorio.ObterResponsavelAsync(fichaId, ct) is not { } responsavel) return ResultadoOperacao.Erro("Responsável não encontrado.");
            (nome, telefone, pessoaId) = (responsavel.Nome, responsavel.Telefone, responsavel.Pessoa_ID);
        }
        else
        {
            if (await repositorio.ObterProfessorAsync(fichaId, ct) is not { } professor) return ResultadoOperacao.Erro("Professor não encontrado.");
            (nome, telefone, pessoaId) = (professor.Nome, professor.Telefone, professor.Pessoa_ID);
        }

        if (string.IsNullOrWhiteSpace(telefone)) return ResultadoOperacao.Erro($"{nome} não tem celular cadastrado.");
        var papel = tipo == TipoConvite.Responsavel ? PapelAtuacao.Responsavel : PapelAtuacao.Professor;
        if (pessoaId is { } pessoa && await repositorio.AcessoAtivoAsync(pessoa, papel, ct)) return ResultadoOperacao.Erro($"{nome} já tem acesso.");

        var agora = relogio.Agora();
        foreach (var anterior in await repositorio.PendentesDaFichaAsync(tipo, fichaId, ct))
        {
            anterior.Encerrar(SituacaoConvite.Substituido, agora, "Reenviado: vale o convite novo.");
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvite, anterior.ConviteAcesso_ID,
                "O convite foi substituído por um novo antes do envio.", ct);
        }

        var (convite, token) = ConviteAcesso.Emitir(tipo, fichaId, nome, telefone, agora, usuarioAtual.Id, usuarioAtual.Nome ?? "Gestor");
        repositorio.AdicionarConvite(convite);
        await emissor.ConviteAsync(convite, token, ct);
        registro.Registrar(Entidade(tipo), fichaId, "Convite de acesso", null, $"Enviado, vale até {convite.ExpiraEm:dd/MM/yyyy}");
        return ResultadoOperacao.Ok($"Convite de {nome} na fila do WhatsApp (vale até {convite.ExpiraEm:dd/MM/yyyy}).", convite.ConviteAcesso_ID);
    }

    private static string Entidade(TipoConvite tipo) =>
        tipo == TipoConvite.Responsavel ? ServicoResponsaveis.Entidade : ServicoProfessores.Entidade;
}

/// <summary>A mensagem do convite só sai se aquele convite continua em aberto (não foi aceito, cancelado nem substituído).</summary>
public class ValidadorOrigemConvite(IConvitesRepositorio repositorio, IRelogioBrasil relogio) : IValidadorOrigemMensagem
{
    public string TipoOrigem => EmissorGatilhos.OrigemConvite;

    public async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default)
    {
        var convite = await repositorio.ObterConviteAsync(mensagem.Origem_ID, ct);
        return convite switch
        {
            null => "O convite não existe mais.",
            { Situacao: not SituacaoConvite.Pendente } => $"O convite foi {ConviteAcesso.Rotulo(convite.Situacao).ToLowerInvariant()} antes do envio.",
            _ when convite.Expirado(relogio.Agora()) => "O convite venceu antes do envio.",
            _ => null
        };
    }
}

/// <summary>
/// A fila de confirmação dos vínculos (BL-M01-06): quem ativou o acesso só vê o aluno depois que o gestor confirma que é
/// mesmo o responsável dele. Recusar exige motivo e fica no registro de alterações.
/// </summary>
public class ServicoVerificacaoVinculos(
    IConvitesRepositorio repositorio,
    IAutorizacao autorizacao,
    IUsuarioAtual usuarioAtual,
    RegistroDeAlteracoes registro,
    IRelogioBrasil relogio)
{
    public bool PodeVerificar => autorizacao.Nivel(Recurso.CadastroAlunosResponsaveis) == NivelAcesso.Total
                                 && autorizacao.Pode(Recurso.CadastroAlunosResponsaveis, Operacao.Alterar);

    public async Task<IReadOnlyList<VinculoParaVerificar>> PendentesAsync(CancellationToken ct = default) =>
        PodeVerificar ? await repositorio.VinculosParaVerificarAsync(ct) : [];

    public async Task<int> ContarPendentesAsync(CancellationToken ct = default) =>
        PodeVerificar ? await repositorio.ContarVinculosParaVerificarAsync(ct) : 0;

    public async Task<ResultadoOperacao> ConfirmarAsync(Guid vinculoId, CancellationToken ct = default)
    {
        if (!PodeVerificar) return ResultadoOperacao.Erro("Só o gestor confirma vínculo.");
        if (await repositorio.ObterVinculoAsync(vinculoId, ct) is not { Verificacao: VerificacaoVinculo.Pendente } vinculo)
            return ResultadoOperacao.Erro("Vínculo não encontrado ou já decidido.");

        vinculo.Confirmar(relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome ?? "Gestor");
        registro.Registrar(ServicoAlunos.Entidade, vinculo.Aluno_ID, "Vínculo verificado", "A confirmar",
            $"Confirmado: {vinculo.Responsavel?.Nome} ({VinculoAlunoResponsavel.Rotulo(vinculo.Parentesco)})");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"{vinculo.Responsavel?.Nome} agora vê {vinculo.Aluno?.Nome} no portal.");
    }

    public async Task<ResultadoOperacao> RecusarAsync(Guid vinculoId, string? motivo, CancellationToken ct = default)
    {
        if (!PodeVerificar) return ResultadoOperacao.Erro("Só o gestor recusa vínculo.");
        if (string.IsNullOrWhiteSpace(motivo)) return ResultadoOperacao.Erro("Informe o motivo da recusa.", "Motivo");
        if (await repositorio.ObterVinculoAsync(vinculoId, ct) is not { Verificacao: VerificacaoVinculo.Pendente } vinculo)
            return ResultadoOperacao.Erro("Vínculo não encontrado ou já decidido.");

        vinculo.Recusar(relogio.Agora(), usuarioAtual.Id, usuarioAtual.Nome ?? "Gestor", motivo.Trim());
        registro.Registrar(ServicoAlunos.Entidade, vinculo.Aluno_ID, "Vínculo verificado", "A confirmar",
            $"Recusado: {vinculo.Responsavel?.Nome}", motivo.Trim());
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Vínculo recusado: {vinculo.Responsavel?.Nome} não vê {vinculo.Aluno?.Nome} no portal.");
    }
}
