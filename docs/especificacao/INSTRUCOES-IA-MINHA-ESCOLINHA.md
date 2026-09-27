# Minha Escolinha — Instruções de Execução para o Agente

**Tipo:** documento de instrução operacional. Não é briefing nem planejamento — é a especificação que o agente de IA deste projeto deve seguir para construir o sistema.
**Escopo desta versão:** escopo mínimo do piloto (Fase 1), 81 entregáveis.
**Origem:** derivado da Especificação Funcional v2.2 e da Especificação Funcional e Técnica do produto Minha Escolinha (LDNA S/A).

---

## 0. Como o agente deve usar este documento

Leia este documento por inteiro antes de escrever qualquer linha de código. Ele é a fonte de verdade sobre **o que** construir e **quais regras** valem. Ele deliberadamente **não** define stack, arquitetura, framework, banco ou padrão de projeto: isso vem do projeto principal onde este documento foi inserido.

**Sequência obrigatória de arranque:**

1. **Inventarie a herança.** Antes de decidir qualquer coisa, leia a documentação, a configuração e o código do projeto principal e produza um registro curto (seção 3) com: linguagem e framework, camada de dados e forma de migração, autenticação e autorização em uso, padrão de organização de módulos, estratégia de testes, fila ou agendamento de tarefas, observabilidade, deploy e ambientes, e convenções de nomenclatura. **Se o projeto principal já resolve um problema, use a solução dele.** Não introduza dependência, padrão ou serviço novo sem registrar a justificativa (seção 12.3).
2. **Confirme as credenciais e o modo de operação das integrações** (seção 8). Enquanto não houver credenciais reais, opere em modo simulado — e deixe isso explícito no código e na configuração.
3. **Produza o plano de execução** a partir das fatias da seção 9, ajustando nomes e estrutura ao padrão do projeto principal. Publique o plano antes de começar.
4. **Execute fatia a fatia**, em ordem. Cada fatia só é dada como concluída quando atende ao seu critério de pronto (seção 9) e tem teste automatizado cobrindo as regras de negócio que ela introduz.
5. **Registre decisões e bloqueios** conforme a seção 12. Nunca preencha uma lacuna com suposição silenciosa.

**Regra geral de conduta:** este documento descreve comportamento, dado e regra. Onde ele for omisso sobre *como* implementar, decida seguindo o projeto principal. Onde ele for omisso sobre *o que* o sistema deve fazer, **não invente** — registre como pendência e siga com o que está definido.

---

## 1. O produto

### 1.1 Missão

Sistema de gestão para escolinhas esportivas que automatiza as três tarefas que hoje consomem o dia do gestor — **cobrar, controlar presença e convocar para jogos** — e conecta escolinha, famílias e ligas no mesmo fluxo de informação. Começa por futsal e futebol, em Londrina e região.

### 1.2 A dor que justifica o produto

Não é hipótese, é operação observada em campo. O gestor hoje faz manualmente, sem nenhum controle: a cobrança da mensalidade, o controle de presença e evasão e — principalmente — a convocação semanal para jogos. O cenário real a atender é **quatro jogos por fim de semana, 10 a 12 convocados por jogo, tudo por WhatsApp manual**.

Toda disputa de prioridade no desenvolvimento se resolve por essa ordem: convocação, cobrança, evasão.

### 1.3 Os quatro públicos

| Público | Usa para |
|---|---|
| **Gestor / dono da escolinha** | Enxergar tudo: alunos, presença, financeiro, jogos da semana |
| **Professor** | Fazer chamada e convocar em poucos toques, na quadra, e ver a própria remuneração |
| **Responsável** | Acompanhar o filho, pagar, responder convocação, avisar falta |
| **Liga / federação** | Montar campeonatos e a tabela semanal com apoio de IA — **fora do escopo desta versão (Fase 3)** |

### 1.4 O que esta versão entrega

O **escopo mínimo do piloto**: uma escolinha operando de verdade, acompanhada pela equipe da LDNA. São 81 entregáveis organizados em 8 fatias verticais (seção 9 e 10).

**Critério de pronto do conjunto:** numa sexta-feira qualquer, o professor monta os quatro jogos do fim de semana e dispara as convocações em um fluxo só; os responsáveis confirmam pelo WhatsApp; o painel mostra quem vai sem ninguém contar na mão; a mensalidade do mês foi cobrada e conciliada sozinha; e o gestor viu quais alunos estão em risco de evasão sem pedir a ninguém.

### 1.5 O que está fora desta versão

Não construa, não prepare estrutura e não crie tabela para: módulo de ligas e federações, campeonatos, geração de tabela por IA, súmula, arbitragem e payout (Fase 3); marketplace e matrícula online (Fase 2); dashboard consolidado, painel financeiro de margem, despesas, documentos com assinatura digital, relatórios exportáveis e portal completo da família (Fase 1, mas adiados no piloto).

Se um item desta lista aparecer como necessário durante a execução, **pare e registre** (seção 12.2) em vez de construí-lo.

---

## 2. Protocolo de trabalho do agente

### 2.1 Unidade de trabalho

A unidade é o **entregável de backlog** (`BL-Mxx-nn`, seção 10). Cada um tem identificador estável, descrição e fatia. Trabalhe um de cada vez, na ordem da fatia.

Para cada entregável:

1. Releia as regras da seção 4 que se aplicam a ele.
2. Implemente comportamento e dado.
3. Escreva o teste automatizado das regras de negócio que ele introduz.
4. Marque o identificador como concluído no registro de progresso (seção 12.4).

### 2.2 Fatias verticais, não camadas horizontais

Cada fatia entrega comportamento ponta a ponta — dado, regra e interface — para um pedaço do fluxo real. **Não** construa "todo o banco", depois "toda a API", depois "todas as telas". Ao fim de cada fatia, alguém precisa conseguir usar aquilo.

### 2.3 O que nunca fazer sem aprovação humana explícita

- Disparar mensagem real de WhatsApp para um número que não seja de teste.
- Emitir cobrança real em ambiente de produção.
- Executar migração destrutiva ou que perca dado existente.
- Introduzir dependência, serviço externo ou padrão arquitetural que o projeto principal não use.
- Alterar a semântica de uma regra da seção 4.
- Implementar qualquer coisa da lista de fora de escopo (seção 1.5).

### 2.4 Ritmo de reporte

