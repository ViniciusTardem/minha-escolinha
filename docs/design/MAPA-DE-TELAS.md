# Mapa de telas — Minha Escolinha

> Onde cada tela mora, quem acessa, com que layout e em que fatia ela chega. As rotas e os atributos de acesso foram conferidos nos controllers e no `Program.cs` em 27/09/2026, com a fatia S3 pronta. O visual de cada superfície está no [`GUIA-DESIGN-SYSTEM.md`](GUIA-DESIGN-SYSTEM.md) (§5).
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
- **No S1** entram os recursos de cadastro (D35):
  - `CadastroAlunosResponsaveis`: gestor com tudo; professor só no escopo das turmas dele;
  - `CadastroProfessores`: gestor;
  - `RegistroPresenca`: agenda e cancelar sessão; o professor, só as próprias. Desde o S3, também a chamada (`Alterar`) e a aba Presença da ficha do aluno (leitura);
  - `DocumentosObservacoesMedicas`: editar a saúde do aluno.
- **Escopo nas fichas:** a ficha fora do escopo responde **404**, como o id de outra escolinha.
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
| Extra EX-01 | Pacotes de créditos e integração com o Stripe (equipe); vitrine de pacotes e compras na tela de créditos, volta do pagamento (gestor); simulador `/dev/stripe` | equipe LDNA e gestor · Painel | `account/billing/*` | **Pronto** (modo Simulado) |
| S1 | Turmas (ocupação de vagas), ficha da turma (horários, composição, sessões), nova turma em etapas; Agenda (por turma, professor ou quadra); Locais | gestor; professor só leitura das suas · Painel | `network/user-table/*`, `account/activity` | **Pronto** (27/09/2026) |
| S1 | Alunos (por turma ou visão geral), **ficha do aluno** (página com abas: responsáveis, matrículas, saúde restrita, consentimentos, histórico; D37), novo aluno em etapas com detecção de duplicidade, desligar (data + motivo + efeitos); Responsáveis; Professores com disponibilidade | gestor; professor P · Painel | `public-profile/profiles/crm`, `account/members/*` | **Pronto** (27/09/2026) |
| S2 | Mensagens (log de entrega + linha do tempo), Templates (categoria, tom, aprovação), Convites em lote, fila de verificação de vínculos; ativação do convite; portal inicial (filhos confirmados e avisos); `/dev/whatsapp` (caixa de entrada por número, com os botões do template) | gestor · Painel; equipe LDNA · Painel (Templates); convidado · Autenticação; responsável · Portal | `account/notifications`, `account/members/import-members` | **Pronto** (27/09/2026), no simulador. O sino não foi construído: as pendências ficam no Painel (D55) |
| S3 | **Hoje → Chamada** (P/F/J num toque, sem sinal, "Concluir chamada" na barra inferior); selo de risco nas listas; aba Presença na ficha, com os alertas (CA-6) | professor · **Quadra**; gestor · Painel | — (tela própria) | **Pronto** (27/09/2026). A reabertura sem sinal espera o certificado confiável (P37) |
| S4 | Financeiro › Pagamentos (agrupado por situação, com as ações isentar, cancelar e reenviar); Divergências (resolução manual com autoria); aba Financeiro na ficha; `/dev/asaas` | gestor · Painel | `account/billing/*` | **Pronto** (28/09/2026), no simulador. Com as ações confirmar, receber por fora e tentar de novo, e o cartão no Painel |
| S5 | **Montar fim de semana** (jogos → convocados por jogo, com alertas → revisar → disparar tudo); **Painel de confirmações** em tempo real (registrar resposta por fora; cancelar, adiar ou alterar em um toque) | professor · **Quadra** (e Painel) | `network/user-cards/*` | Em breve (`/jogos`) |
| S6 | Portal: Início (filhos, pendências, próximos jogos), Jogos (Sim/Não por filho), Pagamentos (total do mês + detalhamento por filho, pagar pela fatura Asaas ou pelo Pix copia-e-cola), Presenças (só leitura), Avisos | responsável · **Portal** | `store-client/checkout/*` (só a forma) | Começado no S2: Início (filhos confirmados) e Avisos prontos; Jogos e Pagamentos "em breve" |
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
| Pacotes de créditos | `GET /Pacotes` | `[Plataforma]` | Painel | pacotes à venda (preço, créditos, por crédito) e a integração com o Stripe: modo, URL do webhook para copiar, eventos a assinar, segredo configurado e os últimos 10 webhooks | EX-01 |
| Novo / editar pacote | `GET /Pacotes/Novo`, `GET /Pacotes/Editar/{id}` e `POST /Pacotes/Salvar` | `[Plataforma]` | Painel | preço em pt-BR (`50,00`); fora de venda some da tela da escolinha; compra já feita guarda o preço dela | EX-01 |
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
| Créditos | `GET /Creditos` (opcional `?pagina=`) | `[Permissao(ConfiguracaoEscolinha)]` | Painel | saldo, plano, aviso de saldo baixo, **vitrine de pacotes** (com o Stripe ligado), compras e o histórico paginado | M02-11, EX-01 |
| Comprar pacote | `POST /Creditos/Comprar` (`pacoteId`) | `FinanceiroEscolinha`, `Alterar` (só o gestor) | — | abre a sessão no Stripe e leva ao Checkout (cartão, boleto ou Pix); em falha, volta com o erro | EX-01 |
| Compra de créditos | `GET /Creditos/Compra/{id}` (opcional `?cancelada=true`) e `GET /Creditos/SituacaoCompra/{id}` (JSON) | `[Permissao(ConfiguracaoEscolinha)]` | Painel | volta do Stripe: pede a conferência na hora e acompanha sozinha (`js/compra-creditos.js`, a cada 3 s, por até 2 min) | EX-01 |

