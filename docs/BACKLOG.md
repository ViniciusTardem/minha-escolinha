# Backlog e registro de progresso — Minha Escolinha (piloto da Fase 1)

Registro de progresso exigido pela seção 12.4 da spec. Os 81 entregáveis e os IDs `BL-Mxx-nn` são os da seção 10 de [`especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md`](especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md). Use esses IDs nas mensagens de commit.

**Status:**

| Status | Significado |
|---|---|
| **Concluído** | Comportamento, dado, tela e teste automatizado prontos; critério de pronto da fatia demonstrado |
| **Parcial** | A parte da fatia está pronta e o restante depende de outra fatia (dito na coluna) |
| **A fazer** | Ainda não iniciado |

Decisões, desvios e pendências ficam em [`DECISOES-EM-ABERTO.md`](DECISOES-EM-ABERTO.md).

## Resumo (27/09/2026, fim do S2)

| Fatia | Itens | Concluídos | Parciais | Situação |
|---|---:|---:|---:|---|
| F0 — Esqueleto vivo | — | — | — | **Pronto** (sem itens de backlog) |
| S0 — Fundação | 12 | 12 | 0 | **Pronto** e commitado (a444b48). O `BL-M02-12` fechou no S1, com o valor por turma. |
| S1 — Cadastro | 20 | 19 | 1 | **Pronto** e commitado (b6a4c3f). O `BL-M04-06` fechou no S2, com o aviso às famílias. |
| S2 — Mensageria e acesso | 14 | 14 | 0 | **Pronto no simulador** (27/09/2026). A prova com um número real espera a conta na Meta (P34). Commit aguardando autorização. |
| S3 — Presença e evasão | 6 | 0 | 0 | — |
| S4 — Financeiro | 8 | 0 | 0 | — |
| S5 — Eventos e convocações | 10 | 0 | 0 | — |
| S6 — Portal do responsável | 6 | 0 | 0 | — |
| S7 — Carga inicial | 5 | 0 | 0 | — |
| **Total** | **81** | **45** | **1** | |

O parcial do S1 depende do S4:
- `BL-M03-08`: o desligamento exige data e motivo, encerra as matrículas, libera a vaga e preserva o histórico. Encerrar a recorrência de cobrança é do S4 (`BL-M06-14`), que nasce da matrícula já encerrada.

---

## S0 — Fundação: conta, identidade e permissões · 12 itens · **Pronto em 26/09/2026**

**Critério de pronto da spec:** é possível criar uma escolinha, definir seus parâmetros, cadastrar a modalidade futsal com o perfil coletiva, e um gestor autenticado enxerga apenas a própria escolinha.

**Demonstrado no navegador em 26/09/2026, com duas escolinhas (A e B):**
- a equipe da plataforma criou A e B com o 1º gestor de cada uma;
- o gestor de A trocou a senha no 1º acesso e ajustou parâmetros; o histórico registra antes → depois;
- o futsal foi criado no perfil coletiva;
- o gestor de A abrindo a modalidade de B pela URL recebeu **404**;
- o gestor de A em `/Plataforma` e `/jobs` recebeu **403**;
- um POST forjado para "entrar como equipe" em B recebeu **403**.

