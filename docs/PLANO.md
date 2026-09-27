# PLANO — Minha Escolinha

Plano vivo, atualizado ao fim de cada fatia. Cada documento responde por uma coisa:

| Pergunta | Documento |
|---|---|
| **O que** construir e quais regras valem | A spec, em [`especificacao/`](especificacao/) (81 entregáveis, R1–R12, 21 critérios de aceite) |
| **Como** construir | A herança do AlternativaDepartamentoPessoal ([`HERANCA.md`](HERANCA.md)) |
| **Em que ordem** e onde estamos | Este plano |

Última atualização: **26/09/2026**. F0 e S0 prontos; o commit aguarda autorização.

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
| S0 — Fundação: conta, identidade e permissões | 12 | **Pronto**: 11 concluídos + 1 parcial que depende da Turma (S1); aguardando commit | 26/09/2026 |
| S1 — Cadastro: turma, sessões, aluno, responsável, equipe | 20 | Próxima | — |
| S2 — Mensageria e acesso das famílias | 14 | — | — |
| S3 — Presença e controle de evasão | 6 | — | — |
| S4 — Financeiro: mensalidade, régua e conciliação | 8 | — | — |
| S5 — Eventos e convocações | 10 | — | — |
| S6 — Portal do responsável | 6 | — | — |
| S7 — Carga inicial da escolinha piloto | 5 | — | — |

Os critérios de aceite, com a fatia em que cada teste nasce, estão no fim do [`BACKLOG.md`](BACKLOG.md).

---

## 4. Próximos passos

### 4.0 Agora
1. **Autorização de commit** do arranque + F0 + S0. O repositório tem a branch `main`, ainda sem commits. Proposta: um commit inicial com o esqueleto e o S0, e daí em diante uma branch por fatia (`fatia/s1-cadastro` etc.), com os IDs `BL-` nas mensagens.
2. Confiar o certificado de desenvolvimento do SDK novo (§7).

### 4.1 O que o S1 precisa e ainda não existe
- **A planilha real da escolinha piloto** (P21). Ela orienta os campos do cadastro e a deduplicação desde já, não só no S7.
- Decisões que o S1 toca: P07 (retenção), P08 (16–17 anos), P12 (alertas de trial), P15 (opt-in no consentimento), P20 (o que o professor vê).
- O resolvedor de escopo real (`IResolvedorEscopo`): turmas do professor e filhos com vínculo verificado. No S0 ele sempre nega.
- A Turma, que completa o `BL-M02-12`: mensalidade, capacidade e mínimo de confirmados por turma, com o valor inicial vindo do catálogo.
- O 1º job recorrente: geração das sessões num horizonte rolante, regenerando só as futuras em "prevista".
- **Maior risco do S1:** celular compartilhado na deduplicação. Só o CPF deduplica sozinho; telefone e nome viram sugestão.

### 4.2 O que o S0 entregou (referência)
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

### 4.3 O que o F0 entregou (referência)
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
| S2 | <ul><li>Motor único de mensagens (R3): catálogo fechado de gatilhos, uma regra por gatilho, outbox transacional, créditos com reserva atômica, janela de silêncio, revalidação da origem no envio e log por mensagem.</li><li>Cliente da Cloud API, simulador e `/dev/whatsapp`.</li><li>Templates com categoria, submetidos cedo à Meta.</li><li>Convite de uso único e modo parcial.</li></ul> | Prazos da Meta. O "pronto" exige resposta real: número de teste da Meta + HTTPS público. |
| S3 | <ul><li>Chamada offline (service worker + IndexedDB, UUID por marcação, a quadra prevalece).</li><li>Alerta avaliado a cada falta, inclusive na sincronização.</li><li>Anti-duplicidade e risco de evasão.</li></ul> | Offline no iOS (sem Background Sync) |
| S4 | <ul><li>Recorrência própria por matrícula → cobrança prevista → emissão por job → régua por avaliador → webhook + varredura.</li><li>Fila de divergências.</li><li>**Reaproveitar um cliente Asaas existente** (SDK `Projetos\Aplicativos\Asaas API` ou o cliente da AgenciaAzul; ver HERANCA §3).</li></ul> | CA-8 e a semântica da Asaas (valor original, estorno) |
| S5 | <ul><li>Evento genérico, convocação por convocado, lote com rascunho até o disparo.</li><li>Painel em tempo real (SignalR, padrão do DP).</li><li>Lembretes, cancelamento e adiamento, alertas de conflito.</li></ul> | O fluxo em lote no celular e o limite de envio de um número novo |
| S6 | <ul><li>`Areas/Portal`: filhos verificados, total do mês por filho.</li><li>Pix obtido antes por job; fatura Asaas para cartão.</li><li>Resposta à convocação.</li></ul> | Dado de menor exposto |
| S7 | <ul><li>Planilha → mapear → validar e classificar → conferência (saúde mascarada) → gravação única.</li><li>ClosedXML (dependência nova, registrar em 12.3).</li></ul> | O formato real da planilha |

