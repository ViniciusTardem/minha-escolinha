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
├── .config/dotnet-tools.json      (dotnet-ef 10)
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
  - Hoje: `Comum`, `Acesso` (M01), `Configuracao` (M02), `Eventos` e `Auditoria`.
  - Próximos: `Turmas`, `Alunos`, `Equipe`, `Mensageria`, `Presenca`, `Financeiro`, `Carga`, `Integracao`.

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
  - `rowversion` onde há concorrência (ex.: saldo de créditos).
- **Multi-escolinha (X01):**
  - toda entidade de escolinha implementa `IEntidadeDaEscolinha` (`Escolinha_ID`), e todo índice único começa por `Escolinha_ID`;
  - o filtro global de escolinha é fechado por padrão: sem contexto, a consulta devolve zero linhas. No EF Core 9 cada entidade tem **um** filtro e `IgnoreQueryFilters()` ignora todos; outro filtro futuro (ex.: exclusão lógica) entra na mesma expressão;
  - o `EscolinhaInterceptor` carimba a escolinha no insert e lança exceção em gravação sem contexto, em outra escolinha ou trocando `Escolinha_ID`;
  - na requisição, o contexto vem do `ContextoEscolinhaMiddleware`; em plataforma, jobs e startup, do `IExecutorEscolinha`, que abre um escopo com o contexto definido;
  - `IgnoreQueryFilters()` só em `AcessoRepositorio.cs` e `ConsultasPlataforma.cs`. Um teste falha em qualquer outro arquivo. Aumentar a lista é decisão registrada, nunca atalho.
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
5. ✅ **Quando o evento chega, cancele o que ficou pendente.** Pagamento confirmado para a régua na hora.
6. ✅ **Webhook: gravar o corpo bruto antes de interpretar**, responder `200` na hora e processar por job.
7. ✅ **Segredo nunca em arquivo versionado.**
8. ✅ **Distinguir erro retentável (5xx, timeout, 429) de definitivo.** O `FalhaIntegracao` do DP será portado com a primeira integração (S2).
9. ✅ **Nunca truncar em silêncio.** O que for cortado vai para o log.
10. ✅ **Toda ação sensível registra quem, quando e o valor anterior** (`RegistroAlteracao`).

**Novas neste projeto:**

11. ✅ **Nenhum envio fora do catálogo de gatilhos (§8.3), nenhum destinatário aluno.** Substitui a regra do DP de copiar o WhatsApp ao atendente (X05).
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
  - `IAutorizacao.Pode(recurso, operacao)` decide o acesso; `PodeAsync(recurso, operacao, alvo)` decide sobre um alvo, com escopo por `IResolvedorEscopo`.
- **Dados e integrações:**
  - datas de auditoria por `IEntidadeAuditavel` + interceptor;
  - ação sensível via `RegistroDeAlteracoes.SeMudou/Registrar`;
  - segredo de negócio (chave Asaas da escolinha) com `IProtetorSegredos` (Data Protection); nunca em texto puro;
  - cliente HTTP externo sempre com `Microsoft.Extensions.Http.Resilience`.
- **Testes:**
  - xUnit + `FakeTimeProvider`;
  - com banco, `[FatoComBanco]` e a coleção `BancoDeTestes`, com `AmbienteAplicacao.ComoPlataformaAsync`, `NaEscolinhaAsync` e `SemContextoAsync`;
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
- **Formato pt-BR** pelos helpers de `Formatacao`: dd/MM/aaaa, R$ 0,00, 24 h, "2,5 %", "ligado/desligado".
- **Checagem de tela:**
  - sem rolagem horizontal em 375 e 320 px;
  - elemento `position: fixed` não aparece na checagem de rolagem, então confira que nada passa da largura da tela; item flex em barra fixa precisa de `min-width: 0`;
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
# (se não existir) e reconcilia os parâmetros das escolinhas.
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

# Certificado HTTPS de desenvolvimento do SDK novo (uma vez; o Windows pede confirmação)
dotnet dev-certs https --trust
```

---

## 8. Avisos operacionais

- **.NET 9 perde o suporte em 10/11/2026.** O projeto está em .NET 9 porque o servidor só tem esse runtime (D02). Antes dessa data, instale o Hosting Bundle do .NET 10 no servidor e suba o projeto, ou publique self-contained (P24).
- **Production exige certificado do Data Protection:** `DataProtection:CertificadoArquivo` e `DataProtection:CertificadoSenha`. Sem ele, o app **não sobe**. Perder o certificado significa perder as chaves Asaas de todas as escolinhas, e cada uma terá de vincular a conta de novo.
- **Integrações:** os clientes Asaas (S4) e WhatsApp (S2) ainda não existem. Quando entrarem, nascem em modo **Simulado** (opções Simulado/Api e trava de ambiente, no padrão do DP) e só mudam para Api com credencial real. Já hoje, a conta Asaas de Produção só é aceita com o sistema em Production.
- **Stripe da LDNA** (só a LDNA cobra as escolinhas, pelos pacotes de créditos; a mensalidade do aluno é da Asaas, R1). Trava de ambiente (`GuardaStripe`):
  - **Simulado** em Production: a compra de créditos fica desligada, com aviso no log;
  - **Teste** em Production: o app **não sobe**, porque crédito comprado com cartão de teste gastaria WhatsApp de verdade. Para testar num servidor, use o ambiente Homologacao;
  - **Producao**: só em Production, com `Stripe:ConfirmarProducao = true`, chave `sk_live_`/`rk_live_` e `Stripe:SegredoWebhook`;
  - em produção, cadastre no painel do Stripe o endpoint `https://<site>/api/v1/webhooks/stripe` com os 4 eventos de Checkout (a tela Plataforma › Pacotes de créditos mostra a URL e a lista).
- **`docs/metronic/` não está no git.** Para rodar `npm run metronic` em outra máquina, copie de `Projetos\SaaS\Adama\docs\metronic`. O recorte servido (`wwwroot/assets`) já está versionado.
- **SQL Express:** teto de 10 GB por banco (P22). O banco de desenvolvimento está com `AUTO_CLOSE` desligado, porque o Hangfire consulta o banco sem parar.
- **O banco de desenvolvimento tem dados de validação:** as escolinhas A e B com os seus gestores. Não use esses dados em demonstração ao cliente sem limpar.
- **`/dev/design`** (vitrine) só existe em Development; fora dele responde 404.
