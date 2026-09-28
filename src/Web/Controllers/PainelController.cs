using Microsoft.AspNetCore.Mvc;
using MinhaEscolinha.Application.Acesso;
using MinhaEscolinha.Application.Alunos;
using MinhaEscolinha.Application.Comum;
using MinhaEscolinha.Application.Configuracao;
using MinhaEscolinha.Application.Equipe;
using MinhaEscolinha.Application.Mensageria;
using MinhaEscolinha.Application.Presencas;
using MinhaEscolinha.Application.Turmas;
using MinhaEscolinha.Domain.Acesso;
using MinhaEscolinha.Web.Infraestrutura;
using MinhaEscolinha.Web.Models;

namespace MinhaEscolinha.Web.Controllers;

/// <summary>
/// Painel inicial: a situação da configuração e do cadastro da escolinha, os próximos treinos, os trials vencendo ou
/// vencidos (sinal na tela, sem mensagem: P12) e o roteiro das próximas entregas do piloto.
/// </summary>
[SomenteAutenticado]
public class PainelController(
    IAutorizacao autorizacao,
    ServicoEscolinha escolinhas,
    ServicoModalidades modalidades,
    ServicoParametros parametros,
    ServicoContaAsaas contaAsaas,
    ServicoCreditos creditos,
    ServicoLocais locais,
    ServicoProfessores professores,
    ServicoTurmas turmas,
    ServicoAlunos alunos,
    ServicoAgenda agenda,
    ServicoConvites convites,
    ServicoVerificacaoVinculos vinculos,
    ServicoMensagens mensagens,
    ServicoPresenca presenca,
    Application.Financeiro.ServicoMensalidades mensalidades,
    IParametros valores,
    IRelogioBrasil relogio) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (autorizacao.EscolinhaId is null)
            return autorizacao.AdministradorPlataforma
                ? RedirectToAction("Index", "Plataforma")
                : RedirectToAction("EscolherEscolinha", "Conta");

        // Quem é só responsável usa o portal (P23, decidido no S2): o painel é de quem gere ou dá aula.
        if (autorizacao.Papeis.Count > 0 && autorizacao.Papeis.All(p => p == PapelAtuacao.Responsavel))
            return RedirectToAction("Index", "Portal");

        var dados = await escolinhas.ObterAsync(ct);
        var veConfiguracao = autorizacao.Pode(Recurso.ConfiguracaoEscolinha, Operacao.Ler);

        var passos = new List<PassoConfiguracao>();
        if (veConfiguracao)
        {
            var listaModalidades = await modalidades.ListarAsync(ct);
            var listaParametros = await parametros.ListarAsync(ct);
            var saldo = (await creditos.ObterAsync(1, ct))?.Saldo ?? 0;
            passos.Add(new("Dados da escolinha", "Nome, CNPJ, endereço e contato que aparecem para as famílias.", "ki-home-3",
                !string.IsNullOrWhiteSpace(dados?.Endereco.Cidade) && dados?.Telefone is not null, "/Escolinha", "Revisar dados"));
            passos.Add(new("Modalidades", $"{listaModalidades.Count} modalidade(s) com perfil e recursos definidos.", "ki-medal-star",
                listaModalidades.Count > 0, "/Modalidades", "Ver modalidades"));
            passos.Add(new("Parâmetros de regra", $"{listaParametros.Count(p => !p.EhPadrao)} de {listaParametros.Count} ajustados para a escolinha (os demais usam o padrão).",
                "ki-setting-3", listaParametros.Any(p => p.UltimaAlteracao is not null), "/Parametros", "Revisar parâmetros"));
            if (autorizacao.Pode(Recurso.FinanceiroEscolinha, Operacao.Ler))
            {
                var conta = await contaAsaas.ObterAsync(ct);
                passos.Add(new("Conta de recebimento", conta.Vinculada ? $"Conta Asaas vinculada (final {conta.FinalChave})." : "Sem conta Asaas: nenhuma cobrança pode ser emitida.",
                    "ki-bank", conta.Vinculada, "/ContaRecebimento", conta.Vinculada ? "Ver conta" : "Vincular conta"));
            }
            passos.Add(new("Créditos de mensagem", $"Saldo atual: {Formatacao.Numero(saldo)} crédito(s).", "ki-wallet", saldo > 0, "/Creditos", "Ver créditos"));

            // Cadastro (S1): local e professor antes da turma; turma antes do aluno.
            var quantosLocais = (await locais.ListarAsync(ct)).Count;
            var quantosProfessores = (await professores.ListarAsync(ct)).Count;
            var quantasTurmas = (await turmas.ListarAsync(false, null, ct)).Count;
            var quantosAlunos = (await alunos.ListarAsync(new FiltroAlunos(), 1, ct)).Total;
            passos.Add(new("Locais de treino", $"{quantosLocais} local(is) cadastrado(s).", "ki-geolocation", quantosLocais > 0, "/Locais",
                quantosLocais > 0 ? "Ver locais" : "Cadastrar local"));
            passos.Add(new("Professores", $"{quantosProfessores} professor(es), com a disponibilidade de cada um.", "ki-teacher", quantosProfessores > 0,
                "/Professores", quantosProfessores > 0 ? "Ver professores" : "Cadastrar professor"));
            passos.Add(new("Turmas", $"{quantasTurmas} turma(s) ativa(s): as sessões de treino saem dos horários.", "ki-category", quantasTurmas > 0,
                "/Turmas", quantasTurmas > 0 ? "Ver turmas" : "Criar turma"));
            passos.Add(new("Alunos", $"{quantosAlunos} aluno(s) na escolinha, com responsáveis e matrícula.", "ki-people", quantosAlunos > 0,
                "/Alunos", quantosAlunos > 0 ? "Ver alunos" : "Cadastrar aluno"));

            // Acesso das famílias e da equipe (S2): quem não ativou continua recebendo tudo pelo WhatsApp.
            var candidatos = await convites.CandidatosAsync(ct);
            var comAcesso = candidatos.Count(c => c.Situacao == SituacaoAcesso.Ativo);
            passos.Add(new("Convites de acesso", $"{comAcesso} de {candidatos.Count} responsáveis e professores com acesso; os demais recebem pelo WhatsApp.",
                "ki-key", comAcesso > 0, "/Convites", comAcesso > 0 ? "Ver convites" : "Convidar"));
        }

        var pendencias = new List<PendenciaPainel>();
        if (await vinculos.ContarPendentesAsync(ct) is > 0 and var aConfirmar)
            pendencias.Add(new("Vínculos a confirmar", $"{aConfirmar} vínculo(s) de quem ativou o acesso: o portal só mostra o aluno depois da sua confirmação.",
                "ki-user-tick", "/Vinculos", "Confirmar"));
        if (await mensagens.ResumoAsync(1, ct) is { } resumo)
        {
            if (resumo.Retidas > 0)
                pendencias.Add(new("Mensagens retidas", $"{resumo.Retidas} mensagem(ns) essencial(is) esperando crédito: saem sozinhas quando houver saldo.",
                    "ki-message-text-2", "/Creditos", "Comprar créditos"));
            else if (resumo.SaldoBaixo)
                pendencias.Add(new("Saldo de créditos baixo", $"Restam {Formatacao.Numero(resumo.Saldo)} crédito(s) de mensagem.", "ki-wallet", "/Creditos", "Ver créditos"));
        }

        var hoje = DateOnly.FromDateTime(relogio.Hoje());
        var proximosTreinos = autorizacao.Pode(Recurso.RegistroPresenca, Operacao.Ler)
            ? (await agenda.ConsultarAsync(new FiltroAgenda { De = hoje, Ate = hoje.AddDays(6) }, ct)).Itens.Take(8).ToList()
            : [];
        var diasAviso = (await valores.ObterAsync(ct)).Matricula.TrialDiasAvisoAntes;
        var (vencendo, vencidos) = autorizacao.Pode(Recurso.CadastroAlunosResponsaveis, Operacao.Ler)
            ? await alunos.TrialsAsync(diasAviso, ct)
            : ([], []);

        // Risco de evasão (S3): o gestor vê quem está com a frequência baixa sem pedir a ninguém; o professor, nas turmas dele.
        var riscos = await presenca.AlunosEmRiscoAsync(ct);

        var proximas = CatalogoModulos.Todos
            .Where(m => m.Situacao == SituacaoModulo.Planejado && (m.Recurso is not { } r || autorizacao.Pode(r, Operacao.Ler)))
            .ToList();

        return View(new PainelViewModel(dados?.Nome ?? "Escolinha", RotulosAcesso.Papeis(autorizacao.Papeis), veConfiguracao, passos, proximas,
            proximosTreinos, vencendo, vencidos, hoje, pendencias, riscos, autorizacao.Pode(Recurso.RegistroPresenca, Operacao.Alterar),
            await mensalidades.ResumoAsync(ct)));
    }
}