| ID | Entregável | Status | Onde está | Testes |
|---|---|---|---|---|
| `BL-M02-01` | Conta da escolinha | Concluído | `Domain/Configuracao/Escolinha.cs`; `ServicoPlataforma.CriarEscolinhaComGestorAsync` (escolinha + modalidade + parâmetros + créditos + 1º gestor numa gravação); `ServicoEscolinha`; telas `Plataforma/Nova` (assistente) e `Escolinha` | `FundacaoBancoTestes` (criação atômica, rollback na falha, CPF de outra pessoa), `ComumTestes` (CNPJ, CPF, telefone) |
| `BL-M02-02` | Cadastro de modalidade e interruptores de perfil | Concluído | `Domain/Configuracao/Modalidade.cs` (perfil, `RecursosModalidade`, `ModelosPerfil`); `ServicoModalidades`; telas `Modalidades` | `DominioConfiguracaoTestes` (R8), `FundacaoBancoTestes` (isolamento A/B) |
| `BL-M02-03` | Tipagem genérica do evento | Concluído (domínio) | `Domain/Eventos/TipoEvento.cs` (`TipoEvento`, `RecursosDoEvento`, `PoliticaPerfil`). A tabela `Evento` nasce no S5 (D13). | `DominioConfiguracaoTestes` (jogo sem adversário é válido; tipo que o perfil não suporta é recusado) |
| `BL-M02-06` | Catálogo de parâmetros de presença e evasão | Concluído | `CatalogoParametros` (7 de presença); `ValidadorParametro`; `ValoresParametros.Presenca`; aba Presença | `CatalogoParametrosTestes` (R7 → catálogo), `FundacaoBancoTestes` |
| `BL-M02-07` | Catálogo de parâmetros financeiros | Concluído | 7 de financeiro: multa, juros, vencimento, emissão, régua antes, no dia e depois | idem |
| `BL-M02-08` | Catálogo de parâmetros de convocação | Concluído | 6 de convocação: modo e janela de disparo, lembrete, sinalizar, atraso, bloquear (desligado, R9) | idem |
| `BL-M02-09` | Catálogo de parâmetros de mensagem | Concluído | Janela de silêncio (21:00 às 07:00) e aviso de saldo de créditos | idem, `JanelaHorarioTestes` (janela que vira a meia-noite) |
| `BL-M02-10` | Vinculação da conta de recebimento Asaas | Concluído | `ContaAsaasEscolinha` (chave cifrada, final visível); `ServicoContaAsaas` (a troca pede a senha; Produção só em Production); telas `ContaRecebimento`. A recusa de emitir cobrança sem conta é testada no S4, onde existe emissão. | `FundacaoBancoTestes` (cifrada no banco, senha exigida, só sandbox fora de Production) |
| `BL-M02-11` | Plano, saldo e histórico de créditos de mensagem | Concluído | `ContaCreditos` + `MovimentoCredito`; tela `Creditos` (gestor, só leitura) e `Plataforma/Creditos` (concessão e ajuste). O débito por mensagem entrou no S2 (BL-M09-04). | `DominioConfiguracaoTestes`, `FundacaoBancoTestes` (só a plataforma concede; o saldo nunca fica negativo) |
| `BL-M02-12` | Parâmetros de nível de turma | Concluído (no S1) | Catálogo com escopo Turma: mensalidade (sem padrão), capacidade 20, mínimo de confirmados 7, como valor inicial das turmas. Desde o S1, a turma nova nasce com o valor da escolinha lido na hora e guarda o seu (`Turma.Capacidade`, `Mensalidade`, `MinimoConfirmados`); mudar a mensalidade da turma fica no registro de alterações. | `CatalogoParametrosTestes`, `CadastroBancoTestes` (a turma nova usa o valor alterado sem reiniciar, CA-18) |
| `BL-M01-01` | Identidade única e vínculos de atuação | Concluído | `Pessoa`, `Credencial`, `VinculoAtuacao`; `ServicoAutenticacao`; login, troca obrigatória, escolha de escolinha, "entrar como equipe" | `ServicoAutenticacaoTestes`, `FundacaoBancoTestes` (vínculo revogado nega já na próxima requisição; duas escolinhas → escolha) |
| `BL-M01-03` | Motor de permissões por papel, escopo e sensibilidade | Concluído | `MatrizPermissoes` (§7 inteira), `Autorizacao` + `IResolvedorEscopo`, `[Permissao]`, `[Plataforma]`. No S1 entrou o escopo real do professor (`ResolvedorEscopoCadastros` + `IEscopoDeLeitura`); o do responsável (filhos com vínculo verificado) entrou com o convite, no S2 (BL-M01-06). | `PermissoesTestes` (a matriz inteira contra a §7, escopo próprio × alheio, negação por padrão), `RegrasDeOuroTestes` (toda action declara acesso) |

**Critérios de aceite tocados no S0:**
- **CA-18** ("nenhum parâmetro exige nova entrega"): catálogo + teste de que a mudança vale na próxima leitura, sem reiniciar. Cada fatia acrescenta um teste por consumidor de parâmetro.

---

## S1 — Cadastro: turma, sessões, aluno, responsável, equipe · 20 itens · **Pronto em 27/09/2026**

**Critério de pronto da spec:** existe uma turma com horários, um professor responsável, alunos matriculados com responsáveis vinculados, e as sessões de treino do mês aparecem como compromissos datados.

**Demonstrado no navegador em 27/09/2026, na escolinha A (Escolinha Piloto Londrina):**
- dois locais e três professores cadastrados. Um deles reaproveitou a pessoa do gestor, que ganhou o papel Professor sem conta nova;
- a turma nasceu em etapas, com horários, local, professor e mensalidade. O horário que batia com outra turma do mesmo professor gerou alerta e só foi salvo com confirmação (R9);
- as sessões até o fim do mês seguinte apareceram na agenda como compromissos datados, nos três recortes (turma, professor, local);
- a mudança do horário de quinta ajustou as 5 sessões previstas seguintes; a sessão cancelada ficou como estava;
- o aluno novo foi cadastrado em etapas: aluno, responsáveis, matrícula, saúde e consentimentos. O 2º filho reaproveitou a mãe pela busca, sem cadastro duplicado (CA-1); o cadastro parecido virou sugestão ("Usar este cadastro" ou "É outra pessoa");
- o desligamento exigiu data e motivo, encerrou a matrícula e liberou a vaga. O aluno saiu da lista padrão e da composição da turma e ficou na ficha, com o histórico;
- com um login de professor (o de gestão de B, que também tem o papel Professor em A), o menu, as listas e a agenda mostraram só a turma desse professor:
  - o aluno de outra turma deu **404** e as telas de gestão deram **403**;
  - a ficha mostrou só o necessário da saúde;
  - o cancelamento de uma sessão própria funcionou;
- a massa de demonstração (`/dev/massa`) montou 3 turmas e 60 alunos em 38 famílias, com 29 responsáveis reaproveitados.

