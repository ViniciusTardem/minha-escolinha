# PLANO — Minha Escolinha

Plano vivo, atualizado ao fim de cada fatia. Cada documento responde por uma coisa:

| Pergunta | Documento |
|---|---|
| **O que** construir e quais regras valem | A spec, em [`especificacao/`](especificacao/) (81 entregáveis, R1–R12, 21 critérios de aceite) |
| **Como** construir | A herança do AlternativaDepartamentoPessoal ([`HERANCA.md`](HERANCA.md)) |
| **Em que ordem** e onde estamos | Este plano |

Última atualização: **28/09/2026**. **As 8 fatias do piloto estão prontas: os 81 entregáveis.** F0, S0 a S7 e os extras EX-01 e EX-02 estão commitados na `main` (o S7 em `a262578`, pronto sem a planilha real). Nada foi enviado a remoto. Depois do S7: a guarda de 90 dias dos webhooks (P22, D96), a chamada sem sinal validada nesta máquina (P37) e a operação documentada em [`operacao/`](operacao/) (runbook, script do banco e roteiro de homologação). O que falta depende de fora: um servidor com HTTPS público, as contas (da Meta para o S2 e o S5, P34; da Asaas para o S4 e o S6, P41), a planilha da escolinha (P21) e as decisões da §8.

---

## 0. Ordem de leitura ao começar uma sessão

1. [`../CLAUDE.md`](../CLAUDE.md): regras, convenções e comandos.
2. Este plano: status (§3) e próximos passos (§4).
3. [`BACKLOG.md`](BACKLOG.md): o que está feito, item por item.
4. [`DECISOES-EM-ABERTO.md`](DECISOES-EM-ABERTO.md): decisões, desvios, pendências e valores provisórios.
5. A spec: as seções da fatia atual e as regras da seção 4.
6. Antes de qualquer tela: [`design/GUIA-DESIGN-SYSTEM.md`](design/GUIA-DESIGN-SYSTEM.md) e [`design/MAPA-DE-TELAS.md`](design/MAPA-DE-TELAS.md).
7. Antes de introduzir biblioteca ou padrão novo: [`HERANCA.md`](HERANCA.md). Se o DP já resolve, use a solução dele; se não, registre o desvio (12.3).

---

## 1. Método de trabalho (obrigatório em toda tarefa)

1. **Entender:** ler a spec da fatia, as regras aplicáveis e o código envolvido.
2. **Planejar:** perguntar só o que muda a implementação. O resto decide-se pelo padrão do DP e fica registrado (12.1).
3. **Implementar cirúrgico:** um entregável `BL-Mxx-nn` por vez, na ordem da fatia, com o teste das regras de negócio que ele introduz.
4. **Validar:**
   - `dotnet build` sem aviso;
   - `dotnet test`, inclusive os testes com banco;
   - `npm run build` com 0 classes faltando;
   - o fluxo percorrido no navegador em 1280, 375 e 320 px, no claro e no escuro;
   - o critério de pronto da fatia demonstrado.
5. **Documentar na mesma entrega:** BACKLOG, DECISOES, este plano e o guia visual, quando mudarem.
6. **Entregar:** relatório da seção 2.4 da spec (entregáveis concluídos, decisões, pendências e o que a próxima fatia precisa). **Commit e push só com autorização explícita**, numa branch por fatia.

**Nunca sem aprovação humana explícita** (spec §2.3):
- mandar WhatsApp real para número que não seja de teste;
- emitir cobrança real em produção;
- rodar migração destrutiva;
- introduzir dependência ou padrão que o DP não usa sem registrar em 12.3;
- alterar a semântica de uma regra R1–R12;
- construir algo da lista fora de escopo (§1.5).

---

## 2. Registro de decisões

O registro completo, com data, está em [`DECISOES-EM-ABERTO.md`](DECISOES-EM-ABERTO.md). As decisões estruturais:

| Tema | Decisão | Ref. |
|---|---|---|
| Runtime | .NET 9, igual ao DP (o servidor só tem o 9). O suporte termina em 10/11/2026: subir para o 10 antes disso. | D02, P24 |
| Multi-escolinha | `Escolinha_ID` + filtro global do EF Core, fechado por padrão, + interceptor que valida as gravações. `IgnoreQueryFilters` só nos arquivos da lista permitida (teste). | X01 |
| Identidade | `Pessoa` + `Credencial` globais; `VinculoAtuacao` (papel × situação) por escolinha; os vínculos são relidos a cada requisição | D04, D07 |
| Circularidade do S0 | A equipe da plataforma cria a escolinha e o 1º gestor numa gravação só; a senha temporária aparece uma vez | D05 |
| Permissões | Matriz da §7 em código, negação por padrão, toda action declara o acesso (teste) | D08 |
| Parâmetros (R7) | Catálogo em código (26 definições) + linhas chave-valor, leitura tipada, histórico | D09, D10 |
| Auditoria | Uma tabela genérica `RegistroAlteracao` para toda ação sensível | D11 |
| Núcleo genérico (R8) | Perfil de modalidade + interruptores; `TipoEvento` e `PoliticaPerfil` no domínio | D12, D13 |
| Conta Asaas | Chave cifrada com Data Protection, final visível, troca pede a senha | D14, X04 |
| Front | Metronic 9 + KTUI + Tailwind 4.1.8 + KeenIcons filled, prefixo `esc-`, marca provisória | D17 |
| Formulários | Erro com campo (`aria-invalid`), assistente abre na etapa do erro, coleção em action com nome de binding | D19, D20 |
| Mensagens (R3) | Motor único: o módulo chama o emissor, a mensagem é gravada com o negócio (outbox), e o envio revalida a origem | D45, D46 |
| Mensalidades (R1) | Recorrência própria pela matrícula; emissão na Asaas por job; conciliação por webhook, varredura e revalidação no envio; divergência só sinaliza | D66 a D72 |
| Convocação (R8) | Evento genérico com turmas N:N; uma convocação por aluno e jogo, com rascunho até o disparo em lote; a primeira resposta vale; tempo real pelo SignalR no padrão do DP | D76 a D79 |
| Portal (R2, R4) | `/Portal` do responsável, só com os filhos de vínculo confirmado; a resposta com a mesma regra do WhatsApp; mensalidades só para quem cuida delas; Pix copia e cola buscado por job | D87 a D91 |
| Carga inicial | Planilha `.xlsx` (ClosedXML, herança do DP) → mapeamento das colunas → conferência pura, refeita na confirmação → gravação única; responsáveis pelo CPF; a saúde fora da conferência e a planilha cifrada até o fim | D92 a D95 |

---

## 3. Roadmap e status

