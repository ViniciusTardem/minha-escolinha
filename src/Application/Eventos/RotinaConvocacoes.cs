using Microsoft.Extensions.Logging;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Application.Eventos;

/// <summary>
/// A resposta do botão da convocação (BL-M07-09, CA-12): o motor registra toda resposta no log e devolve o botão ao módulo
/// de origem (S2, <see cref="IReceptorResposta"/>). "Vai" é o primeiro botão; "Não vai", o segundo. Vale a primeira
/// resposta (P03). O painel aberto atualiza sozinho depois da gravação (tempo real).
/// </summary>
public class RespostasConvocacao(IEventosRepositorio eventos, EmissorGatilhos emissor, IRelogioBrasil relogio)
{
    public async Task<string?> AplicarAsync(Mensagem mensagem, RespostaMensagem resposta, CancellationToken ct = default)
    {
        if (!PayloadBotao.TentarLer(resposta.Payload, out _, out var indice) || indice is not (0 or 1)) return null;
        if (await eventos.ObterConvocacaoAsync(mensagem.Origem_ID, ct) is not { } convocacao) return null;

        var vai = indice == 0;
        // P45: o botão da convocação de antes da mudança de horário não confirma o horário novo.
        if (convocacao.MensagemAnteriorANovaConfirmacao(mensagem.CriadaEm))
        {
            convocacao.RegistrarRespostaAntiga(vai, mensagem.DestinatarioNome, resposta.RecebidaEm, relogio.Agora());
            return "Resposta à convocação de antes da mudança de horário: fica no histórico e não vale.";
        }
        var resultado = convocacao.Responder(vai, OrigemResposta.WhatsApp, mensagem.DestinatarioNome, mensagem.Destinatario_ID,
            resposta.RecebidaEm, relogio.Agora());
        if (resultado == ResultadoResposta.Aplicada)
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvocacao, convocacao.Convocacao_ID, "A família já respondeu.", ct);

        return resultado switch
        {
            ResultadoResposta.Aplicada => $"Convocação {(vai ? "confirmada" : "recusada")} pela resposta de {mensagem.DestinatarioNome}.",
            ResultadoResposta.Posterior => "Já havia resposta: vale a primeira (P03). Esta fica no histórico da convocação.",
            ResultadoResposta.Tardia => "O jogo já tinha começado: a resposta fica no histórico da convocação.",
            ResultadoResposta.Repetida => "A mesma resposta já estava registrada.",
            _ => "A convocação não estava valendo: a resposta fica no histórico."
        };
    }
}

/// <summary>O botão da convocação criada.</summary>
public class ReceptorRespostaConvocacao(RespostasConvocacao respostas) : IReceptorResposta
{
    public virtual GatilhoMensagem Gatilho => GatilhoMensagem.ConvocacaoCriada;

    public Task<string?> ReceberAsync(Mensagem mensagem, RespostaMensagem resposta, CancellationToken ct = default) =>
        respostas.AplicarAsync(mensagem, resposta, ct);
}

/// <summary>O botão do lembrete a quem não respondeu (a mesma convocação).</summary>
public class ReceptorRespostaLembreteConvocacao(RespostasConvocacao respostas) : ReceptorRespostaConvocacao(respostas)
{
    public override GatilhoMensagem Gatilho => GatilhoMensagem.ConvocacaoSemResposta;
}

public sealed record ResumoRotinaConvocacoes(int Lembretes, int SemResposta)
{
    public override string ToString() => $"{Lembretes} lembrete(s), {SemResposta} sem resposta";
}

/// <summary>
/// A rotina da convocação (na escolinha do contexto; job a cada 15 minutos):
/// <list type="bullet">
/// <item>lembrete a quem não respondeu (BL-M07-11), na janela configurada antes do evento (<c>convocacao.lembrete.horas_antes</c>,
/// lida na hora, regra de ouro 14): uma vez por convocação, e só se ela saiu antes do momento do lembrete (a família que
/// acabou de receber a convocação não recebe o lembrete em seguida);</item>
/// <item>o evento começou: a convocação que ainda esperava vira "sem resposta" (P19).</item>
/// </list>
/// </summary>
public class RotinaConvocacoes(
    IEventosRepositorio eventos,
    TextosEventos textos,
    IParametros parametros,
    EmissorGatilhos emissor,
    IContextoEscolinha contexto,
    IRelogioBrasil relogio,
    ILogger<RotinaConvocacoes> logger)
{
    public async Task<ResumoRotinaConvocacoes> ExecutarAsync(CancellationToken ct = default)
    {
        var p = (await parametros.ObterAsync(ct)).Convocacao;
        var agora = relogio.Agora();
        int lembretes = 0, semResposta = 0;

        foreach (var evento in await eventos.ComPendentesAteAsync(agora.AddHours(p.LembreteHorasAntes), ct))
        {
            var pendentes = (await eventos.ConvocacoesAsync([evento.Evento_ID], ct)).Where(c => c.Pendente).ToList();
            if (evento.Inicio <= agora)
            {
                semResposta += pendentes.Count(c => c.MarcarSemResposta(agora));
                continue;
            }

            var momento = evento.Inicio.AddHours(-p.LembreteHorasAntes);
            var lembrar = pendentes.Where(c => c.LembreteEm is null && c.EnviadaEm < momento).ToList();
            if (lembrar.Count == 0) continue;
            var dados = await textos.ParaMensagemAsync(evento, ct);
            var nomes = await eventos.NomesDosAlunosAsync(lembrar.Select(c => c.Aluno_ID).ToList(), ct);
            foreach (var convocacao in lembrar)
            {
                await emissor.LembreteConvocacaoAsync(convocacao, dados, nomes.GetValueOrDefault(convocacao.Aluno_ID, "—"), ct);
                convocacao.RegistrarLembrete(agora);
                lembretes++;
            }
        }

        await eventos.SalvarAsync(ct);
        var resumo = new ResumoRotinaConvocacoes(lembretes, semResposta);
        if (lembretes + semResposta > 0) logger.LogInformation("Convocações da escolinha {Escolinha}: {Resumo}.", contexto.EscolinhaId, resumo);
        return resumo;
    }
}