| ID | Entregável | Status | Onde está | Testes |
|---|---|---|---|---|
| `BL-M04-01` | Cadastro e edição de turma | Concluído | <ul><li>`Domain/Turmas/Turma.cs`: modalidade, categoria, local, professor, capacidade, mensalidade, mínimo de confirmados, meta de frequência e horários.</li><li>`ServicoTurmas`; telas `Turmas` (nova em etapas, edição, encerrar).</li><li>A turma nova usa os valores do catálogo (BL-M02-12).</li><li>Mudar a mensalidade fica no registro de alterações.</li><li>Versão de linha contra gravação simultânea (D37).</li></ul> | `CadastroBancoTestes` (valores da escolinha lidos na hora, conflito confirmado, encerrar só sem matrícula ativa); `DominioCadastroTestes` (resumo dos horários) |
| `BL-M04-03` | Composição da turma e ocupação de vagas | Concluído | Lista de turmas com a ocupação (matrículas ativas ÷ capacidade, selo "Lotada"); ficha da turma com os alunos. A vaga cheia avisa e não bloqueia (D38). | `CadastroBancoTestes` (o desligamento libera a vaga) |
| `BL-M04-04` | Geração das sessões de treino | Concluído | <ul><li>`GeradorSessoes` (domínio puro) e `ServicoSessoes`.</li><li>Gera ao gravar a turma e todo dia às 03:15, pelo job `sessoes-gerar-horizonte`, até o fim do mês seguinte.</li><li>Só a sessão prevista e futura muda; a chave (horário, data) impede duplicidade (D31).</li></ul> | `DominioCadastroTestes` (horizonte, sem duplicar, mudança de horário e de dia, sessão já começada, turma encerrada); `CadastroBancoTestes` (compromissos datados, recorrente idempotente) |
| `BL-M04-06` | Cancelamento de sessão com notificação | Concluído (no S2) | `SessaoTreino.Cancelar` (motivo, autor e data); tela `Sessoes/Cancelar`, para o gestor e para o professor da sessão. **Desde o S2**, o cancelamento emite o gatilho "Treino cancelado" na mesma gravação: um aviso por família da turma, com os nomes dos filhos dela, pelo WhatsApp e, para quem ativou o acesso, também no portal. É urgente se o treino é nas próximas 24 horas (fura a janela de silêncio). | `CadastroBancoTestes` (motivo obrigatório, não cancela duas vezes, a cancelada fica quando o horário muda, o professor cancela só a própria); `MensageriaBancoTestes` (um aviso por família, depois da gravação) |
| `BL-M04-08` | Ocupação de agenda de professor e de quadra | Concluído | `IOcupacaoAgenda` (`ServicoAgenda.CompromissosAsync`) devolve os compromissos datados (`Domain/Agenda/Compromisso.cs`) por professor e por local. É a consulta que o S5 (eventos) vai usar; não há tabela de ocupação. | `CadastroBancoTestes` (compromissos por local) |
| `BL-M04-09` | Alerta de conflito de professor | Concluído | `DetectorConflitos`, com três alertas: <ul><li>outro treino do mesmo professor no horário;</li><li>fora da disponibilidade declarada;</li><li>dentro de uma indisponibilidade.</li></ul> Ao salvar a turma, pede confirmação e registra quem confirmou; na agenda, o alerta aparece em cada sessão. Sinaliza e não bloqueia (R9). O alerta de quadra ocupada, fora dos 81 itens, entrou junto (EX-02). | `DominioCadastroTestes` (5 casos); `CadastroBancoTestes` (alerta, confirmação registrada, alerta na agenda) |
| `BL-M04-11` | Consultas de agenda (turma, professor, quadra) | Concluído | Tela `Agenda` (`ServicoAgenda.ConsultarAsync`): recorte por turma, professor ou local; filtros de período (padrão: o mês; até 93 dias), modalidade e situação. | `CadastroBancoTestes` (recortes por turma, professor e local) |
| `BL-M03-01` | Cadastro de responsável com detecção de duplicidade | Concluído | <ul><li>`Responsavel`: ficha por escolinha (D32).</li><li>O mesmo CPF reaproveita sozinho.</li><li>O mesmo celular ou o mesmo nome vira sugestão, e quem cadastra decide (D33).</li><li>A busca de responsável fica no cadastro do aluno.</li><li>Telas `Responsaveis` (lista, ficha, edição).</li></ul> | `CadastroBancoTestes` (CA-1, celular compartilhado só sugere, B não vê o CPF que A digitou) |
| `BL-M03-02` | Cadastro de aluno | Concluído | <ul><li>`Aluno`: nome, nascimento, CPF opcional, endereço, categoria, contato de emergência, situação, datas e dados de saúde.</li><li>Não tem telefone próprio (R4).</li><li>Assistente `Alunos/Novo` em 5 etapas.</li><li>O mesmo CPF barra; o mesmo nome com o mesmo nascimento pede confirmação.</li></ul> | `CadastroBancoTestes` (CPF barrado, homônimo confirmado); `DominioCadastroTestes` (idade) |
| `BL-M03-03` | Vínculo aluno ↔ responsável com permissões por vínculo | Concluído | `VinculoAlunoResponsavel`: parentesco, **recebe cobrança**, **recebe notificação** e o **pagador** (um por aluno, que sempre recebe cobrança; D34). Telas para vincular, editar e desvincular; o último responsável não sai. A verificação do vínculo entrou no S2 (BL-M01-06). | `CadastroBancoTestes` (pagador único, último responsável); `DominioCadastroTestes` (pagador recebe cobrança) |
| `BL-M03-04` | Matrícula em turma e múltiplas turmas | Concluído | `Matricula`: uma ativa por aluno e turma, com mensalidade e datas próprias. `ServicoMatriculas`; telas `Matriculas` (nova, encerrar, mensalidade, esta com registro de alterações). | `CadastroBancoTestes` (duas turmas, matrícula repetida barrada); `DominioCadastroTestes` (período da matrícula) |
| `BL-M03-05` | Organização por turma e visão geral da escolinha | Concluído | Lista de alunos com busca, filtro por turma e por situação (padrão: quem está na escolinha); ficha da turma com a composição. | `CadastroBancoTestes` (listas por situação, composição da turma) |
| `BL-M03-07` | Status do aluno e transições | Concluído | `MaquinaSituacaoAluno`, com as transições da §6 validadas e nenhuma automática, mais o atalho inativo → desistente decidido pelo responsável (D43). O trial vencido é só sinal (P12), no painel e nas listas. Tela `Alunos/Situacao`. | `DominioCadastroTestes` (16 pares de transição, trial vencido, rematrícula) |
| `BL-M03-08` | Desligamento com data e motivo e efeitos em cascata | **Parcial** | <ul><li>Exige data e motivo; a data não vem antes da matrícula nem do início da situação atual.</li><li>Vale também para quem está em pausa (D43).</li><li>Encerra todas as matrículas na data e libera a vaga.</li><li>Tira o aluno das listas de trabalho e da composição da turma.</li><li>Preserva o histórico (ficha e registro de alterações).</li><li>A rematrícula volta a ativo com a data nova.</li></ul> **Falta encerrar a recorrência de cobrança:** ela nasce da matrícula no S4 e para na data de fim; cancelar as cobranças previstas é do BL-M06-14 (S4). | `CadastroBancoTestes` (CA-2 e CA-3, parte do S1; desligar direto da pausa); `DominioCadastroTestes` (data e motivo, rematrícula) |
| `BL-M03-09` | Consentimento na matrícula e sua revogação | Concluído | <ul><li>`ConsentimentoAluno` separado para tratamento de dados e uso de imagem, com a versão do termo, o responsável, a origem, quem registrou e quando.</li><li>Etapa no cadastro do aluno; telas para registrar e revogar.</li><li>Os textos dos termos são provisórios (P29).</li></ul> | `CadastroBancoTestes` (finalidades separadas; revogar a imagem não para a operação); `DominioCadastroTestes` (o último registro vale) |
| `BL-M03-10` | Restrição de acesso a dado sensível do aluno | Concluído | `PoliticaSaude` define o acesso à saúde: <ul><li>completo para o gestor e para o responsável do próprio aluno, quando o vínculo estiver verificado (S2);</li><li>só o necessário ao treino para o professor da turma (P20);</li><li>nada para os demais.</li></ul> A restrição vale no view model. O registro de alterações não guarda o conteúdo (D36). "Documentos de campeonato" é Fase 3 (I09). | `CadastroBancoTestes` (professor × gestor, auditoria sem conteúdo) |
| `BL-M03-11` | Observações médicas e contato de emergência | Concluído | Alergias, restrições, observações médicas e contato de emergência (nome, telefone, parentesco) na ficha; tela `Alunos/Saude`. | `CadastroBancoTestes` |
| `BL-M08-01` | Cadastro de professor e equipe | Concluído | <ul><li>`Professor`: ficha por escolinha, com dados pessoais e contato; as turmas sob responsabilidade aparecem na ficha.</li><li>Três origens: pessoa nova, pessoa que já tem acesso (ganha o papel Professor) e responsável já cadastrado.</li><li>Telas `Professores`.</li></ul> | `CadastroBancoTestes` (a pessoa com acesso ganha a ficha de professor e o papel Professor) |
| `BL-M08-04` | Escala e disponibilidade de horário | Concluído | Faixas semanais recorrentes e indisponibilidades pontuais (com motivo e autoria); tela `Professores/Disponibilidade`. Alimentam os alertas do BL-M04-09. | `DominioCadastroTestes` (faixa declarada, indisponibilidade) |
| `BL-M08-05` | Restrição de acesso do professor às próprias turmas | Concluído | <ul><li>`ResolvedorEscopoCadastros` e `IEscopoDeLeitura`, aplicados no menu, nas listas (turmas, alunos, responsáveis, agenda), nas fichas (404 fora do escopo) e nos irmãos e marcas financeiras (D41).</li><li>Sem valor nem CPF para o professor.</li><li>O S1 não tem exportação; as que vierem usam o mesmo escopo.</li></ul> | `CadastroBancoTestes` (turmas, alunos, responsáveis, agenda, irmão de outra turma, cancelar só a própria); `PermissoesTestes` (resolvedor real) |

