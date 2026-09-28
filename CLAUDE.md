# CLAUDE.md — Minha Escolinha

Instruções para quem trabalha neste repositório, pessoa ou agente. Leia antes de qualquer tarefa.

## Comece por aqui — [`docs/PLANO.md`](docs/PLANO.md)

O plano diz onde estamos, a ordem de leitura e o método de trabalho. O progresso item a item está em [`docs/BACKLOG.md`](docs/BACKLOG.md); decisões, desvios e pendências, em [`docs/DECISOES-EM-ABERTO.md`](docs/DECISOES-EM-ABERTO.md).

---

## 1. O que é este projeto

**Minha Escolinha** (LDNA S/A) é um sistema de gestão para escolinhas esportivas, começando por futsal e futebol em Londrina. Automatiza as três tarefas que consomem o dia do gestor, nesta ordem de prioridade: **convocar para jogos, cobrar a mensalidade, controlar presença e evasão**.

| Fonte | Responde por |
|---|---|
| [`docs/especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md`](docs/especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md) e os 2 PDFs | **O quê**: piloto da Fase 1, 81 entregáveis `BL-Mxx-nn` em 8 fatias (S0–S7), 12 regras invioláveis (R1–R12) e 21 critérios de aceite |
| O **AlternativaDepartamentoPessoal** (DP), inventariado em [`docs/HERANCA.md`](docs/HERANCA.md) | **Como** construir. Se o DP já resolve um problema, use a solução dele. |

Públicos:
- **gestor:** painel;
- **professor:** quadra, no celular;
- **responsável:** portal web e WhatsApp, sem instalar nada;
- **equipe LDNA:** área da plataforma.

---

## 2. Onde este projeto vive

```
EscolinhaEsporte/                  (pasta; solução MinhaEscolinha.sln)
├── global.json · Directory.Build.props (prefixo MinhaEscolinha) · Directory.Packages.props (versões fixas)
├── .config/dotnet-tools.json      (dotnet-ef 9.0.20)
├── docs/  PLANO · HERANCA · BACKLOG · DECISOES-EM-ABERTO · especificacao/ · design/
│          metronic/ (referência local do Metronic, FORA do git: 61 MB, licença comercial)
├── src/
│   ├── Domain/          entidades e regras puras (sem pacotes)
│   ├── Application/     casos de uso, DTOs, validadores, interfaces
│   ├── Infrastructure/  EF Core, repositórios, interceptadores, segurança, Hangfire, Migrations
│   └── Web/             MVC + Razor, layouts, TagHelpers, front (Estilos/, scripts/, wwwroot/)
└── tests/Testes/        xUnit
```

- **A dependência aponta para dentro:**
  - `Domain` não depende de nada;
  - `Application` depende de `Domain` e declara as interfaces;
  - `Infrastructure` implementa essas interfaces;
  - `Web` depende de `Application` e `Infrastructure`.
- **Módulos:** a mesma pasta aparece nas camadas.
  - Hoje:
    - `Comum`, `Acesso` (M01), `Configuracao` (M02), `Eventos` e `Auditoria`;
    - `Turmas` (M04), `Alunos` (M03) e `Equipe` (M08), do S1; `Agenda`, só no domínio: compromisso datado e conflitos;
    - `Plataforma` e `Integracao` (Stripe, EX-01; WhatsApp, S2; Asaas, S4);
    - `Mensageria` (M09), do S2: motor, templates, convites (em `Acesso`) e o portal inicial;
    - `Presencas` (M05), do S3: chamada, alerta de faltas e risco de evasão;
    - `Financeiro` (M06), do S4: mensalidades, régua, conciliação e divergências.
    - Na `Infrastructure`, os repositórios do cadastro ficam juntos em `Cadastro/`.
  - Próximos: a tabela `Evento` e a convocação (S5, sobre o `Eventos` do domínio) e `Carga` (S7).

