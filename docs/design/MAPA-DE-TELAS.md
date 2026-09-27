# Mapa de telas — Minha Escolinha

> Onde cada tela mora, quem acessa, com que layout e em que fatia ela chega. As rotas e os atributos de acesso foram conferidos nos controllers e no `Program.cs` em 26/09/2026, com a fatia S0 pronta. O visual de cada superfície está no [`GUIA-DESIGN-SYSTEM.md`](GUIA-DESIGN-SYSTEM.md) (§5).
>
> **Atualize este mapa na mesma entrega da tela:** status, rota, acesso e data.

## Como ler

**Status**

| Status | Significado |
|---|---|
| **Pronto** | tela no ar, com o acesso declarado e validada no navegador em 1280, 375 e 320 px, no claro e no escuro (D22) |
| **Em breve** | só a página explicativa do módulo (`/{chave}`), gerada pelo `CatalogoModulos` |
| **Planejado** | ainda não existe |

**Acesso.** Toda action declara um destes atributos, e um teste de arquitetura garante isso (D08):

| Atributo | Quem entra |
|---|---|
| `[AllowAnonymous]` | qualquer pessoa, sem login |
| `[SomenteAutenticado]` | qualquer pessoa logada |
| `[Plataforma]` | a equipe LDNA (`AdministradorPlataforma`) |
| `[Permissao(Recurso)]` | quem tem o recurso na matriz de permissões, na escolinha da sessão; com `Operacao.Alterar` quando a action grava |

- No S0, os dois recursos das telas da escolinha são **só do gestor**: `ConfiguracaoEscolinha` (dados, modalidades, parâmetros e créditos) e `FinanceiroEscolinha` (conta de recebimento).
- A equipe LDNA só abre essas telas depois de "Entrar como equipe", que cria um vínculo de gestor auditado (D06).
- **Troca de senha pendente:** um filtro global manda qualquer página para `/Conta/TrocarSenha`. As actions marcadas com † (`[PermitirSemTrocarSenha]`) abrem mesmo assim.

**Layouts:** Autenticação (`_LayoutAutenticacao`), Painel (`_Layout`), Quadra (`_LayoutQuadra`), Portal (`_LayoutPortal`) e Vitrine (`_LayoutVitrine`).

**Rotas:** a padrão é `{controller=Painel}/{action=Index}/{id?}`. Todo POST valida o antiforgery (filtro global).

---

## 1. Visão geral por fatia

A tabela da §4.5 do plano, com o status de hoje.