**Critérios de aceite tocados no S1:**
- **CA-1:** completo (teste no banco e no navegador). A importação (S7) repete o teste.
- **CA-2**, parte do S1: data e motivo obrigatórios; matrículas encerradas na data. A recorrência é do S4.
- **CA-3**, parte do S1: o desligado sai das listas e da composição da turma e fica no histórico. A chamada (S3) e a convocação (S5) repetem o teste.
- **CA-18:** a turma nova nasce com o valor da escolinha lido na hora.
- **R4:** o aluno não tem telefone nem e-mail próprios no cadastro. Quem recebe mensagem é sempre um responsável; o contato de emergência serve à quadra e não recebe mensagem.

## S2 — Mensageria e acesso das famílias · 14 itens · **Pronto no simulador em 27/09/2026**

**Critério de pronto da spec:** um responsável real recebe uma mensagem pelo WhatsApp, responde, e a resposta aparece no sistema com o log de entrega registrado.

**Situação:** o fluxo inteiro foi demonstrado com o simulador, que devolve os webhooks assinados pelo endpoint de verdade. A prova com um número real espera a conta na Meta (P34). O cliente da API oficial já existe e tem teste de contrato.

**Demonstrado no navegador em 27/09/2026, na escolinha A:**
- **Janela de silêncio:** o convite da Ana Almeida, criado às 05:43, ficou "Na fila, sai em 27/09/2026 07:00".
- **Reenvio:** com a janela ajustada só para a validação, o reenvio mandou o link novo na hora, e o link antigo deixou de valer. Achado: a mensagem do convite substituído ficava na fila até as 07:00. Agora ela é suprimida na hora (D51).
- **Ativação:**
  - no simulador (`/dev/whatsapp`), o link abriu a ativação;
  - o CPF errado mostrou "Restam 4 tentativa(s)", e o certo levou à criação da senha;
  - a ativação sem e-mail entrou direto no portal, que mostrou o vínculo em confirmação e nenhum dado do aluno.