---

## 3. Banco de dados

- **Bancos:**
  - `escolinhaesporte`: SQL Server 2019 Express, compatibilidade 150 (`UseCompatibilityLevel(150)`). As migrations saem só daqui e são aplicadas no startup.
  - `escolinhaesporte_Testes`: recriado e migrado a cada execução dos testes com banco.
- **Convenções:**
  - PK `uniqueidentifier` com `NEWSEQUENTIALID()`, chamada `<Entidade>_ID`;
  - datas `datetime2(3)` no horário de Brasília;
  - tabelas no singular; índices nomeados `IX_`/`UQ_`;
  - configuração EF em `Data/Configuracoes`;
  - `rowversion` onde há concorrência: saldo de créditos, compra, turma, aluno, mensagem, presença e cobrança;
  - `TimeOnly` como `time(0)` (horários de turma e de disponibilidade).
- **Armadilha do EF com filho novo:** com a chave preenchida, o filho novo achado pelo grafo (a coleção de uma entidade já rastreada) é tratado como existente e gera `UPDATE`, que falha por concorrência. Há duas saídas:
  - a entidade nova ganha `Guid.NewGuid()` na criação e entra por `DbSet.Add` explícito, que é o padrão dos cadastros;
  - o filho que o domínio acrescenta pela coleção (a linha do tempo e as respostas da mensagem) nasce **sem** chave, e o banco gera (`NEWSEQUENTIALID`, D56).

  E o contrário: a entidade que entra por `DbSet.Add` com a chave de um pai rastreado já aparece na coleção dele (correção de relacionamentos do EF). Acrescentá-la de novo à coleção deixa a lista com ela duas vezes; confira com `Contains` antes (D65).
- **Multi-escolinha (X01):**
  - toda entidade de escolinha implementa `IEntidadeDaEscolinha` (`Escolinha_ID`), e todo índice único começa por `Escolinha_ID`;
  - o filtro global de escolinha é fechado por padrão: sem contexto, a consulta devolve zero linhas. No EF Core 9 cada entidade tem **um** filtro e `IgnoreQueryFilters()` ignora todos; outro filtro futuro (ex.: exclusão lógica) entra na mesma expressão;
  - o `EscolinhaInterceptor` carimba a escolinha no insert e lança exceção em gravação sem contexto, em outra escolinha ou trocando `Escolinha_ID`;
  - na requisição, o contexto vem do `ContextoEscolinhaMiddleware`; em plataforma, jobs e startup, do `IExecutorEscolinha`, que abre um escopo com o contexto definido;
  - `IgnoreQueryFilters()` só em `AcessoRepositorio.cs` e `ConsultasPlataforma.cs`. Um teste falha em qualquer outro arquivo. Aumentar a lista é decisão registrada, nunca atalho;
  - globais de propósito, fora do filtro: a identidade (`Pessoa`, `Credencial`), o webhook bruto (`WebhookRecebido`, que guarda a escolinha do aviso da Asaas numa coluna comum), os templates da plataforma (`TemplateMensagem`, D48) e o índice `RastreioWhatsApp`, que guarda só o id da Meta, a escolinha e a mensagem (D50).
- **Credenciais só em user-secrets** (dev) ou variável de ambiente (produção). O login `sa` do SQL local só vale para desenvolvimento.

---

## 4. Regras

### 4.1 Regras invioláveis do produto (spec §4)

