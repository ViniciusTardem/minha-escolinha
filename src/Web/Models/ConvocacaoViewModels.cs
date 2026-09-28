using MinhaEscolinha.Application.Eventos;
using MinhaEscolinha.Domain.Agenda;

namespace MinhaEscolinha.Web.Models;

/// <summary>O formulário do jogo (novo, na tela da semana, ou a alteração), com os alertas de agenda quando voltou para confirmar.</summary>
public sealed record FormularioJogoViewModel(EventoRequisicao Requisicao, FormularioEvento Opcoes, IReadOnlyList<ConflitoAgenda>? Alertas,
    bool Alteracao);

/// <summary>A montagem do fim de semana (BL-M07-08): a semana e o jogo novo, aberto quando voltou com erro ou alerta.</summary>
public sealed record SemanaConvocacoesViewModel(SemanaConvocacoes Tela, FormularioJogoViewModel Formulario, bool AbrirNovo);

/// <summary>Cancelar ou adiar: o motivo vai na mensagem às famílias.</summary>
public sealed record EncerrarJogoViewModel(EncerrarEventoRequisicao Requisicao, PainelEvento Jogo);
