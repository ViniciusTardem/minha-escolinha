using MinhaEscolinha.Domain.Agenda;
using MinhaEscolinha.Domain.Comum;
using MinhaEscolinha.Domain.Configuracao.Parametros;
using MinhaEscolinha.Domain.Equipe;
using MinhaEscolinha.Domain.Eventos;
using MinhaEscolinha.Domain.Mensageria;

namespace MinhaEscolinha.Testes.Eventos;

/// <summary>
/// S5 sem banco: as máquinas do evento e da convocação (spec §6), as respostas (P03, P19), os alertas de agenda (BL-M07-03,
/// BL-M07-06), a janela de disparo (RF-CON-003), o link de mapa (CA-11) e a permissão das telas (BL-M07-18).
/// </summary>
public class EventosUnidadeTestes
{
    private static readonly DateTime Agora = new(2026, 10, 2, 10, 0, 0); // sexta
    private static readonly DateTime Sabado9h = new(2026, 10, 3, 9, 0, 0);

    private static Evento Jogo(DateTime? inicio = null, string? adversario = null) =>
        Evento.Novo(TipoEvento.Jogo, Guid.NewGuid(), [Guid.NewGuid()], inicio ?? Sabado9h, 60, Guid.NewGuid(), Guid.NewGuid(), adversario,
            adversarioPermitido: true, "Chegar 30 min antes");