| Fatia | Itens | Status | Data |
|---|---:|---|---|
| Arranque (spec §0): herança inventariada; sem credenciais de Asaas e WhatsApp, então as integrações nascem em modo Simulado; plano publicado | — | **Pronto** | 26/09/2026 |
| F0 — Esqueleto vivo (template §13) | — | **Pronto** | 26/09/2026 |
| Design system (4 superfícies + vitrine `/dev/design`) | — | **Pronto** | 26/09/2026 |
| S0 — Fundação: conta, identidade e permissões | 12 | **Pronto** e commitado (`a444b48`). O parcial (`BL-M02-12`) fechou no S1, com a Turma. | 26/09/2026 |
| Extra EX-01 — Compra de pacotes de créditos pelo Stripe da LDNA (X08) | — | **Pronto** no modo Simulado e commitado (`3c28d97`); falta configurar as chaves de teste | 26/09/2026 |
| S1 — Cadastro: turma, sessões, aluno, responsável, equipe | 20 | **Pronto** e commitado (`b6a4c3f`), com os 20 concluídos: o `BL-M04-06` fechou no S2 e o `BL-M03-08` no S4 (recorrência no desligamento). Com o extra EX-02 (alerta de quadra ocupada). | 27/09/2026 |
| S2 — Mensageria e acesso das famílias | 14 | **Pronto no simulador** e commitado (`c642f2f`), com os 14 concluídos. A prova com um número real espera a conta na Meta (P34). | 27/09/2026 |
| S3 — Presença e controle de evasão | 6 | **Pronto** e commitado (`c6bdbc4`), com os 6 concluídos. A reabertura da chamada sem sinal pelo service worker espera um navegador que confie no certificado e o teste em aparelho real (P37). | 27/09/2026 |
| S4 — Financeiro: mensalidade, régua e conciliação | 8 | **Pronto no simulador** e commitado (`44df87a`), com os 8 concluídos. A prova com a sandbox da Asaas espera a conta (P41). | 28/09/2026 |
| S5 — Eventos e convocações | 10 | **Pronto no simulador** e commitado (`ec42cc3`), com os 10 concluídos. A resposta de uma família real pelo WhatsApp espera a conta na Meta (P34). | 28/09/2026 |
| S6 — Portal do responsável | 6 | **Pronto no simulador** e commitado (`f9699c8`), com os 6 concluídos. O pagamento na sandbox da Asaas espera a conta (P41). | 28/09/2026 |
| S7 — Carga inicial da escolinha piloto | 5 | **Pronto** e commitado (`a262578`), com os 5 concluídos, sem a planilha real: o modelo e o mapeamento das colunas a recebem sem código novo (P21). | 28/09/2026 |

| Depois do S7 — pendências decididas pelo responsável e os 4 itens adiados liberados (EX-03 a EX-07) | — | **Pronto** e commitado em blocos (`1b93862`, `6d8c103`, `1447f10`, `bdd6716`, `311f7c5`, `0a3793e`, `1896994` e o bloco final), com as decisões D97 a D110 | 28/09/2026 |

Os critérios de aceite, com a fatia em que cada teste nasce, estão no fim do [`BACKLOG.md`](BACKLOG.md).

---

## 4. Próximos passos

### 4.0 Agora
1. **Carregar a base real da escolinha piloto** (P21), assim que a planilha chegar: baixar o modelo em Importação (ou usar a planilha como está), enviar, mapear as colunas, conferir e confirmar. Depois, confirmar as primeiras mensalidades e mandar os convites em lote (§4.1). Se a planilha trouxer um campo que o modelo não tem, é a P54.
2. **Subir um servidor de homologação** com HTTPS público, pelo [`operacao/RUNBOOK.md`](operacao/RUNBOOK.md): o banco pelo script de `operacao/sql`, a publicação self-contained, as variáveis de ambiente, o certificado do Data Protection e o keep-alive. É o que destrava os itens 3 a 5: os webhooks da Asaas e da Meta só chamam endereço https público, e o service worker da chamada só registra com certificado válido. Depois, percorrer o [`operacao/ROTEIRO-HOMOLOGACAO.md`](operacao/ROTEIRO-HOMOLOGACAO.md).
3. **Provar o S4 com a sandbox da Asaas** (P41), assim que houver a conta sandbox e um endereço https público. O passo a passo, conferido na documentação da Asaas, está no runbook (§6.1):
   - gravar `Asaas:Modo = Api` e `Asaas:UrlPublica` (user-secrets ou variáveis de ambiente);
   - cadastrar a chave Pix na conta sandbox e vincular a chave de API na tela Conta de recebimento; o webhook é cadastrado sozinho;
   - confirmar uma mensalidade, esperar a emissão, pagar sem dinheiro (`confirmPaymentReceipt`) e conferir a conciliação e a régua parada.
4. **Provar o S2 e o S5 com um número real** (P34), assim que a LDNA tiver:
   - a conta WhatsApp Business na Cloud API;
   - a URL pública em HTTPS para o webhook;
   - os templates aprovados;
   - os números de teste.

   O passo a passo da conta na Meta está no runbook (§6.2), e os 22 textos decididos (D97), em [`operacao/TEMPLATES-WHATSAPP.md`](operacao/TEMPLATES-WHATSAPP.md). Com isso, grave em user-secrets os comandos do CLAUDE.md §7 (`WhatsApp:Modo = Api`, as 4 chaves, `WhatsApp:NumerosTeste` e `WhatsApp:UrlPublica`). Então:
   - convide um número de teste;
   - responda pelo celular;
   - confira a resposta e o log em Mensagens;
   - convoque um aluno cujo responsável tenha o número de teste, toque em "Vai" no celular e confira o painel do jogo mudando sozinho.
5. **A chamada sem sinal num Android e num iPhone** (P37), no servidor com HTTPS (roteiro de homologação §5). Nesta máquina ela já foi validada, com um Chrome que aceita o certificado de desenvolvimento (§9), e virou teste de navegador (`ChamadaSemSinalNavegadorTestes`, D108): rode-o contra o servidor com as variáveis `ESC_E2E_*`.
6. **Stripe em modo de teste**, quando o responsável quiser sair do simulador:
   - criar um sandbox próprio no painel do Stripe (P27);
   - gravar em user-secrets `Stripe:Modo = Teste` e `Stripe:ChaveSecreta` (chave de teste, `sk_test_` ou `rk_test_` restrita);
   - para os webhooks em localhost, rodar o Stripe CLI (`stripe listen --forward-to https://localhost:7186/api/v1/webhooks/stripe`) e gravar o `whsec_` que ele mostrar em `Stripe:SegredoWebhook`. Sem isso, a volta do pagamento e a varredura confirmam as compras.
### 4.1 O roteiro de entrada da escolinha piloto
Com os 81 entregáveis prontos, a entrada da escolinha segue esta ordem, cada passo numa tela que já existe:
1. **A equipe da plataforma** cria a escolinha com o 1º gestor e concede créditos de mensagem (S0).
2. **A gestão** cadastra os dados, a modalidade, os locais, os professores e as turmas, e ajusta os parâmetros; os valores provisórios estão marcados (S0, S1, P05).
3. **A gestão vincula a conta Asaas** da escolinha em Conta de recebimento (S0, P41).
4. **A carga inicial** da planilha: enviar, mapear, conferir e confirmar (S7).
5. **As primeiras mensalidades** de cada matrícula, confirmadas em Mensalidades (S4, D66).
6. **Os convites em lote** às famílias e aos professores, em Convites (S2); a fila de vínculos confirma quem ativou (D53).
7. **A rotina:** chamada na quadra (S3), régua de cobrança (S4), convocações (S5) e o portal das famílias (S6).

Antes do piloto de verdade: as contas da Meta e da Asaas (P34, P41), o servidor com HTTPS público e o certificado do Data Protection (CLAUDE.md §8), e a revisão jurídica dos termos (P29).

### 4.2 O que o S7 entregou (referência)
- **A carga inicial por planilha** (`/Importacao`): enviar o `.xlsx`, mapear as colunas (o cabeçalho sugere, e o modelo é reconhecido inteiro), conferir e confirmar. Nada entra antes da confirmação, e a gravação é de uma vez só (D92).
- **A conferência** como função pura: cada linha apta, com aviso ou bloqueada, com o motivo; o quadro do que a planilha não resolve (turma de outro nome, responsável parecido, aluno parecido). Ela é refeita na confirmação, com a base relida (D93).
- **Responsáveis sem duplicidade:** pelo CPF, na planilha e no cadastro; os irmãos juntos pelo nome e celular de uma linha que trouxe o CPF (D94, CA-1).
- **Dado sensível:** a saúde fora da conferência e do registro; a planilha cifrada enquanto o lote está aberto e apagada no fim; os consentimentos com a declaração da gestão (D95).
- **O modelo para baixar,** com as instruções e as turmas ativas. O ClosedXML é herança do DP.
- **Testes:** 420 passando, 14 deles do S7 (9 de unidade e 5 com banco).