| Regra | Conteúdo |
|---|---|
| **R1** | Pagamento só via Asaas (Pix, boleto, cartão). O dinheiro cai na conta da escolinha. |
| **R2** | Canais: app/portal + WhatsApp (API oficial). Nada essencial existe só no app. |
| **R3** | Nenhum módulo dispara mensagem por conta própria: os módulos produzem gatilhos, e um único motor envia. |
| **R4** | Nenhuma mensagem automática vai para o aluno menor. A comunicação é sempre mediada pelo responsável. |
| **R5** | Dado de saúde e documento do aluno: só gestor, professor da turma e o responsável do próprio aluno. |
| **R6** | Consentimento registrado na matrícula (imagem separado de dados). Revogar o de imagem não para a operação. |
| **R7** | Regra de negócio sensível é parâmetro do catálogo, por escolinha. Nunca constante no código. |
| **R8** | O núcleo não pressupõe esporte coletivo: evento com tipo + perfil de modalidade. **É a decisão mais cara de reverter.** |
| **R9** | Sinalizar, não bloquear. Bloqueio só se a escolinha ligar, e vem desligado. |
| **R10** | Poucos toques na quadra. A chamada funciona sem sinal, e o registro da quadra prevalece. |
| **R11** | Nada de trabalho duplicado: dado que existe num módulo não é redigitado em outro. |
| **R12** | Não invente requisito: registre a pendência e implemente o mínimo que não fecha portas. |

### 4.2 Nunca sem aprovação humana explícita (spec §2.3)

- WhatsApp real para número que não seja de teste.
- Cobrança real em produção.
- Migração destrutiva.
- Dependência ou padrão novo sem registrar em 12.3.
- Mudar a semântica de R1–R12.
- Qualquer item fora de escopo (§1.5): ligas, campeonatos, marketplace, dashboard consolidado, relatórios exportáveis, entre outros.
- **Commit e push só com autorização.**

### 4.3 Regras de OURO do código

**Herdadas do DP:**
1. ❌ **Nunca `DateTime.Now`, `DateTime.UtcNow` ou `DateTime.Today`.** Use `IRelogioBrasil`, `DataHoraBrasil` ou `TimeProvider` (teste de arquitetura).
2. ❌ **Nunca chamar sistema externo dentro do request HTTP.** Asaas, WhatsApp e Stripe sempre por job Hangfire com retentativa. **Única exceção (D26):** abrir a sessão de pagamento do Stripe na compra de créditos. A pessoa espera para ir ao pagamento; a compra é gravada antes e a chamada é idempotente.
3. ❌ **Nunca gravar URL temporária de terceiro.** Grave o identificador e busque de novo.
4. ✅ **Todo job relê o estado antes de agir.** Entre agendar e executar, tudo pode ter mudado.
5. ✅ **Quando o evento chega, cancele o que ficou pendente.** Pagamento confirmado para a régua na hora. Convite substituído ou cancelado suprime na hora a mensagem que ainda estava na fila (`EmissorGatilhos.CancelarPendentesAsync`, D51).
6. ✅ **Webhook: gravar o corpo bruto antes de interpretar**, responder `200` na hora e processar por job.
7. ✅ **Segredo nunca em arquivo versionado.**
8. ✅ **Distinguir erro retentável (5xx, timeout, 429) de definitivo.** O `FalhaIntegracao` do DP (`Application/Comum`) entrou com o Stripe (EX-01) e vale para a Asaas e o WhatsApp.
9. ✅ **Nunca truncar em silêncio.** O que for cortado vai para o log.
10. ✅ **Toda ação sensível registra quem, quando e o valor anterior** (`RegistroAlteracao`).

**Novas neste projeto:**

11. ✅ **Nenhum envio fora do catálogo de gatilhos (§8.3), nenhum destinatário aluno.** Substitui a regra do DP de copiar o WhatsApp ao atendente (X05). Teste: só o `EmissorGatilhos` cria `Mensagem`, e só o `ServicoEnvioMensagens` chama o canal.
12. ✅ **Todo dado de escolinha passa pelo filtro.** `IgnoreQueryFilters` só na lista permitida (teste).
13. ✅ **Toda action declara o acesso** com `[Permissao(Recurso, Operacao)]`, `[Plataforma]`, `[SomenteAutenticado]` ou `[AllowAnonymous]`. O padrão é negar (teste).
14. ✅ **Parâmetro de regra vem do catálogo** (`IParametros`) e vale o valor do momento da decisão. Multa e juros ficam congelados na emissão.
15. ✅ **Parâmetro de action que é coleção ou dicionário declara o nome do binding** (`[FromForm(Name = "...")]`). Sem isso, o ASP.NET liga com prefixo vazio e o dicionário recebe todos os campos do formulário (teste).

