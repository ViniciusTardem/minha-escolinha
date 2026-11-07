using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Carga;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Domain.Alunos;
using MinhaEscolinha.Domain.Carga;
using MinhaEscolinha.Domain.Configuracao;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Financeiro;
using MinhaEscolinha.Domain.Integracao;
using MinhaEscolinha.Domain.Mensageria;
using MinhaEscolinha.Domain.Plataforma;
using MinhaEscolinha.Domain.Presencas;
using MinhaEscolinha.Domain.Turmas;

namespace MinhaEscolinha.Web.Infraestrutura;

/// <summary>
/// Erro de caso de uso no formulário. Com <see cref="ResultadoOperacao.Campo"/>, o campo fica marcado
/// (<see cref="CampoInvalidoTagHelper"/>) e o formulário em etapas abre na etapa dele; sem campo, só o resumo do topo.
/// </summary>
public static class ErrosDeFormulario
{
    public static void AdicionarErro(this ModelStateDictionary modelState, ResultadoOperacao resultado) =>
        modelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensagem);

    /// <summary>Formulário cujos campos têm prefixo (<c>Nova.Inicio</c>): o erro vai para o campo com o prefixo.</summary>
    public static void AdicionarErro(this ModelStateDictionary modelState, ResultadoOperacao resultado, string prefixo) =>
        modelState.AddModelError(resultado.Campo is { Length: > 0 } campo ? $"{prefixo}.{campo}" : string.Empty, resultado.Mensagem);
}

/// <summary>Chaves de TempData lidas pelo layout (viram toast KTUI por JS em arquivo).</summary>
public static class ChavesTempData
{
    public const string Sucesso = "Sucesso";
    public const string Erro = "Erro";
}

public enum VarianteSelo
{
    Neutro,
    Sucesso,
    Info,
    Alerta,
    Perigo,
    Primario
}

/// <summary>
/// Estados que a tela sinaliza sem haver enum de domínio por trás (a conta existe ou não, o saldo passou do aviso).
/// Passam pelo mesmo mapa central: o mesmo estado tem a mesma cor em qualquer tela.
/// </summary>
public enum SinalTela
{
    /// <summary>O link dos termos venceu sem resposta (D111).</summary>
    LinkTermosVencido,
    ContaRecebimentoVinculada,
    ContaRecebimentoAusente,
    SaldoCreditosBaixo,
    PacoteAVenda,
    PacoteForaDeVenda,
    TrialVencendo,
    TrialVencido,
    TurmaLotada,
    SemMatricula,
    ComAcesso,
    SemAcesso,
    Pagador,

    /// <summary>Frequência da janela de risco abaixo do mínimo (BL-M05-11): sinal, não bloqueio (R9).</summary>
    RiscoEvasao,

    /// <summary>A primeira mensalidade da matrícula, esperando a gestão confirmar (P17).</summary>
    MensalidadeAConfirmar,

    /// <summary>A prevista não vai para a Asaas: falta conta, pagador com CPF, ou a Asaas recusou.</summary>
    MensalidadeImpedida,

    /// <summary>Desligado com mensalidade em aberto: o débito que sobrou do desligamento (BL-M06-14).</summary>
    DebitoRemanescente,

    /// <summary>Mensalidade vencida há os dias do parâmetro, na escolha dos convocados (RF-CON-016, quando ligado).</summary>
    Inadimplente,

    /// <summary>A escolinha bloqueia a convocação de inadimplente (RF-CON-017, desligado por padrão: R9).</summary>
    ConvocacaoBloqueada,

    /// <summary>Nenhum responsável que recebe notificação tem celular: a convocação não chega pelo WhatsApp.</summary>
    SemCelular,

    /// <summary>Já convocado para outro jogo no mesmo horário (BL-M07-06): sinaliza, não bloqueia.</summary>
    OutroJogoNoHorario,

    /// <summary>Chegou outra resposta depois da primeira, ou depois do início do jogo (P03, P19): o professor decide.</summary>
    OutraResposta,

    /// <summary>A resposta mudou perto do jogo (P51): pede atenção de quem convocou.</summary>
    MudouPertoDoJogo
}