### 2.4 Transversais

| Tela | Rota | Acesso | Layout | Observações |
|---|---|---|---|---|
| Módulo "em breve" | `GET /{chave}` | `[SomenteAutenticado]` | Painel | ver a §4 |
| Erro | `/Erro` e `/Erro/{codigo}` | `[AllowAnonymous]` † | Painel com login; Autenticação sem | 404 "Página não encontrada", 403 "Acesso negado" e 500 "Algo deu errado", com o código de rastreio. Fora de Development, exceção vai para `/Erro`, e status sem corpo, para `/Erro/{codigo}` (menos em `/api`). Em Development, exceção abre a página do desenvolvedor |
| Saúde | `GET /health` | anônimo | JSON | status do banco e do Hangfire, sem detalhe de erro |
| Vitrine do design system | `/dev/design`, `/dev/design/quadra`, `/dev/design/portal`, `/dev/design/autenticacao` | `[AllowAnonymous]` † | Vitrine, Quadra, Portal e Autenticação | só em Development; fora dele, 404 |
| Simulador do Stripe | `/dev/stripe` (sessões: confirmar, recusar, expirar) e `/dev/stripe/checkout/{id}` (pagar com cartão, gerar boleto ou Pix, desistir) | `[SomenteAutenticado]` | Painel; o checkout usa a Autenticação | só em Development com `Stripe:Modo = Simulado`; cada ação manda o webhook assinado ao endpoint de verdade (EX-01) |
| Webhook do Stripe | `POST /api/v1/webhooks/stripe` | `[AllowAnonymous]` (assinatura `Stripe-Signature`) | API | grava o corpo bruto e responde 200; o job interpreta. 400 com assinatura inválida, 503 com o Stripe desligado (EX-01) |

---

## 3. S1: telas prontas (27/09/2026)

Todas no Painel. "Professor: escopo" = o professor abre, mas só com o que é das turmas dele; fora disso, 404.