Ao terminar cada fatia, reporte: entregáveis concluídos, decisões tomadas (seção 12.1), pendências abertas (12.2) e o que a próxima fatia precisa que ainda não existe.

---

## 3. Herança do projeto principal

Este projeto **não define stack**. Ele herda a do projeto principal, que já foi usado para construir outros sistemas da LDNA — entre eles o sistema financeiro da própria agência.

**Antes da fatia S0, produza e mantenha atualizado um registro com:**

| O que detectar | Por que importa aqui |
|---|---|
| Linguagem, framework e versão | Base de tudo |
| Camada de dados e mecanismo de migração | O domínio desta aplicação tem ~14 entidades e várias máquinas de estado |
| Autenticação e autorização existentes | A seção 7 é uma matriz de permissões por papel e escopo; use o mecanismo que já existe |
| Multi-tenant, se houver | Uma pessoa pode atuar em mais de uma escolinha; o dado de uma nunca aparece no contexto da outra |
| Organização de módulos e convenção de nomes | Os módulos desta aplicação devem seguir a mesma organização |
| Estratégia e ferramenta de teste | Cada regra de negócio desta especificação precisa de teste |
| Fila, job agendado ou cron | A régua de cobrança, os lembretes de convocação e os alertas de falta dependem disso |
| Envio de mensagens e integrações já existentes | Se já houver integração com WhatsApp ou com gateway de pagamento, reutilize |
| Observabilidade e log | O log de entrega de mensagem (seção 8.2) se apoia nisso |
| Ambientes, segredos e deploy | Credenciais de Asaas e WhatsApp nunca no código |

**Regra:** onde o projeto principal tiver solução, use a dele. Onde não tiver, escolha a opção mais simples que atenda e registre a decisão (seção 12.1). Reinventar o que já existe no projeto principal é erro, não zelo.

---

## 4. Regras invioláveis

Estas regras valem para todo o sistema e não podem ser flexibilizadas por conveniência de implementação. Elas vêm do documento funcional e várias são obrigação legal ou decisão de negócio já fechada.

**R1 — Pagamento é via Asaas.** Pix, boleto e cartão. Na mensalidade do aluno, o valor cai na conta da própria escolinha. Não integre outro meio de pagamento.

**R2 — Mensagem é app/portal + WhatsApp via API oficial.** Esses são os dois canais. Nenhuma funcionalidade essencial — convocação, cobrança, aviso — pode existir só no app: tudo precisa funcionar integralmente por WhatsApp e por acesso web sem instalação.

**R3 — Nenhum módulo dispara mensagem por conta própria.** Existe um comportamento único de disparo — mesmos templates, mesmo registro de entrega, mesmo consumo de créditos — para qualquer evento de origem. Os módulos produzem gatilhos; quem envia é sempre o mesmo caminho.

**R4 — Comunicação com menor é sempre mediada pelo responsável.** Nenhuma mensagem automática é dirigida ao aluno menor de idade. Sem exceção.

**R5 — Dado sensível só para quem tem necessidade operacional.** Dados de saúde e documentos do aluno: gestor da escolinha, professor da turma em que o aluno treina, e responsável do próprio aluno. Ninguém mais.

**R6 — Consentimento registrado na matrícula**, com data, hora e finalidade, separando uso de imagem de tratamento de dados. A revogação do consentimento de imagem não pode impedir a operação essencial — cobrança, presença e convocação seguem funcionando.

**R7 — Regra de negócio sensível é parâmetro, não constante.** Limite de faltas, janela e percentual de risco de evasão, tom de mensagem, multa e juros, dias de atraso para sinalização, bloqueio por inadimplência, janela de silêncio, mínimo de confirmados, janelas de disparo e de lembrete: todos configuráveis por escolinha, alteráveis pela operação sem nova entrega de software.

**R8 — O núcleo não pressupõe esporte coletivo.** Todo compromisso datado é um **evento com tipo**, e o perfil da modalidade decide se existe adversário, placar, convocação ou graduação. Esta versão entrega apenas o perfil *coletiva* e apenas o tipo *jogo* — mas o modelo precisa nascer genérico. **Esta é a decisão mais cara de reverter no projeto inteiro.** Um modelo que assume "jogo" obriga a reescrever o núcleo quando entrarem judô, natação ou ballet.

**R9 — Sinalizar, não bloquear.** O sistema aponta risco — evasão, inadimplência — e deixa a decisão com o humano. Bloqueio automático só existe quando a escolinha o liga explicitamente, e vem desligado de fábrica.

**R10 — Poucos toques na quadra.** A chamada e a convocação são executadas em pé, com uma mão, durante o treino. A chamada precisa funcionar sem sinal de internet, e o registro feito na quadra prevalece sobre alteração concorrente feita no painel.

**R11 — Nada de trabalho duplicado.** Se um dado já existe em um módulo, não é redigitado em outro.

**R12 — Não invente requisito.** Se algo necessário não estiver aqui, registre como pendência (seção 12.2) e implemente o mínimo que não fecha portas. Nunca crie regra de negócio por conta própria.

---

## 5. Modelo de domínio

As entidades abaixo são o vocabulário obrigatório do sistema. Nomeie conforme a convenção do projeto principal, mas **não funda entidades distintas nem renomeie conceitos**.