/// <summary>
/// Mapa central estado → selo (docs/design/GUIA-DESIGN-SYSTEM.md §4). A View nunca escolhe a cor de um status:
/// usa <c>&lt;selo-status valor="..."&gt;</c>, que passa por aqui. Cada fatia acrescenta as máquinas de estado dela.
/// </summary>
public static class ApresentacaoStatus
{
    public static (string Rotulo, VarianteSelo Variante) Selo(Enum valor) => valor switch
    {
        SituacaoVinculo.Ativo => ("Ativo", VarianteSelo.Sucesso),
        SituacaoVinculo.Revogado => ("Revogado", VarianteSelo.Neutro),

        AmbienteAsaas.Sandbox => ("Sandbox", VarianteSelo.Info),
        AmbienteAsaas.Producao => ("Produção", VarianteSelo.Sucesso),

        TipoMovimentoCredito.Concessao => ("Concessão", VarianteSelo.Sucesso),
        TipoMovimentoCredito.Consumo => ("Consumo", VarianteSelo.Neutro),
        TipoMovimentoCredito.Estorno => ("Estorno", VarianteSelo.Info),
        TipoMovimentoCredito.Ajuste => ("Ajuste", VarianteSelo.Alerta),

        PapelAtuacao.Gestor => ("Gestor", VarianteSelo.Primario),
        PapelAtuacao.Professor => ("Professor", VarianteSelo.Info),
        PapelAtuacao.Responsavel => ("Responsável", VarianteSelo.Neutro),

        SituacaoCompraCreditos.AguardandoPagamento => ("Aguardando pagamento", VarianteSelo.Info),
        SituacaoCompraCreditos.AguardandoConfirmacao => ("Boleto ou Pix emitido", VarianteSelo.Alerta),
        SituacaoCompraCreditos.Paga => ("Paga", VarianteSelo.Sucesso),
        SituacaoCompraCreditos.Expirada => ("Expirada", VarianteSelo.Neutro),
        SituacaoCompraCreditos.Falhou => ("Falhou", VarianteSelo.Perigo),

        SituacaoWebhook.Recebido => ("Recebido", VarianteSelo.Info),
        SituacaoWebhook.Processado => ("Processado", VarianteSelo.Sucesso),
        SituacaoWebhook.Ignorado => ("Ignorado", VarianteSelo.Neutro),
        SituacaoWebhook.Falhou => ("Falhou", VarianteSelo.Perigo),

        SinalTela.ContaRecebimentoVinculada => ("Conta vinculada", VarianteSelo.Sucesso),
        SinalTela.ContaRecebimentoAusente => ("Sem conta", VarianteSelo.Alerta),
        SinalTela.SaldoCreditosBaixo => ("Saldo baixo", VarianteSelo.Alerta),
        SinalTela.PacoteAVenda => ("À venda", VarianteSelo.Sucesso),
        SinalTela.PacoteForaDeVenda => ("Fora de venda", VarianteSelo.Neutro),
        SinalTela.TrialVencendo => ("Trial vencendo", VarianteSelo.Alerta),
        SinalTela.TrialVencido => ("Trial vencido", VarianteSelo.Alerta),
        SinalTela.TurmaLotada => ("Lotada", VarianteSelo.Alerta),
        SinalTela.SemMatricula => ("Sem turma", VarianteSelo.Alerta),
        SinalTela.ComAcesso => ("Com acesso", VarianteSelo.Sucesso),
        SinalTela.SemAcesso => ("Sem acesso ainda", VarianteSelo.Neutro),
        SinalTela.Pagador => ("Pagador", VarianteSelo.Primario),
        SinalTela.RiscoEvasao => ("Risco de evasão", VarianteSelo.Alerta),
        SinalTela.MensalidadeAConfirmar => ("A confirmar", VarianteSelo.Alerta),
        SinalTela.MensalidadeImpedida => ("Não emitida", VarianteSelo.Alerta),
        SinalTela.DebitoRemanescente => ("Débito após o desligamento", VarianteSelo.Alerta),
        SinalTela.Inadimplente => ("Mensalidade em atraso", VarianteSelo.Perigo),
        SinalTela.ConvocacaoBloqueada => ("Convocação bloqueada", VarianteSelo.Perigo),
        // Nenhum responsável que recebe notificação tem celular: a convocação não chega pelo WhatsApp.
        SinalTela.SemCelular => ("Sem WhatsApp", VarianteSelo.Alerta),
        SinalTela.OutroJogoNoHorario => ("Outro jogo no horário", VarianteSelo.Alerta),
        SinalTela.OutraResposta => ("Outra resposta", VarianteSelo.Alerta),
        SinalTela.MudouPertoDoJogo => ("Mudou perto do jogo", VarianteSelo.Alerta),

        SituacaoAluno.Trial => ("Trial", VarianteSelo.Info),
        SituacaoAluno.Ativo => ("Ativo", VarianteSelo.Sucesso),
        SituacaoAluno.Inativo => ("Inativo", VarianteSelo.Neutro),
        SituacaoAluno.Desistente => ("Desistente", VarianteSelo.Neutro),

        SituacaoMatricula.Ativa => ("Ativa", VarianteSelo.Sucesso),
        SituacaoMatricula.Encerrada => ("Encerrada", VarianteSelo.Neutro),

        SituacaoTurma.Ativa => ("Ativa", VarianteSelo.Sucesso),
        SituacaoTurma.Encerrada => ("Encerrada", VarianteSelo.Neutro),

        SituacaoSessao.Prevista => ("Prevista", VarianteSelo.Info),
        SituacaoSessao.Realizada => ("Realizada", VarianteSelo.Sucesso),
        SituacaoSessao.Cancelada => ("Cancelada", VarianteSelo.Perigo),
        SituacaoSessao.Remarcada => ("Remarcada", VarianteSelo.Alerta),

        EstadoConsentimento.Pendente => ("Pendente", VarianteSelo.Alerta),

        SituacaoPedidoTermos.Aguardando => ("Aguardando resposta", VarianteSelo.Info),
        SituacaoPedidoTermos.Respondido => ("Respondido", VarianteSelo.Sucesso),
        SituacaoPedidoTermos.Cancelado => ("Substituído", VarianteSelo.Neutro),
        SinalTela.LinkTermosVencido => ("Link vencido", VarianteSelo.Alerta),
        EstadoConsentimento.Concedido => ("Autorizado", VarianteSelo.Sucesso),
        EstadoConsentimento.Recusado => ("Recusado", VarianteSelo.Neutro),
        EstadoConsentimento.Revogado => ("Revogado", VarianteSelo.Neutro),

        SituacaoMensagem.Pendente => ("Na fila", VarianteSelo.Neutro),
        SituacaoMensagem.Retida => ("Retida", VarianteSelo.Alerta),
        SituacaoMensagem.Enviada => ("Enviada", VarianteSelo.Info),
        SituacaoMensagem.Entregue => ("Entregue", VarianteSelo.Sucesso),
        SituacaoMensagem.Lida => ("Lida", VarianteSelo.Sucesso),
        SituacaoMensagem.Falhou => ("Falhou", VarianteSelo.Perigo),
        SituacaoMensagem.Suprimida => ("Suprimida", VarianteSelo.Neutro),

        SituacaoConvite.Pendente => ("Enviado", VarianteSelo.Info),
        SituacaoConvite.Aceito => ("Aceito", VarianteSelo.Sucesso),
        SituacaoConvite.Cancelado => ("Cancelado", VarianteSelo.Neutro),
        SituacaoConvite.Substituido => ("Substituído", VarianteSelo.Neutro),

        SituacaoAcesso.SemAcesso => ("Sem acesso", VarianteSelo.Neutro),
        SituacaoAcesso.Convidado => ("Convidado", VarianteSelo.Info),
        SituacaoAcesso.ConviteExpirado => ("Convite vencido", VarianteSelo.Alerta),
        SituacaoAcesso.Ativo => ("Com acesso", VarianteSelo.Sucesso),

        VerificacaoVinculo.NaoSolicitada => ("Sem acesso", VarianteSelo.Neutro),
        VerificacaoVinculo.Pendente => ("A confirmar", VarianteSelo.Alerta),
        VerificacaoVinculo.Verificado => ("Confirmado", VarianteSelo.Sucesso),
        VerificacaoVinculo.Recusado => ("Recusado", VarianteSelo.Perigo),

        SituacaoAprovacaoTemplate.Rascunho => ("Rascunho", VarianteSelo.Neutro),
        SituacaoAprovacaoTemplate.EmAnalise => ("Em análise na Meta", VarianteSelo.Info),
        SituacaoAprovacaoTemplate.Aprovado => ("Aprovado", VarianteSelo.Sucesso),
        SituacaoAprovacaoTemplate.Rejeitado => ("Rejeitado", VarianteSelo.Perigo),

        SituacaoPresenca.Presente => ("Presente", VarianteSelo.Sucesso),
        SituacaoPresenca.Falta => ("Falta", VarianteSelo.Perigo),
        SituacaoPresenca.Justificada => ("Falta justificada", VarianteSelo.Alerta),

        SituacaoAlertaFaltas.Disparado => ("Disparado", VarianteSelo.Info),
        SituacaoAlertaFaltas.Suprimido => ("Suprimido", VarianteSelo.Neutro),
        SituacaoAlertaFaltas.Anulado => ("Anulado", VarianteSelo.Neutro),

        SituacaoCobranca.Prevista => ("Prevista", VarianteSelo.Neutro),
        SituacaoCobranca.Emitida => ("Emitida", VarianteSelo.Info),
        SituacaoCobranca.Paga => ("Paga", VarianteSelo.Sucesso),
        SituacaoCobranca.Vencida => ("Vencida", VarianteSelo.Perigo),
        SituacaoCobranca.PagaEmAtraso => ("Paga em atraso", VarianteSelo.Sucesso),
        SituacaoCobranca.Cancelada => ("Cancelada", VarianteSelo.Neutro),
        SituacaoCobranca.Isenta => ("Isenta", VarianteSelo.Neutro),

        SinalCobranca.Nenhum => ("Sem divergência", VarianteSelo.Neutro),
        SinalCobranca.ValorDivergente => ("Valor divergente", VarianteSelo.Alerta),
        SinalCobranca.Estorno => ("Estorno", VarianteSelo.Alerta),
        SinalCobranca.Chargeback => ("Contestação no cartão", VarianteSelo.Alerta),
        SinalCobranca.ExcluidaNaAsaas => ("Excluída na Asaas", VarianteSelo.Alerta),
        SinalCobranca.PagaDepoisDeEncerrada => ("Paga depois de encerrada", VarianteSelo.Alerta),
        SinalCobranca.ContaAnterior => ("Conta Asaas anterior", VarianteSelo.Alerta),

        SituacaoEvento.Agendado => ("Agendado", VarianteSelo.Info),
        SituacaoEvento.Confirmado => ("Confirmado", VarianteSelo.Info),
        SituacaoEvento.Realizado => ("Realizado", VarianteSelo.Sucesso),
        SituacaoEvento.ResultadoHomologado => ("Resultado registrado", VarianteSelo.Sucesso),
        SituacaoEvento.Cancelado => ("Cancelado", VarianteSelo.Perigo),
        SituacaoEvento.Adiado => ("Adiado", VarianteSelo.Alerta),

        SituacaoConvocacao.Selecionada => ("Não enviada", VarianteSelo.Neutro),
        SituacaoConvocacao.Enviada => ("Aguardando resposta", VarianteSelo.Info),
        SituacaoConvocacao.Confirmada => ("Vai", VarianteSelo.Sucesso),
        SituacaoConvocacao.Recusada => ("Não vai", VarianteSelo.Perigo),
        SituacaoConvocacao.SemResposta => ("Sem resposta", VarianteSelo.Alerta),
        SituacaoConvocacao.Cancelada => ("Cancelada", VarianteSelo.Neutro),

        SituacaoImportacao.Aberta => ("Em conferência", VarianteSelo.Info),
        SituacaoImportacao.Confirmada => ("Importada", VarianteSelo.Sucesso),
        SituacaoImportacao.Descartada => ("Descartada", VarianteSelo.Neutro),

        ClasseLinha.Apta => ("Apta", VarianteSelo.Sucesso),
        ClasseLinha.ComAviso => ("Com aviso", VarianteSelo.Alerta),
        ClasseLinha.Bloqueada => ("Bloqueada", VarianteSelo.Perigo),

        _ => (valor.ToString(), VarianteSelo.Neutro)
    };

