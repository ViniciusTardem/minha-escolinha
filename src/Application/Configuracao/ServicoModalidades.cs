using FluentValidation;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Configuracao;

namespace MinhaEscolinha.Application.Configuracao;

/// <summary>Modalidades da escolinha com o perfil e os interruptores de recurso (BL-M02-02, R8).</summary>
public class ServicoModalidades(
    IConfiguracaoRepositorio repositorio,
    RegistroDeAlteracoes registro,
    IValidator<ModalidadeRequisicao> validador)
{
    public async Task<IReadOnlyList<ModalidadeResposta>> ListarAsync(CancellationToken ct = default) =>
        (await repositorio.ListarModalidadesAsync(ct)).Select(Resposta).ToList();

    public async Task<ModalidadeResposta?> ObterAsync(Guid id, CancellationToken ct = default) =>
        await repositorio.ObterModalidadeAsync(id, ct) is { } m ? Resposta(m) : null;

    public async Task<ResultadoOperacao> CriarAsync(ModalidadeRequisicao requisicao, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (await repositorio.NomeModalidadeEmUsoAsync(requisicao.Nome.Trim(), null, ct))
            return ResultadoOperacao.Erro($"Já existe a modalidade {requisicao.Nome.Trim()}.", nameof(ModalidadeRequisicao.Nome));

        var modalidade = Modalidade.Nova(requisicao.Nome, requisicao.Perfil);
        modalidade.AplicarRecursos(requisicao.Recursos);
        modalidade.DuracaoPadraoEventoMinutos = requisicao.DuracaoPadraoEventoMinutos;
        repositorio.AdicionarModalidade(modalidade);
        registro.Registrar(nameof(Modalidade), null, "Criação", null, Descrever(modalidade));
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Modalidade {modalidade.Nome} cadastrada.", modalidade.Modalidade_ID);
    }

    public async Task<ResultadoOperacao> EditarAsync(ModalidadeRequisicao requisicao, CancellationToken ct = default)
    {
        var validacao = await validador.ValidateAsync(requisicao, ct);
        if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
        if (requisicao.Id is not { } id || await repositorio.ObterModalidadeAsync(id, ct) is not { } modalidade)
            return ResultadoOperacao.Erro("Modalidade não encontrada.");
        if (await repositorio.NomeModalidadeEmUsoAsync(requisicao.Nome.Trim(), id, ct))
            return ResultadoOperacao.Erro($"Já existe a modalidade {requisicao.Nome.Trim()}.", nameof(ModalidadeRequisicao.Nome));

        var antes = Descrever(modalidade);
        modalidade.Nome = requisicao.Nome.Trim();
        modalidade.Perfil = requisicao.Perfil;
        modalidade.AplicarRecursos(requisicao.Recursos);
        modalidade.DuracaoPadraoEventoMinutos = requisicao.DuracaoPadraoEventoMinutos;

        if (!registro.SeMudou(nameof(Modalidade), id, "Configuração", antes, Descrever(modalidade)))
            return ResultadoOperacao.Ok("Nada mudou.");
        await repositorio.SalvarAsync(ct);
        return ResultadoOperacao.Ok($"Modalidade {modalidade.Nome} atualizada.");
    }

    private static ModalidadeResposta Resposta(Modalidade m) =>
        new(m.Modalidade_ID, m.Nome, m.Perfil, m.Recursos, m.DuracaoPadraoEventoMinutos);

    /// <summary>Texto curto da configuração, para a trilha de auditoria.</summary>
    private static string Descrever(Modalidade m)
    {
        var r = m.Recursos;
        var ligados = new List<string>();
        if (r.Adversario) ligados.Add("adversário");
        if (r.PlacarSumula) ligados.Add("placar/súmula");
        if (r.Classificacao) ligados.Add("classificação");
        if (r.Convocacao) ligados.Add("convocação");
        if (r.Graduacao) ligados.Add("graduação");
        if (r.Apresentacao) ligados.Add("apresentação");
        return $"{m.Nome} · {ModelosPerfil.Rotulo(m.Perfil)} · {(ligados.Count == 0 ? "sem recursos" : string.Join(", ", ligados))} · " +
               $"{m.DuracaoPadraoEventoMinutos} min";
    }
}