### 3.1 Turmas, agenda e locais

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Turmas | `GET /Turmas` (`?encerradas=true`, `?modalidade=`) | `[Permissao(CadastroAlunosResponsaveis)]`; professor: escopo | ocupação (barra + selo "Lotada"), horários resumidos, professor e local; a mensalidade só para quem vê valores | M04-01, M04-03 |
| Ficha da turma | `GET /Turmas/Ficha/{id}` | idem | dados, alunos matriculados (e os que saíram, com o período), próximas sessões com "cancelar" | M04-01, M04-03, M04-04 |
| Nova turma | `GET` e `POST /Turmas/Nova` | `ConfiguracaoEscolinha`, `Alterar` | assistente em 4 etapas: turma, horários, local e professor, vagas e valores. Valores iniciais do catálogo; conflito de agenda (professor ou quadra ocupada, D44) pede confirmação | M04-01, M04-09, EX-02 |
| Editar turma | `GET` e `POST /Turmas/Editar/{id}` | idem | a mesma view, inteira (sem etapas, D42); ajusta as sessões previstas; versão de linha | M04-01, M04-04 |
| Encerrar turma | `GET` e `POST /Turmas/Encerrar/{id}` | idem | só sem matrícula ativa; motivo obrigatório; tira da agenda as previstas futuras (D39) | M04-01 |
| Agenda | `GET /Agenda` (`recorte`, `id`, `de`, `ate`, `modalidadeId`, `situacao`) | `[Permissao(RegistroPresenca)]`; professor: escopo | por turma, professor ou local; o mês com setas; filtros secundários recolhidos em `<details>`; alertas por sessão (professor e quadra ocupada) | M04-08, M04-11, M04-09, EX-02 |
| Cancelar sessão | `GET` e `POST /Sessoes/Cancelar/{id}` | `RegistroPresenca`, `Alterar`; professor: só as próprias | motivo obrigatório; desde o S2, o aviso vai a cada família da turma pelo WhatsApp (e no portal de quem ativou) | M04-06 |
| Locais de treino | `GET /Locais` | `[Permissao(ConfiguracaoEscolinha)]` | endereço, capacidade e as turmas ativas (link para a agenda do local) | M04-08 |
| Novo e editar local | `GET /Locais/Novo`, `GET /Locais/Editar/{id}`, `POST /Locais/Salvar` | `ConfiguracaoEscolinha`, `Alterar` | view `Formulario`, com `_CamposEndereco` | M04-08 |

### 3.2 Alunos, responsáveis, matrículas e consentimentos

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Alunos | `GET /Alunos` (`busca`, `turmaId`, `situacao`, `pagina`) | `[Permissao(CadastroAlunosResponsaveis)]`; professor: escopo | por turma ou a escolinha inteira; padrão: quem está na escolinha (trial, ativo, inativo); selos "Trial vencido" e "Sem turma" | M03-05, M03-07 |
| Ficha do aluno | `GET /Alunos/Ficha/{id}` (`?aba=`) | idem | abas Resumo, Responsáveis, Matrículas, Saúde, Consentimentos e Histórico; ações de situação no cabeçalho; saúde por papel (R5); CPF, valores e irmãos por escopo (D41) | M03-02 a M03-11 |
| Novo aluno | `GET` e `POST /Alunos/Novo` (`?turmaId=`) | `CadastroAlunosResponsaveis` **e** `FinanceiroEscolinha`, `Alterar` | assistente em 5 etapas: aluno, responsáveis (busca, `_ResponsavelDoAluno`), matrícula, saúde, consentimentos; sugestões de cadastro parecido | M03-01 a M03-04, M03-09, M03-11 |
| Editar dados do aluno | `GET` e `POST /Alunos/Editar/{id}` | `CadastroAlunosResponsaveis`, `Alterar`; professor: escopo | dados básicos e endereço; versão de linha | M03-02 |
| Saúde do aluno | `GET` e `POST /Alunos/Saude/{id}` | `DocumentosObservacoesMedicas`, `Alterar` | alergias, restrições, observações e contato de emergência; o registro de alterações não guarda o conteúdo (D36) | M03-10, M03-11 |
| Mudar a situação | `GET /Alunos/Situacao/{id}?para=` e `POST /Alunos/Situacao/{id}` | `FinanceiroEscolinha`, `Alterar` | as transições da §6 e o atalho inativo → desistente (D43); desligar pede data e motivo e mostra os efeitos; rematrícula pede a turma; ajustar o prazo do trial | M03-07, M03-08 |
| Nova matrícula | `GET /Matriculas/Nova?alunoId=` e `POST /Matriculas/Nova` | `CadastroAlunosResponsaveis` e `FinanceiroEscolinha`, `Alterar` | turma, início e mensalidade própria; turma cheia só avisa (D38) | M03-04 |
| Encerrar matrícula, mensalidade | `GET` e `POST /Matriculas/Encerrar/{id}`, `/Matriculas/Mensalidade/{id}` | idem | encerrar pede data e motivo; mudar a mensalidade vai para o registro de alterações | M03-04 |
| Responsáveis | `GET /Responsaveis` (`busca`, `pagina`) | `[Permissao(CadastroAlunosResponsaveis)]`; professor: escopo | um cadastro por pessoa, com os filhos (só os do escopo) e o selo de acesso | M03-01 |
| Ficha do responsável | `GET /Responsaveis/Ficha/{id}` | idem | dados e alunos, com parentesco e as marcas do vínculo para quem vê o financeiro | M03-01, M03-03 |
| Editar responsável | `GET` e `POST /Responsaveis/Editar/{id}` | `CadastroAlunosResponsaveis`, `Alterar`; professor: escopo | CPF, telefone, e-mail e endereço | M03-01 |
| Busca de responsável | `GET /Responsaveis/Buscar?termo=` (JSON) | `CadastroAlunosResponsaveis`, `Alterar`; só nível Total | usada pelo `busca-responsavel.js`; CPF mascarado | M03-01 |
| Vincular responsável | `GET /Responsaveis/Vincular?alunoId=` e `POST /Responsaveis/Vincular` | `FinanceiroEscolinha`, `Alterar` | existente (busca) ou novo, com parentesco e as marcas | M03-03 |
| Vínculo | `GET` e `POST /Responsaveis/Vinculo/{id}`; `POST /Responsaveis/Desvincular/{id}` | idem | parentesco, pagador, recebe cobrança e recebe notificação; o último responsável não sai | M03-03 |
| Registrar e revogar consentimento | `GET /Consentimentos/Registrar?alunoId=&finalidade=`, `POST /Consentimentos/Registrar`; `GET` e `POST /Consentimentos/Revogar/{id}` | `CadastroAlunosResponsaveis`, `Alterar` | o texto do termo com a versão (provisório, P29), quem respondeu e a origem; a revogação registra quem e quando, com motivo opcional | M03-09 |