| Entidade | Conteúdo essencial | Relações |
|---|---|---|
| **Escolinha** | Nome/razão social, CNPJ, endereço, contato, dados de recebimento (conta Asaas), plano e saldo de créditos de mensagem, modalidades oferecidas, **todos os parâmetros de regra de R7** | Raiz de quase tudo |
| **Modalidade** | Nome, perfil (nesta versão apenas *coletiva*), interruptores de recurso: adversário, placar/súmula, classificação, convocação, graduação, apresentação | Pertence à escolinha; referenciada pela turma |
| **Turma** | Nome, modalidade, categoria/faixa etária, horários, quadra/local, capacidade de vagas, professor responsável, mensalidade, meta de frequência, mínimo de confirmados por jogo | Escolinha → Turma; Turma → Sessões, Alunos |
| **Sessão de treino** | Turma, data e horário, local, professor, status, motivo de cancelamento, lista de presença | Gerada a partir dos horários da turma |
| **Aluno** | Nome, data de nascimento, CPF (se houver), endereço, contato de emergência, categoria, status, data de matrícula e de desligamento com motivo, observações médicas | Aluno ↔ Responsável (N:N); Aluno ↔ Turma (N:N) |
| **Responsável** | Nome, CPF, telefone/WhatsApp, e-mail (recuperação de acesso e recibo, **não** é canal de notificação), endereço, preferência de notificação, consentimentos, situação do acesso | Um responsável tem vários alunos; um aluno tem vários responsáveis |
| **Vínculo aluno↔responsável** | Tipo (pai, mãe, avô, tutor), **permissões por vínculo**: recebe cobrança / recebe notificação, estado de verificação | É onde mora a permissão, não no responsável |
| **Professor / Equipe** | Nome, CPF, contato, turmas sob responsabilidade, tipo de vínculo, forma de remuneração (fixo, por turma, comissão), dados bancários/Pix, disponibilidade de horário | Pode atuar em mais de uma escolinha |
| **Quadra / Local** | Nome, endereço, horários disponíveis, capacidade | Consumida por turma e por evento |
| **Evento** | **Tipo** (nesta versão sempre *jogo*), data, horário, duração, quadra/local, turma(s) envolvida(s), adversário, informações logísticas, estado, resultado no formato do perfil | Evento 1:N Convocações |
| **Convocação** | Evento, aluno convocado, responsáveis notificados, estado da confirmação **daquele convocado**, origem da resposta (app, WhatsApp ou registro manual do professor), data/hora da resposta, histórico de lembretes | **Uma por convocado, não uma por jogo** |
| **Cobrança / Transação** | Tipo, valor, desconto/multa aplicados, método, estado, vencimento e pagamento, aluno e responsável relacionados | Recorrência por aluno × turma |
| **Mensagem** | Tipo, canal, destinatário, template, conteúdo, data/hora, estado de entrega, resposta recebida, créditos consumidos | Log de tudo que sai |

**Multiplicidades que o modelo precisa suportar desde o início:** um responsável com vários filhos em visão única; um aluno em mais de uma turma; um professor em mais de uma escolinha; uma escolinha com mais de uma modalidade.

---

## 6. Máquinas de estado

Implemente como transições explícitas e validadas, não como campo livre.

**Aluno** — `trial` → (`ativo` | `desistente`) · `ativo` ↔ `inativo` · `ativo` → `desistente` · `desistente` → `ativo` (rematrícula)

- `trial` tem data limite e gera dois alertas ao gestor: um antes do vencimento (padrão 3 dias) e outro no dia. Vencido o prazo sem conversão, **permanece em `trial` sinalizado como vencido** — o sistema nunca desliga ninguém sozinho.
- `inativo` é pausa temporária: sem cobrança e sem chamada.
- `desistente` exige data e motivo, encerra a recorrência de cobrança e libera a vaga.
- A rematrícula devolve a `ativo` com nova data de matrícula, preservando todo o histórico.

**Cobrança** — `prevista` → `emitida` → `paga` | `vencida` → `paga em atraso` | `cancelada` | `isenta`

- `paga` interrompe a régua de cobrança na mesma hora.
- `isenta` não gera cobrança nem entra na inadimplência.
- **Pagamento parcial ou divergente não muda o estado automaticamente**: fica sinalizado para tratamento manual.

**Convocação (por convocado)** — `enviada` → `confirmada` | `recusada` | `sem resposta` | `cancelada`

- `sem resposta` é o estado após esgotado o prazo de lembrete.
- O professor pode sobrescrever qualquer estado, registrando que a resposta veio por fora.

**Evento** — `agendado` → `confirmado` → `realizado` → `resultado homologado` | `cancelado` | `adiado`

- `cancelado` e `adiado` notificam todos os envolvidos e levam as convocações a `cancelada`.

**Sessão de treino** — `prevista` → `realizada` | `cancelada` | `remarcada`

---

## 7. Papéis e permissões

`T` total · `P` parcial, apenas o escopo próprio · `L` somente leitura · `—` sem acesso

| Recurso | Gestor | Professor | Responsável |
|---|:--:|:--:|:--:|
| Cadastro de alunos e responsáveis | T | P (da sua turma) | P (próprios dados e dos filhos) |
| Documentos e observações médicas | T | P (o necessário ao treino) | P (dos filhos) |
| Registro de presença | T | P | L (do próprio filho) |
| Configuração de parâmetros da escolinha | T | — | — |
| Financeiro da escolinha | T | — | — |
| Remuneração própria do professor | T | P (a própria) | — |
| Pagamentos do próprio filho | T | — | P |
| Criar evento e convocar | T | P | — |
| Responder convocação | P (registro de resposta recebida por fora) | P (idem) | P |
| Cadastro de professores e remuneração | T | — | — |

**Regras transversais:** uma pessoa pode acumular papéis sem contas separadas; um professor em mais de uma escolinha alterna sem que os dados de uma apareçam no contexto da outra; toda ação sensível (alteração de mensalidade, exclusão de aluno, isenção, cancelamento de cobrança) fica registrada com autor, data e valor anterior; a revogação de acesso é imediata e preserva o histórico do que a pessoa registrou.

---

## 8. Integrações

Apenas duas, ambas restrição de negócio já decidida. Não acrescente outras.

### 8.1 Asaas — cobrança e conciliação

**O que precisa acontecer:**

- Cobrança recorrente por aluno × turma, com Pix, boleto e cartão, caindo na conta da escolinha.
- **Conciliação automática:** confirmado o pagamento no Asaas, o estado muda sem intervenção humana e a régua daquela cobrança para na mesma hora.
- Um responsável que pagou **não pode, em nenhuma hipótese**, receber cobrança posterior daquela mensalidade.
- Pagamento parcial ou divergente **não concilia sozinho**.
- Multa e juros conforme parâmetro da escolinha.
- Encerramento da recorrência quando o aluno é desligado, sinalizando débito em aberto remanescente.

**Exigências de implementação:**

- Todo recebimento de confirmação precisa ser **idempotente**: a mesma confirmação recebida duas vezes não pode gerar dois efeitos.
- Toda operação precisa ser **rastreável**: guarde a referência da transação no Asaas junto da cobrança local.
- Indisponibilidade do Asaas não pode perder cobrança: a emissão precisa ser reprocessável.
- Credenciais por ambiente, nunca no código. **Enquanto não houver credenciais, opere contra o ambiente de teste do Asaas ou contra uma implementação simulada**, com a mesma interface.

### 8.2 WhatsApp — canal de mensagem

**O que precisa acontecer:**

