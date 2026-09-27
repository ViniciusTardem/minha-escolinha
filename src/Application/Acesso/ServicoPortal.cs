using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;

namespace MinhaEscolinha.Application.Acesso;

/// <summary>Um aluno ligado à pessoa, com a verificação do vínculo.</summary>
public sealed record FilhoDaPessoa(Guid AlunoId, string Nome, SituacaoAluno Situacao, IReadOnlyList<string> Turmas, VerificacaoVinculo Verificacao);

public sealed record FilhoNoPortal(Guid AlunoId, string Nome, SituacaoAluno Situacao, IReadOnlyList<string> Turmas);

/// <param name="VinculosAConfirmar">Quantos alunos esperam a confirmação da escolinha (sem nome: bloqueio total, BL-M01-06).</param>
public sealed record InicioPortal(string Escolinha, string Nome, IReadOnlyList<FilhoNoPortal> Filhos, int VinculosAConfirmar, int AvisosNaoLidos);

/// <summary>
/// O começo do portal do responsável (S2): os filhos com vínculo confirmado e os avisos (o segundo canal do motor,
/// BL-M09-02). Jogos, pagamentos e presenças chegam no S6. Aluno com vínculo a confirmar não aparece: nem o nome.
/// </summary>
public class ServicoPortal(
    IConvitesRepositorio repositorio,
    IConfiguracaoRepositorio configuracao,
    ServicoMensagens mensagens,
    IUsuarioAtual usuarioAtual,
    IContextoEscolinha contexto)
{
    public bool EhResponsavel => usuarioAtual.Id is not null && contexto.Papeis.Contains(PapelAtuacao.Responsavel);

    public async Task<InicioPortal?> InicioAsync(CancellationToken ct = default)
    {
        if (!EhResponsavel) return null;
        var filhos = await repositorio.FilhosDaPessoaAsync(usuarioAtual.Id!.Value, ct);
        return new InicioPortal(
            (await configuracao.ObterEscolinhaAsync(ct))?.Nome ?? "Escolinha",
            usuarioAtual.Nome ?? "Responsável",
            filhos.Where(f => f.Verificacao == VerificacaoVinculo.Verificado)
                .Select(f => new FilhoNoPortal(f.AlunoId, f.Nome, f.Situacao, f.Turmas)).ToList(),
            filhos.Count(f => f.Verificacao == VerificacaoVinculo.Pendente),
            await mensagens.AvisosNaoLidosAsync(ct));
    }
}