| Fatia | Telas | Quem · layout | Referência no demo1 | Status |
|---|---|---|---|---|
| S0 | Entrar, trocar senha, escolher escolinha | todos · Autenticação | `authentication/branded/sign-in` | **Pronto** |
| S0 | Plataforma › Escolinhas: lista e **nova escolinha em etapas** (escolinha → modalidade → 1º gestor; a senha aparece uma vez); concessão de créditos | equipe LDNA · Painel | `network/user-table/*`, `account/home/settings-plain` | **Pronto**, sem a etapa "revisão" prevista no plano |
| S0 | Escolinha › Dados, Modalidades (perfil + interruptores), **Parâmetros** (abas por domínio, com histórico), Conta de recebimento Asaas (vincular ou trocar pedindo senha; mostra só o final da chave), Créditos (saldo e histórico); painel inicial com as próximas fatias como "em breve" | gestor · Painel | `account/home/company-profile`, `account/home/settings-sidebar`, `account/integrations`, `account/billing/history` | **Pronto** |
| S1 | Turmas (ocupação de vagas), ficha da turma (horários, composição, sessões), nova turma em etapas; Agenda (por turma, professor ou quadra); Locais | gestor; professor só leitura das suas · Painel | `network/user-table/*`, `account/activity` | Em breve (`/turmas`, `/agenda`); Locais planejado |
| S1 | Alunos (por turma ou visão geral), **ficha do aluno** (drawer com responsáveis, matrículas, saúde restrita, consentimentos, histórico), novo aluno em etapas com detecção de duplicidade, desligar (data + motivo + efeitos); Professores com disponibilidade | gestor; professor P · Painel | `public-profile/profiles/crm`, `account/members/*` | Em breve (`/alunos`); Professores planejado |
| S2 | Mensagens (log de entrega + linha do tempo), Templates (categoria, tom, aprovação), Convites em lote, fila de verificação de vínculos, sino; ativação do convite; `/dev/whatsapp` (caixa de entrada por número, com botões Sim/Não) | gestor · Painel; convidado · Autenticação | `account/notifications`, `account/members/import-members` | Em breve (`/mensagens`); o resto planejado |
| S3 | **Hoje → Chamada** (P/F num toque, offline, "Concluir chamada" na barra inferior); selo de risco nas listas; aba de alertas na ficha (CA-6) | professor · **Quadra**; gestor · Painel | — (tela própria) | Em breve (`/chamada`) |
| S4 | Financeiro › Pagamentos (agrupado por situação, com as ações isentar, cancelar e reenviar); Divergências (resolução manual com autoria); aba Financeiro na ficha; `/dev/asaas` | gestor · Painel | `account/billing/*` | Em breve (`/mensalidades`) |
| S5 | **Montar fim de semana** (jogos → convocados por jogo, com alertas → revisar → disparar tudo); **Painel de confirmações** em tempo real (registrar resposta por fora; cancelar, adiar ou alterar em um toque) | professor · **Quadra** (e Painel) | `network/user-cards/*` | Em breve (`/jogos`) |
| S6 | Portal: Início (filhos, pendências, próximos jogos), Jogos (Sim/Não por filho), Pagamentos (total do mês + detalhamento por filho, pagar pela fatura Asaas ou pelo Pix copia-e-cola), Presenças (só leitura), Avisos | responsável · **Portal** | `store-client/checkout/*` (só a forma) | Planejado; o layout está pronto |
| S7 | Importação: enviar planilha → mapear → validar e classificar → **conferência** (novos, reaproveitados, vínculos, bloqueados; saúde mascarada) → confirmar; modelo `.xlsx` para baixar | gestor ou equipe · Painel | `account/members/import-members` | Em breve (`/importacao`) |

As referências ficam em `docs/metronic/html/demo1/` (cópia local, fora do git).

---

## 2. S0: telas prontas (26/09/2026)

### 2.1 Fora do sistema (`ContaController`)

| Tela | Rota | Acesso | Layout | Observações | BL |
|---|---|---|---|---|---|
| Entrar | `GET` e `POST /Conta/Login` | `[AllowAnonymous]`; o POST aceita 10 tentativas por minuto por IP | Autenticação | já logado → Painel; troca pendente → Trocar senha; uma escolinha → entra nela; mais de uma → Escolher escolinha; equipe LDNA sem escolinha → `/Plataforma` | M01-01 |
| Trocar senha | `GET` e `POST /Conta/TrocarSenha` | `[SomenteAutenticado]` † | Autenticação | obrigatória no 1º acesso (senha temporária); em erro, as três senhas voltam vazias | M01-01 |
| Escolher escolinha | `GET` e `POST /Conta/EscolherEscolinha` | `[SomenteAutenticado]` | Autenticação | um botão por vínculo; a equipe vê "Ir para a área da plataforma"; sem vínculo e fora da equipe → Acesso negado | M01-01 |
| Acesso negado | `/Conta/AcessoNegado` | `[AllowAnonymous]` † | Painel com login; Autenticação sem | responde 403; é o destino do cookie quando falta permissão | M01-03 |

Ações sem tela própria:
- `POST /Conta/UsarPlataforma` (`[Plataforma]`): tira a escolinha da sessão e vai para `/Plataforma`;
- `POST /Conta/Sair` (`[SomenteAutenticado]` †).

### 2.2 Plataforma: equipe LDNA (`PlataformaController`)