---

## 5. Convenções de código

- **Linguagem e estilo:**
  - tudo em português: classes, métodos, propriedades, tabelas, colunas e rotas;
  - `namespace` file-scoped, sem `region`; DI por construtor;
  - métodos assíncronos com sufixo `Async` e `CancellationToken ct = default`;
  - sem AutoMapper: mapeamento explícito.
- **Casos de uso:**
  - o retorno é `ResultadoOperacao`, nunca exceção para regra de negócio: `Ok`, `Erro(mensagem, campo?)` ou `Invalido(validacao)`, que traz o campo da 1ª falha;
  - no controller, `ModelState.AdicionarErro(resultado)`: o campo com erro ganha `aria-invalid` (`CampoInvalidoTagHelper`), e o assistente em etapas abre na etapa dele;
  - em erro, devolva a tela com o que foi digitado; em sucesso, PRG + toast (`TempData[ChavesTempData.Sucesso]`).
- **Concorrência:** entidade disputada tem versão de linha. O repositório traduz o conflito do EF para `ConflitoDeConcorrenciaException` (Application), e quem chama relê num escopo novo e decide de novo (D27). Nunca ignore o conflito.
- **Validação:** FluentValidation na `Application`, com validador ao lado do DTO. Não use `FluentValidation.AspNetCore` (descontinuado).
- **Usuário e contexto:**
  - `IUsuarioAtual.Id` é o `Pessoa_ID`;
  - `IContextoEscolinha` diz a escolinha da requisição e se a pessoa é da plataforma;
  - `IAutorizacao.Pode(recurso, operacao)` decide o acesso; `PodeAsync(recurso, operacao, alvo)` decide sobre um alvo, com escopo por `IResolvedorEscopo` (`ResolvedorEscopoCadastros`: turma, aluno, sessão e professor; o responsável, só os filhos com vínculo verificado, D53);
  - **listas filtram na consulta** pelas turmas de `IEscopoDeLeitura.TurmasAsync(recurso)`. A ficha confere `PodeAsync` e devolve 404 fora do escopo. O que vem junto (irmãos, outros filhos, marcas financeiras, CPF, valores) passa pelo mesmo escopo (D41);
  - dado de saúde pela `PoliticaSaude` (completo, treino ou nada), aplicada no view model. O registro de alterações nunca guarda o conteúdo (D36).
- **Dados e integrações:**
  - datas de auditoria por `IEntidadeAuditavel` + interceptor;
  - ação sensível via `RegistroDeAlteracoes.SeMudou/Registrar`;
  - segredo de negócio (chave Asaas da escolinha) com `IProtetorSegredos` (Data Protection); nunca em texto puro;
  - cliente HTTP externo sempre com `Microsoft.Extensions.Http.Resilience`;
  - valor em reais digitado: `decimal` com o binder `DecimalBrasileiroModelBinder` (`Moeda.TentarLer`) e campo mostrado com `Moeda.ParaCampo` (D30);
  - **mensagem:**
    - o módulo chama um método do `EmissorGatilhos`, um por gatilho do catálogo;
    - a mensagem é gravada na mesma transação da mudança de negócio, e o despacho sai depois do commit (D45);
    - o texto vem do template, com o nome da escolinha como sujeito (D48);
  - CPF (login ou digitado) nunca vai inteiro para o log: fica `***.456.789-**` (D52);
  - **presença:**
    - toda marcação passa pelo `ServicoChamada.SincronizarAsync`, online ou da fila do aparelho;
    - o id (UUID) gerado no aparelho garante que cada marcação seja aplicada uma vez só, e o registro da quadra prevalece (D59);
    - o alerta de faltas entra na mesma gravação das presenças (D61);
    - o motivo de saúde nunca vai para a página guardada no aparelho nem para o log das marcações (D64);
  - **mensalidade:**
    - nasce da projeção (`RotinaCobrancas`), nunca à mão. Quem muda a matrícula ou a situação do aluno chama o `EfeitosFinanceirosDoCadastro`: o fim da recorrência entra na mesma gravação, e a projeção sai depois, num job (D66, D71);
    - toda conversa com a Asaas sai de um job (`JobsFinanceiro`), com a chave da escolinha aberta só para a chamada (`ServicoAcessoAsaas`);
    - o que a Asaas diz de uma cobrança (webhook, varredura ou conferência) passa pela `ConciliacaoCobrancas`, que compara o valor original (D70);
    - divergência só sinaliza: nenhuma rotina muda a situação de uma cobrança com sinal, e a régua a ignora (D72).