- Envio por template, dentro das categorias aceitas pela política do WhatsApp (utilidade, autenticação, marketing).
- **Captura de resposta**: a convocação é respondida pelo WhatsApp e o estado muda no sistema, em tempo real, sem o professor atualizar a tela.
- **Log de entrega** por mensagem — enviada, entregue, lida, falhou — consultável pelo gestor.
- **Contador de créditos** por mensagem enviada, com saldo por escolinha.
- **Fallback ao zerar créditos** (regra provisória, vale até decisão comercial): suspende o envio por WhatsApp, mantém a notificação pelo app/portal, alerta o gestor com o caminho de recarga e **retém em fila as mensagens essenciais** — cobrança, convocação, cancelamento — despachando-as quando houver saldo. Nenhuma mensagem essencial é descartada em silêncio.
- **Janela de silêncio** configurável (padrão sugerido 21h–7h). Escapam só as urgentes: cancelamento ou alteração de evento ou treino nas próximas 24 horas, e comunicado de segurança.
- **Descadastro de marketing** respeitado, sem afetar mensagens de utilidade.

**Dependência externa crítica:** a aprovação dos templates pela política do WhatsApp tem prazo fora do controle do projeto. Estruture o catálogo de templates cedo, na fatia S2, para que a submissão comece o quanto antes. **Não bloqueie o desenvolvimento esperando a aprovação** — construa contra uma implementação simulada com a mesma interface e troque depois.

### 8.3 Catálogo de gatilhos

Esta é a lista fechada de disparos automáticos desta versão. Nenhum outro envio automático deve existir.

| Evento no sistema | Destinatário | Quando | Espera resposta |
|---|---|---|:--:|
| Regra de faltas atingida | Responsáveis do aluno | No registro da falta que atinge a regra | Não |
| Treino cancelado ou remarcado | Responsáveis da turma | Imediato | Não |
| Mensalidade a vencer | Responsável financeiro | Janela configurável antes do vencimento | Não |
| Mensalidade vencida | Responsável financeiro | No vencimento e conforme a régua | Não |
| Pagamento confirmado | Responsável financeiro | Na conciliação | Não |
| Convocação criada | Responsáveis dos convocados | No disparo ou na janela configurada | **Sim (sim/não)** |
| Convocação sem resposta | Responsáveis pendentes | Janela configurável antes do evento | **Sim** |
| Evento alterado ou cancelado | Convocados | Imediato (urgente) | Não |
| Convite de acesso | Responsável ou professor | No convite | Não |

---
## 9. Fatias de execução

Oito fatias verticais, em ordem obrigatória. A fatia só termina quando o critério de pronto é verdadeiro e demonstrável — não quando o código "está lá".

### S0 — Fundação: conta, identidade e permissões

**Entrega:** a escolinha existe no sistema, com gestor identificado, modalidade cadastrada e todos os parâmetros de regra configuráveis.

**Pontos de atenção:**
- A conta e o primeiro gestor nascem juntos: resolva essa circularidade explicitamente.
- Os parâmetros de R7 são catálogo, não campos soltos: agrupe por domínio (presença, financeiro, convocação, mensagem) e permita leitura por qualquer módulo.
- **A tipagem genérica do evento e os interruptores de perfil de modalidade nascem aqui** (R8). Mesmo entregando só *coletiva* e só *jogo*.

**Pronto quando:** é possível criar uma escolinha, definir seus parâmetros, cadastrar a modalidade futsal com o perfil coletiva, e um gestor autenticado enxerga apenas a própria escolinha.

### S1 — Cadastro: turma, sessões, aluno, responsável, equipe

**Entrega:** a estrutura sobre a qual tudo opera.

**Pontos de atenção:**
- Turma antes de aluno; professor antes de turma.
- **As sessões de treino são geradas a partir dos horários da turma** — sem elas não existe o que chamar.
- O vínculo aluno↔responsável carrega as permissões (recebe cobrança / recebe notificação). Um responsável com dois filhos é cadastro único com dois vínculos.
- A deduplicação de responsável precisa existir no primeiro cadastro, não depois.
- Ocupação de agenda de professor e de quadra, com alerta de conflito.

**Pronto quando:** existe uma turma com horários, um professor responsável, alunos matriculados com responsáveis vinculados, e as sessões de treino do mês aparecem como compromissos datados.

### S2 — Mensageria e acesso das famílias

**Entrega:** a mensagem sai e a resposta volta. É a fatia que destrava todas as seguintes.

**Pontos de atenção:**
- Construa o comportamento único de disparo (R3) com o catálogo de templates, o contador de créditos, o log de entrega e a captura de resposta.
- Convite de acesso, ativação com identificação da pessoa e verificação do vínculo responsável↔aluno.
- **Modo parcial:** enquanto nem todos ativaram o acesso, quem não ativou continua recebendo tudo por WhatsApp, sem furo de comunicação.
- Submeta os templates à aprovação do WhatsApp assim que o catálogo existir.

**Pronto quando:** um responsável real recebe uma mensagem pelo WhatsApp, responde, e a resposta aparece no sistema com o log de entrega registrado.

### S3 — Presença e controle de evasão

**Entrega:** a chamada na quadra e o alerta que antecipa a perda do aluno.

**Pontos de atenção:**
- Chamada em poucos toques (R10), funcionando sem sinal, com o registro da quadra prevalecendo.
- Regra de alerta configurável: número de faltas no mês **ou** faltas consecutivas.
- O alerta dispara **no registro da falta que atinge a regra**, não no fechamento do mês.
- Janela anti-duplicidade: o mesmo aluno não recebe dois alertas dentro da janela (padrão 15 dias).
- Sinalização de risco de evasão por frequência baixa recente, com os limiares vindos dos parâmetros.

**Pronto quando:** o professor faz a chamada pelo celular na quadra, sem internet, e ao reconectar o alerta de faltas sai sozinho para o responsável certo.

### S4 — Financeiro: mensalidade, régua e conciliação

**Entrega:** o dinheiro entra sem ninguém cobrar na mão.

**Pontos de atenção:**
- Recorrência por turma, emissão via Asaas, régua com escalonamento de tom, conciliação automática.
- Pagamento parcial ou divergente **não concilia** — sinaliza.
- Encerramento da recorrência no desligamento do aluno.
- Restrição de acesso: o professor não vê o financeiro da escolinha.

**Pronto quando:** a mensalidade do mês é emitida sozinha, o lembrete sai antes do vencimento, o pagamento é conciliado sem ação humana e a régua para na hora.