### 3.3 Equipe e ferramentas

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Professores | `GET /Professores` | `[Permissao(CadastroProfessores)]` | contato, turmas e o selo de acesso | M08-01 |
| Ficha do professor | `GET /Professores/Ficha/{id}` | idem | dados, turmas, disponibilidade, indisponibilidades e os alertas das próximas sessões | M08-01, M08-04 |
| Novo professor | `GET /Professores/Novo` (`?candidato=`) e `POST` | `CadastroProfessores`, `Alterar` | pessoa nova, pessoa com acesso a esta escolinha (ganha o papel Professor) ou responsável já cadastrado (D32) | M08-01 |
| Editar professor | `GET` e `POST /Professores/Editar/{id}` | idem | — | M08-01 |
| Disponibilidade | `GET` e `POST /Professores/Disponibilidade/{id}`; `POST /Professores/Indisponibilidade/{id}` e `/Professores/RemoverIndisponibilidade/{id}` | idem | faixas semanais (linhas repetíveis) e indisponibilidades com motivo | M08-04 |
| Massa de demonstração | `GET` e `POST /dev/massa` | `ConfiguracaoEscolinha` e `FinanceiroEscolinha`, `Alterar` | só em Development (fora dele, 404); uma vez por escolinha (D40) | — |

O **Painel** ganhou, no S1, os passos do cadastro (local, professor, turma, aluno), os treinos dos próximos 7 dias e os trials vencendo e vencidos.

---

## 3A. S2: telas prontas (27/09/2026)

### 3A.1 Gestão (Painel)

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Mensagens | `GET /Mensagens` (`busca`, `gatilho`, `situacao`, `canal`, `de`, `ate`, `alunoId`, `pagina`) | `[Permissao(ConfiguracaoEscolinha)]` | resumo de 30 dias (saldo, enviadas, entregues ou lidas, na fila ou retidas, falharam ou suprimidas) e o log paginado; na fila, "Sai em"; retida, suprimida e falhou com o motivo. No celular, a data e o canal descem para baixo do nome | M09-08 |
| Detalhe da mensagem | `GET /Mensagens/Detalhe/{id}` | idem | conteúdo (o link do convite mascarado), destinatário, aluno, template, prioridade, créditos, respostas e a linha do tempo; id de outra escolinha → 404 | M09-07, M09-08 |
| Convites de acesso | `GET /Convites` (`tipo`, `situacao`, `busca`) | `[Permissao(ConfiguracaoEscolinha)]` | responsáveis e professores com celular, com o selo de acesso; convidar um a um ou marcar e "Convidar marcados" (até 100); reenviar (confirmação) e cancelar | M01-04, M12-07 |
| Convidar, em lote, reenviar, cancelar | `POST /Convites/Convidar/{id}?tipo=`, `POST /Convites/Lote` (`selecionados`), `POST /Convites/Reenviar/{id}`, `POST /Convites/Cancelar/{id}` | `ConfiguracaoEscolinha`, `Alterar` | voltam para a tela de origem (`voltar`), com o toast | M01-04, M12-07 |
| Vínculos a confirmar | `GET /Vinculos`; `POST /Vinculos/Confirmar/{id}` e `/Vinculos/Recusar/{id}` | `CadastroAlunosResponsaveis`, `Alterar`, nível Total | quem ativou, o CPF conferido e o aluno; recusar exige motivo | M01-06 |
| Acesso nas fichas | fichas do responsável e do professor | quem convida | bloco `_AcessoDaFicha` (selo, validade, convidar ou reenviar); na ficha do responsável, o selo "portal: ..." de cada vínculo | M01-04, M01-06 |