    public static string ClasseVariante(VarianteSelo variante) => variante switch
    {
        VarianteSelo.Sucesso => "kt-badge-success",
        VarianteSelo.Info => "kt-badge-info",
        VarianteSelo.Alerta => "kt-badge-warning",
        VarianteSelo.Perigo => "kt-badge-destructive",
        VarianteSelo.Primario => "kt-badge-primary",
        _ => "kt-badge-secondary"
    };
}

public static class Formatacao
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    /// <summary>"agora", "há 5 min", "há 3 h", "ontem", "há 4 dias" ou a data — ambos em horário de Brasília.</summary>
    public static string TempoRelativo(DateTime quando, DateTime agora)
    {
        var diferenca = agora - quando;
        if (diferenca < TimeSpan.FromMinutes(1)) return "agora";
        if (diferenca < TimeSpan.FromHours(1)) return $"há {(int)diferenca.TotalMinutes} min";
        if (diferenca < TimeSpan.FromHours(24) && quando.Date == agora.Date) return $"há {(int)diferenca.TotalHours} h";
        if (quando.Date == agora.Date.AddDays(-1)) return "ontem";
        if (diferenca < TimeSpan.FromDays(7)) return $"há {Math.Max(2, (int)diferenca.TotalDays)} dias";
        return quando.ToString("dd/MM/yyyy", PtBr);
    }

    /// <summary>"+5543987654321" → "(43) 98765-4321". Outro formato volta como veio.</summary>
    public static string Telefone(string? e164)
    {
        if (string.IsNullOrEmpty(e164)) return string.Empty;
        var n = e164.StartsWith("+55", StringComparison.Ordinal) ? e164[3..] : e164;
        return n.Length switch
        {
            11 => $"({n[..2]}) {n[2..7]}-{n[7..]}",
            10 => $"({n[..2]}) {n[2..6]}-{n[6..]}",
            _ => e164
        };
    }

    public static string Cnpj(string? digitos) => Domain.Comum.Cnpj.Formatar(digitos);

    /// <summary>
    /// Valor de parâmetro (texto invariável gravado) como o gestor lê: "2,5 %", "ligado", "21:00 às 07:00",
    /// "1, 5, 10 dias" ou o rótulo da opção. Usado no editor e no histórico.
    /// </summary>
    public static string Parametro(DefinicaoParametro definicao, string? valor, string seVazio = "—")
    {
        if (valor is null) return seVazio;
        switch (definicao.Tipo)
        {
            case TipoParametro.Booleano:
                return valor == "true" ? "ligado" : "desligado";
            case TipoParametro.Escolha:
                return definicao.Opcoes?.FirstOrDefault(o => o.Valor == valor)?.Rotulo ?? valor;
            case TipoParametro.Janela when JanelaHorario.TentarLer(valor, out var janela):
                return janela.ParaExibicao();
            case TipoParametro.ListaDias:
                valor = valor.Replace(",", ", ");
                break;
            default:
                valor = valor.Replace('.', ',');
                break;
        }
        return definicao.Unidade is null ? valor : $"{valor} {definicao.Unidade}";
    }

    public static string Cep(string? digitos) => digitos is { Length: 8 } ? $"{digitos[..5]}-{digitos[5..]}" : digitos ?? string.Empty;

    public static string DataHora(DateTime? quando) => quando?.ToString("dd/MM/yyyy HH:mm", PtBr) ?? "—";

    public static string Data(DateTime? quando) => quando?.ToString("dd/MM/yyyy", PtBr) ?? "—";

    public static string Data(DateOnly? dia) => dia?.ToString("dd/MM/yyyy", PtBr) ?? "—";

    /// <summary>"29/09 (ter)".</summary>
    public static string DiaCurto(DateOnly dia) => dia.ToString("dd/MM (ddd)", PtBr);

    /// <summary>"Terça-feira, 29 de setembro" (só a primeira letra maiúscula).</summary>
    public static string DiaPorExtenso(DateOnly dia) => PrimeiraMaiuscula(dia.ToString("dddd, d 'de' MMMM", PtBr));

    /// <summary>"18:00–19:00".</summary>
    public static string Horario(DateTime inicio, DateTime fim) => $"{inicio.ToString("HH:mm", PtBr)}–{fim.ToString("HH:mm", PtBr)}";

    public static string Horario(TimeOnly inicio, TimeOnly fim) => $"{inicio.ToString("HH:mm", PtBr)}–{fim.ToString("HH:mm", PtBr)}";

    /// <summary>"Setembro de 2026".</summary>
    public static string Mes(DateOnly dia) => PrimeiraMaiuscula(dia.ToString("MMMM 'de' yyyy", PtBr));

    private static string PrimeiraMaiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], PtBr) + texto[1..];

    public static string Cpf(string? digitos) => Domain.Comum.Cpf.Formatar(digitos);

    /// <summary>"9 anos".</summary>
    public static string Idade(int anos) => anos == 1 ? "1 ano" : $"{anos} anos";

    public static string Numero(int valor) => valor.ToString("N0", PtBr);

    /// <summary>"62,5 %" e "60 %": até uma casa decimal, com espaço antes do símbolo.</summary>
    public static string Percentual(decimal valor) => $"{valor.ToString("0.#", PtBr)} %";

    /// <summary>"R$ 1.234,50", com espaço sem quebra depois do símbolo: o valor não se parte no fim da linha.</summary>
    public static string Moeda(decimal valor) => valor.ToString("C", PtBr).Replace("R$ ", "R$ ");
}