- **Vínculo:** a gestão confirmou o vínculo em `/Vinculos`, e o portal passou a mostrar o Arthur.
- **Cancelamento do treino** de sábado da Sub-9:
  - gerou 33 avisos: 32 pelo WhatsApp, um por família, e 1 no portal da Ana;
  - o saldo caiu de 3.000 para 2.967: os 32 avisos mais o convite (o aviso do portal não gasta crédito).
- **Retorno do WhatsApp:** no simulador, entregar, ler e responder mandaram os webhooks. O detalhe da mensagem mostrou a linha do tempo (criada, enviada, entregue, lida, resposta recebida) e a resposta.
- **Login por CPF:** a Ana entrou de novo pelo CPF, em 320 px, e foi direto ao portal. As telas de gestão deram **403** (P23 → D54).
- **Isolamento:** a gestão de B viu 0 mensagens, e a mensagem de A aberta pela URL deu **404**.
- **Templates:** a equipe da plataforma editou o texto do convite. Um texto com os marcadores fora de ordem voltou com o erro no campo.
- **Telas:** todas as telas novas foram conferidas em 375 e 320 px, no claro e no escuro, sem rolagem horizontal (nem da tabela) e sem erro no console.

| ID | Entregável | Status | Onde está | Testes |
|---|---|---|---|---|
| `BL-M09-01` | Motor único de disparo | Concluído | <ul><li>O `EmissorGatilhos` é a porta dos módulos, com um método por gatilho do catálogo. Ele grava a `Mensagem` no mesmo SaveChanges da mudança de negócio (outbox), com chave única contra duplicidade.</li><li>O `DespachoMensagensInterceptor` enfileira o envio depois do commit. O `ServicoEnvioMensagens` envia no job, relendo tudo (D46).</li><li>A varredura `mensagens-varredura`, a cada 5 min, recolhe o que ficou para trás.</li><li>Só o emissor cria mensagem, e só o motor chama o canal (teste de arquitetura).</li></ul> | `MensageriaBancoTestes` (cancelar o treino avisa cada família uma vez e despacha depois da gravação; janela de silêncio; retentativa; revalidação da origem); `RegrasDeOuroTestes` |
| `BL-M09-02` | Entrega pelos dois canais | Concluído (o envio real espera a P34) | <ul><li>WhatsApp sempre, como piso, e o aviso no portal (`/Portal/Avisos`) para quem ativou o acesso e tem os vínculos confirmados (D47).</li><li>Cliente da Cloud API oficial (`WhatsAppClienteHttp`, sem SDK) e simulador `/dev/whatsapp`, com a trava de ambiente (D49).</li></ul> | `WhatsAppClienteHttpTestes` (contrato: template, parâmetros, botões, classificação dos erros da Meta); `GuardaWhatsAppTestes`; `MensageriaBancoTestes` (o aviso chega no portal de quem ativou) |
| `BL-M09-03` | Catálogo de templates por categoria | Concluído (a submissão à Meta espera a P34) | <ul><li>`TemplateMensagem`, da plataforma: um por gatilho e tom, com nome na Meta, categoria (utilidade, autenticação ou marketing), botões e situação de aprovação.</li><li>13 textos padrão, provisórios (P02), gravados no startup quando faltam.</li><li>Tela `Templates`, da equipe da plataforma (D48).</li></ul> | `CatalogoGatilhosTestes` (um template por gatilho; as regras da Meta nos 13 textos); `MensageriaBancoTestes` (fora de produção, a API só manda template aprovado) |
| `BL-M09-04` | Contador e saldo de créditos | Concluído | <ul><li>Um crédito por WhatsApp enviado, reservado na mesma gravação da reserva de envio. O que não sai é estornado, e o aviso do portal não gasta.</li><li>Sem saldo, a mensagem essencial fica retida e a comum é suprimida; a retida sai quando o saldo volta (P06).</li><li>O saldo e o razão ficam na tela `Creditos`; o painel avisa as retidas e o saldo baixo.</li></ul> | `MensageriaBancoTestes` (débito e índice global; sem crédito; a falha retentável não cobra duas vezes e a definitiva estorna) |
| `BL-M09-07` | Captura de leitura e de resposta | Concluído | <ul><li>**WhatsApp:** os retornos (enviada, entregue, lida, falhou) chegam pelo webhook e só avançam. A resposta chega pelo id da mensagem ou, quando vem solta, pelo celular, com o 9º dígito normalizado.</li><li>**Portal:** abrir o aviso marca como lido.</li><li>**Botão:** o de um gatilho que espera resposta volta ao módulo de origem (`IReceptorResposta`; o 1º receptor será a convocação, no S5). O texto livre e o botão de outra mensagem ficam só no log.</li></ul> | `MensageriaBancoTestes` (webhook fora de ordem, repetido e com assinatura errada; resposta solta; botão ao módulo de origem; aviso lido no portal); `MensagemTestes` |
| `BL-M09-08` | Log de entrega consultável | Concluído | <ul><li>Tela `Mensagens`: filtros por gatilho, situação, canal e data, e o resumo dos últimos 30 dias.</li><li>Tela `Mensagens/Detalhe`: linha do tempo, motivo de retenção e de supressão, créditos e respostas.</li></ul> | `MensageriaBancoTestes` (B não vê as mensagens nem os convites de A) |
| `BL-M09-13` | Mediação obrigatória do responsável | Concluído (I08) | O destinatário é sempre responsável ou professor, garantido pelo enum e por uma restrição no banco. O aluno não tem telefone (S1). | `CatalogoGatilhosTestes` (nenhum destinatário é o aluno) |
| `BL-M09-14` | Catálogo fechado de gatilhos | Concluído | <ul><li>`GatilhoMensagem` tem 11 gatilhos: os 9 da §8.3, com "treino cancelado ou remarcado" e "evento alterado ou cancelado" separados.</li><li>Cada gatilho tem uma `RegraGatilho`: destinatário, momento, se espera resposta, fase, categoria, se é essencial, se pode ser urgente e se vira aviso no portal.</li><li>No S2 disparam "Treino cancelado" e "Convite de acesso"; os demais entram com as suas fatias.</li></ul> | `CatalogoGatilhosTestes` (regra e template para cada gatilho; só a convocação espera resposta); `RegrasDeOuroTestes` |
| `BL-M01-04` | Emissão, reenvio e cancelamento de convite | Concluído | <ul><li>`ConviteAcesso` e `ServicoConvites`, para responsável e professor.</li><li>Tela `Convites` e o bloco de acesso nas fichas.</li><li>Reenviar troca o link; cancelar invalida. A mensagem que ainda não saiu é suprimida na hora (D51).</li></ul> | `MensageriaBancoTestes` (convite substituído; convite cancelado; link mascarado no banco); `MensageriaUnidadeTestes` (só o hash é guardado) |
| `BL-M01-05` | Ativação com identificação da pessoa | Concluído | <ul><li>`/Convite/{token}`: página anônima, com limite de tentativas. Nada aparece antes de o CPF ser conferido.</li><li>O 5º CPF errado cancela o convite.</li><li>Quem já tem conta entra com a própria senha. Os demais criam o acesso, com login pelo e-mail ou pelo CPF (D52).</li></ul> | `MensageriaBancoTestes` (confere o CPF, cria o acesso e entra pelo CPF; cinco CPFs errados; conta de outra escolinha); `ServicoAutenticacaoTestes` (login pelo CPF; o CPF não vai inteiro ao log) |
| `BL-M01-06` | Vínculo verificado responsável ↔ aluno e fila de confirmação | Concluído | <ul><li>A verificação fica no próprio vínculo.</li><li>A fila `/Vinculos` confirma ou recusa, e a recusa exige motivo. O painel mostra os vínculos pendentes.</li><li>O escopo do responsável são os vínculos verificados: sem eles, o portal não mostra nada do aluno.</li><li>O vínculo criado para quem já tem acesso nasce confirmado (D53).</li></ul> | `MensageriaBancoTestes` (o portal só mostra o aluno depois da confirmação); `PermissoesTestes` (resolvedor do responsável); `MensageriaUnidadeTestes` (só confirma ou recusa o que foi pedido; a recusa exige motivo) |
| `BL-M01-12` | Mediação obrigatória do responsável na comunicação | Concluído (I08) | A mesma garantia do `BL-M09-13`. | Os mesmos do `BL-M09-13` |
| `BL-M12-07` | Convite em lote por WhatsApp | Concluído | Seleção na tela `Convites`, até 100 por vez: um convite individual por pessoa, num fluxo só. Quem não pode receber (sem celular ou já com acesso) fica no resumo, com o motivo. | `MensageriaBancoTestes` (convite em lote) |
| `BL-M12-11` | Seleção de canal por estado de acesso (modo parcial) | Concluído | O emissor decide por destinatário: WhatsApp sempre, e o portal só para quem ativou e tem os vínculos confirmados. Quem não ativou recebe tudo pelo WhatsApp (D47). | `MensageriaBancoTestes` (o cancelamento do treino avisa cada família; o aviso chega no portal de quem ativou) |