O **Painel** ganhou, no S2, o passo "Convites de acesso" e as pendências da gestão no topo: vínculos a confirmar, mensagens retidas e saldo abaixo do aviso (D55). Quem é só responsável vai para o portal (D54).

### 3A.2 Plataforma, famílias e ferramentas

| Tela | Rota | Acesso | Layout | Observações | BL |
|---|---|---|---|---|---|
| Templates de WhatsApp | `GET /Templates`; `GET` e `POST /Templates/Editar/{id}` | `[Plataforma]` | Painel | um por gatilho e tom: nome na Meta, categoria, situação na Meta, botões e texto com `{{1}}`...; texto de template aprovado só muda com nome novo | M09-03 |
| Ativação do convite | `GET /Convite/{token}`; `POST /Convite/{token}/Identificar` e `/Convite/{token}/Ativar` | `[AllowAnonymous]`, com o limite de tentativas do login | Autenticação | 1º o CPF (nada aparece antes); depois o e-mail opcional e a senha, ou a senha da conta que já existe. Convite usado, vencido ou cancelado mostra "Convite indisponível" | M01-05 |
| Portal: Início | `GET /Portal` | `[SomenteAutenticado]`, escopo do responsável | Portal | os filhos com vínculo confirmado; enquanto houver vínculo pendente, só o aviso, sem nomes; avisos novos | M01-06, M12-11 |
| Portal: Avisos | `GET /Portal/Avisos` | idem | Portal | os avisos do motor (os mesmos do WhatsApp); abrir marca como lido | M09-02, M09-07 |
| Portal: em breve | `GET /Portal/EmBreve?aba=jogos` e `?aba=pagamentos` | idem | Portal | até o S5 e o S6 | — |
| Simulador do WhatsApp | `GET /dev/whatsapp`; `POST /dev/whatsapp/{id}/entregar`, `/ler`, `/falhar`, `/botao/{indice}`, `/responder`; `POST /dev/whatsapp/limpar` | `[SomenteAutenticado]` | Painel | só em Development com `WhatsApp:Modo = Simulado`; cada ação manda o webhook assinado ao endpoint de verdade | M09-02, M09-07 |
| Webhook do WhatsApp | `GET` (verificação da Meta) e `POST /api/v1/webhooks/whatsapp` | `[AllowAnonymous]` (assinatura `X-Hub-Signature-256`) | API | grava o corpo bruto e responde 200; o job interpreta. 400 com assinatura inválida, 503 com o WhatsApp desligado | M09-07 |

---

## 3B. S3: telas prontas (27/09/2026)

### 3B.1 Quadra (`ChamadaController`, `_LayoutQuadra`)

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Chamada: o dia | `GET /Chamada` | `RegistroPresenca`, `Alterar`; professor: as turmas dele | os treinos de hoje e os dos últimos 7 dias sem chamada; ao abrir com sinal, as páginas das sessões ficam guardadas no aparelho | M05-01 |
| Chamada da sessão | `GET /Chamada/Sessao/{id}` | idem; fora do escopo → 404 | P, F ou J por aluno; o motivo da justificada (e se é de saúde); "Todos presentes" e "Concluir chamada"; treino cancelado ou de data futura só com o aviso | M05-01, M05-13 |
| Token para o envio | `GET /Chamada/Token` (JSON, sem cache) | `RegistroPresenca`, `Alterar` | o antiforgery na hora do envio: a página guardada pode ter um token antigo | M05-01 |
| Sincronizar | `POST /Chamada/Sincronizar` (JSON: `marcacoes`, até 500) | idem | o resultado de cada marcação (aplicada, repetida, ignorada ou recusada) e quantos alertas saíram; 409 quando outra marcação grava a mesma presença ao mesmo tempo (a fila manda de novo) | M05-01, M05-08 |
| Sem sinal | `GET /Chamada/SemSinal` | `[AllowAnonymous]`, sem dado | o que o service worker mostra quando a página pedida nunca foi guardada | M05-01 |