### S5 — Eventos e convocações

**Entrega:** a funcionalidade-âncora do produto.

**Pontos de atenção:**
- Cadastro do evento com duração (necessária para os alertas de conflito).
- Seleção de convocados dentro da turma — o professor escolhe quem joga.
- **Convocação em lote para múltiplos eventos do mesmo fim de semana, em um único fluxo.**
- Confirmação sim/não com painel atualizando em tempo real.
- Lembrete automático a quem não respondeu.
- Cancelamento e alteração em um toque, notificando todos.
- Alertas de conflito: aluno em dois eventos, professor com agenda ocupada.
- Registro manual da resposta que veio por fora.

**Pronto quando:** o professor monta 4 eventos com 12 convocados cada e dispara tudo em um fluxo só; as respostas chegam pelo WhatsApp e o painel mostra confirmados, recusados e pendentes sem atualizar a tela.

### S6 — Portal do responsável

**Entrega:** a superfície da família.

**Pontos de atenção:**
- **Funciona por WhatsApp e por acesso web, sem instalação** (R2).
- Visão única de múltiplos filhos, sem troca de conta.
- Pagamentos, pendências, total do mês e pagamento direto.
- Receber e responder convocação.
- Verificação de vínculo antes de exibir qualquer dado, e mediação obrigatória (R4).

**Pronto quando:** um responsável com dois filhos vê os dois numa conta só, paga a mensalidade e responde a convocação identificando de qual filho se trata.

### S7 — Carga inicial da escolinha piloto

**Entrega:** o caminho de entrada da escolinha que hoje opera em planilha.

**Pontos de atenção:**
- Importação por planilha com validação, classificação das linhas e deduplicação de responsáveis.
- Tela de conferência antes de confirmar o lote — nada entra sem revisão humana.
- Restrição de dado sensível também na importação.

**Pronto quando:** a base real da escolinha piloto entra no sistema em uma operação, com os erros apontados antes da confirmação.

---

## 10. Backlog executável

81 entregáveis. Os identificadores são estáveis e correspondem aos da Especificação Funcional e Técnica do produto — use-os no controle de tarefas e nas mensagens de commit.

### S0 — Fundação — conta, identidade e permissões  ·  12 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M02-01` | Conta da escolinha | Cadastro da conta principal com identificação, dados institucionais, endereço, contato e modalidades oferecidas (seção 10, RF-PAI-001). |
| `BL-M02-02` | Cadastro de modalidade e interruptores de perfil | Define quais recursos existem na modalidade (RF-MOD-002). |
| `BL-M02-03` | Tipagem genérica do evento | O núcleo trata todo compromisso datado como evento com tipo, sem pressupor adversário, placar ou classificação (RF-MOD-001). |
| `BL-M02-06` | Catálogo de parâmetros de presença e evasão | Regra de faltas, tom, percentual e janela de risco, janela anti-duplicidade, tratamento da falta justificada (RF-PRE-002, 003, 005, 011). |
| `BL-M02-07` | Catálogo de parâmetros financeiros | Multa, juros e janelas da régua de cobrança (RF-FIN-009, RF-FIN-004). |
| `BL-M02-08` | Catálogo de parâmetros de convocação | Janela de disparo e de lembrete, dias de atraso para sinalização, sinalização ligada/desligada, bloqueio por inadimplência (RF-CON-003, 005, 016, 017). |
| `BL-M02-09` | Catálogo de parâmetros de mensagem | Janela de silêncio e limite de aviso de saldo de créditos (RF-MSG-009, RF-MSG-006). |
| `BL-M02-10` | Vinculação da conta de recebimento Asaas | Parâmetro sem o qual nenhuma cobrança é emitida (RF-FIN-002). |
| `BL-M02-11` | Plano, saldo e histórico de créditos de mensagem | Plano contratado, pacote associado, saldo consultável e histórico (RF-MSG-004, seção 12). |
| `BL-M02-12` | Parâmetros de nível de turma | Mensalidade, capacidade de vagas e mínimo de confirmados (RF-FIN-001, RF-ALU-013, RF-CON-011). |
| `BL-M01-01` | Identidade única e vínculos de atuação | Uma pessoa com múltiplos papéis e contextos, sem contas separadas (RF-PER-001). |
| `BL-M01-03` | Motor de permissões por papel, escopo e sensibilidade | Resolve toda decisão de acesso conforme a matriz 3.2 e RF-LGP-003. |

### S1 — Cadastro — turma, sessões, aluno, responsável, equipe  ·  20 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M04-01` | Cadastro e edição de turma | Criar e manter turma com modalidade, categoria, horários, local, capacidade, professor e mensalidade (entidade Turma; RF-FIN-001, RF-MOD-004). |
| `BL-M04-03` | Composição da turma e ocupação de vagas | Exibir alunos matriculados e ocupação frente à capacidade, insumo de RF-ALU-013. |
| `BL-M04-04` | Geração das sessões de treino | Gerar sessões a partir dos horários da turma, com data, horário, local e professor (RF-PRE-012). |
| `BL-M04-06` | Cancelamento de sessão com notificação | Cancelar sessão específica com motivo e notificar os responsáveis da turma (RF-PRE-013). |
| `BL-M04-08` | Ocupação de agenda de professor e de quadra | Registrar e expor a ocupação gerada por sessões, consumível por M07 e, na F3, por M14. |
| `BL-M04-09` | Alerta de conflito de professor | Alertar ao marcar treino em horário já ocupado pelo professor ou fora de sua disponibilidade (RF-CON-018). |
| `BL-M04-11` | Consultas de agenda (turma, professor, quadra) | Três recortes de consulta do calendário de sessões, com filtros por período, modalidade e estado. |
| `BL-M03-01` | Cadastro de responsável com detecção de duplicidade | Cadastro único por pessoa, reaproveitado entre irmãos (RF-ALU-003, CA-ALU-01). |
| `BL-M03-02` | Cadastro de aluno | Todos os dados da seção 4.1, com detecção de possível duplicidade (RF-ALU-001). |
| `BL-M03-03` | Vínculo aluno ↔ responsável com permissões por vínculo | Tipo de vínculo e marcação de recebe cobrança / recebe notificação (RF-ALU-002). |
| `BL-M03-04` | Matrícula em turma e múltiplas turmas | Uma matrícula por turma, com mensalidade e datas próprias (RF-ALU-005). |
| `BL-M03-05` | Organização por turma e visão geral da escolinha | Duas visões sobre a mesma base de alunos (RF-ALU-004). |
| `BL-M03-07` | Status do aluno e transições | Os quatro status da seção 9, sem nenhuma transição automática (RF-ALU-006). |
| `BL-M03-08` | Desligamento com data e motivo e efeitos em cascata | Encerra recorrência, libera vaga, remove das listas, preserva histórico (CA-ALU-02, CA-ALU-03, RF-FIN-013). |
| `BL-M03-09` | Consentimento na matrícula e sua revogação | Registro separado de imagem e de tratamento de dados, com data, hora e finalidade (RF-LGP-001, RF-LGP-002). |
| `BL-M03-10` | Restrição de acesso a dado sensível do aluno | Gestor, professor da turma e responsável do próprio aluno; liga apenas documentos de campeonato (RF-LGP-003). |
| `BL-M03-11` | Observações médicas e contato de emergência | Alergias, restrições e contato de emergência na ficha do aluno (RF-ALU-010). |
| `BL-M08-01` | Cadastro de professor e equipe | Registrar dados pessoais, contato e turmas sob responsabilidade, reaproveitando cadastro de pessoa existente. |
| `BL-M08-04` | Escala e disponibilidade de horário | Registrar faixas recorrentes e indisponibilidades pontuais por professor. |
| `BL-M08-05` | Restrição de acesso do professor às próprias turmas | Aplicar o escopo de turma em toda navegação, busca, listagem e exportação. |