**Critérios de aceite tocados no S2:**
- **CA-17:** coberto no motor. Toda mensagem vai pelo WhatsApp e o portal é uma cópia. O S5 e o S6 repetem o teste.
- **CA-19:** coberto. O destinatário nunca é o aluno, pelo enum e pela restrição no banco (teste). O S5 e o S6 repetem.
- **CA-20:** coberto. O catálogo tem uma regra por gatilho, conferida 1:1 contra o enum. O teste de arquitetura garante que só o emissor cria mensagem e só o motor chama o canal.
- **CA-18:** consumidores `mensagem.*`. A janela de silêncio é lida na hora do envio, e o aviso de saldo baixo aparece no painel.
- **R4:** nenhum aviso vai ao aluno. O convite vai a responsável ou professor.

---

## S3 — Presença e controle de evasão · 6 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M05-01` | Chamada da sessão de treino | A fazer (offline: X07) |
| `BL-M05-07` | Configuração da regra de alerta e do risco | A fazer (os parâmetros já existem desde o S0) |
| `BL-M05-08` | Disparo do alerta de faltas | A fazer |
| `BL-M05-09` | Janela anti-duplicidade | A fazer |
| `BL-M05-11` | Cálculo e sinalização de risco de evasão | A fazer |
| `BL-M05-13` | Restrição de acesso a motivo de saúde | A fazer |