O service worker é o arquivo estático `/sw-chamada.js`, com escopo `/Chamada` (guia §6.29).

### 3B.2 Gestão (Painel)

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Ficha do aluno, aba Presença | `GET /Alunos/Ficha/{id}?aba=presenca` | `RegistroPresenca`, leitura; professor: as turmas dele | a frequência da janela de risco, as presenças recentes (com o link para a chamada) e, para a gestão, os alertas de faltas com os destinatários e o estado de cada mensagem (CA-6). O motivo de saúde só para a gestão e o professor da turma | M05-11, M05-13 |
| Alunos em risco | `GET /Alunos?EmRisco=true` | a lista de alunos (§3.2) | a caixa "Só quem está em risco de evasão (N)" e o selo "Risco: N %" | M05-11 |

O **Painel** ganhou, no S3, o botão "Chamada de hoje" nos próximos treinos e o cartão "Risco de evasão", com os alunos abaixo do mínimo e o link "Ver todos". A **Agenda** ganhou o botão "Chamada" em cada sessão do dia e dos dias anteriores.

---

## 3C. S4: telas prontas (28/09/2026)

### 3C.1 Gestão (Painel, `MensalidadesController`)

Tudo exige `FinanceiroEscolinha`, que é só do gestor: o professor recebe 403 em qualquer rota (D74). As ações pedem `Alterar` e voltam, com PRG e toast, para a tela de origem (lista ou ficha).

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Painel de pagamentos | `GET /Mensalidades` (`competencia`, `grupo`, `turmaId`, `busca`) | `FinanceiroEscolinha` | o mês, com os seis totais por grupo, os filtros e a tabela (`_LinhaMensalidade`). Sem mês na URL, abre o de hoje, ou o seguinte na entrada no sistema | M06-03 |
| Atualizar agora | `POST /Mensalidades/Atualizar` | `Alterar` | a rotina da madrugada, sob demanda: projeção, vencidas, régua e emissões | M06-01 |
| Confirmar a primeira | `GET` e `POST /Mensalidades/Confirmar/{id}`; `POST /Mensalidades/ConfirmarMarcadas` (`selecionadas`) | `Alterar` | o valor só desta mensalidade; em lote, o valor da matrícula | M06-01 |
| Cancelar ou isentar | `GET /Mensalidades/Encerrar/{id}?para=Cancelada\|Isenta` e `POST` | `Alterar` | pede o motivo; a emitida é excluída na Asaas por job | M06-06 |
| Receber por fora | `GET` e `POST /Mensalidades/Receber/{id}` | `Alterar` | data (sem futuro), meio e observação; a régua para e a família recebe a confirmação | M06-06 |
| Reenviar | `POST /Mensalidades/Reenviar/{id}` | `Alterar` | o link da fatura de novo, pelo motor | M06-04 |
| Tentar de novo | `POST /Mensalidades/TentarDeNovo/{id}` | `Alterar` | a recusada pela Asaas volta para a emissão | M06-02 |
| Divergências | `GET /Mensalidades/Divergencias` | `FinanceiroEscolinha` | a fila, de qualquer mês | M06-06 |
| Resolver | `GET` e `POST /Mensalidades/Resolver/{id}` | `Alterar` | ciência, aceitar o valor, cancelar ou isentar, com explicação; sem divergência → 404 | M06-06 |
| Ficha do aluno, aba Financeiro | `GET /Alunos/Ficha/{id}?aba=financeiro` | `FinanceiroEscolinha` (para os demais, a aba não aparece) | as 24 mensalidades mais recentes, com as mesmas ações; o contador da aba mostra só o que pede atenção | M06-03, M06-24 |

O **Painel** ganhou o cartão "Mensalidades", com as primeiras a confirmar, as impedidas, as vencidas e as divergências. A **Conta de recebimento** passou a mostrar a integração (Simulador ou API), o endereço do webhook e quando ele foi cadastrado na Asaas.

### 3C.2 Integração e ferramentas

