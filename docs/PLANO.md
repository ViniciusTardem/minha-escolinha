# PLANO — Minha Escolinha

Plano vivo, atualizado ao fim de cada fatia. Cada documento responde por uma coisa:

| Pergunta | Documento |
|---|---|
| **O que** construir e quais regras valem | A spec, em [`especificacao/`](especificacao/) (81 entregáveis, R1–R12, 21 critérios de aceite) |
| **Como** construir | A herança do AlternativaDepartamentoPessoal ([`HERANCA.md`](HERANCA.md)) |
| **Em que ordem** e onde estamos | Este plano |

Última atualização: **27/09/2026**. F0, S0, o extra EX-01, o S1 (com o extra EX-02) e o S2 estão commitados na `main`. A prova do S2 com um número real espera a conta na Meta (P34). O S3 está pronto e aguarda a autorização de commit. A próxima fatia é o S4.

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

---

## 3. Roadmap e status

| Fatia | Itens | Status | Data |
|---|---:|---|---|
| Arranque (spec §0): herança inventariada; sem credenciais de Asaas e WhatsApp, então as integrações nascem em modo Simulado; plano publicado | — | **Pronto** | 26/09/2026 |
| F0 — Esqueleto vivo (template §13) | — | **Pronto** | 26/09/2026 |
| Design system (4 superfícies + vitrine `/dev/design`) | — | **Pronto** | 26/09/2026 |
| S0 — Fundação: conta, identidade e permissões | 12 | **Pronto** e commitado (`a444b48`). O parcial (`BL-M02-12`) fechou no S1, com a Turma. | 26/09/2026 |
| Extra EX-01 — Compra de pacotes de créditos pelo Stripe da LDNA (X08) | — | **Pronto** no modo Simulado e commitado (`3c28d97`); falta configurar as chaves de teste | 26/09/2026 |
| S1 — Cadastro: turma, sessões, aluno, responsável, equipe | 20 | **Pronto** e commitado (`b6a4c3f`): 19 concluídos e 1 parcial, que depende do S4 (recorrência no desligamento). O `BL-M04-06` fechou no S2. Com o extra EX-02 (alerta de quadra ocupada). | 27/09/2026 |
| S2 — Mensageria e acesso das famílias | 14 | **Pronto no simulador** e commitado (`c642f2f`), com os 14 concluídos. A prova com um número real espera a conta na Meta (P34). | 27/09/2026 |
| S3 — Presença e controle de evasão | 6 | **Pronto**, com os 6 concluídos. A reabertura da chamada sem sinal pelo service worker espera um navegador que confie no certificado e o teste em aparelho real (P37). Aguarda autorização de commit. | 27/09/2026 |
| S4 — Financeiro: mensalidade, régua e conciliação | 8 | Próxima | — |
| S5 — Eventos e convocações | 10 | — | — |
| S6 — Portal do responsável | 6 | — | — |
| S7 — Carga inicial da escolinha piloto | 5 | — | — |

Os critérios de aceite, com a fatia em que cada teste nasce, estão no fim do [`BACKLOG.md`](BACKLOG.md).

---

## 4. Próximos passos

### 4.0 Agora
1. **Autorizar o commit do S3**, direto na `main`, como o S2.
2. **Provar o S2 com um número real** (P34), assim que a LDNA tiver:
   - a conta WhatsApp Business na Cloud API;
   - a URL pública em HTTPS para o webhook;
   - os templates aprovados;
   - os números de teste.

   Com isso, grave em user-secrets os comandos do CLAUDE.md §7 (`WhatsApp:Modo = Api`, as 4 chaves, `WhatsApp:NumerosTeste` e `WhatsApp:UrlPublica`). Então:
   - convide um número de teste;
   - responda pelo celular;
   - confira a resposta e o log em Mensagens.
3. **S4 — Financeiro:** próxima fatia (§4.1).
4. **Validar a chamada sem sinal com o service worker** (P37):
   - confiar o certificado de desenvolvimento (§7) e repetir o roteiro: abrir a lista com sinal, tirar a rede, recarregar a sessão e marcar;
   - antes do piloto, repetir num Android e num iPhone, no servidor com HTTPS.
5. **Stripe em modo de teste**, quando o responsável quiser sair do simulador:
   - criar um sandbox próprio no painel do Stripe (P27);
   - gravar em user-secrets `Stripe:Modo = Teste` e `Stripe:ChaveSecreta` (chave de teste, `sk_test_` ou `rk_test_` restrita);
   - para os webhooks em localhost, rodar o Stripe CLI (`stripe listen --forward-to https://localhost:7186/api/v1/webhooks/stripe`) e gravar o `whsec_` que ele mostrar em `Stripe:SegredoWebhook`. Sem isso, a volta do pagamento e a varredura confirmam as compras.