/// <summary>
/// Os efeitos do cadastro na convocação (CA-3): quem é desligado, pausado ou sai da turma deixa as convocações dos jogos
/// a partir da data. A escolhida e ainda não disparada some; a disparada fica cancelada no histórico, e o que estava na
/// fila é recolhido. O jogo que já passou fica como está (o histórico do período em que o aluno esteve ativo).
/// </summary>
public class EfeitosConvocacaoDoCadastro(IEventosRepositorio eventos, EmissorGatilhos emissor, IRelogioBrasil relogio)
{
    /// <param name="turmaId">Só as convocações por esta turma (matrícula encerrada); <c>null</c> = todas (desligamento, pausa).</param>
    /// <returns>Quantas convocações saíram (quem chama salva).</returns>
    public async Task<int> RetirarAlunoAsync(Guid alunoId, Guid? turmaId, DateOnly aPartirDe, string motivo, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        // O jogo que já começou é histórico: com a data no passado, só saem os que ainda vão acontecer.
        var desde = aPartirDe.ToDateTime(TimeOnly.MinValue);
        if (desde < agora) desde = agora;
        var retiradas = 0;
        foreach (var convocacao in await eventos.ConvocacoesFuturasDoAlunoAsync(alunoId, turmaId, desde, ct))
        {
            if (!convocacao.Disparada)
            {
                eventos.RemoverConvocacao(convocacao);
                retiradas++;
                continue;
            }
            if (!convocacao.Cancelar(motivo, agora)) continue;
            await emissor.CancelarPendentesAsync(EmissorGatilhos.OrigemConvocacao, convocacao.Convocacao_ID, motivo, ct);
            retiradas++;
        }
        return retiradas;
    }
}

/// <summary>
/// A convocação e o lembrete só saem se a convocação ainda espera resposta (regra de ouro 4): respondida (pelo outro
/// responsável, pelo registro por fora) ou cancelada, a mensagem da fila é suprimida.
/// </summary>
public class ValidadorOrigemConvocacao(IEventosRepositorio eventos) : IValidadorOrigemMensagem
{
    public string TipoOrigem => EmissorGatilhos.OrigemConvocacao;

    public async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default)
    {
        if (await eventos.ObterConvocacaoAsync(mensagem.Origem_ID, ct) is not { } convocacao) return "A convocação não existe mais.";
        return convocacao.Situacao switch
        {
            SituacaoConvocacao.Enviada => null,
            SituacaoConvocacao.Cancelada => "A convocação foi cancelada.",
            SituacaoConvocacao.SemResposta => "O jogo já começou.",
            _ => "A convocação já foi respondida."
        };
    }
}

/// <summary>O aviso de jogo alterado ou cancelado só sai se ainda vale: o alterado, com o jogo aberto; o cancelado, com o jogo encerrado.</summary>
public class ValidadorOrigemEvento(IEventosRepositorio eventos) : IValidadorOrigemMensagem
{
    public string TipoOrigem => EmissorGatilhos.OrigemEvento;

    public async Task<string?> MotivoParaNaoEnviarAsync(Mensagem mensagem, CancellationToken ct = default)
    {
        if (await eventos.ObterAsync(mensagem.Origem_ID, ct) is not { } evento) return "O jogo não existe mais.";
        return mensagem.Gatilho switch
        {
            GatilhoMensagem.EventoCancelado when !evento.Encerrado => "O jogo não está cancelado.",
            GatilhoMensagem.EventoAlterado when !evento.Aberto => "O jogo foi cancelado, adiado ou já aconteceu depois da alteração.",
            _ => null
        };
    }
}