    private static Convocacao Enviada()
    {
        var c = Convocacao.Selecionar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Agora, "Professor");
        c.Disparar(Agora);
        return c;
    }

    // ── Evento (R8, §6) ──

    [Fact]
    public void O_jogo_nao_pressupoe_adversario_e_o_adversario_so_existe_se_a_modalidade_tem()
    {
        var semAdversario = Jogo();
        Assert.Null(semAdversario.Adversario);
        Assert.Equal("Jogo da Sub-11", semAdversario.Titulo("Sub-11"));
        Assert.Equal("Jogo da Sub-11 contra Tigre", Jogo(adversario: " Tigre ").Titulo("Sub-11"));

        Assert.Throws<InvalidOperationException>(() => Evento.Novo(TipoEvento.Jogo, Guid.NewGuid(), [Guid.NewGuid()], Sabado9h, 60,
            Guid.NewGuid(), Guid.NewGuid(), "Tigre", adversarioPermitido: false, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => Evento.Novo(TipoEvento.Jogo, Guid.NewGuid(), [Guid.NewGuid()], Sabado9h, 5,
            Guid.NewGuid(), Guid.NewGuid(), null, true, null));
        Assert.Throws<ArgumentException>(() => Evento.Novo(TipoEvento.Jogo, Guid.NewGuid(), [], Sabado9h, 60, Guid.NewGuid(), Guid.NewGuid(),
            null, true, null));
        Assert.Equal(Sabado9h.AddMinutes(60), semAdversario.Fim);
    }

    [Fact]
    public void A_maquina_do_evento_so_muda_o_aberto_e_realizar_so_depois_de_comecar()
    {
        var jogo = Jogo();
        jogo.Confirmar();
        Assert.Equal(SituacaoEvento.Confirmado, jogo.Situacao);
        Assert.Throws<InvalidOperationException>(jogo.Confirmar);
        Assert.Throws<InvalidOperationException>(() => jogo.Realizar(Agora)); // ainda não começou
        Assert.Throws<ArgumentException>(() => jogo.Encerrar(adiar: false, " ", Agora, "Gestão"));

        jogo.Encerrar(adiar: true, "Chuva forte", Agora, "Gestão");
        Assert.Equal(SituacaoEvento.Adiado, jogo.Situacao);
        Assert.True(jogo.Encerrado);
        Assert.Throws<InvalidOperationException>(() => jogo.Encerrar(false, "de novo", Agora, "Gestão"));
        Assert.Throws<InvalidOperationException>(() => jogo.Alterar(Sabado9h.AddDays(7), 60, Guid.NewGuid(), Guid.NewGuid(), null, true, null));

        var jogado = Jogo(Agora.AddHours(-2));
        jogado.Realizar(Agora);
        Assert.Equal(SituacaoEvento.Realizado, jogado.Situacao);
    }

    [Fact]
    public void A_alteracao_so_avisa_as_familias_quando_muda_o_quando_ou_o_onde()
    {
        var jogo = Jogo();
        var local = jogo.Local_ID;
        var professor = jogo.Professor_ID;

        var logistica = jogo.Alterar(Sabado9h, 60, local, professor, null, true, "Uniforme azul");
        Assert.True(logistica.Mudou);
        Assert.False(logistica.AvisaFamilias);

        Assert.True(jogo.Alterar(Sabado9h.AddHours(1), 60, local, professor, null, true, "Uniforme azul").AvisaFamilias);
        Assert.True(jogo.Alterar(Sabado9h.AddHours(1), 60, Guid.NewGuid(), professor, null, true, "Uniforme azul").MudouLocal);
        Assert.False(jogo.Alterar(Sabado9h.AddHours(1), 60, jogo.Local_ID, professor, null, true, "Uniforme azul").Mudou);
    }

    // ── Convocação (§6, P03, P19) ──

    /// <summary>D87: quem respondeu muda a própria resposta, por qualquer canal; a de outra pessoa, ou depois do registro por fora, não.</summary>
    [Fact]
    public void Quem_respondeu_muda_a_propria_resposta_e_a_de_outra_pessoa_fica_para_quem_convocou()
    {
        var maria = Guid.NewGuid();
        var c = Enviada();
        Assert.Equal(ResultadoResposta.Aplicada, c.Responder(true, OrigemResposta.WhatsApp, "Maria", maria, Agora, Agora));
        Assert.Equal(ResultadoResposta.Repetida, c.Responder(true, OrigemResposta.Portal, "Maria", maria, Agora.AddMinutes(1), Agora));
        Assert.Equal(ResultadoResposta.Aplicada, c.Responder(false, OrigemResposta.Portal, "Maria", maria, Agora.AddMinutes(2), Agora));
        Assert.Equal((SituacaoConvocacao.Recusada, OrigemResposta.Portal), (c.Situacao, c.Origem!.Value));
        Assert.Contains(c.Historico, h => h.Detalhe?.EndsWith("mudou de \"Vai\"") == true);

        Assert.Equal(ResultadoResposta.Posterior, c.Responder(true, OrigemResposta.WhatsApp, "João", Guid.NewGuid(), Agora.AddMinutes(3), Agora));
        Assert.Equal(SituacaoConvocacao.Recusada, c.Situacao);
        Assert.StartsWith("Vai · João", c.OutraResposta);

        // Depois do registro por fora, a decisão é de quem convocou: a família muda só a "outra resposta".
        c.RegistrarPorFora(true, "Carlos Treinador", null, Agora.AddMinutes(4));
        Assert.Equal(ResultadoResposta.Posterior, c.Responder(false, OrigemResposta.Portal, "Maria", maria, Agora.AddMinutes(5), Agora));
        Assert.Equal(SituacaoConvocacao.Confirmada, c.Situacao);
        Assert.StartsWith("Não vai · Maria", c.OutraResposta);
    }

    [Fact]
    public void Vale_a_primeira_resposta_a_diferente_fica_visivel_e_o_professor_sobrescreve()
    {
        var c = Enviada();
        Assert.True(c.Pendente);

        Assert.Equal(ResultadoResposta.Aplicada, c.Responder(true, OrigemResposta.WhatsApp, "Maria", Guid.NewGuid(), Agora, Agora));
        Assert.Equal((SituacaoConvocacao.Confirmada, OrigemResposta.WhatsApp, "Maria"), (c.Situacao, c.Origem!.Value, c.RespondidoPorNome));

        // O outro responsável responde depois: vale a primeira, e a diferente fica para o professor decidir.
        Assert.Equal(ResultadoResposta.Posterior, c.Responder(false, OrigemResposta.WhatsApp, "João", Guid.NewGuid(), Agora.AddMinutes(5), Agora));
        Assert.Equal(SituacaoConvocacao.Confirmada, c.Situacao);
        Assert.StartsWith("Não vai · João", c.OutraResposta);

        // A mesma resposta de novo não vira "outra resposta".
        var igual = Enviada();
        igual.Responder(true, OrigemResposta.WhatsApp, "Maria", null, Agora, Agora);
        igual.Responder(true, OrigemResposta.WhatsApp, "João", null, Agora, Agora);
        Assert.Null(igual.OutraResposta);

        c.RegistrarPorFora(false, "Carlos Treinador", Guid.NewGuid(), Agora.AddMinutes(10));
        Assert.Equal((SituacaoConvocacao.Recusada, OrigemResposta.RegistroManual), (c.Situacao, c.Origem!.Value));
        Assert.Null(c.OutraResposta);
        Assert.Contains(c.Historico, h => h.Tipo == TipoHistoricoConvocacao.RespostaPosterior);
        Assert.Contains(c.Historico, h => h.Tipo == TipoHistoricoConvocacao.RegistroManual);
    }

    [Fact]
    public void Sem_resposta_no_inicio_do_jogo_e_a_resposta_tardia_so_fica_no_historico()
    {
        var c = Enviada();
        Assert.True(c.MarcarSemResposta(Sabado9h));
        Assert.False(c.MarcarSemResposta(Sabado9h)); // uma vez só

        Assert.Equal(ResultadoResposta.Tardia, c.Responder(true, OrigemResposta.WhatsApp, "Maria", null, Sabado9h.AddMinutes(20), Sabado9h));
        Assert.Equal(SituacaoConvocacao.SemResposta, c.Situacao);
        Assert.Contains("depois do início", c.OutraResposta);

        // O professor ainda registra o que aconteceu (§6: sobrescreve qualquer estado).
        c.RegistrarPorFora(true, "Carlos", null, Sabado9h.AddMinutes(30));
        Assert.Equal(SituacaoConvocacao.Confirmada, c.Situacao);
    }

    [Fact]
    public void So_a_convocacao_disparada_e_cancelada_e_a_selecionada_nao_recebe_resposta()
    {
        var selecionada = Convocacao.Selecionar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Agora, "Professor");
        Assert.False(selecionada.Disparada);
        Assert.False(selecionada.Cancelar("jogo cancelado", Agora)); // quem chama tira a escolhida
        Assert.False(selecionada.PodeRegistrarPorFora);
        Assert.Throws<InvalidOperationException>(() => selecionada.RegistrarPorFora(true, "Carlos", null, Agora));

        var enviada = Enviada();
        Assert.True(enviada.Cancelar("Jogo cancelado: chuva", Agora));
        Assert.Equal(SituacaoConvocacao.Cancelada, enviada.Situacao);
        Assert.Equal(ResultadoResposta.Ignorada, enviada.Responder(true, OrigemResposta.WhatsApp, "Maria", null, Agora, Agora));
        Assert.Equal(SituacaoConvocacao.Cancelada, enviada.Situacao);
        Assert.Throws<InvalidOperationException>(() => enviada.Disparar(Agora));
    }

    [Fact]
    public void A_contagem_do_painel_separa_o_que_ainda_nao_saiu()
    {
        var contagem = ContagemConvocacoes.De([SituacaoConvocacao.Selecionada, SituacaoConvocacao.Enviada, SituacaoConvocacao.Confirmada,
            SituacaoConvocacao.Confirmada, SituacaoConvocacao.Recusada, SituacaoConvocacao.SemResposta, SituacaoConvocacao.Cancelada]);
        Assert.Equal((1, 1, 2, 1, 1, 1), (contagem.Selecionadas, contagem.Aguardando, contagem.Confirmadas, contagem.Recusadas,
            contagem.SemResposta, contagem.Canceladas));
        Assert.Equal(5, contagem.Disparadas);
    }

    // ── Alertas (BL-M07-03, BL-M07-06) ──

    [Fact]
    public void Aluno_em_dois_jogos_no_mesmo_horario_e_sinalizado()
    {
        var este = Guid.NewGuid();
        var outros = new[]
        {
            new ConvocacaoDoAluno(Guid.Empty, Guid.NewGuid(), "Jogo contra Tigre", Sabado9h.AddMinutes(30), Sabado9h.AddMinutes(90)),
            new ConvocacaoDoAluno(Guid.Empty, Guid.NewGuid(), "Jogo", Sabado9h.AddHours(1), Sabado9h.AddHours(2)), // encosta: não conflita
            new ConvocacaoDoAluno(Guid.Empty, este, "Este mesmo", Sabado9h, Sabado9h.AddHours(1))
        };
        var conflitos = RegrasConvocacao.Conflitos(este, Sabado9h, Sabado9h.AddHours(1), outros);
        Assert.Equal("Jogo contra Tigre", Assert.Single(conflitos).Evento);
    }

    [Fact]
    public void O_jogo_alerta_professor_ocupado_fora_da_disponibilidade_indisponivel_e_local_ocupado()
    {
        var professor = new Professor { Professor_ID = Guid.NewGuid() };
        professor.DefinirNome("Carlos Treinador");
        professor.Disponibilidade.Add(new FaixaDisponibilidade { DiaSemana = DayOfWeek.Saturday, Inicio = new TimeOnly(13, 0), Fim = new TimeOnly(18, 0) });
        professor.Indisponibilidades.Add(new IndisponibilidadeProfessor { Inicio = Sabado9h.AddHours(-1), Fim = Sabado9h.AddHours(3), Motivo = "Viagem" });
        var treino = new Compromisso(TipoCompromisso.Treino, Guid.NewGuid(), "Sub-9 · sábado", Sabado9h.AddMinutes(30), Sabado9h.AddMinutes(90),
            Guid.NewGuid(), professor.Professor_ID, Guid.NewGuid(), false);
        var cancelado = treino with { Id = Guid.NewGuid(), Cancelado = true };

        var alertas = DetectorConflitos.ParaCompromisso(professor, Sabado9h, Sabado9h.AddHours(1), [treino, cancelado]);
        Assert.Equal([TipoConflito.ProfessorOcupado, TipoConflito.ForaDaDisponibilidade, TipoConflito.ProfessorIndisponivel],
            alertas.Select(a => a.Tipo));
        Assert.Contains("o treino da Sub-9 · sábado", alertas[0].Descricao);

        var local = DetectorConflitos.LocalOcupado("Ginásio Moringão", Sabado9h, Sabado9h.AddHours(1), [treino]).ToList();
        Assert.Equal(TipoConflito.LocalOcupado, Assert.Single(local).Tipo);
        Assert.Contains("Ginásio Moringão já recebe o treino da Sub-9", local[0].Descricao);
    }

    // ── Mensagem (RF-CON-003, CA-11) ──

    [Fact]
    public void Fora_da_janela_de_disparo_a_convocacao_sai_no_proximo_inicio()
    {
        var janela = new JanelaHorario(new TimeOnly(8, 0), new TimeOnly(20, 0));
        Assert.Null(PoliticaEnvio.ProximoInicioDaJanela(janela, new DateTime(2026, 10, 2, 10, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 3, 8, 0, 0), PoliticaEnvio.ProximoInicioDaJanela(janela, new DateTime(2026, 10, 2, 21, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 2, 8, 0, 0), PoliticaEnvio.ProximoInicioDaJanela(janela, new DateTime(2026, 10, 2, 6, 30, 0)));
        Assert.Null(PoliticaEnvio.ProximoInicioDaJanela(new JanelaHorario(new TimeOnly(8, 0), new TimeOnly(8, 0)), new DateTime(2026, 10, 2, 23, 0, 0)));
    }

    [Fact]
    public void O_link_de_mapa_busca_pelo_endereco_ou_pelo_nome_do_lugar()
    {
        var endereco = new Endereco { Logradouro = "Rua Tietê", Numero = "1000", Bairro = "Centro", Cidade = "Londrina", Uf = "PR" };
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=Rua%20Tiet%C3%AA%2C%201000%2C%20Centro%2C%20Londrina%2FPR",
            endereco.LinkMapa("Ginásio"));
        Assert.Equal("https://www.google.com/maps/search/?api=1&query=Gin%C3%A1sio%20Moring%C3%A3o", new Endereco().LinkMapa("Ginásio Moringão"));
        Assert.Null(new Endereco().LinkMapa(null));
    }

    // ── Acesso (BL-M07-18, §7) ──

    [Fact]
    public void Toda_tela_da_convocacao_exige_a_permissao_e_o_responsavel_nao_convoca()
    {
        var controller = typeof(MinhaEscolinha.Web.Controllers.ConvocacaoController);
        var daClasse = controller.GetCustomAttributes(typeof(MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute), false)
            .Cast<MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute>();
        Assert.Contains(daClasse, p => p.Recurso == Domain.Acesso.Recurso.EventosConvocacao);

        var gravam = controller.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), false).Length > 0)
            .ToList();
        Assert.NotEmpty(gravam);
        Assert.All(gravam, m => Assert.Contains(m.GetCustomAttributes(typeof(MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute), false)
                .Cast<MinhaEscolinha.Web.Infraestrutura.PermissaoAttribute>(),
            p => p.Operacao == Domain.Acesso.Operacao.Alterar
                 && p.Recurso is Domain.Acesso.Recurso.EventosConvocacao or Domain.Acesso.Recurso.ResponderConvocacao));

        Assert.Equal(Domain.Acesso.NivelAcesso.Nenhum,
            Domain.Acesso.MatrizPermissoes.Nivel(Domain.Acesso.Recurso.EventosConvocacao, Domain.Acesso.PapelAtuacao.Responsavel));
        Assert.Equal(Domain.Acesso.NivelAcesso.Parcial,
            Domain.Acesso.MatrizPermissoes.Nivel(Domain.Acesso.Recurso.EventosConvocacao, Domain.Acesso.PapelAtuacao.Professor));
    }

    /// <summary>O tempo real só avisa quem vê os jogos; o responsável acompanha pelo portal (e os papéis acumulam).</summary>
    [Fact]
    public void So_a_gestao_e_o_professor_entram_no_tempo_real_dos_paineis()
    {
        Assert.True(MinhaEscolinha.Web.Hubs.ConvocacoesHub.Acompanha([Domain.Acesso.PapelAtuacao.Gestor]));
        Assert.True(MinhaEscolinha.Web.Hubs.ConvocacoesHub.Acompanha([Domain.Acesso.PapelAtuacao.Professor]));
        Assert.True(MinhaEscolinha.Web.Hubs.ConvocacoesHub.Acompanha([Domain.Acesso.PapelAtuacao.Responsavel, Domain.Acesso.PapelAtuacao.Professor]));
        Assert.False(MinhaEscolinha.Web.Hubs.ConvocacoesHub.Acompanha([Domain.Acesso.PapelAtuacao.Responsavel]));
        Assert.False(MinhaEscolinha.Web.Hubs.ConvocacoesHub.Acompanha([]));
    }
}