### S2 — Mensageria e acesso das famílias  ·  14 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M09-01` | Motor único de disparo | Receber gatilhos de qualquer módulo e executar canal, template, elegibilidade, envio e log de forma uniforme (RF-MSG-001). |
| `BL-M09-02` | Entrega pelos dois canais | Enviar por notificação no app/portal e por WhatsApp via API oficial, com o WhatsApp como canal de piso (RF-MSG-002; RF-PAI-015). |
| `BL-M09-03` | Catálogo de templates por categoria | Manter templates configuráveis por tipo de mensagem, classificados em utilidade, autenticação ou marketing, com variações de tom (RF-MSG-003). |
| `BL-M09-04` | Contador e saldo de créditos | Debitar o crédito a cada mensagem de WhatsApp enviada, com saldo consultável e histórico de consumo (RF-MSG-004; seção 12). |
| `BL-M09-07` | Captura de leitura e de resposta | Capturar confirmação de leitura e resposta pelos dois canais e devolvê-las ao módulo de origem (RF-MSG-005). |
| `BL-M09-08` | Log de entrega consultável | Registrar enviada, entregue, lida e falhou por mensagem, com o motivo de cada supressão, consultável pelo gestor (RF-MSG-007). |
| `BL-M09-13` | Mediação obrigatória do responsável | Garantir que nenhum gatilho tenha o aluno menor como destinatário (RF-LGP-007). |
| `BL-M09-14` | Catálogo fechado de gatilhos | Implementar as linhas da matriz 7.1 como a lista de gatilhos do sistema, com destinatário, momento, resposta esperada e fase (seção 7.1). |
| `BL-M01-04` | Emissão, reenvio e cancelamento de convite | Convite de professor e de responsável sobre cadastro existente (RF-ACS-001). |
| `BL-M01-05` | Ativação com identificação da pessoa | Nenhum dado antes da identificação (RF-ACS-001). |
| `BL-M01-06` | Vínculo verificado responsável ↔ aluno e fila de confirmação | Bloqueio total enquanto pendente; confirmação e recusa pelo gestor (RF-ACS-002). |
| `BL-M01-12` | Mediação obrigatória do responsável na comunicação | Nenhuma mensagem automática dirigida ao menor (RF-LGP-007). |
| `BL-M12-07` | Convite em lote por WhatsApp | Selecionar responsáveis e disparar convite individual identificado, em um único fluxo (RF-ONB-003; RF-ACS-001). |
| `BL-M12-11` | Seleção de canal por estado de acesso (modo parcial) | Escolher o canal de cada mensagem conforme o responsável tenha ou não ativado o app, sem furo de comunicação (RF-ONB-004; RF-PAI-015). |

### S3 — Presença e controle de evasão  ·  6 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M05-01` | Chamada da sessão de treino | Marcar presença e falta por aluno em um toque, na quadra, sobre a sessão do dia (RF-PRE-001). |
| `BL-M05-07` | Configuração da regra de alerta e do risco | Parametrizar por escolinha regra de faltas, tom, janela anti-duplicidade, percentual e janela de risco (RF-PRE-002, RF-PRE-003, RF-PRE-005, RF-PRE-011). |
| `BL-M05-08` | Disparo do alerta de faltas | Avaliar a regra a cada falta registrada e disparar a mensagem ao responsável no momento do atingimento (RF-PRE-003; CA-PRE-01). |
| `BL-M05-09` | Janela anti-duplicidade | Suprimir alerta repetido dentro da janela configurada e registrar a supressão (RF-PRE-011). |
| `BL-M05-11` | Cálculo e sinalização de risco de evasão | Apurar a frequência na janela configurada e destacar visualmente os alunos abaixo do percentual (RF-PRE-005). |
| `BL-M05-13` | Restrição de acesso a motivo de saúde | Aplicar a restrição de dado sensível ao motivo de falta por doença (RF-LGP-003). |

### S4 — Financeiro — mensalidade, régua e conciliação  ·  8 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M06-01` | Mensalidade recorrente por turma | Definir valor e periodicidade por turma e criar a recorrência do aluno na matrícula. |
| `BL-M06-02` | Emissão e cobrança via Asaas | Emitir a cobrança do ciclo e disponibilizá-la em Pix, boleto e cartão na conta da escolinha. |
| `BL-M06-03` | Painel de status de pagamento por aluno | Listar alunos por situação de pagamento no agrupamento de RF-FIN-003, com filtros e ações. |
| `BL-M06-04` | Régua de cobrança com escalonamento de tom | Configurar e executar os degraus antes, no dia e após o vencimento, com tom crescente. |
| `BL-M06-05` | Conciliação automática | Atualizar o estado da cobrança e interromper a régua na confirmação do pagamento. |
| `BL-M06-06` | Tratamento de pagamento parcial ou divergente | Fila de divergências com resolução manual e registro de autoria. |
| `BL-M06-14` | Encerramento da recorrência no desligamento | Encerrar a recorrência, cancelar previstas e sinalizar débito remanescente. |
| `BL-M06-24` | Restrição de acesso ao financeiro | Garantir que apenas o gestor acesse o financeiro da escolinha, em qualquer caminho de navegação. |