### 4.1 O que o S4 precisa e ainda não existe
- **Critério de pronto da spec:** a mensalidade do mês é emitida sozinha, o lembrete sai antes do vencimento, o pagamento é conciliado sem ação humana e a régua para na hora.
- **Decisões que o S4 toca:**
  - o cliente Asaas: reaproveitar um dos dois achados na herança (HERANCA §3) ou escrever o próprio, no padrão do DP, com simulador `/dev/asaas` e trava de ambiente;
  - P17: vencimento, emissão antecipada, pró-rata e pagador sem CPF;
  - P18: dinheiro em espécie, estorno e chargeback;
  - P22: limpeza do corpo bruto dos webhooks;
  - P36: o descadastro pelo WhatsApp diante da mensagem essencial de cobrança.
- **O que as fatias anteriores deixam pronto:**
  - a conta Asaas da escolinha, com a chave cifrada, e os parâmetros `financeiro.*` (S0);
  - a matrícula com o valor próprio e o pagador marcado no vínculo, e o desligamento que encerra as matrículas (S1). É dele que nasce o encerramento da recorrência (`BL-M06-14`, CA-2);
  - os gatilhos de mensalidade e de pagamento no catálogo, com templates; a revalidação da origem no envio (CA-8), a janela de silêncio e a retenção das essenciais sem crédito (S2);
  - o padrão de webhook bruto + job + varredura, do Stripe (EX-01) e do WhatsApp (S2).
- **Maior risco do S4:** o CA-8 ("quem pagou nunca recebe cobrança") e a semântica da Asaas (valor original, data do pagamento, estorno).

### 4.2 O que o S3 entregou (referência)
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

### 4.3 O que o S2 entregou (referência)
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

### 4.4 O que o S1 entregou (referência)
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

### 4.5 O que o S0 entregou (referência)
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

### 4.6 O que o F0 entregou (referência)
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

## 5. Fases seguintes (resumo)

| Fatia | O essencial | Maior risco |
|---|---|---|
| S4 | <ul><li>Recorrência própria por matrícula → cobrança prevista → emissão por job → régua por avaliador → webhook + varredura.</li><li>Fila de divergências.</li><li>**Reaproveitar um cliente Asaas existente** (SDK `Projetos\Aplicativos\Asaas API` ou o cliente da AgenciaAzul; ver HERANCA §3).</li></ul> | CA-8 e a semântica da Asaas (valor original, estorno) |
| S5 | <ul><li>Evento genérico, convocação por convocado, lote com rascunho até o disparo.</li><li>Painel em tempo real (SignalR, padrão do DP).</li><li>Lembretes, cancelamento e adiamento, alertas de conflito.</li></ul> | O fluxo em lote no celular e o limite de envio de um número novo |
| S6 | <ul><li>`Areas/Portal`: filhos verificados, total do mês por filho.</li><li>Pix obtido antes por job; fatura Asaas para cartão.</li><li>Resposta à convocação.</li></ul> | Dado de menor exposto |
| S7 | <ul><li>Planilha → mapear → validar e classificar → conferência (saúde mascarada) → gravação única.</li><li>ClosedXML (dependência nova, registrar em 12.3).</li></ul> | O formato real da planilha |

**Massa de demonstração**, construída aos poucos (spec §13): 1 escolinha, 3 turmas, cerca de 60 alunos, irmãos, alunos com 2 responsáveis, 4 jogos no fim de semana com 12 convocados cada, e mensalidades pagas, vencidas, isentas e parciais. **O S1 montou a base** (turmas, alunos e famílias, em `/dev/massa`); os jogos entram no S5 e as mensalidades no S4.

---

## 6. Fontes para portar (AlternativaDepartamentoPessoal)