| Tela | Rota | Acesso | Layout | Observações | BL |
|---|---|---|---|---|---|
| Escolinhas | `GET /Plataforma` | `[Plataforma]` | Painel | tabela com gestores, créditos e conta de recebimento; ações "Créditos" e "Entrar como equipe" (com confirmação) | M02-01 |
| Nova escolinha | `GET` e `POST /Plataforma/Nova` | `[Plataforma]` | Painel | assistente em 3 etapas: escolinha → modalidade → 1º gestor. Grava tudo de uma vez; em erro, volta na etapa do campo (D19) | M02-01, M01-01 |
| Escolinha criada | `GET /Plataforma/Criada` | `[Plataforma]` | Painel | a senha temporária aparece uma única vez (TempData); sem ela, volta para a lista | M02-01 |
| Conceder créditos | `GET /Plataforma/Creditos/{id}` e `POST /Plataforma/Creditos` | `[Plataforma]` | Painel | quantidade negativa = ajuste; o saldo nunca fica negativo | M02-11 |
| Jobs | `/jobs` | política `Jobs`: equipe LDNA sem troca de senha pendente | o painel do próprio Hangfire (fora do MVC) | item de menu da seção Plataforma | — |

Ação sem tela própria: `POST /Plataforma/EntrarComoEquipe/{id}` cria o vínculo de gestor (auditado, D06) e entra na escolinha.

### 2.3 Escolinha: gestor

| Tela | Rota | Acesso | Layout | Observações | BL |
|---|---|---|---|---|---|
| Painel | `GET /` | `[SomenteAutenticado]` | Painel | passos da configuração (feitos e a fazer) para quem lê a configuração; próximas entregas filtradas pela permissão. Sem escolinha na sessão: equipe → `/Plataforma`; os demais → Escolher escolinha | — |
| Dados da escolinha | `GET /Escolinha` | `[Permissao(ConfiguracaoEscolinha)]` | Painel | identificação, endereço e contato | M02-01 |
| Editar dados | `GET` e `POST /Escolinha/Editar` | `ConfiguracaoEscolinha`, `Alterar` | Painel | cada campo alterado vai para a auditoria | M02-01 |
| Modalidades | `GET /Modalidades` | `[Permissao(ConfiguracaoEscolinha)]` | Painel | um cartão por modalidade, com o perfil e os recursos ligados | M02-02, M02-03 |
| Nova modalidade | `GET` e `POST /Modalidades/Nova` | `ConfiguracaoEscolinha`, `Alterar` | Painel | view `Formulario`; os recursos começam pelo perfil Coletiva | M02-02 |
| Editar modalidade | `GET` e `POST /Modalidades/Editar/{id}` | `ConfiguracaoEscolinha`, `Alterar` | Painel | a mesma view; id de outra escolinha → 404 | M02-02 |
| Parâmetros | `GET /Parametros?dominio=...` e `POST /Parametros/Salvar` | ler: `ConfiguracaoEscolinha`; salvar: `Alterar` | Painel | abas `Presenca` (padrão), `Financeiro`, `Convocacao`, `Mensagem` e `Matricula` (na tela: Presença e evasão, Financeiro, Convocação, Mensagens, Matrícula e turmas); em erro, a aba volta com o que foi digitado e os campos marcados | M02-06 a M02-09, M02-12 |
| Histórico dos parâmetros | `GET /Parametros/Historico` (opcional `?chave=`) | `[Permissao(ConfiguracaoEscolinha)]` | Painel | antes → depois, quem e quando | M02-06 a M02-09 |
| Conta de recebimento | `GET /ContaRecebimento` | `[Permissao(FinanceiroEscolinha)]` | Painel | ambiente, final da chave e data do vínculo | M02-10 |
| Vincular ou trocar a conta | `GET` e `POST /ContaRecebimento/Vincular` | `FinanceiroEscolinha`, `Alterar` | Painel | pede a senha de quem troca; conta de Produção só com o sistema em Production | M02-10 |
| Créditos | `GET /Creditos` (opcional `?pagina=`) | `[Permissao(ConfiguracaoEscolinha)]` | Painel | saldo, plano, aviso de saldo baixo e o histórico paginado; só leitura (quem concede é a plataforma) | M02-11 |

### 2.4 Transversais