### S5 — Eventos e convocações  ·  10 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M07-01` | Cadastro do evento | Criar evento tipado com data, horário, local, turma(s) e adversário opcional; na F1, apenas o tipo jogo (RF-CON-001, RF-MOD-001). |
| `BL-M07-03` | Alerta de conflito de agenda do professor | Verificar a agenda e a disponibilidade do professor responsável ao marcar o evento e alertar antes de confirmar (RF-CON-018). |
| `BL-M07-05` | Seleção de convocados | Selecionar nominalmente um subconjunto da turma, com contador e sinalizações visíveis (RF-CON-002). |
| `BL-M07-06` | Alerta de aluno em dois jogos conflitantes | Alertar, sem bloquear, quando o aluno já estiver convocado para outro jogo em horário conflitante no mesmo dia (RF-CON-010). |
| `BL-M07-07` | Disparo da convocação | Disparar aos responsáveis dos convocados, imediatamente ou na janela configurada, com o conteúdo mínimo de CA-CON-02 e a identificação do filho (RF-CON-003). |
| `BL-M07-08` | Convocação em lote de múltiplos jogos | Montar e disparar as convocações de vários jogos do fim de semana em um único fluxo, sem sair da tela (RF-CON-007). |
| `BL-M07-09` | Confirmação sim/não com painel em tempo real | Receber a resposta pelos dois canais e refletir confirmados, recusados e sem resposta no painel sem atualização manual (RF-CON-004). |
| `BL-M07-11` | Lembrete a quem não respondeu | Disparar lembrete automático, na janela configurada, apenas aos responsáveis pendentes (RF-CON-005). |
| `BL-M07-13` | Cancelamento e alteração do evento em um toque | Cancelar, adiar ou alterar o evento e comunicar todos os convocados pelos dois canais, levando as convocações a `cancelada` (RF-CON-012). |
| `BL-M07-18` | Escopo de acesso por turma | Restringir criação, convocação e consulta às turmas que o professor leciona (RF-EQP-005, matriz 3.2). |

### S6 — Portal do responsável  ·  6 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M10-01` | Operação por WhatsApp e acesso web sem instalação | Garantir que convocação, cobrança e aviso funcionem integralmente pelas três superfícies (RF-PAI-015). |
| `BL-M10-05` | Pagamentos, pendências e total do mês | Exibir mensalidades pagas, pendentes e próximo vencimento, com o total devido e o detalhamento por filho (RF-PAI-003; CA-PAI-03). |
| `BL-M10-06` | Pagamento no app via Asaas | Pagar por Pix, cartão ou boleto dentro do app, com o valor caindo na conta da escolinha (RF-PAI-003; RF-FIN-002). |
| `BL-M10-07` | Receber e responder convocação | Receber a convocação com identificação do filho e responder sim ou não pelas três superfícies (RF-PAI-004; CA-PAI-02). |
| `BL-M10-11` | Verificação de vínculo antes da exibição de dados | Não exibir nenhum dado do filho antes de a escolinha confirmar o vínculo do responsável (RF-ACS-002; RF-LGP-003). |
| `BL-M10-12` | Mediação obrigatória do responsável | Garantir que nenhuma comunicação do sistema seja dirigida ao aluno menor de idade (RF-LGP-007). |

### S7 — Carga inicial da escolinha piloto  ·  5 itens

| ID | Entregável | O que precisa fazer |
|---|---|---|
| `BL-M12-01` | Envio e leitura da planilha de importação | Receber o arquivo, interpretar o conteúdo e mapear colunas para os campos de aluno e responsável (RF-ONB-001). |
| `BL-M12-02` | Validação e classificação das linhas | Classificar cada linha como apta, com aviso ou bloqueada, com o motivo do bloqueio (RF-ONB-001). |
| `BL-M12-03` | Deduplicação de responsáveis na importação | Reaproveitar responsável já existente e criar apenas o novo vínculo, sem duplicidade (CA-ALU-01, RF-ALU-003). |
| `BL-M12-04` | Tela de conferência e confirmação do lote | Apresentar novos, reaproveitados, vínculos e bloqueados, e gravar somente após confirmação explícita (RF-ONB-001). |
| `BL-M12-05` | Restrição de dado sensível na importação | Aplicar RF-LGP-003 às observações médicas exibidas na conferência e gravadas no cadastro. |
## 11. Critérios de aceite

Estes cinco fluxos são o teste de aceitação do conjunto. Escreva teste automatizado para cada um. Eles não são sugestão: são a definição de que o sistema funciona.

**Matrícula**

1. Ao cadastrar um segundo filho para um responsável já existente, o sistema reaproveita o cadastro e não cria duplicidade.
2. Ao desligar um aluno, data e motivo são obrigatórios e a recorrência de cobrança daquela turma é encerrada automaticamente.
3. Um aluno desligado some das listas de chamada e de convocação, mas permanece no histórico do período em que esteve ativo.

**Alerta de evasão**

4. Com a regra "3 faltas no mês", o alerta dispara no registro da terceira falta do mês corrente.
5. O mesmo aluno não gera dois alertas dentro da janela anti-duplicidade.
6. O gestor vê, na ficha do aluno, qual alerta foi disparado, quando e para qual responsável.

**Financeiro**

7. Confirmado o pagamento no Asaas, o estado muda sem intervenção humana e a régua para na mesma hora.
8. Um responsável que pagou não recebe cobrança posterior daquela mensalidade em nenhuma hipótese.
9. Pagamento parcial ou divergente fica sinalizado e não é conciliado automaticamente.

**Convocação de fim de semana**

10. O professor monta 4 eventos com 12 convocados cada e dispara todas as convocações em um único fluxo.
11. Cada responsável recebe uma mensagem por convocado, com data, hora, local com link de mapa e a pergunta de confirmação.
12. A resposta dada no WhatsApp aparece no painel do professor sem que ele atualize a tela.
13. Um responsável com dois filhos convocados para eventos diferentes recebe as duas convocações de forma distinguível.
14. Cancelado o evento, todos os convocados são avisados e as convocações passam a `cancelada`.

**Responsável com dois filhos**

15. Uma única conta mostra os dois filhos, com pagamentos, convocações e presenças atribuídos corretamente.
16. O responsável vê o valor total devido no mês e o detalhamento por filho.