## S4 — Financeiro: mensalidade, régua e conciliação · 8 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M06-01` | Mensalidade recorrente por turma | A fazer |
| `BL-M06-02` | Emissão e cobrança via Asaas | A fazer (exige conta vinculada, BL-M02-10) |
| `BL-M06-03` | Painel de status de pagamento por aluno | A fazer |
| `BL-M06-04` | Régua de cobrança com escalonamento de tom | A fazer |
| `BL-M06-05` | Conciliação automática | A fazer |
| `BL-M06-06` | Tratamento de pagamento parcial ou divergente | A fazer |
| `BL-M06-14` | Encerramento da recorrência no desligamento | A fazer |
| `BL-M06-24` | Restrição de acesso ao financeiro | A fazer (a matriz já nega; falta o teste por tela) |

## S5 — Eventos e convocações · 10 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M07-01` | Cadastro do evento | A fazer (tipagem genérica pronta desde o S0) |
| `BL-M07-03` | Alerta de conflito de agenda do professor | A fazer |
| `BL-M07-05` | Seleção de convocados | A fazer |
| `BL-M07-06` | Alerta de aluno em dois jogos conflitantes | A fazer |
| `BL-M07-07` | Disparo da convocação | A fazer |
| `BL-M07-08` | Convocação em lote de múltiplos jogos | A fazer |
| `BL-M07-09` | Confirmação sim/não com painel em tempo real | A fazer |
| `BL-M07-11` | Lembrete a quem não respondeu | A fazer |
| `BL-M07-13` | Cancelamento e alteração do evento em um toque | A fazer |
| `BL-M07-18` | Escopo de acesso por turma | A fazer |

## S6 — Portal do responsável · 6 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M10-01` | Operação por WhatsApp e acesso web sem instalação | A fazer |
| `BL-M10-05` | Pagamentos, pendências e total do mês | A fazer |
| `BL-M10-06` | Pagamento no app via Asaas | A fazer |
| `BL-M10-07` | Receber e responder convocação | A fazer |
| `BL-M10-11` | Verificação de vínculo antes da exibição de dados | A fazer |
| `BL-M10-12` | Mediação obrigatória do responsável | A fazer (I08) |

## S7 — Carga inicial da escolinha piloto · 5 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M12-01` | Envio e leitura da planilha de importação | A fazer (depende da planilha real, P21) |
| `BL-M12-02` | Validação e classificação das linhas | A fazer |
| `BL-M12-03` | Deduplicação de responsáveis na importação | A fazer |
| `BL-M12-04` | Tela de conferência e confirmação do lote | A fazer |
| `BL-M12-05` | Restrição de dado sensível na importação | A fazer |

---

## Extras — pedidos do responsável fora dos 81 itens