Base: `C:\Users\vinic\Documents\Projetos\.NET\AlternativaDepartamentoPessoal\`. O que já foi portado está listado em [`HERANCA.md`](HERANCA.md). Padrões a **seguir** (sem copiar) nas próximas fatias:

| Padrão | Onde está no DP | Fatia |
|---|---|---|
| Integração: opções Simulado/Api, trava de ambiente, simulador com página `/dev`, contrato HTTP com handler falso | ZapSign e GoHighLevel | S4 (o WhatsApp seguiu o padrão no S2) |
| Outbox + varredura de lembretes | `docs/integrador/08-MOTOR-LEMBRETES.md` | S4, S5 (o motor de mensagens seguiu o padrão no S2) |
| Webhook: gravar o corpo bruto, responder 200, processar por job | ZapSign | S4 (o WhatsApp seguiu o padrão no S2) |
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
- **Segredos** (user-secrets `minha-escolinha-web`): `ConnectionStrings:ConexaoPadrao`, `Seed:Administrador:Login`, `Seed:Administrador:Nome` e `Seed:Administrador:Senha`. As chaves do WhatsApp (modo Api) e do Stripe (modo Teste) só entram quando houver conta (CLAUDE.md §7).
- **Endereços:** https://localhost:7186 (e http://localhost:5186); vitrine em `/dev/design`; simuladores em `/dev/whatsapp` e `/dev/stripe`; Hangfire em `/jobs`; saúde em `/health`.
- **Dados de validação no banco de desenvolvimento:**
  - as escolinhas "Escolinha Piloto Londrina" (A) e "Escolinha Bola na Rede" (B), cada uma com o seu gestor, criadas na validação do S0. As senhas não ficam em arquivo versionado;
  - em A, desde o S1: as turmas, os alunos e as famílias da validação e da massa de demonstração (`/dev/massa`);
  - em A, desde o S2: a responsável Ana Almeida ativou o acesso sem e-mail (login pelo CPF), e o vínculo com o Arthur foi confirmado. O treino de sábado, 03/10, da Sub-9 foi cancelado na validação, e o log tem as mensagens do convite e do cancelamento;
  - em A, desde o S3: a turma "Sub-11 · segunda e quarta" tem uma sessão realizada no domingo, 27/09, com a falta da Ana Lima, o alerta disparado e a mensagem à Maria Lima. Para a validação, o limite de faltas foi a 1 e voltou a 4, e um horário de domingo entrou e saiu da turma, tudo com histórico;
  - para validar o escopo do professor com um login real, o login de gestão de B (`gestor.b@bolanarede.test`) também tem o papel Professor em A: a ficha de professor "Paula Souza" foi ligada a essa pessoa por SQL, e o vínculo Professor em A foi criado.

---

## 8. Pendências que dependem do responsável ou do cliente

| O que | Bloqueia | Ref. |
|---|---|---|
| A planilha real da escolinha piloto | Importação (S7). O S1 seguiu com os campos da §5 da spec; conferir se a planilha traz algo a mais. | P21 |
| Conta WhatsApp Business da LDNA na Cloud API, URL HTTPS pública, templates aprovados e números de teste | A prova do S2 com um número real e todo envio real | P14, P34 |
| Validar a categoria e os textos dos 13 templates (tela Templates, da plataforma) | Submissão à Meta | P02 |
| Conta Asaas da escolinha piloto (sandbox e produção) | "Pronto" do S4 | P21 |
| Pacote de créditos (comercial) | Envio real (S2) | P06, P21 |
| Hospedagem com HTTPS público | Webhooks do WhatsApp e da Asaas | P21 |
| Identidade visual (logo, cor) | Trocar a marca provisória | D17 |
| Confirmar os valores provisórios dos parâmetros com a escolinha | Nada bloqueia; a tela marca os provisórios | P05 |
| Chaves de teste do Stripe (sandbox próprio) e, para produção, chave de produção + segredo do webhook | Sair do simulador (EX-01) | P27 |
| Definir o plano recorrente (preço, periodicidade, créditos incluídos) e os pacotes à venda | Cobrança do plano pelo Stripe; venda real dos pacotes | P25, P26 |
| A "Especificação Funcional e Técnica", se existir | Detalhe de campos por módulo | P21 |
| Revisão jurídica dos termos de consentimento (tratamento de dados e uso de imagem), incluindo o WhatsApp no termo de tratamento de dados | Produção; opt-in da Meta | P29, P15 |
| Decidir: a mudança de horário da turma avisa as famílias ("Treino remarcado")? A gestão é avisada das respostas livres? "SAIR" desliga o recebimento? | Nada bloqueia; seguem os encaminhamentos provisórios | P33, P35, P36 |
| Confirmar com a escolinha: a meta de frequência da turma entra no risco? O risco exige um mínimo de sessões? A quadra usa aparelho compartilhado? | Nada bloqueia; seguem os encaminhamentos provisórios | P32, P38, P39 |
| Validar a chamada sem sinal com o service worker: certificado de desenvolvimento confiável e, antes do piloto, Android e iPhone no servidor com HTTPS | A reabertura da chamada sem sinal (CA-21) | P37 |

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