### 4.3 O que o S6 entregou (referência)
- **O portal do responsável** (`/Portal`), no celular e no computador, sem instalar nada: Início, Jogos, Pagamentos, Avisos e as presenças de cada filho (D87).
- **Uma conta, todos os filhos** (CA-15): só os filhos com vínculo confirmado; antes disso, nenhum nome, jogo, mensalidade nem presença (BL-M10-11).
- **A resposta pelo portal** (BL-M10-07), com a regra do WhatsApp: vale a primeira da família, quem respondeu muda a própria resposta, e a registrada pela escolinha não muda. O painel do jogo aberto é avisado (D88).
- **Pagamentos** (CA-16): o total do mês com o detalhamento por filho, só para quem cuida das mensalidades (D89).
- **Pagar pelo portal** (BL-M10-06): Pix copia e cola, buscado por job depois da emissão e de novo pela rotina, e a fatura da Asaas para boleto e cartão (D90).
- **Presenças** só leitura (D91).
- **Testes:** 406 passando, 8 deles do S6 (3 de unidade e 5 com banco).

### 4.4 O que o S5 entregou (referência)
- **O evento genérico:** tipo e adversário pelo perfil da modalidade (R8), turmas N:N, duração da modalidade, local e professor da turma por padrão, situações da §6 com transições manuais (D76).
- **Montagem do fim de semana numa tela:** os jogos da semana, a escolha dos convocados como rascunho, com os sinais (trial, risco, atraso, sem quem receba mensagem, outro jogo no horário), e o disparo de todos numa gravação só (D77, CA-10).
- **Mensagem por convocado,** com o nome do filho, o jogo, o dia e a hora, o local e o link de mapa, e os botões "Vai" e "Não vai" (D85, CA-11, CA-13). Janela de disparo, e o aviso de quando as mensagens saem.
- **Resposta:** pelo botão, com a primeira valendo e a outra visível (P03); por fora, com autoria; "sem resposta" no início do jogo (P19) (D78).
- **Painel em tempo real** pelo hub `/hubs/convocacoes`, no padrão do DP, só para a gestão e os professores (D79, CA-12).
- **Lembrete** a quem não respondeu, pela rotina de 15 minutos (D80).
- **Alterar, cancelar e adiar,** com aviso por família e o registro campo a campo (D81, CA-14).
- **Escopo** do professor pelas turmas do jogo (D82); **alertas de agenda** do jogo (D83); **desligamento** tira das próximas convocações (D84, CA-3).
- **Testes:** 398 passando, 24 deles do S5 (13 de unidade e 11 com banco). Os avisos de consulta do EF viraram erro nos testes (D86).

### 4.5 O que o S4 entregou (referência)
- **Recorrência pela matrícula:** previstas até o fim do mês seguinte, sem cobrar para trás; trial e pausa não pagam; a primeira de cada matrícula espera a gestão (D66).
- **Emissão na Asaas:** um job por mensalidade na data de emissão, na conta da escolinha e no nome do pagador; a família escolhe Pix, boleto ou cartão; multa e juros congelados; sem duplicar; a impedida mostra o motivo, e a recusada volta com "Tentar de novo" ou com a conta vinculada de novo (D67, D73).
- **Painel de pagamentos** por mês, com os grupos provisórios (P40), a aba Financeiro na ficha e o cartão no painel da gestão (D68).
- **Régua** com tom crescente, um degrau por vez, pelo motor, com o link da fatura (D69).
- **Conciliação:** webhook por escolinha com token, corpo bruto e job; valor original e data do pagamento; varredura de hora em hora; revalidação na hora do envio (CA-8) (D70).
- **Divergências:** sinalizam sem mudar a situação; a fila resolve com explicação e autor; recebimento por fora (D72).
- **Desligamento:** as previstas depois da data saem canceladas, e o débito emitido fica sinalizado (D71, CA-2).
- **Acesso:** o financeiro é só do gestor, em todos os caminhos (D74).
- **Integração:** cliente próprio da API, trava de ambiente e simulador `/dev/asaas` (D75).
- **Testes:** 374 passando, 36 deles novos no S4. Entre eles:
  - CA-2, CA-7, CA-8 e CA-9;
  - o consumidor `financeiro.*` (CA-18);
  - o contrato HTTP da Asaas e a trava de ambiente;
  - o professor sem acesso ao financeiro.

### 4.6 O que o S3 entregou (referência)
- **Chamada na quadra** (`/Chamada`):
  - o treino do dia e a chamada esquecida dos últimos 7 dias;
  - P, F ou J num toque, "Todos presentes" e "Concluir chamada" na barra inferior (D58).
- **Sem sinal** (X07):
  - fila no aparelho com UUID e sequência por marcação;
  - o servidor aplica cada marcação uma vez só, e o registro da quadra prevalece (D59);
  - service worker com as páginas da chamada; a página guardada não traz motivo de saúde (D60).
- **Alerta de faltas:** no mês ou em sequência, avaliado a cada falta, na mesma gravação; janela anti-duplicidade por aluno; a falta corrigida recolhe o aviso e anula o alerta (D61, D62).
- **Risco de evasão:** frequência na janela contra o mínimo da escolinha; selo na ficha e na lista, e cartão no painel (D63).
- **Saúde:** o motivo de saúde fica só na presença, visível para a gestão e o professor da turma (D64).
- **Ficha do aluno:** aba Presença com a frequência, as presenças e os alertas, com os destinatários e o estado das mensagens (CA-6).
- **Defeito do S1 corrigido:** o horário novo da turma gerava sessões em dobro (D65).
- **Testes:** 338 passando, 19 deles novos no S3. Entre eles:
  - CA-4, CA-5, CA-6 e CA-21, e o CA-3 na chamada;
  - o consumidor `presenca.*` (CA-18);
  - a restrição do motivo de saúde.

### 4.7 O que o S2 entregou (referência)
- **Motor único de mensagens** (R3):
  - catálogo fechado de 11 gatilhos, com uma regra por gatilho;
  - outbox na mesma gravação do negócio, despacho depois do commit e varredura a cada 5 min (D45);
  - na hora do envio, revalida a origem, respeita a janela de silêncio (com a exceção das urgentes das próximas 24 h), reserva o crédito e retém ou suprime sem saldo (D46).
- **Canais:**
  - WhatsApp sempre, pela Cloud API oficial sem SDK, com trava de ambiente e simulador `/dev/whatsapp` (D49);
  - aviso no portal para quem ativou o acesso e tem os vínculos confirmados (D47).
- **Log e retorno:**
  - tela Mensagens, com filtros, e o detalhe com a linha do tempo, o motivo e as respostas;
  - webhook assinado, com o índice global pelo id da Meta e pelo celular (D50);
  - o botão volta ao módulo de origem.
- **Templates:** 13 textos da plataforma, em rascunho até a Meta aprovar (D48, P02).
- **Acesso das famílias:**
  - convite de uso único, em lote ou um a um; reenviar e cancelar (D51);
  - ativação pelo CPF, com login pelo e-mail ou pelo CPF (D52);
  - fila de confirmação dos vínculos (D53);
  - portal com os filhos confirmados e os avisos; quem é só responsável vai direto para ele (D54).