- **Testes:**
  - xUnit + `FakeTimeProvider`;
  - com banco, `[FatoComBanco]` e a coleção `BancoDeTestes`, com `AmbienteAplicacao.ComoPlataformaAsync`, `NaEscolinhaAsync` e `SemContextoAsync`. O WhatsApp é o `GatewayWhatsAppFalso` e os jobs vão para o `AgendadorMensageriaDeTeste`; a Asaas é o `GatewayAsaasFalso` e os jobs do financeiro vão para o `AgendadorFinanceiroDeTeste`;
  - toda regra de negócio tem teste, e toda regra de ouro que pode ser verificada no código tem teste em `Arquitetura/RegrasDeOuroTestes.cs`.

---

## 6. Convenções visuais — fonte da verdade: [`docs/design/GUIA-DESIGN-SYSTEM.md`](docs/design/GUIA-DESIGN-SYSTEM.md)

Metronic 9 + KTUI + Tailwind 4.1.8 + KeenIcons (só `ki-filled`). O mapa das telas por fatia está em [`docs/design/MAPA-DE-TELAS.md`](docs/design/MAPA-DE-TELAS.md).

- **Classes:**
  - só as que existem no CSS servido; `npm run verificar-classes` precisa dar **0 faltando**;
  - prefixo do projeto `esc-`, `data-esc-`, `window.esc`; nunca `me-` (é o margin-end do Tailwind).
- **CSP `script-src 'self'`:** nada de `<script>` inline, `style=` ou `onclick=`; o JS de cada tela vai em arquivo.
- **Cor:**
  - só tokens (`--primary`, `--foreground`, `--muted`...); cor de status só via `<selo-status>`, a partir do mapa central `ApresentacaoStatus`;
  - claro e escuro por token; a chave do tema é `localStorage['kt-theme']`.
- **Layouts e toque:**
  - 4 superfícies: `_LayoutAutenticacao`, `_Layout` (painel), `_LayoutQuadra` e `_LayoutPortal`;
  - quadra e portal com alvos de 48 px (`esc-toque`) e barra de ação inferior fixa.