**Massa de demonstração**, construída aos poucos (spec §13): 1 escolinha, 3 turmas, cerca de 60 alunos, irmãos, alunos com 2 responsáveis, 4 jogos no fim de semana com 12 convocados cada, e mensalidades pagas, vencidas, isentas e parciais.

---

## 6. Fontes para portar (AlternativaDepartamentoPessoal)

Base: `C:\Users\vinic\Documents\Projetos\.NET\AlternativaDepartamentoPessoal\`. O que já foi portado está listado em [`HERANCA.md`](HERANCA.md). Padrões a **seguir** (sem copiar) nas próximas fatias:

| Padrão | Onde está no DP | Fatia |
|---|---|---|
| Integração: opções Simulado/Api, trava de ambiente, simulador com página `/dev`, contrato HTTP com handler falso | ZapSign e GoHighLevel | S2, S4 |
| Outbox + varredura de lembretes | `docs/integrador/08-MOTOR-LEMBRETES.md` | S2, S4, S5 |
| Webhook: gravar o corpo bruto, responder 200, processar por job | ZapSign | S2, S4 |
| Agendador em memória nos testes | `AgendadorDeTeste` | S1+ |
| Tempo real: o servidor empurra `{id, versão}`; recuperação com `?desde=` | `AdmissoesHub` | S5 |
| Ficha lateral: drawer com versão e resposta 409 no conflito | `ficha.js` | S1 |

---

## 7. Ambiente local

- **.NET:** o projeto usa o **SDK 9.0.100** (`global.json`, com `rollForward: latestFeature`), como o DP. Os runtimes 9 da máquina são o 9.0.0 do ASP.NET Core e o 9.0.5 do .NET.
- **SDK 10 também instalado:** o SDK 10.0.401 entrou pelo winget (o instalador pediu reinício do PC) e fica pronto para quando o projeto subir para o 10 (P24). Uma cópia dele no perfil do usuário (`%LOCALAPPDATA%\Microsoft\dotnet`) não é mais necessária e pode ser apagada.
- **Certificado HTTPS de desenvolvimento:** o SDK novo gerou um certificado ainda não confiado. Rode `dotnet dev-certs https --trust` uma vez; o Windows pede confirmação.
- **SQL Server:**
  - servidor `VINICIUSTARDEM\SQLEXPRESS`, banco `escolinhaesporte` (compatibilidade 150, AUTO_CLOSE desligado);
  - testes em `escolinhaesporte_Testes`, recriado a cada execução, com autenticação do Windows ou a conexão em `ESC_TESTES_SQL`.
- **Segredos** (user-secrets `minha-escolinha-web`): `ConnectionStrings:ConexaoPadrao`, `Seed:Administrador:Login`, `Seed:Administrador:Nome` e `Seed:Administrador:Senha`.
- **Endereços:** https://localhost:7186 (e http://localhost:5186); vitrine em `/dev/design`; Hangfire em `/jobs`; saúde em `/health`.
- **Dados de validação no banco de desenvolvimento:** as escolinhas "Escolinha Piloto Londrina" (A) e "Escolinha Bola na Rede" (B), cada uma com o seu gestor, criadas na validação do S0. As senhas não ficam em arquivo versionado.

---

## 8. Pendências que dependem do responsável ou do cliente

| O que | Bloqueia | Ref. |
|---|---|---|
| A planilha real da escolinha piloto | Campos e deduplicação (S1); importação (S7) | P21 |
| Número de WhatsApp: da LDNA ou da escolinha? Provedor (Cloud API oficial)? | S2 | P14 |
| Validar a categoria de WhatsApp de cada gatilho | Submissão dos templates (S2) | P02 |
| Conta Asaas da escolinha piloto (sandbox e produção) | "Pronto" do S4 | P21 |
| Pacote de créditos (comercial) | Envio real (S2) | P06, P21 |
| Hospedagem com HTTPS público | Webhooks do WhatsApp e da Asaas | P21 |
| Identidade visual (logo, cor) | Trocar a marca provisória | D17 |
| Confirmar os valores provisórios dos parâmetros com a escolinha | Nada bloqueia; a tela marca os provisórios | P05 |
| A "Especificação Funcional e Técnica", se existir | Detalhe de campos por módulo | P21 |

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