| Tela | Rota | Acesso | Layout | Observações |
|---|---|---|---|---|
| Módulo "em breve" | `GET /{chave}` | `[SomenteAutenticado]` | Painel | ver a §3 |
| Erro | `/Erro` e `/Erro/{codigo}` | `[AllowAnonymous]` † | Painel com login; Autenticação sem | 404 "Página não encontrada", 403 "Acesso negado" e 500 "Algo deu errado", com o código de rastreio. Fora de Development, exceção vai para `/Erro`, e status sem corpo, para `/Erro/{codigo}` (menos em `/api`). Em Development, exceção abre a página do desenvolvedor |
| Saúde | `GET /health` | anônimo | JSON | status do banco e do Hangfire, sem detalhe de erro |
| Vitrine do design system | `/dev/design`, `/dev/design/quadra`, `/dev/design/portal`, `/dev/design/autenticacao` | `[AllowAnonymous]` † | Vitrine, Quadra, Portal e Autenticação | só em Development; fora dele, 404 |

---

## 3. Páginas "em breve" (catálogo de módulos)

A rota `{chave}` (registrada antes da rota padrão) só aceita as chaves dos módulos com situação `Planejado` no `CatalogoModulos`. A página mostra o que o módulo vai fazer, a fatia e a lista de recursos.

| Rota | Módulo | Seção do menu | Fatia | Aparece no menu para (recurso, leitura) | Ícone |
|---|---|---|---|---|---|
| `/turmas` | Turmas | Operação | S1 | `CadastroAlunosResponsaveis`: gestor, professor e responsável | `ki-category` |
| `/alunos` | Alunos | Operação | S1 | `CadastroAlunosResponsaveis`: gestor, professor e responsável | `ki-people` |
| `/agenda` | Agenda | Operação | S1 | `RegistroPresenca`: gestor, professor e responsável | `ki-calendar` |
| `/chamada` | Chamada | Operação | S3 | `RegistroPresenca`: gestor, professor e responsável | `ki-check-squared` |
| `/jogos` | Jogos e convocações | Operação | S5 | `EventosConvocacao`: gestor e professor | `ki-cup` |
| `/mensalidades` | Mensalidades | Financeiro | S4 | `FinanceiroEscolinha`: gestor | `ki-dollar` |
| `/mensagens` | Mensagens | Comunicação | S2 | `ConfiguracaoEscolinha`: gestor | `ki-message-text-2` |
| `/importacao` | Importação | Escolinha | S7 | `ConfiguracaoEscolinha`: gestor | `ki-file-up` |

- **A página não confere o recurso** (não mostra dado nenhum): qualquer pessoa logada a abre pela URL. Quem filtra é o menu.
- **Pela matriz atual, uma pessoa só com o papel Responsável veria no menu do painel** Turmas, Alunos, Agenda e Chamada. O responsável usa o portal (S6): decidir lá se o painel o redireciona.
- **Quando o módulo ficar pronto:** no `CatalogoModulos`, troque a `Situacao` para `Disponivel` e aponte a `Url` para o controller real. A chave sai da rota `{chave}` sozinha, e o menu, a busca rápida e o Painel acompanham.

---

## 4. S1 a S7: notas por fatia

O que cada fatia encontra pronto no design system e o que ainda precisa ser construído. As referências de seção (§) são do [`GUIA-DESIGN-SYSTEM.md`](GUIA-DESIGN-SYSTEM.md).

### S1 — Cadastro (gestor; professor com escopo) · Painel

- **Onde entra:** `/turmas`, `/alunos` e `/agenda` deixam de ser "em breve". Locais e Professores ainda não têm entrada no catálogo.
- **Pronto para usar:** assistente em etapas (§6.6), select dependente (§6.7), seleção com busca (§6.10), abas (§6.8), `_Paginacao` (§6.15), tabela padrão (§6.17).
- **A construir** (plano, 4.4): a ficha lateral única (drawer com número de versão e resposta 409 em conflito), o modal de confirmação com motivo (desligar aluno) e o menu de ações ⋮; os selos das máquinas Aluno e Sessão (§4.3).
- **Dado de saúde:** sai do view model de quem não pode vê-lo (R5). Nunca esconder por CSS.

### S2 — Mensageria e acesso (gestor · Painel; convidado · Autenticação)