| ID | Entregável | Status | Onde está | Testes |
|---|---|---|---|---|
| `EX-01` | **Compra de pacotes de créditos pelo Stripe da LDNA** (X08): a equipe cadastra os pacotes; o gestor compra na tela de créditos e paga no Checkout do Stripe (cartão, boleto ou Pix); os créditos entram sozinhos quando o Stripe confirma, uma vez só. | Concluído (26/09/2026), no modo Simulado. Pronto para o modo Teste assim que as chaves forem configuradas. | <ul><li>Domínio: `Domain/Plataforma` (`PacoteCreditos`, `CompraCreditos`) e `Domain/Integracao/WebhookRecebido`.</li><li>Casos de uso: `Application/Plataforma` (serviços, `ProcessadorPagamentosStripe`, `ConferenciaCompraCreditos`) e `Application/Integracao/Stripe`.</li><li>Integração: `Infrastructure/Integracao/Stripe` (cliente HTTP, simulador, trava de ambiente) e `Infrastructure/Plataforma` (repositório e jobs).</li><li>Telas `/Creditos`, `/Creditos/Compra/{id}` e `/Pacotes`; webhook `/api/v1/webhooks/stripe`; simulador `/dev/stripe`.</li></ul> | 52 testes (`Plataforma/*`): assinatura do webhook, contrato HTTP, trava de ambiente, transições da compra e, no banco, o fluxo completo (webhook repetido, fora de ordem, conferências simultâneas, boleto, expiração, isolamento entre escolinhas, varredura) |
| `EX-02` | **Alerta de quadra ocupada** (resposta do responsável à P31): outra turma no mesmo local e horário gera alerta ao salvar a turma e na agenda. Sinaliza, não bloqueia (R9). | Concluído (27/09/2026), no S1 | `DetectorConflitos.LocalOcupado` e `SobrepostosPorLocal` (domínio, para o S5 reaproveitar); `ServicoTurmas` (alerta com confirmação registrada); `ServicoAgenda` (alerta por sessão, sem nomear o outro treino); `TurmasAtivasDoLocalAsync` | `DominioCadastroTestes` (sobreposto, encostado e outro dia; agenda por local); `CadastroBancoTestes` (alerta, confirmação e agenda) |

Pendências do EX-01: P25 (plano recorrente), P26 (pacotes e preços comerciais), P27 (sandbox próprio no Stripe). O EX-02 deixa em aberto os "horários disponíveis" do local (P31).

---

## Critérios de aceite (seção 11) — onde nasce o teste de cada um

| CA | Resumo | Fatia | Status |
|---|---|---|---|
| CA-1 | 2º filho reaproveita o responsável, sem duplicidade | S1 (e S7) | **Coberto no S1** (`CadastroBancoTestes` e navegador); a importação repete no S7 |
| CA-2 | Desligamento exige data e motivo e encerra a recorrência | S1 e S4 | Parte do S1 coberta (data e motivo, matrículas encerradas); a recorrência é do S4 |
| CA-3 | Desligado some da chamada e da convocação, fica no histórico | S1, S3 e S5 | Parte do S1 coberta (sai das listas e da turma, fica no histórico); chamada no S3, convocação no S5 |
| CA-4 | "3 faltas no mês" dispara na 3ª falta | S3 | A fazer |
| CA-5 | Sem dois alertas dentro da janela anti-duplicidade | S3 | A fazer |
| CA-6 | Gestor vê na ficha qual alerta saiu, quando e para quem | S3 | A fazer (P09) |
| CA-7 | Pagamento confirmado muda o estado e para a régua | S4 | A fazer |
| CA-8 | Quem pagou nunca recebe cobrança posterior | S4 | A fazer |
| CA-9 | Parcial ou divergente fica sinalizado, sem conciliar | S4 | A fazer |
| CA-10 | 4 eventos × 12 convocados em um fluxo | S5 | A fazer |
| CA-11 | Uma mensagem por convocado, com data, hora, mapa e pergunta | S5 | A fazer |
| CA-12 | Resposta no WhatsApp aparece no painel sem atualizar | S5 | A fazer |
| CA-13 | Dois filhos convocados: mensagens distinguíveis | S5 | A fazer |
| CA-14 | Evento cancelado avisa todos e cancela as convocações | S5 | A fazer |
| CA-15 | Uma conta mostra os dois filhos | S6 | A fazer (P10) |
| CA-16 | Total do mês e detalhamento por filho | S6 | A fazer |
| CA-17 | Nada essencial só no app | S2 (e S5, S6) | **Coberto no S2** (toda mensagem vai pelo WhatsApp; o portal é cópia). O S5 e o S6 repetem. |
| CA-18 | Nenhum parâmetro exige nova entrega | S0 (+ um teste por consumidor em cada fatia) | **Parcial**: catálogo e "vale sem reiniciar" testados; no S1, o consumidor `turma.*` (a turma nova usa o valor alterado); no S2, o `mensagem.*` (janela de silêncio lida na hora do envio) |
| CA-19 | Nenhuma comunicação dirigida ao menor | S2 (e S5, S6) | **Coberto no S2** (destinatário nunca é o aluno: enum e restrição no banco). O S5 e o S6 repetem. |
| CA-20 | Nenhum envio fora do catálogo de gatilhos | S2 (e S5, S6) | **Coberto no S2** (regra 1:1 por gatilho e teste de arquitetura: só o emissor cria mensagem, só o motor envia) |
| CA-21 | Chamada sem sinal, registro da quadra prevalece | S3 | A fazer |