- **Service worker:** só na chamada (`/sw-chamada.js`, escopo `/Chamada`). Ao mudar o que ele guarda ou como guarda, suba a `VERSAO` dele, para a cópia antiga sair dos aparelhos (D60).
- **Formato pt-BR** pelos helpers de `Formatacao`: dd/MM/aaaa, R$ 0,00, 24 h, "2,5 %", "ligado/desligado".
- **Texto:** linguagem neutra, e o nome da escolinha nunca vem depois da palavra "escolinha". "A escolinha Escolinha X" duplica a palavra; use "Escolinha X informa" ou "em Escolinha X".
- **Checagem de tela:**
  - sem rolagem horizontal em 375 e 320 px;
  - elemento `position: fixed` não aparece na checagem de rolagem, então confira que nada passa da largura da tela; item flex em barra fixa precisa de `min-width: 0`;
  - **tabela no celular:** ficam só as colunas principais (cerca de 280 px, para caber em 320 px sem rolar a tabela). O dado secundário desce para a célula principal com `sm:hidden`, e a ação vira só ícone. O `<th>` da coluna escondida leva o mesmo `hidden sm:table-cell` da célula: sem isso, as colunas desalinham;
  - cabeçalho de cartão com `flex-wrap` leva `py-4`: sem ele, quando quebra em duas linhas, o conteúdo encosta na borda;
  - texto longo sem espaço (um link) leva `wrap-anywhere`;
  - botão só com ícone tem `aria-label`; modal no fim do `<body>`;
  - `esc.reinicializarComponentes()` depois de HTML injetado.

---

## 7. Comandos

```powershell
# .NET 9, igual ao DP: SDK 9.0.100 no global.json (rollForward latestFeature)
dotnet build MinhaEscolinha.sln          # precisa terminar sem aviso
dotnet test tests/Testes                 # inclui os testes com banco (escolinhaesporte_Testes, autenticação do Windows)
                                         # outra conexão para os testes: variável ESC_TESTES_SQL

# App em https://localhost:7186. No startup: aplica as migrations, cria o 1º administrador da plataforma
# (se não existir), reconcilia os parâmetros das escolinhas e grava os templates de WhatsApp que faltam.
dotnet run --project src/Web --launch-profile https

# Front (rodar em src/Web). O resultado vai para o git, porque o servidor não roda npm.
npm install
npm run build                            # metronic + fontes + bibliotecas + css + verificar-classes
npm run css                              # só o Tailwind (css/esc.css)
npm run verificar-classes                # precisa dar "0 classes faltando"

# Migration (só deste banco)
dotnet tool restore
dotnet ef migrations add NomeDaMigration --project src/Infrastructure --startup-project src/Web

# Segredos locais (UserSecretsId minha-escolinha-web). Nunca em appsettings versionado.
dotnet user-secrets set "ConnectionStrings:ConexaoPadrao" "<connection string>" --project src/Web
dotnet user-secrets set "Seed:Administrador:Login" "<e-mail>" --project src/Web
dotnet user-secrets set "Seed:Administrador:Nome" "<nome>" --project src/Web
dotnet user-secrets set "Seed:Administrador:Senha" "<senha temporária>" --project src/Web   # troca obrigatória no 1º acesso

# Stripe da LDNA (compra de créditos). Padrão: Simulado (/dev/stripe, sem conta). Para o modo de teste do Stripe:
dotnet user-secrets set "Stripe:Modo" "Teste" --project src/Web
dotnet user-secrets set "Stripe:ChaveSecreta" "sk_test_..." --project src/Web        # ou uma chave restrita rk_test_
dotnet user-secrets set "Stripe:SegredoWebhook" "whsec_..." --project src/Web        # o que o Stripe CLI mostrar
stripe listen --forward-to https://localhost:7186/api/v1/webhooks/stripe             # Stripe CLI: webhook em localhost

# WhatsApp (Cloud API da Meta). Padrão: Simulado (/dev/whatsapp, sem conta). Para a API real com número de teste (P34):
dotnet user-secrets set "WhatsApp:Modo" "Api" --project src/Web
dotnet user-secrets set "WhatsApp:IdNumero" "<id do número na Meta>" --project src/Web
dotnet user-secrets set "WhatsApp:TokenAcesso" "<token permanente>" --project src/Web         # usuário do sistema
dotnet user-secrets set "WhatsApp:SegredoApp" "<segredo do app>" --project src/Web           # confere a assinatura do webhook
dotnet user-secrets set "WhatsApp:TokenVerificacao" "<texto à escolha>" --project src/Web    # o mesmo do painel da Meta
dotnet user-secrets set "WhatsApp:NumerosTeste:0" "+5543..." --project src/Web            # fora de Production, só estes recebem
dotnet user-secrets set "WhatsApp:UrlPublica" "https://<endereço público>" --project src/Web  # vai no link do convite

# Asaas (mensalidades). Padrão: Simulado (/dev/asaas, sem conta). A chave de API é de cada escolinha, vinculada na tela
# Conta de recebimento (fora de Production, só Sandbox). Para a API de verdade (P41):
dotnet user-secrets set "Asaas:Modo" "Api" --project src/Web
dotnet user-secrets set "Asaas:UrlPublica" "https://<endereço público>" --project src/Web     # o webhook de cada escolinha usa este endereço
dotnet user-secrets set "Asaas:EmailAvisosWebhook" "<e-mail>" --project src/Web             # opcional: a Asaas avisa quando o webhook falha

# Certificado HTTPS de desenvolvimento do SDK novo (uma vez; o Windows pede confirmação)
dotnet dev-certs https --trust
```