- **Onde entra:** `/mensagens` deixa de ser "em breve". Templates, Convites e Verificação de vínculos entram como itens novos da seção Comunicação do catálogo.
- **Ativação do convite:** no `_LayoutAutenticacao`; a forma já está na vitrine (`/dev/design/autenticacao`). Se tiver etapas, carregue o `assistente.js` pela seção `Scripts` (§2.2).
- **Sino:** no header do `_Layout` (§5.2), a construir.
- **`/dev/whatsapp`:** o simulador, no padrão `/dev/<parceiro>` do DP, fora de Production.
- **A construir:** o selo da máquina Mensagem (§4.3). Antes de pôr texto de terceiros em toast, escapar no `esc.toast` (§6.4).

### S3 — Presença e evasão (professor · Quadra; gestor · Painel)

- **Onde entra:** o controller `Chamada`, com o `_LayoutQuadra`; `/chamada` deixa de ser "em breve".
- **Pronto para usar:** a linha da chamada (`esc-linha-chamada`), o P/F (`esc-segmento` com `esc-presente` e `esc-falta`), a barra de ação ("Concluir chamada") e o selo de conexão (§5.3 e §6.9). Forma validada em `/dev/design/quadra`.
- **A construir:** a fila offline (service worker com escopo `/Chamada/` e IndexedDB), ligada ao selo por `data-pendentes` e por uma função pública de atualização (hoje interna ao `app.js`); o selo de risco (Sinais, §4.3) nas listas; a aba de alertas na ficha (CA-6).
- **R10:** poucos toques; funciona sem sinal; o registro da quadra prevalece.

### S4 — Financeiro (gestor · Painel)

- **Onde entra:** `/mensalidades` deixa de ser "em breve"; Divergências; a aba Financeiro na ficha; o simulador `/dev/asaas`.
- **A construir:** o selo da Cobrança e o sinal de inadimplência (§4.3); o helper `Formatacao.Moeda` (R$, §8); as ações por linha (isentar, cancelar, reenviar), que pedem o menu de ações ⋮ e o modal com motivo.

### S5 — Eventos e convocações (professor · Quadra e Painel)

- **Onde entra:** o controller `Convocacao`, com o `_LayoutQuadra` (e telas no painel); `/jogos` deixa de ser "em breve".
- **A construir** (plano, 4.4): `_CartaoJogo`, `_SelecaoConvocados` (contador e sinais de conflito, risco e inadimplência) e `_PainelConfirmacoes` (contadores contra o mínimo de confirmados, em tempo real); os selos Convocação e Evento (§4.3).
- **Tempo real:** o SignalR já é servido (`lib/signalr`); carregue pela seção `Scripts` da tela.

### S6 — Portal do responsável (responsável · Portal)

- **Onde entra:** a `Areas/Portal`, com o `_LayoutPortal` (pronto e validado em `/dev/design/portal`). As quatro abas do layout (Início, Jogos, Pagamentos, Avisos) ganham rotas no lugar de `#`.
- **Presenças (só leitura)** não tem aba própria no layout: decidir onde entra.
- **A construir** (plano, 4.4): `_CartaoFilho`, `_CartaoConvocacao` (Sim/Não com o nome do filho) e `_CartaoCobranca`; chips dos filhos com 48 px (hoje têm 36).
- **Pronto para usar:** os chips (`data-esc-escolha`), a resposta Sim/Não (`esc-resposta`) e o copiar (`data-esc-copiar`) para o Pix copia-e-cola.

### S7 — Carga inicial (gestor ou equipe · Painel)

- **Onde entra:** `/importacao` deixa de ser "em breve".
- **Pronto para usar:** o assistente em etapas para enviar → mapear → validar → conferir → confirmar; o ícone `ki-file-down` para o modelo `.xlsx`.
- **Na conferência:** a saúde aparece mascarada.

---

## 5. Como uma tela nova entra neste mapa

1. **Controller:** toda action com o atributo de acesso. Sem ele, o teste de arquitetura falha.
2. **Layout** conforme a superfície (§5 do guia).
3. **Catálogo:** módulo novo ou pronto vai para o `CatalogoModulos` (`Situacao`, `Url`, `Recurso`). O menu, a busca rápida e o Painel saem de lá.
4. **`ViewData["ModuloAtivo"]`** com a chave do módulo, para marcar o item ativo do menu.
5. **Checklist de aceite** (§12 do guia) e a vitrine atualizada.
6. **Este mapa:** status, rota, acesso, layout e data, na mesma entrega.
