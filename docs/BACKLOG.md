# Backlog e registro de progresso — Minha Escolinha (piloto da Fase 1)

Registro de progresso exigido pela seção 12.4 da spec. Os 81 entregáveis e os IDs `BL-Mxx-nn` são os da seção 10 de [`especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md`](especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md). Use esses IDs nas mensagens de commit.

**Status:**

| Status | Significado |
|---|---|
| **Concluído** | Comportamento, dado, tela e teste automatizado prontos; critério de pronto da fatia demonstrado |
| **Parcial** | A parte da fatia está pronta e o restante depende de outra fatia (dito na coluna) |
| **A fazer** | Ainda não iniciado |

Decisões, desvios e pendências ficam em [`DECISOES-EM-ABERTO.md`](DECISOES-EM-ABERTO.md).

## Resumo (26/09/2026)

| Fatia | Itens | Concluídos | Parciais | Situação |
|---|---:|---:|---:|---|
| F0 — Esqueleto vivo | — | — | — | **Pronto** (sem itens de backlog) |
| S0 — Fundação | 12 | 11 | 1 | **Pronto** (critério da fatia demonstrado), aguardando autorização de commit |
| S1 — Cadastro | 20 | 0 | 0 | Próxima |
| S2 — Mensageria e acesso | 14 | 0 | 0 | — |
| S3 — Presença e evasão | 6 | 0 | 0 | — |
| S4 — Financeiro | 8 | 0 | 0 | — |
| S5 — Eventos e convocações | 10 | 0 | 0 | — |
| S6 — Portal do responsável | 6 | 0 | 0 | — |
| S7 — Carga inicial | 5 | 0 | 0 | — |
| **Total** | **81** | **11** | **1** | |

O parcial do S0 é o `BL-M02-12`: os parâmetros de turma estão no catálogo, mas o valor por turma só pode ser gravado quando a Turma existir (S1).

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
| `BL-M02-11` | Plano, saldo e histórico de créditos de mensagem | Concluído | `ContaCreditos` + `MovimentoCredito`; tela `Creditos` (gestor, só leitura) e `Plataforma/Creditos` (concessão e ajuste). O débito por mensagem é do S2 (BL-M09-04). | `DominioConfiguracaoTestes`, `FundacaoBancoTestes` (só a plataforma concede; o saldo nunca fica negativo) |
| `BL-M02-12` | Parâmetros de nível de turma | Parcial | Catálogo com escopo Turma: mensalidade (sem padrão), capacidade 20, mínimo de confirmados 7, como valor inicial das turmas. **O valor por turma entra com a Turma no S1** (BL-M04-01). | `CatalogoParametrosTestes` |
| `BL-M01-01` | Identidade única e vínculos de atuação | Concluído | `Pessoa`, `Credencial`, `VinculoAtuacao`; `ServicoAutenticacao`; login, troca obrigatória, escolha de escolinha, "entrar como equipe" | `ServicoAutenticacaoTestes`, `FundacaoBancoTestes` (vínculo revogado nega já na próxima requisição; duas escolinhas → escolha) |
| `BL-M01-03` | Motor de permissões por papel, escopo e sensibilidade | Concluído (motor) | `MatrizPermissoes` (§7 inteira), `Autorizacao` + `IResolvedorEscopo`, `[Permissao]`, `[Plataforma]`. Os escopos reais (turmas do professor, filhos do responsável) entram no S1. | `PermissoesTestes` (a matriz inteira contra a §7, escopo próprio × alheio, negação por padrão), `RegrasDeOuroTestes` (toda action declara acesso) |

**Critérios de aceite tocados no S0:**
- **CA-18** ("nenhum parâmetro exige nova entrega"): catálogo + teste de que a mudança vale na próxima leitura, sem reiniciar. Cada fatia acrescenta um teste por consumidor de parâmetro.

---