| Tela | Rota | Acesso | Observações | BL |
|---|---|---|---|---|
| Webhook da Asaas | `POST /api/v1/webhooks/asaas/{escolinhaId}` | `[AllowAnonymous]`, com o token da escolinha no cabeçalho `asaas-access-token` (401 sem ele) | grava o corpo bruto e responde 200; um job processa | M06-05 |
| Simulador da Asaas | `GET /dev/asaas`; `POST /dev/asaas/{id}/pagar`, `pagar-diferente`, `vencer`, `estornar` e `excluir` | `[SomenteAutenticado]`, na escolinha ativa; só em Development e com o modo Simulado (fora disso, 404) | as cobranças que o sistema emitiu; cada ação manda o aviso pelo webhook de verdade | — |
| Fatura do simulador | `GET /dev/asaas/fatura/{id}` | idem | o link que vai na mensagem da régua; pagar com Pix, boleto ou cartão | — |

---

## 4. Páginas "em breve" (catálogo de módulos)

A rota `{chave}` (registrada antes da rota padrão) só aceita as chaves dos módulos com situação `Planejado` no `CatalogoModulos`. A página mostra o que o módulo vai fazer, a fatia e a lista de recursos.

| Rota | Módulo | Seção do menu | Fatia | Aparece no menu para (recurso, leitura) | Ícone |
|---|---|---|---|---|---|
| `/jogos` | Jogos e convocações | Operação | S5 | `EventosConvocacao`: gestor e professor | `ki-cup` |
| `/importacao` | Importação | Escolinha | S7 | `ConfiguracaoEscolinha`: gestor | `ki-file-up` |

- **A página não confere o recurso** (não mostra dado nenhum): qualquer pessoa logada a abre pela URL. Quem filtra é o menu.
- **Desde o S1**, Turmas, Alunos, Responsáveis e Agenda (Operação) e Professores e Locais de treino (Escolinha) são módulos prontos no `CatalogoModulos` e saíram desta lista. **Desde o S2**, também Mensagens e Convites de acesso (Comunicação), e Templates (Plataforma). **Desde o S3**, a Chamada (Operação). **Desde o S4**, as Mensalidades (Financeiro).
- **Quem é só Responsável não vê o painel:** desde o S2, o painel o leva ao portal (D54).
- **Quando o módulo ficar pronto:** no `CatalogoModulos`, troque a `Situacao` para `Disponivel` e aponte a `Url` para o controller real. A chave sai da rota `{chave}` sozinha, e o menu, a busca rápida e o Painel acompanham.

---

## 5. S1 a S7: notas por fatia

O que cada fatia encontra pronto no design system e o que ainda precisa ser construído. As referências de seção (§) são do [`GUIA-DESIGN-SYSTEM.md`](GUIA-DESIGN-SYSTEM.md).

### S1 — Cadastro (gestor; professor com escopo) · Painel — **pronto em 27/09/2026** (§3)

- **Construído com o que já existia:** assistente em etapas (§6.6), abas (§6.8), `_Paginacao` (§6.15) e tabela padrão (§6.17).
- **Construído no S1** (guia §6.19 a §6.24): linhas repetíveis, busca de responsável e `_ResponsavelDoAluno`, `_CamposEndereco`, barra de ocupação, item de agenda, cabeçalho de etapa e filtros recolhidos.
- **Não construído:**
  - a ficha lateral (drawer): a ficha é página com abas (D37);
  - o modal de confirmação com motivo: desligar, encerrar e cancelar são páginas próprias com o motivo no formulário;
  - o menu de ações ⋮: as ações ficam no cabeçalho da ficha.
- **Dado de saúde:** sai do view model de quem não pode vê-lo (R5), nunca escondido por CSS. O professor vê só o necessário ao treino.

### S2 — Mensageria e acesso (gestor · Painel; convidado · Autenticação; responsável · Portal) — **pronto em 27/09/2026** (§3A)

- **Construído com o que já existia:** tabela padrão (§6.17), `_Paginacao`, `esc-linhas`, `<selo-status>` com as cinco máquinas novas (§4.2), `_LayoutAutenticacao` na ativação e `_LayoutPortal` no portal.
- **Construído no S2** (guia §6.25 a §6.28): `_AcessoDaFicha`, as pendências do painel, a linha do tempo da mensagem e o simulador `/dev/whatsapp`. Novo padrão: a tabela cabe em 320 px sem rolar (§6.17).
- **Não construído:** o sino no header (D55). A ativação não usa o assistente: são duas páginas, CPF e depois senha.
- **Toast com texto de terceiros:** o `esc.toast` já escapa (D23), então os nomes das famílias vão seguros.