- **Painel:** mostra as pendências da gestão (vínculos a confirmar, mensagens retidas, saldo baixo) (D55).
- **Testes:** 319 passando, 61 deles novos no S2. Entre eles:
  - CA-17, CA-19 e CA-20;
  - o consumidor `mensagem.*` (CA-18);
  - isolamento A/B das mensagens e dos convites;
  - o teste de arquitetura: só o emissor cria mensagem e só o motor envia.

### 4.8 O que o S1 entregou (referência)
- **Turmas e agenda:**
  - locais e professores, com disponibilidade semanal e indisponibilidades;
  - turma em etapas, com os valores iniciais do catálogo;
  - sessões num horizonte rolante, geradas na gravação e pelo job diário das 03:15 (D31);
  - agenda por turma, professor ou local, com os alertas de conflito do professor e de quadra ocupada (R9: sinaliza e não bloqueia; D44).
- **Alunos e famílias:**
  - aluno novo em 5 etapas, com deduplicação de responsável (CPF reaproveita; celular e nome sugerem) e de aluno;
  - vínculos com "recebe cobrança", "recebe notificação" e um pagador por aluno;
  - matrícula por turma, com valor e datas próprios;
  - situação pela máquina da §6, mais o atalho inativo → desistente (D43), com o trial vencido só sinalizado;
  - desligamento com data e motivo, que encerra as matrículas e preserva o histórico;
  - consentimentos separados, com versão do termo e revogação;
  - saúde restrita por papel (R5).
- **Escopo do professor** em menu, listas, fichas, busca e agenda, inclusive irmãos e marcas financeiras (D41).
- **Massa de demonstração** em `/dev/massa` (D40): 3 turmas e 60 alunos em 38 famílias.
- **Testes:** 258 passando, 73 deles do cadastro (domínio e banco). Entre eles:
  - CA-1, e CA-2 e CA-3 na parte do S1;
  - o consumidor de parâmetro de turma (CA-18);
  - escopo do professor e isolamento A/B;
  - saúde fora do registro de alterações.

### 4.9 O que o S0 entregou (referência)
- **Criação da escolinha pela plataforma:** assistente em 3 etapas (escolinha → modalidade → 1º gestor). Grava tudo junto: escolinha, modalidade, os 26 parâmetros, conta de créditos, gestor e auditoria. A senha temporária aparece uma vez.
- **Acesso:**
  - login com troca obrigatória de senha e bloqueio;
  - escolha de escolinha para quem atua em mais de uma;
  - "entrar como equipe", auditado;
  - área da plataforma com lista, créditos e `/jobs`.
- **Telas do gestor:**
  - painel com os passos de configuração e as próximas entregas;
  - dados da escolinha e modalidades (perfil + interruptores);
  - parâmetros em 5 abas, com selo de provisório e histórico em pt-BR;
  - conta de recebimento Asaas (sandbox, senha, chave cifrada) e créditos.
- **Isolamento:** filtro + interceptor + contexto por requisição. Demonstrado no navegador: 404 pela URL de outra escolinha, 403 nas áreas da plataforma.
- **Testes:** 132, entre eles:
  - a matriz de permissões inteira contra a §7;
  - R7 → catálogo;
  - isolamento A/B;
  - criação atômica;
  - regras de arquitetura: relógio, `IgnoreQueryFilters`, acesso declarado, binding de coleções.

### 4.10 O que o F0 entregou (referência)
- **Base do projeto:**
  - solução de 4 camadas + testes, em .NET 9;
  - Central Package Management, com o prefixo `MinhaEscolinha` nas DLLs;
  - EF Core 9 com migration no startup;
  - Hangfire com as filas `esc_*` e `/health` (banco + Hangfire).
- **Segurança e operação:**
  - Serilog em arquivo diário;
  - CSP estrita e rate limit no login;
  - chaves do Data Protection no banco (certificado obrigatório em Production);
  - seed do 1º administrador da plataforma por user-secrets.
- **Front:**
  - pipeline `npm run build`: recorte do Metronic, Inter local, SignalR, Tailwind e verificador de classes;
  - 4 layouts: autenticação, painel, quadra e portal;
  - TagHelpers `<selo-status>`, `<cabecalho-pagina>` e `<estado-vazio>`;
  - vitrine `/dev/design`.

---

## 5. Depois do piloto

As 8 fatias da Fase 1 (S0 a S7) estão prontas. O que a spec deixou fora do piloto (§1.5: ligas, campeonatos, marketplace, dashboard consolidado, relatórios exportáveis, entre outros) e os itens adiados do backlog (a visão única dos filhos, a remarcação de treino, a recuperação de senha, a alternância de escolinha) não entram sem aprovação (§1). As decisões pendentes estão na §8 e em [`DECISOES-EM-ABERTO.md`](DECISOES-EM-ABERTO.md).

**Massa de demonstração**, construída aos poucos (spec §13): 1 escolinha, 3 turmas, cerca de 60 alunos, irmãos, alunos com 2 responsáveis, 4 jogos no fim de semana com 12 convocados cada, e mensalidades pagas, vencidas, isentas e parciais. **O S1 montou a base** (turmas, alunos e famílias, em `/dev/massa`). No S4, as mensalidades nascem da projeção das matrículas da massa ("Atualizar agora"), e o simulador da Asaas produz as pagas, as parciais e os estornos. Os jogos entram no S5.

---

## 6. Fontes para portar (AlternativaDepartamentoPessoal)