## S1 — Cadastro: turma, sessões, aluno, responsável, equipe · 20 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M04-01` | Cadastro e edição de turma | A fazer |
| `BL-M04-03` | Composição da turma e ocupação de vagas | A fazer |
| `BL-M04-04` | Geração das sessões de treino | A fazer |
| `BL-M04-06` | Cancelamento de sessão com notificação | A fazer (a notificação sai pelo motor do S2) |
| `BL-M04-08` | Ocupação de agenda de professor e de quadra | A fazer |
| `BL-M04-09` | Alerta de conflito de professor | A fazer |
| `BL-M04-11` | Consultas de agenda (turma, professor, quadra) | A fazer |
| `BL-M03-01` | Cadastro de responsável com detecção de duplicidade | A fazer |
| `BL-M03-02` | Cadastro de aluno | A fazer |
| `BL-M03-03` | Vínculo aluno ↔ responsável com permissões por vínculo | A fazer |
| `BL-M03-04` | Matrícula em turma e múltiplas turmas | A fazer |
| `BL-M03-05` | Organização por turma e visão geral da escolinha | A fazer |
| `BL-M03-07` | Status do aluno e transições | A fazer |
| `BL-M03-08` | Desligamento com data e motivo e efeitos em cascata | A fazer |
| `BL-M03-09` | Consentimento na matrícula e sua revogação | A fazer |
| `BL-M03-10` | Restrição de acesso a dado sensível do aluno | A fazer |
| `BL-M03-11` | Observações médicas e contato de emergência | A fazer |
| `BL-M08-01` | Cadastro de professor e equipe | A fazer |
| `BL-M08-04` | Escala e disponibilidade de horário | A fazer |
| `BL-M08-05` | Restrição de acesso do professor às próprias turmas | A fazer |

## S2 — Mensageria e acesso das famílias · 14 itens · A fazer

| ID | Entregável | Status |
|---|---|---|
| `BL-M09-01` | Motor único de disparo | A fazer |
| `BL-M09-02` | Entrega pelos dois canais | A fazer |
| `BL-M09-03` | Catálogo de templates por categoria | A fazer (submeter à Meta assim que existir) |
| `BL-M09-04` | Contador e saldo de créditos | A fazer (saldo e razão já existem desde o S0) |
| `BL-M09-07` | Captura de leitura e de resposta | A fazer |
| `BL-M09-08` | Log de entrega consultável | A fazer |
| `BL-M09-13` | Mediação obrigatória do responsável | A fazer (I08) |
| `BL-M09-14` | Catálogo fechado de gatilhos | A fazer |
| `BL-M01-04` | Emissão, reenvio e cancelamento de convite | A fazer |
| `BL-M01-05` | Ativação com identificação da pessoa | A fazer |
| `BL-M01-06` | Vínculo verificado responsável ↔ aluno e fila de confirmação | A fazer |
| `BL-M01-12` | Mediação obrigatória do responsável na comunicação | A fazer (I08) |
| `BL-M12-07` | Convite em lote por WhatsApp | A fazer |
| `BL-M12-11` | Seleção de canal por estado de acesso (modo parcial) | A fazer |

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

## Critérios de aceite (seção 11) — onde nasce o teste de cada um

| CA | Resumo | Fatia | Status |
|---|---|---|---|
| CA-1 | 2º filho reaproveita o responsável, sem duplicidade | S1 (e S7) | A fazer |
| CA-2 | Desligamento exige data e motivo e encerra a recorrência | S1 e S4 | A fazer |
| CA-3 | Desligado some da chamada e da convocação, fica no histórico | S3 e S5 | A fazer |
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
| CA-17 | Nada essencial só no app | S2 (e S5, S6) | A fazer |
| CA-18 | Nenhum parâmetro exige nova entrega | S0 (+ um teste por consumidor em cada fatia) | **Parcial**: catálogo e "vale sem reiniciar" testados |
| CA-19 | Nenhuma comunicação dirigida ao menor | S2 (e S5, S6) | A fazer |
| CA-20 | Nenhum envio fora do catálogo de gatilhos | S2 (e S5, S6) | A fazer |
| CA-21 | Chamada sem sinal, registro da quadra prevalece | S3 | A fazer |