### S3 — Presença e evasão (professor · Quadra; gestor · Painel) — **pronto em 27/09/2026** (§3B)

- **Construído com o que já existia:**
  - a linha da chamada, o controle segmentado, a barra de ação e o selo de conexão (§5.3 e §6.9);
  - `<selo-status>` com as duas máquinas novas e o sinal de risco (§4.2);
  - as abas da ficha e o `<estado-vazio>`.
- **Construído no S3** (guia §6.29):
  - o J e o painel do motivo;
  - a fila no aparelho e o service worker;
  - o `esc.atualizarConexao()` público, para o selo acompanhar a fila;
  - a lista do dia (`esc-item-chamada`).
- **Não validado nesta máquina:** a reabertura da página sem sinal, que depende do service worker e de um certificado confiável (P37).
- **R10:** poucos toques; funciona sem sinal; o registro da quadra prevalece.

### S4 — Financeiro (gestor · Painel) — **pronto em 28/09/2026** (§3C)

- **Construído com o que já existia:** tabela padrão (§6.17), abas da ficha, `<selo-status>` com as duas máquinas novas e três sinais (§4.2), `Formatacao.Moeda` e o binder de reais (§8), `_LayoutAutenticacao` na fatura do simulador.
- **Construído no S4** (guia §6.30): a `_LinhaMensalidade` das três listas, as telas de ação com o `_ResumoMensalidade`, o cartão do Painel e o simulador `/dev/asaas`. Novas regras: o `<th>` da coluna escondida acompanha a célula, e o cabeçalho de cartão que quebra leva `py-4`.
- **Não construído:** o menu de ações ⋮ e o modal com motivo. As ações da linha são ícones com `aria-label`, e cancelar, isentar, receber e resolver são páginas próprias, com o motivo no formulário, como no S1.
- **O sinal de inadimplência** (§4.3) nasce com a convocação, no S5 (`convocacao.inadimplencia.*`).

### S5 — Eventos e convocações (professor · Quadra e Painel)

- **Onde entra:** o controller `Convocacao`, com o `_LayoutQuadra` (e telas no painel); `/jogos` deixa de ser "em breve".
- **A construir** (plano, 4.4): `_CartaoJogo`, `_SelecaoConvocados` (contador e sinais de conflito, risco e inadimplência) e `_PainelConfirmacoes` (contadores contra o mínimo de confirmados, em tempo real); os selos Convocação e Evento (§4.3).
- **Tempo real:** o SignalR já é servido (`lib/signalr`); carregue pela seção `Scripts` da tela.

### S6 — Portal do responsável (responsável · Portal)

- **Onde entra:** o `PortalController` do S2 (`Views/Portal`, sem área), com o `_LayoutPortal`. Início e Avisos já existem; Jogos e Pagamentos trocam o "em breve" pelas telas de verdade.
- **Presenças (só leitura)** não tem aba própria no layout: decidir onde entra.
- **A construir** (plano, 4.4): `_CartaoFilho`, `_CartaoConvocacao` (Sim/Não com o nome do filho) e `_CartaoCobranca`; chips dos filhos com 48 px (hoje têm 36).
- **Pronto para usar:** os chips (`data-esc-escolha`), a resposta Sim/Não (`esc-resposta`) e o copiar (`data-esc-copiar`) para o Pix copia-e-cola.

### S7 — Carga inicial (gestor ou equipe · Painel)

- **Onde entra:** `/importacao` deixa de ser "em breve".
- **Pronto para usar:** o assistente em etapas para enviar → mapear → validar → conferir → confirmar; o ícone `ki-file-down` para o modelo `.xlsx`.
- **Na conferência:** a saúde aparece mascarada.

---

## 6. Como uma tela nova entra neste mapa

1. **Controller:** toda action com o atributo de acesso. Sem ele, o teste de arquitetura falha.
2. **Layout** conforme a superfície (§5 do guia).
3. **Catálogo:** módulo novo ou pronto vai para o `CatalogoModulos` (`Situacao`, `Url`, `Recurso`). O menu, a busca rápida e o Painel saem de lá.
4. **`ViewData["ModuloAtivo"]`** com a chave do módulo, para marcar o item ativo do menu.
5. **Checklist de aceite** (§12 do guia) e a vitrine atualizada.
6. **Este mapa:** status, rota, acesso, layout e data, na mesma entrega.