Base: `C:\Users\vinic\Documents\Projetos\.NET\AlternativaDepartamentoPessoal\`. O que já foi portado está listado em [`HERANCA.md`](HERANCA.md). Padrões a **seguir** (sem copiar) nas próximas fatias:

| Padrão | Onde está no DP | Fatia |
|---|---|---|
| Integração: opções Simulado/Api, trava de ambiente, simulador com página `/dev`, contrato HTTP com handler falso | ZapSign e GoHighLevel | Adotado no S2 (WhatsApp) e no S4 (Asaas) |
| Outbox + varredura de lembretes | `docs/integrador/08-MOTOR-LEMBRETES.md` | Adotado no S2 (motor) e no S4 (régua); S5 (lembrete da convocação) |
| Webhook: gravar o corpo bruto, responder 200, processar por job | ZapSign | Adotado no S2 (WhatsApp) e no S4 (Asaas) |
| Agendador em memória nos testes | `AgendadorDeTeste` | Adotado no S2 (`AgendadorMensageriaDeTeste`) |
| Tempo real: o servidor empurra `{id, versão}`; recuperação com `?desde=` | `AdmissoesHub` | S5 |
| Ficha lateral: drawer com versão e resposta 409 no conflito | `ficha.js` | Não adotado no S1: a ficha é página com abas; a versão de linha entrou sem o drawer (D37) |

---

## 7. Ambiente local

- **.NET:** o projeto usa o **SDK 9.0.100** (`global.json`, com `rollForward: latestFeature`), como o DP. Os runtimes 9 da máquina são o 9.0.0 do ASP.NET Core e o 9.0.5 do .NET.
- **SDK 10 também instalado:** o SDK 10.0.401 entrou pelo winget (o instalador pediu reinício do PC) e fica pronto para quando o projeto subir para o 10 (P24). Uma cópia dele no perfil do usuário (`%LOCALAPPDATA%\Microsoft\dotnet`) não é mais necessária e pode ser apagada.
- **Certificado HTTPS de desenvolvimento:** o SDK novo gerou um certificado ainda não confiado. Rode `dotnet dev-certs https --trust` uma vez; o Windows pede confirmação.
- **SQL Server:**
  - servidor `VINICIUSTARDEM\SQLEXPRESS`, banco `escolinhaesporte` (compatibilidade 150, AUTO_CLOSE desligado);
  - testes em `escolinhaesporte_Testes`, recriado a cada execução, com autenticação do Windows ou a conexão em `ESC_TESTES_SQL`.
- **Segredos** (user-secrets `minha-escolinha-web`): `ConnectionStrings:ConexaoPadrao`, `Seed:Administrador:Login`, `Seed:Administrador:Nome` e `Seed:Administrador:Senha`. As chaves do WhatsApp (modo Api) e do Stripe (modo Teste) só entram quando houver conta (CLAUDE.md §7). A Asaas não tem segredo de instalação: a chave é de cada escolinha, cifrada no banco (D14), e o modo Api pede só `Asaas:Modo` e `Asaas:UrlPublica`.
- **Endereços:** https://localhost:7186 (e http://localhost:5186); vitrine em `/dev/design`; simuladores em `/dev/whatsapp`, `/dev/stripe` e `/dev/asaas`; Hangfire em `/jobs`; saúde em `/health`.
- **Dados de validação no banco de desenvolvimento:**
  - as escolinhas "Escolinha Piloto Londrina" (A) e "Escolinha Bola na Rede" (B), cada uma com o seu gestor, criadas na validação do S0. As senhas não ficam em arquivo versionado;
  - em A, desde o S1: as turmas, os alunos e as famílias da validação e da massa de demonstração (`/dev/massa`);
  - em A, desde o S2: a responsável Ana Almeida ativou o acesso sem e-mail (login pelo CPF), e o vínculo com o Arthur foi confirmado. O treino de sábado, 03/10, da Sub-9 foi cancelado na validação, e o log tem as mensagens do convite e do cancelamento;
  - em A, desde o S3: a turma "Sub-11 · segunda e quarta" tem uma sessão realizada no domingo, 27/09, com a falta da Ana Lima, o alerta disparado e a mensagem à Maria Lima. Para a validação, o limite de faltas foi a 1 e voltou a 4, e um horário de domingo entrou e saiu da turma, tudo com histórico;
  - em A, desde o S4: a conta de recebimento está vinculada (Sandbox), com o simulador. As 53 mensalidades de outubro foram projetadas: 50 a confirmar, a da Alice Pereira impedida (pagador sem CPF), a do Antônio Moura paga com divergência aceita (R$ 100,00) e a do Anthony Pacheco cancelada depois do estorno. Os parâmetros ajustados para a validação (emissão, lembrete e janela de silêncio) voltaram pela tela, com histórico. O simulador guarda as faturas em `%LOCALAPPDATA%\MinhaEscolinha\simulador-asaas.json`;
  - para validar o escopo do professor com um login real, o login de gestão de B (`gestor.b@bolanarede.test`) também tem o papel Professor em A: a ficha de professor "Paula Souza" foi ligada a essa pessoa por SQL, e o vínculo Professor em A foi criado;
  - em A, desde o S7: a importação de uma planilha de validação criou 4 alunos (Helena e Theo Martins, Sophia Pacheco e Laura Dias), com a Cláudia Martins, o Roberto Martins e a Fernanda Dias; a Sophia entrou no cadastro da Sabrina Pacheco. As mensalidades de outubro deles estão a confirmar. Há dois envios na Importação, um importado e um descartado;
  - em A, desde o S6: a responsável Sabrina Pacheco ativou o acesso sem e-mail (login pelo CPF), com os vínculos do Anthony, do Benjamin e do Raul confirmados. As mensalidades de outubro do Benjamin (em aberto, com o Pix) e do Raul (paga pela fatura do simulador) foram emitidas. O Benjamin tem a resposta "Vai" pelo portal no jogo contra o Leões da Vila e a presença no treino de 28/09, às 19:00. A janela de silêncio e a emissão, mudadas para a validação, voltaram pela tela, com histórico;
  - em A, desde a validação da P37 (28/09): a presença do Caio Wagner na sessão de 28/09, às 19:00, da turma "Sub-13 · noite", marcada sem sinal pela gestão de A.

---

## 8. Pendências que dependem do responsável ou do cliente

| O que | Bloqueia | Ref. |
|---|---|---|
| A planilha real da escolinha piloto | A carga da base real. O S7 está pronto sem ela: o modelo segue os campos da §5 da spec, e a planilha real pede só o mapeamento das colunas; conferir se ela traz algo a mais (P54). | P21 |
| Conta WhatsApp Business da LDNA na Cloud API, URL HTTPS pública, templates aprovados e números de teste | A prova do S2 e do S5 com um número real e todo envio real | P14, P34 |
| Submeter os 22 templates à Meta, com os textos decididos (D97) e os exemplos de [`operacao/TEMPLATES-WHATSAPP.md`](operacao/TEMPLATES-WHATSAPP.md) | Envio real pelo WhatsApp | P34 |
| Conta de e-mail (SMTP) para o link de nova senha (`Email:*`, runbook §4.5) | Só o e-mail de nova senha: sem ele, a gestão gera o link na ficha | D107 |
| Conta Asaas sandbox (da escolinha piloto ou da LDNA, para o teste) e endereço https público; para produção, a conta da escolinha | A prova do S4 fora do simulador; a cobrança real | P21, P41 |
| Pacote de créditos (comercial) | Envio real (S2) | P06, P21 |
| Hospedagem com HTTPS público (o passo a passo está no [`operacao/RUNBOOK.md`](operacao/RUNBOOK.md)) | Webhooks do WhatsApp e da Asaas; a chamada sem sinal em aparelho real | P21, P37 |
| Identidade visual (logo, cor) | Trocar a marca provisória | D17 |
| Confirmar os valores provisórios dos parâmetros com a escolinha | Nada bloqueia; a tela marca os provisórios | P05 |
| Chaves de teste do Stripe (sandbox próprio) e, para produção, chave de produção + segredo do webhook | Sair do simulador (EX-01) | P27 |
| Definir o plano recorrente (preço, periodicidade, créditos incluídos) e os pacotes à venda | Cobrança do plano pelo Stripe; venda real dos pacotes | P25, P26 |
| A "Especificação Funcional e Técnica", se existir | Detalhe de campos por módulo | P21 |
| Revisão jurídica dos termos de consentimento (tratamento de dados e uso de imagem), incluindo o WhatsApp no termo de tratamento de dados | Produção; opt-in da Meta | P29, P15 |
| A chamada sem sinal num Android e num iPhone, no servidor com HTTPS (nesta máquina já foi validada) | A reabertura da chamada sem sinal no aparelho real (CA-21) | P37 |
| Confirmar com a planilha real: ela diz quem paga? Traz endereço ou outro campo fora do modelo? | Nada bloqueia; seguem os encaminhamentos provisórios | P52, P54 |
| Antes de 10/11/2026: publicar self-contained com o runtime 9 mais recente; depois dessa data o .NET 9 fica sem correção de segurança, e subir para o .NET 10 é decisão do responsável | Segurança do servidor | P24, D109 |

---

## 9. Registro de sessões

| Data | O que aconteceu |
|---|---|
| 26/09/2026 | **Arranque:** herança inventariada, spec copiada para `docs/especificacao`, Metronic copiado do Adama, docs antigos do DP removidos, plano aprovado. |
| 26/09/2026 | **F0, design system e S0:** construídos. Validação no navegador (Playwright) com as escolinhas A e B. |
| 26/09/2026 | **Defeitos achados na validação e corrigidos:** <ul><li>O salvar dos parâmetros falhava em 4 das 5 abas: o dicionário era ligado com prefixo vazio. Corrigido com nome de binding explícito e um teste de arquitetura (D20).</li><li>O assistente voltava para a etapa 1 depois de um erro, e a aba de parâmetros perdia o que tinha sido digitado. Corrigido com o erro ligado ao campo (D19).</li><li>A barra de ação da quadra vazava da tela em 375 px (elemento fixo, invisível à checagem de rolagem). Corrigido no `custom.css`.</li><li>O histórico de parâmetros mostrava o valor cru. Agora usa o formato pt-BR.</li></ul> |
| 26/09/2026 | **Revisão do guia visual (feita por agente, conferida no navegador):** <ul><li>Toast escapado (D23).</li><li>Mensagens de validação só em pt-BR (D24).</li><li>Estados sem enum passam pelo mapa de selos (D25).</li><li>Borda de erro no campo de senha.</li><li>Bloqueio de duplo envio que não trava envio cancelado.</li><li>Passos do painel legíveis no celular.</li></ul> |
| 26/09/2026 | **Ambiente:** o SDK .NET 10 foi instalado na máquina pelo winget. As variáveis de ambiente deixaram de ser necessárias. |
| 26/09/2026 | **Volta ao .NET 9** (pedido do responsável: o servidor só tem o runtime 9): <ul><li>versões iguais às do DP;</li><li>o filtro por escolinha deixou de ser nomeado (recurso do EF 10);</li><li>a migration `Inicial` continua válida: o EF 9 não vê diferença de modelo, então não houve migração nova nem perda de dado.</li></ul> Build sem aviso, 132 testes passando, app conferido no navegador. |
| 26/09/2026 | **Integrações:** a varredura por conteúdo achou dois clientes Asaas reaproveitáveis (HERANCA §3). Nenhum cliente de WhatsApp Cloud API. |
| 26/09/2026 | **Resultado:** build sem aviso; 132 testes passando; 0 classes faltando; nenhum elemento fora da tela em 375 e 320 px nas 19 telas, no claro e no escuro. |
| 26/09/2026 | **Commit do S0** na `main` (`a444b48`), autorizado pelo responsável. |
| 26/09/2026 | **Extra EX-01, Stripe da LDNA** (decisão do responsável: só a LDNA cobra as escolinhas; a mensalidade do aluno continua na Asaas). Pacotes de créditos com compra pelo Checkout (cartão, boleto ou Pix), webhook assinado, job de conferência e varredura, no padrão de integração do DP, sem SDK. <ul><li>Validado no navegador com o simulador: compra por cartão (créditos na hora) e por boleto (créditos na confirmação).</li><li>Achado e tratado: o webhook e a volta do pagamento conferem a mesma compra ao mesmo tempo; a versão de linha já impedia o crédito em dobro, e agora a segunda conferência relê em vez de falhar (D27).</li></ul> 184 testes passando. |
| 27/09/2026 | **Commit do EX-01** na `main` (`3c28d97`), autorizado pelo responsável. |
| 27/09/2026 | **S1 construído** (20 itens), direto na `main` por decisão do responsável e sem a planilha real ("seguir sem ela"): os campos são os da §5 da spec, e o que ela não diz foi registrado como provisório (D30 a D42, P28 a P33). |
| 27/09/2026 | **Validação no navegador** (Playwright), com o login de gestão de A e com o de gestão de B atuando como professor em A: locais, professores, turma em etapas com alerta de conflito, agenda, aluno em etapas com CA-1 pela busca, desligamento, escopo do professor (404 e 403) e massa de demonstração. |
| 27/09/2026 | **Defeitos achados na validação e corrigidos:** <ul><li>O professor via o irmão de outra turma e as marcas financeiras na ficha e nas listas (D41, com teste de regressão).</li><li>A sugestão de cadastro parecido mostrava o CPF inteiro; agora mascarado.</li><li>A edição da turma abria com as etapas escondidas (D42).</li><li>A massa falhava com duas modalidades: a turma nascia sem modalidade. Agora escolhe o futsal, com teste.</li><li>As linhas repetíveis não renumeravam o campo invariável escondido (`linhas.js`).</li><li>A data por extenso saía "Quinta-Feira, 1 De Outubro"; agora só a primeira letra é maiúscula.</li><li>Textos com gênero ("matriculado", "Ele já entra") passaram a ser neutros.</li><li>A idade de quem nasceu em 29/02 segue o Código Civil (art. 132, §3º).</li></ul> |
| 27/09/2026 | **Resultado:** build sem aviso; 254 testes passando (69 do cadastro); 0 classes faltando; nenhuma rolagem horizontal da página em 375 e 320 px nas 28 telas do S1, no claro e no escuro. Aguarda autorização de commit. |
| 27/09/2026 | **Respostas do responsável:** commit do S1 autorizado; P28 → atalho inativo → desistente (D43); P31 → alerta de quadra ocupada (D44, EX-02); P29 → a LDNA revisa os termos antes da produção. P28 e P31 implementados antes do commit, com testes e conferidos no navegador. 258 testes passando. |
| 27/09/2026 | **Commit do S1** na `main` (`b6a4c3f`), autorizado pelo responsável. |
| 27/09/2026 | **S2 construído** (14 itens), direto na `main`: <ul><li>motor único com outbox;</li><li>cliente da Cloud API sem SDK e simulador;</li><li>templates da plataforma;</li><li>convite, ativação pelo CPF e verificação de vínculo;</li><li>portal inicial.</li></ul> Decisões D45 a D57; pendências P34 a P36. |
| 27/09/2026 | **Validação no navegador** (Playwright), com a gestão de A e de B, a responsável que ativou o acesso e a equipe da plataforma: <ul><li>convite na janela de silêncio e reenvio;</li><li>ativação pelo CPF e portal;</li><li>confirmação do vínculo;</li><li>cancelamento do treino, com 32 WhatsApp e 1 aviso no portal;</li><li>entrega, leitura e resposta pelo simulador;</li><li>login pelo CPF e isolamento A/B;</li><li>edição de template.</li></ul> |
| 27/09/2026 | **Defeitos achados nos testes e na validação, e corrigidos:** <ul><li>Os eventos novos da mensagem eram gravados como UPDATE (id preenchido pela coleção). Agora o banco gera o id (D56).</li><li>A mensagem do convite substituído ou cancelado ficava na fila até o fim da janela de silêncio. Agora é suprimida na hora (D51).</li><li>"A escolinha Escolinha Piloto Londrina" nos templates e na ativação. Agora o nome entra como sujeito (D48).</li><li>O link longo do convite vazava do cartão no simulador. No celular, as listas de mensagens e de convites escondiam a situação e as ações à direita. Agora o dado secundário desce para a célula principal.</li><li>O CPF usado como login ia inteiro para o log. Agora vai mascarado.</li><li>Textos: "Sai às" + data, "resposta ... enviado" e a tela de cancelamento que ainda falava em "próxima etapa do piloto".</li></ul> |
| 27/09/2026 | **Resultado:** build sem aviso; 319 testes passando (61 novos no S2); 0 classes faltando; nenhuma rolagem horizontal, nem da tabela, em 375 e 320 px nas telas do S2, no claro e no escuro. Aguarda autorização de commit. |
| 27/09/2026 | **Commit do S2** na `main` (`c642f2f`), autorizado pelo responsável ("continue"), que pediu a lista das pendências no fim de tudo. |
| 27/09/2026 | **S3 construído** (6 itens), direto na `main`: <ul><li>chamada na quadra, com fila no aparelho e service worker;</li><li>alerta de faltas pelo motor, com janela anti-duplicidade e anulação na correção;</li><li>risco de evasão;</li><li>restrição do motivo de saúde;</li><li>aba Presença na ficha.</li></ul> Decisões D58 a D65; pendências P37 a P39. O teste do CA-21 ficou no roteiro assistido, sem dependência nova (D22). |
| 27/09/2026 | **Validação no navegador** (Playwright), com o celular emulado sem rede: <ul><li>marcações guardadas no aparelho;</li><li>sincronização ao reconectar;</li><li>o alerta e a mensagem à responsável certa;</li><li>a ficha, o painel e a lista de alunos em risco;</li><li>as telas novas em 375 e 320 px, no claro e no escuro.</li></ul> O service worker não registra nesta máquina: o certificado de desenvolvimento não é confiável (P37). |
| 27/09/2026 | **Defeitos achados e corrigidos:** <ul><li>Incluir um horário na turma gerava as sessões dele duas vezes, e a gravação falhava (defeito do S1, D65, com teste de regressão).</li><li>O aviso de faltas dizia "1 falta(s)". Agora a quantidade vai flexionada, nos templates e no banco de desenvolvimento.</li></ul> |
| 27/09/2026 | **Resultado:** build sem aviso; 338 testes passando (19 novos no S3); 0 classes faltando; nenhuma rolagem horizontal em 375 e 320 px nas telas do S3, no claro e no escuro. Aguarda autorização de commit. |
| 27/09/2026 | **Commit do S3** na `main` (`c6bdbc4`), com a autorização por fatia do responsável ("continuar"). |
| 28/09/2026 | **S4 construído** (8 itens), direto na `main`: <ul><li>recorrência pela matrícula;</li><li>emissão por job, com o cliente próprio da Asaas, a trava de ambiente e o simulador `/dev/asaas`;</li><li>régua com tom crescente;</li><li>conciliação por webhook, varredura e revalidação no envio;</li><li>fila de divergências e recebimento por fora;</li><li>fim da recorrência no desligamento;</li><li>financeiro só do gestor.</li></ul> Decisões D66 a D75; pendências P40 a P44. |
| 28/09/2026 | **Validação no navegador** (Playwright), com o simulador da Asaas: <ul><li>a entrada no sistema, com as 53 mensalidades de outubro;</li><li>a confirmação em lote, a emissão e o lembrete;</li><li>o pagamento por Pix conciliado sozinho, com a régua parada;</li><li>a divergência aceita e o estorno cancelado;</li><li>o professor sem acesso;</li><li>as telas em 375 e 320 px, no claro e no escuro.</li></ul> |
| 28/09/2026 | **Defeitos achados e corrigidos:** <ul><li>A mensalidade impedida entrava na conta de "indo para a Asaas". Agora ela não vira job (D67).</li><li>A régua não dizia de qual escolinha era a cobrança. Os templates começam por "Escolinha X informa" (D69).</li><li>A paga e estornada só aceitava "ciência" e continuaria contada como paga. Agora a fila cancela ou isenta (D72).</li><li>A recusa definitiva da Asaas prendia a mensalidade para sempre, inclusive com a chave revogada. Agora voltam pelo "Tentar de novo" e pela conta vinculada de novo (D67, D73).</li><li>Na entrada no sistema, o painel abria no mês vazio. Agora abre no mês seguinte.</li><li>No celular, o cabeçalho "Situação" aparecia sem a célula dele.</li><li>No computador, as ações da linha quebravam em duas.</li><li>O cabeçalho de cartão que quebra encostava na borda de cima, também em Convites, do S2.</li><li>O cartão do painel tinha o link longo, e a aba Financeiro mostrava "0".</li></ul> |
| 28/09/2026 | **Resultado:** build sem aviso; 374 testes passando (36 novos no S4); 0 classes faltando; nenhuma rolagem horizontal, nem das tabelas, em 375 e 320 px nas telas do S4, no claro e no escuro. |
| 28/09/2026 | **Commit do S4** na `main` (`44df87a`), com a autorização por fatia do responsável ("continuar"). |
| 28/09/2026 | **S5 construído** (10 itens), direto na `main`: <ul><li>evento genérico, com turmas N:N e as situações da §6;</li><li>montagem do fim de semana numa tela, com a escolha como rascunho e os sinais;</li><li>disparo em lote pelo motor, com o link de mapa e os botões;</li><li>resposta pelo botão, por fora e "sem resposta";</li><li>painel em tempo real pelo hub, no padrão do DP;</li><li>lembrete pela rotina de 15 minutos;</li><li>alterar, cancelar e adiar, com aviso por família;</li><li>escopo do professor e alertas de agenda do jogo.</li></ul> Decisões D76 a D86; pendências P45 a P48. |
| 28/09/2026 | **Validação no navegador** (Playwright), com o simulador do WhatsApp: <ul><li>os quatro jogos do fim de semana, os alertas de professor e de local, os 48 convocados e o disparo num fluxo só;</li><li>a resposta pelo botão mudando o painel aberto, sem recarregar, e a outra resposta visível;</li><li>a resposta por fora, a alteração do horário e o cancelamento, com os avisos às famílias;</li><li>o professor restrito às turmas dele (404 fora delas);</li><li>as telas em 1280, 375 e 320 px, no claro e no escuro.</li></ul> |
| 28/09/2026 | **Defeitos achados e corrigidos:** <ul><li>O jogo novo não herdava o local da turma (D76).</li><li>O disparo na janela de silêncio não dizia que as mensagens só sairiam às 07:00 (D77).</li><li>Duas alterações do jogo no mesmo segundo perderiam o segundo aviso, e o registro da alteração não guardava o local, o professor e as informações (D81).</li><li>Qualquer pessoa autenticada da escolinha entrava no tempo real; agora só a gestão e os professores, e a conexão fecha quando a autenticação expira (D79).</li><li>O motivo da mensagem suprimida dizia "já respondida" para a convocação sem resposta.</li><li>O encerramento de matrícula não dizia quantas convocações saíram.</li><li>As advertências do EF no log (do S1 e do S4), que agora são erro nos testes (D86).</li><li>Na tela: o título do jogo criado escondido sob a barra fixa, a turma repetida em cada linha, os contadores zerados no jogo cancelado e os botões da barra em duas linhas no celular.</li></ul> |
| 28/09/2026 | **Resultado:** build sem aviso; 398 testes passando (24 do S5); 0 classes faltando; nenhuma rolagem horizontal em 375 e 320 px nas telas do S5, no claro e no escuro. |
| 28/09/2026 | **Commit do S5** na `main` (`ec42cc3`), com a autorização por fatia do responsável ("continuar"). |
| 28/09/2026 | **S6 construído** (6 itens), direto na `main`: <ul><li>o módulo `Portal`, com o Início dos filhos confirmados, os Jogos com a resposta, os Pagamentos com o total do mês, as Presenças e os Avisos;</li><li>a resposta pelo portal com a regra do WhatsApp, e a mesma pessoa mudando a própria resposta;</li><li>o Pix copia e cola buscado na Asaas por job, depois da emissão e pela rotina.</li></ul> Decisões D87 a D91; pendências P49 a P51; inconsistência I15. |
| 28/09/2026 | **Validação no navegador** (Playwright), com os simuladores da Asaas e do WhatsApp: <ul><li>o convite e a ativação da Sabrina Pacheco, com nada à mostra antes da confirmação dos vínculos;</li><li>os três filhos numa conta só;</li><li>a resposta pelo portal mudando o painel do jogo aberto, sem recarregar;</li><li>o Pix copiado, o pagamento pela fatura e a conciliação sozinha;</li><li>as telas em 320, 375 e 1280 px, no claro e no escuro.</li></ul> |
| 28/09/2026 | **Defeitos achados e corrigidos:** <ul><li>O "Vai" vinha preenchido antes da resposta, e a resposta registrada pela escolinha convidava a "mudar de ideia" (D88).</li><li>A aba Jogos, sem filho confirmado, não explicava a confirmação.</li><li>O valor em reais quebrava entre "R$" e o número, em qualquer tela. Agora o espaço não quebra.</li><li>Em 320 px, o botão da fatura transbordava, a linha de cada filho quebrava e o rótulo "Justificadas" passava do cartão.</li><li>O painel do jogo falava só do WhatsApp.</li><li>O token da chamada (S3) gerava um aviso do antiforgery no log a cada pedido.</li></ul> A checagem de tela passou a medir também o conteúdo que transborda o próprio botão ou cartão, e não só o que passa da tela. |
| 28/09/2026 | **Resultado:** build sem aviso; 406 testes passando (8 do S6); 0 classes faltando; nenhuma rolagem horizontal nem transbordo em 320 e 375 px nas telas do portal, no claro e no escuro. Um teste de contrato do WhatsApp (S2) falhou uma vez numa rodada completa e passou nas sete rodadas seguintes; a causa não foi achada. |
| 28/09/2026 | **Commit do S6** na `main` (`f9699c8`), com a autorização por fatia do responsável ("continuar"). |
| 28/09/2026 | **S7 construído** (5 itens), direto na `main` e sem a planilha real ("seguir sem ela"): <ul><li>o envio do `.xlsx` e o modelo para baixar, com o ClosedXML do DP;</li><li>o mapeamento das colunas pelo cabeçalho;</li><li>a conferência como função pura, refeita na confirmação;</li><li>os responsáveis sem duplicidade;</li><li>a gravação única e a saúde fora da conferência.</li></ul> Decisões D92 a D95; pendências P52 a P54. |
| 28/09/2026 | **Validação no navegador** (Playwright), na escolinha A, com uma planilha escrita como uma escolinha escreveria: <ul><li>o mapeamento, a conferência com os motivos e o quadro de decisões;</li><li>a confirmação com a declaração das autorizações;</li><li>a ficha com a saúde restrita, os consentimentos e a "Carga inicial" no histórico;</li><li>a filha nova aparecendo na hora no portal da responsável que já tinha acesso;</li><li>as mensalidades projetadas;</li><li>as telas em 320, 375 e 1280 px, no claro e no escuro.</li></ul> |
| 28/09/2026 | **Defeitos achados e corrigidos:** <ul><li>O repositório filtrava e ordenava os lotes depois da projeção, o que o EF não traduz (achado nos testes).</li><li>"2º responsável" e "Celular 2" não eram reconhecidos no mapeamento.</li><li>Em 320 px, as tabelas rolavam de 1 a 4 px, e um rótulo passava do cartão.</li></ul> |
| 28/09/2026 | **Resultado:** build sem aviso; 420 testes passando (14 do S7); 0 classes faltando; nenhuma rolagem horizontal, nem das tabelas, em 320 e 375 px nas telas do S7, no claro e no escuro. **Os 81 entregáveis do piloto estão prontos.** |
| 28/09/2026 | **Commit do S7** na `main` (`a262578`), com a autorização por fatia do responsável ("continuar"). Fim das fatias do piloto; a lista de pendências foi entregue ao responsável, como pedido. |
| 28/09/2026 | **Pendências sem dependência externa** (pedido do responsável: "deixa o WhatsApp por último, pode seguir"): <ul><li>**P37 validada nesta máquina:** um Chrome separado, sem janela, que aceita o certificado de desenvolvimento, registrou o service worker; a sessão reabriu sem rede, a marcação ficou na fila depois de recarregar, a fila saiu ao voltar a rede e o "Sair" apagou a cópia guardada. Falta o aparelho real.</li><li>**P22 aplicada** (D96): o job diário `webhooks-limpar-corpo` apaga o corpo bruto dos webhooks encerrados há mais de 90 dias, e a linha fica. Migration só aditiva (`WebhookCorpoApagado`), com teste.</li><li>**Operação documentada** em `docs/operacao/`, portada do DP: o runbook (publicação, variáveis, integrações, jobs, problemas e volta de versão), o script do banco e do login, e o roteiro de homologação por fatia, com os critérios de aceite.</li><li>**Conferência na documentação da Asaas:** os endpoints da sandbox do runbook; e a conta sem chave Pix, que recebe um Pix que só vale até as 23:59 do dia. O sistema já tratava a validade; D90 corrigida e o ponto entrou na prova da P41.</li></ul> Commit local `13fca8f`, com o app conferido: a migration aplicada no banco de desenvolvimento, o `/health` saudável e o job registrado às 04:30. |
| 28/09/2026 | **Os itens do WhatsApp, por último:** <ul><li>**P02:** a revisão dos 13 templates para a submissão à Meta (`operacao/TEMPLATES-WHATSAPP.md`), conferida nas regras publicadas pela Meta. Utilidade para todos; quatro textos terminam numa variável e ganham a proposta de uma frase fixa; os de 6 ou 7 variáveis correm o risco de "variáveis demais para o tamanho"; os exemplos de cada variável. Nada foi mudado nos textos: a decisão é do responsável.</li><li>**P34 e P14:** o passo a passo da conta na Meta no runbook (§6.2), do app ao webhook, e o que muda para o número de produção.</li><li>**P55, nova:** o envio procura o template pelo nome gravado na mensagem, e trocar a versão na tela descarta o que está na fila com o nome antigo.</li><li>**O teste de contrato instável do WhatsApp (S6):** ao menos 13 rodadas completas seguidas passaram desde a falha (5 delas hoje), e a causa não apareceu. A mensagem daquela falha se perdeu porque a saída do `dotnet test` foi cortada. Na próxima falha, guardar a saída inteira.</li></ul> |
| 28/09/2026 | **As referências decididas pelo responsável** ("fazer as referências P32, P39, P38, P40, P44, P50, P45, P47, P51, P24, P02, P33, P35, P36, P55, P42, P43, P46, P48, P49, P53, P31, CA-21, D22" e "Item 22, pode fazer"), em blocos commitados: <ul><li>templates nos dois tons e o nome anterior aceito no envio (P02, P55);</li><li>risco com a meta da turma e o mínimo de sessões, fila da chamada por pessoa (P32, P39, P38);</li><li>respostas das famílias, SAIR, remarcação de treino e aviso de horário novo (P35, P36, P33, BL-M04-07);</li><li>nova confirmação, turma do professor, mudança perto do jogo, texto da família e placar (P45, P47, P51, P48, P46);</li><li>cobrar de novo, cliente atualizado na Asaas, QR code, periodicidade e mensalidades no portal (P43, P42, P49, P44, P50);</li><li>horários do local e descarte diário do lote esquecido (P31, P53);</li><li>visão única dos filhos, troca de escolinha e recuperação de senha por e-mail ou link da gestão (BL-M10-02, BL-M01-02, BL-M01-09);</li><li>o CA-21 automatizado no Chrome, com o `Microsoft.Playwright` (D22, D108, X09), aprovado contra o app;</li><li>P24 (continua no .NET 9) e P40 (grupos provisórios) decididas sem código.</li></ul> Decisões D97 a D110. Continuam aguardando o responsável: o servidor, a sandbox da Asaas, a conta na Meta, a revisão jurídica, a identidade visual, o Stripe, a planilha real, os valores dos parâmetros, os aparelhos reais e as fases fora do escopo. |
| 29/09/2026 | **Termos de consentimento pelo link do responsável** (melhoria pedida pelo responsável, D111, EX-08): o cadastro manda o link de aceite ao 1º responsável por e-mail, pelo motor único (canal E-mail e gatilho novos, aprovados); a página confere o CPF e registra cada termo em separado; a ficha mostra a situação e o link para quem não tem e-mail. Validado no app (e-mail na caixa local, página em 375 e 320 px); corrigido o radio sem escolha, que o KTUI pintava como marcado. 447 testes. |