**Condições que valem para o sistema inteiro**

17. Nenhuma funcionalidade essencial existe apenas no app.
18. Nenhum parâmetro de regra exige nova entrega de software para mudar.
19. Nenhuma comunicação automática é dirigida ao aluno menor de idade.
20. Nenhum envio automático existe fora do catálogo de gatilhos da seção 8.3.
21. A chamada funciona sem sinal, e o registro da quadra prevalece.

---

## 12. Decisões, pendências e registro

### 12.1 O que o agente decide sozinho

Decida e siga, registrando a decisão em uma linha:

- Qualquer escolha de implementação coberta pelo padrão do projeto principal.
- Nomenclatura de módulo, arquivo, rota e campo, conforme a convenção herdada.
- Estrutura de tela e navegação, desde que respeite R10 na quadra.
- Valor **inicial** de parâmetro configurável, usando os padrões sugeridos deste documento — são valores de partida, não regra.
- Ordem interna de construção dentro de uma fatia.
- Estratégia de teste, dentro da ferramenta já usada no projeto.

### 12.2 O que exige decisão humana antes de implementar

Pare, registre e pergunte. Não invente:

| Pendência | Por que trava | Encaminhamento provisório |
|---|---|---|
| **Duração padrão do evento** | Sem término não há detecção de conflito de agenda | Adote uma duração padrão por modalidade, editável, e sinalize a pendência |
| **Categoria de WhatsApp de cada gatilho** | Categoria errada faz a mensagem ser recusada pela política | Classifique os gatilhos da seção 8.3 e submeta para validação antes de aprovar templates |
| **Quem responde quando há dois responsáveis habilitados** | Define o resultado da confirmação | Vale a primeira resposta; o professor pode sobrescrever |
| **Falta justificada conta para o alerta de evasão** | Muda a regra de disparo | Contar em separado e não disparar alerta |
| **Faixas mínima e máxima dos parâmetros numéricos** | Permite configuração que inviabiliza a operação | Usar os padrões deste documento como faixa inicial |
| **Comportamento definitivo ao zerar créditos** | Depende de definição comercial | Aplicar o fallback da seção 8.2 |
| **Retenção de dados após o desligamento do aluno** | Obrigação legal, afeta o que pode ser apagado | Não apagar nada; marcar como desligado e aguardar definição |
| **Tratamento do atleta de 16 e 17 anos** | Acesso e comunicação do adolescente | Manter mediação integral pelo responsável (R4) |

### 12.3 Desvios da herança

Se precisar introduzir biblioteca, serviço ou padrão que o projeto principal não usa, registre antes de usar: o que motivou, o que foi considerado, por que a solução existente não serve, e qual o custo de reverter. Sem esse registro, não introduza.

### 12.4 Registro de progresso

Mantenha atualizado, no formato que o projeto principal já usa para documentação:

- **Entregáveis concluídos**, por identificador `BL-Mxx-nn`.
- **Decisões tomadas** (12.1 e 12.3), datadas.
- **Pendências abertas** (12.2), com o que está bloqueado por cada uma.
- **Desvios da especificação**, se houver, com justificativa.

---

## 13. Qualidade

**Testes obrigatórios.** Toda regra de negócio deste documento precisa de teste automatizado — em especial as máquinas de estado (seção 6), a matriz de permissões (seção 7), a conciliação e a não-cobrança de quem já pagou (seção 8.1), e os 21 critérios da seção 11.

**Dados de teste.** Crie uma carga que reproduza o cenário real: uma escolinha, três turmas, cerca de 60 alunos, alguns com dois responsáveis, irmãos na mesma escolinha, quatro eventos no mesmo fim de semana com 12 convocados cada, mensalidades em estados diferentes (paga, vencida, isenta, parcial). É sobre esse cenário que os critérios de aceite rodam.

**Segurança e privacidade.** Segredo nunca no código. Dado sensível com acesso restrito por papel e escopo (R5). Registro de quem alterou o quê nas ações sensíveis. Nenhum dado de menor exposto em superfície pública.

**Resiliência.** Indisponibilidade de Asaas ou de WhatsApp não pode perder informação nem duplicar efeito. Toda operação que sai do sistema precisa ser reprocessável e idempotente.

**Desempenho onde importa.** A tela de chamada e a de convocação são usadas em pé, na quadra, muitas vezes com conexão ruim. São os dois lugares onde lentidão mata o produto.

---

## 14. Documentos de origem

Este documento é autossuficiente para executar o escopo mínimo. Quando precisar de mais profundidade em algum ponto, os documentos de origem do produto são:

| Precisa de | Documento |
|---|---|
| Por que o produto faz o que faz; visão das três fases; os 179 requisitos numerados | **Especificação Funcional v2.2** |
| Fluxo passo a passo, campos e obrigatoriedade, regras detalhadas, exceções e critérios de aceite por módulo | **Especificação Funcional e Técnica** — 16 módulos |
| O recorte completo da Fase 1 (174 itens) | **Escopo do MVP — Fase 1** |
| O recorte mínimo que está sendo executado aqui (81 itens) | **Escopo Mínimo — Piloto da Fase 1** |

Os identificadores `RF-`, `BL-`, `CA-` são os mesmos em todos eles.

---

## 15. Glossário

| Termo | Significado |
|---|---|
| **Escolinha** | Conta principal. Agrupa turmas, alunos, professores e finanças |
| **Turma** | Grupo de alunos com modalidade, categoria, horário, local, professor e mensalidade próprios |
| **Sessão de treino** | Ocorrência datada do horário da turma; é sobre ela que a presença é registrada |
| **Evento** | Compromisso datado além do treino. Nesta versão, sempre um jogo |
| **Convocação** | Chamada de um aluno específico para um evento. Uma por convocado |
| **Responsável** | Adulto vinculado ao aluno; pode ser financeiro, legal ou ambos |
| **Régua de cobrança** | Sequência automática de mensagens em torno do vencimento |
| **Crédito de mensagem** | Unidade consumida a cada mensagem de WhatsApp enviada |
| **Perfil de modalidade** | Conjunto de recursos ativos: adversário, placar, convocação, graduação |
| **Modo parcial** | Operação enquanto nem todos os responsáveis ativaram o acesso |

---

**Fim das instruções.** Comece pela seção 0, passo 1: inventarie a herança do projeto principal e publique o registro antes de iniciar a fatia S0.