---

## 8. Avisos operacionais

- **.NET 9 perde o suporte em 10/11/2026.** O projeto está em .NET 9 porque o servidor só tem esse runtime (D02). Antes dessa data, instale o Hosting Bundle do .NET 10 no servidor e suba o projeto, ou publique self-contained (P24).
- **Production exige certificado do Data Protection:** `DataProtection:CertificadoArquivo` e `DataProtection:CertificadoSenha`. Sem ele, o app **não sobe**. Perder o certificado significa perder as chaves Asaas de todas as escolinhas, e cada uma terá de vincular a conta de novo.
- **WhatsApp** (S2): roda em modo **Simulado** (`/dev/whatsapp`) até haver conta na Meta (P34). Trava de ambiente (`GuardaWhatsApp`):
  - **Simulado** em Production: o canal fica desligado, com aviso no log. As mensagens essenciais ficam retidas, e o portal continua;
  - **Api** exige `IdNumero`, `TokenAcesso`, `SegredoApp` e `TokenVerificacao`. Fora de Production, exige também `WhatsApp:NumerosTeste` e só manda para esses números (os demais são suprimidos, com o motivo). Em Production, exige `WhatsApp:ConfirmarProducao = true`;
  - só sai template **aprovado** pela Meta (tela Plataforma › Templates);
  - webhook: `https://<site>/api/v1/webhooks/whatsapp`, com o GET de verificação e o POST assinado. No painel da Meta, assine o campo `messages`.
- **Asaas** (S4): roda em modo **Simulado** (`/dev/asaas`) até haver conta sandbox (P41). Trava de ambiente (`GuardaAsaas`):
  - **Simulado** em Production: a emissão fica desligada, com aviso no log. As mensalidades continuam previstas, com o motivo;
  - **Api** em Production exige `Asaas:ConfirmarProducao = true`. Fora de Production, a conta de produção é recusada ao vincular, então só a Sandbox cobra;
  - `Asaas:UrlPublica` precisa ser https. O webhook de cada escolinha é `https://<site>/api/v1/webhooks/asaas/{escolinha}`, cadastrado na conta dela pela API quando a conta é vinculada (modo Api), com o token dela no cabeçalho `asaas-access-token`;
  - as notificações da própria Asaas ficam desligadas no cliente: só o motor fala com a família (R3). A chave desabilitada ou revogada na Asaas faz a emissão ser recusada; vincular a chave nova libera as recusadas (D73).
- **Stripe da LDNA** (só a LDNA cobra as escolinhas, pelos pacotes de créditos; a mensalidade do aluno é da Asaas, R1). Trava de ambiente (`GuardaStripe`):
  - **Simulado** em Production: a compra de créditos fica desligada, com aviso no log;
  - **Teste** em Production: o app **não sobe**, porque crédito comprado com cartão de teste gastaria WhatsApp de verdade. Para testar num servidor, use o ambiente Homologacao;
  - **Producao**: só em Production, com `Stripe:ConfirmarProducao = true`, chave `sk_live_`/`rk_live_` e `Stripe:SegredoWebhook`;
  - em produção, cadastre no painel do Stripe o endpoint `https://<site>/api/v1/webhooks/stripe` com os 4 eventos de Checkout (a tela Plataforma › Pacotes de créditos mostra a URL e a lista).
- **`docs/metronic/` não está no git.** Para rodar `npm run metronic` em outra máquina, copie de `Projetos\SaaS\Adama\docs\metronic`. O recorte servido (`wwwroot/assets`) já está versionado.
- **SQL Express:** teto de 10 GB por banco (P22). O banco de desenvolvimento está com `AUTO_CLOSE` desligado, porque o Hangfire consulta o banco sem parar.
- **O banco de desenvolvimento tem dados de validação.** Não use esses dados em demonstração ao cliente sem limpar:
  - as escolinhas A e B, com os seus gestores;
  - em A, os cadastros do S1 e a massa de demonstração;
  - o login de gestão de B (`gestor.b@bolanarede.test`) também tem o papel Professor em A, para validar o escopo do professor com login real. A ficha de professor "Paula Souza" foi ligada a essa pessoa por SQL;
  - em A, desde o S2: a responsável Ana Almeida tem acesso (login pelo CPF, com o vínculo do Arthur confirmado), e o log tem as mensagens da validação (convite e treino cancelado);
  - em A, desde o S3: uma sessão de domingo, 27/09, na turma "Sub-11 · segunda e quarta", com a falta da Ana Lima, o alerta e a mensagem à responsável;
  - em A, desde o S4: a conta de recebimento (Sandbox, com o simulador) e as mensalidades de outubro da validação: 50 a confirmar, 1 impedida, 1 paga com divergência aceita e 1 cancelada depois do estorno. As faturas do simulador ficam em `%LOCALAPPDATA%\MinhaEscolinha\simulador-asaas.json`.
- **Chamada sem sinal:** o navegador só registra o service worker com certificado confiável. Em desenvolvimento, rode `dotnet dev-certs https --trust`; sem isso, a fila funciona com a página aberta, mas a página não reabre sem sinal (P37).
- **Jobs recorrentes** (horário de Brasília, registrados no startup):
  - `sessoes-gerar-horizonte`, às 03:15: gera as sessões de treino até o fim do mês seguinte, uma escolinha por vez (D31);
  - `stripe-varredura-compras`, a cada 20 minutos: confere as compras de créditos pendentes. Sai quando o Stripe está desligado (`Stripe:Habilitado = false`);
  - `mensagens-varredura`, a cada 5 minutos, uma escolinha por vez: solta as mensagens retidas quando o saldo volta e reenfileira as que venceram (D45);
  - `mensalidades-rotina`, às 06:30, uma escolinha por vez: projeção, vencidas, régua e emissões (D66). As mensagens da régua esperam o fim da janela de silêncio;
  - `mensalidades-varredura`, de hora em hora (:40): confere na Asaas as mensalidades em aberto que o webhook não trouxe (D70). Sai quando a Asaas está desligada.
- **`/dev/design`** (vitrine), **`/dev/massa`** (massa de demonstração: 3 turmas e 60 alunos em 38 famílias na escolinha ativa, uma vez por escolinha, D40), **`/dev/whatsapp`** (o celular das famílias: entregar, ler, falhar, tocar num botão ou responder manda o webhook assinado; só com o modo Simulado) e **`/dev/asaas`** (a conta Asaas de mentira: pagar, pagar outro valor, vencer, estornar e excluir mandam o aviso pelo webhook, com o token da escolinha; a fatura fica em `/dev/asaas/fatura/{id}`; só com o modo Simulado) só existem em Development. Fora dele respondem 404.
