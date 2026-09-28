# Herança do projeto principal — Minha Escolinha

Registro da seção 3 da spec. A Minha Escolinha **não define stack**: herda a do projeto principal da LDNA.

| Item | Onde está |
|---|---|
| Projeto principal (a herança) | **AlternativaDepartamentoPessoal (DP)**, em `C:\Users\vinic\Documents\Projetos\.NET\AlternativaDepartamentoPessoal` |
| Template de stack e documentação | `docs/TEMPLATE-STACK-ARQUITETURA.md` do DP |
| Referência visual | `Projetos\SaaS\Adama\docs\metronic`, com guia verificado classe a classe; cópia local em `docs/metronic/`, fora do git |

**Regra:** onde o DP tem solução, use a dele. Onde não tem, escolha a opção mais simples, registre o desvio em [`DECISOES-EM-ABERTO.md`](DECISOES-EM-ABERTO.md) (12.3) e só então introduza.

Inventário feito em 26/09/2026 e atualizado ao fim de cada fatia.

---

## 1. O que a spec manda detectar

| O que detectar | No DP | Na Minha Escolinha |
|---|---|---|
| **Linguagem, framework e versão** | C#, ASP.NET Core MVC + Razor, .NET 9 | **Igual**: .NET 9, SDK 9.0.100, pacotes 9.0.20. O servidor só tem o runtime 9 (D02); subir para o 10 antes do fim do suporte (P24). |
| **Camada de dados e migração** | EF Core SQL Server. Migrations em `Infrastructure/Migrations`, aplicadas no startup. PK `<Entidade>_ID` com `NEWSEQUENTIALID()`, `datetime2(3)` no horário de Brasília, índices `IX_`/`UQ_`, tabelas no singular. | Igual. Banco próprio (`escolinhaesporte`, compatibilidade 150) e migration `Inicial` com 11 tabelas. |
| **Autenticação e autorização** | Cookie próprio + tabela `Usuario` + BCrypt (fator 11) + perfis e políticas. Carimbo de segurança revalidado, troca obrigatória de senha, bloqueio (5 tentativas → 15 min), rate limit no login. | O mesmo mecanismo sobre `Pessoa` + `Credencial` (global) + `VinculoAtuacao` (papel por escolinha), mais o motor de permissões da matriz da §7 (D04, D08) |
| **Multi-tenant** | Não existe (um cliente só) | Novo: `Escolinha_ID` + filtro global do EF Core + interceptor + contexto por requisição (X01) |
| **Organização de módulos e nomes** | 4 camadas, uma pasta por módulo nas camadas, tudo em português, prefixo das DLLs no `Directory.Build.props` | Igual, com o prefixo `MinhaEscolinha` |
| **Testes** | xUnit 2.9 + `FakeTimeProvider`. Banco `_Testes` com `[FatoComBanco]`, agendador de jobs em memória, testes de regras de ouro e de estrutura, contrato HTTP com handler falso. | Igual: xUnit 2.9.3, `[FatoComBanco]` em `escolinhaesporte_Testes`, regras de ouro em teste |
| **Fila e agendamento** | Hangfire 1.8 no mesmo processo e no mesmo banco, filas separadas, recorrentes no fuso de Brasília, painel `/jobs` só para o administrador | Igual (1.8.25), com as filas `esc_padrao`, `esc_mensagens`, `esc_webhooks` e `esc_integracoes` (D16) |
| **Mensagens e integrações existentes** | Padrão completo: `Opcoes<Parceiro>` com Habilitado e Modo (Simulado/Api), trava de ambiente no startup, simulador com página `/dev/<parceiro>` e estado em arquivo, webhook bruto + job + varredura, `FalhaIntegracao` retentável × definitiva, cliente com `Http.Resilience`. Aplicado ao ZapSign e ao GoHighLevel. | **WhatsApp Cloud API (S2) e Asaas (S4) seguem esse padrão**, em modo Simulado até haver credencial (D49, D75). A Asaas ganhou cliente próprio: os clientes de outros projetos da LDNA serviram de referência (seção 3). |
| **Observabilidade e log** | Serilog em arquivo diário; `/health` (banco + heartbeat do Hangfire); middleware de log de exceção | Igual. `/health` responde `Healthy` para banco e Hangfire. |
| **Ambientes, segredos e deploy** | user-secrets no dev, variáveis de ambiente em produção. Guarda contra banco de produção, CSP estrita, publish self-contained win-x64, keep-alive no `/health`. | Igual, com **UserSecretsId novo** (`minha-escolinha-web`; sem isso, o app leria os segredos do DP) e certificado do Data Protection obrigatório em Production (X04) |

---

## 2. O que foi portado do DP (F0 e S0)

Portado com o prefixo trocado; ajustes anotados.

| Camada | Arquivos |
|---|---|
| Raiz | `global.json` (SDK 9.0.100, igual ao do DP), `Directory.Build.props`, `Directory.Packages.props` (linha 9.0.x, versões do DP), `.gitignore` (+ `docs/metronic/`), `.config/dotnet-tools.json` (dotnet-ef 9.0.20) |
| Domain | `Comum/DataHoraBrasil`, `IEntidadeAuditavel`, `TextoHelper` (+ `NormalizarParaBusca`), `Cpf`, `Telefone` (+ sobrecarga de 1 argumento). `Cnpj` e `Endereco` são novos. |
| Application | `Comum/ResultadoOperacao` (+ `Campo`, `Invalido`: X06), `Pagina`, `IRelogioBrasil`, `IUsuarioAtual` (Id = `Pessoa_ID`); `Acesso/Senhas` (hash, senha temporária, regra de senha forte) |
| Infrastructure | `Tempo/RelogioBrasil`; `Data/Interceptadores/AuditoriaInterceptor`; `Seguranca` (BCrypt, guarda de banco de produção); `Data/Seed/SeedAdministradorPlataforma`; `Hangfire/HangfireEscolinhaExtensions`; trechos de `DependencyInjection` |
| Web | <ul><li>Pipeline: `Program.cs`; `Infraestrutura/Autenticacao` (cookie, carimbo, revalidação a cada 5 min), `UsuarioAtualHttp`, `CatalogoModulos`, `Formatacao`, `ChavesTempData`, `Saude`.</li><li>`Filters/ExigirTrocaSenhaFilter`; `Middleware/CabecalhosSegurancaMiddleware` (CSP sem `https:` em img/media, com `worker-src 'self'` e `manifest-src 'self'`) e `ExceptionLoggingMiddleware`.</li><li>Controllers `Conta`, `Erro` e `Modulos`; views `_Layout`, `_LayoutAutenticacao`, `_Sidebar`, `_Header`, `_Mensagens` e `_ErrosFormulario`.</li><li>Front: `wwwroot/js/tema.js`, `app.js`, `assistente.js` e `selecao-busca.js`; `package.json`; `scripts/copiar-fontes`, `copiar-bibliotecas` e `verificar-classes`.</li></ul> |
| Testes | `BancoDeTestes` (fixture, `[FatoComBanco]`), regras de ouro (relógio), guarda de ambiente, `DataHoraBrasil` |

### 2.1 O que o S1 acrescentou (27/09/2026)

O S1 não trouxe dependência nova: NuGet e npm continuam iguais aos do S0. As peças abaixo são novas e não têm equivalente direto no DP.

| Peça | Onde | Por quê |
|---|---|---|
| Linhas repetíveis | `wwwroot/js/linhas.js` | Horários da turma e faixas de disponibilidade do professor. <ul><li>Adicionar e retirar linhas renumera `name`, `id` e o oculto `__Invariant` que o ASP.NET põe nos campos de hora.</li><li>A linha nova sai de um `<template>` do servidor; o JS não monta HTML.</li><li>Sem JS, as linhas desenhadas pelo servidor funcionam, inclusive uma linha em branco extra.</li></ul> |
| Busca de responsável | `wwwroot/js/busca-responsavel.js` | CA-1: achar o responsável já cadastrado dentro do cadastro do aluno. Usa `esc.fetch` e mostra o CPF mascarado. Sem JS, o CPF igual reaproveita sozinho no servidor, e celular ou nome igual viram sugestão. |
| Valor em reais | `Domain/Comum/Moeda`, `Web/Infraestrutura/DecimalBrasileiro` | D30: o binder padrão em pt-BR leria "150.50" como 15.050. |
| Dias e horários da semana | `Domain/Comum/DiasSemana` (`HorarioSemanal`) | Semana começando na segunda, nomes em pt-BR ("na terça", "no sábado") e sobreposição de horários. |
| `TimeOnly` como `time(0)` | `EscolinhaDbContext.ConfigureConventions` | Convenção nova, ao lado do `datetime2(3)` herdado. |
| Versão de linha no formulário | `Application/Comum/Cadastros` (`Versao`, `LerVersao`, `MensagemConflito`) | D37. No DP, a versão vai pelo drawer (`ficha.js`); aqui vai num campo escondido do formulário, e o conflito vira mensagem. |
| Massa de demonstração | `Web/Infraestrutura/Desenvolvimento/MassaDemonstracao`, página `/dev/massa` | D40. Segue a ideia das páginas `/dev` do DP: só em Development. |

### 2.2 O que o S2 acrescentou (27/09/2026)

O S2 não trouxe dependência nova: NuGet e npm continuam iguais aos do S1. O cliente HTTP usa o `Microsoft.Extensions.Http.Resilience` que já existia.

| Peça | Onde | Relação com o DP |
|---|---|---|
| Motor de mensagens: outbox, despacho e varredura | `Application/Mensageria` (`EmissorGatilhos`, `ServicoEnvioMensagens`, `VarreduraMensagens`) e `Infrastructure/Mensageria` (`DespachoMensagensInterceptor`, `JobsMensageria`) | Segue o motor de lembretes do DP (outbox + varredura). **Novo:** o despacho sai de um interceptor do SaveChanges, depois do commit, então nenhum módulo precisa lembrar de enfileirar (D45). |
| Cliente da Cloud API e simulador | `Infrastructure/Integracao/WhatsApp` (`WhatsAppClienteHttp`, `SimuladorWhatsApp`, `GuardaWhatsApp`); página `/dev/whatsapp` | O padrão ZapSign e GoHighLevel do DP: opções Simulado/Api, trava de ambiente, simulador com estado em arquivo e contrato com handler falso. **Novo:** POST sem retentativa HTTP (D49). |
| Webhook do WhatsApp | `ProcessadorWebhooksWhatsApp`; `Api/WebhooksController` | O padrão do DP (corpo bruto, 200 na hora, job), na tabela genérica `WebhookRecebido` (D28), mais o índice global `RastreioWhatsApp` (D50). |
| Convite, ativação e portal | `Application/Acesso` (`ServicoConvites`, `ServicoAtivacaoConvite`, `ServicoPortal`); controllers `Convites`, `Ativacao`, `Vinculos` e `Portal` | **Não há equivalente no DP:** lá a equipe cria os usuários, e não existe área do cliente final. Aqui a família ativa sozinha, pelo link e pelo CPF (D51 a D54). |
| Agendador e gateway de teste | `AgendadorMensageriaDeTeste`, `GatewayWhatsAppFalso` | Seguem o `AgendadorDeTeste` do DP. |

### 2.3 O que o S3 acrescentou (27/09/2026)

O S3 não trouxe dependência nova: NuGet e npm continuam iguais aos do S2. O IndexedDB, o service worker e o `crypto.randomUUID` são do próprio navegador.

| Peça | Onde | Relação com o DP |
|---|---|---|
| Chamada sem sinal: fila e sincronização | `wwwroot/js/chamada.js`; `ChamadaController` (`Token` e `Sincronizar`); `Application/Presencas/ServicoChamada` | **Não há equivalente no DP**, que não tem trabalho offline (X07). O envio segue o `esc.fetch` do DP (antiforgery no cabeçalho), com o token buscado na hora porque a página pode estar guardada há horas (D60). |
| Service worker | `wwwroot/sw-chamada.js`, escopo `/Chamada` | Novo (X07). A CSP herdada já tinha `worker-src 'self'`. |
| Marcação idempotente | `Domain/Presencas` (`MarcacaoPresenca`, `RegraMarcacao`) | Segue a ideia do webhook do DP (guardar o que chegou antes de interpretar, e reconhecer o repetido pelo id), aplicada ao aparelho (D59). |
| Alerta com revalidação | `AvaliacaoFaltas` e `ValidadorOrigemAlertaFaltas` | As regras de ouro 4 e 5 do DP, no motor do S2 (D61, D62). |

### 2.4 O que o S4 acrescentou (28/09/2026)

O S4 não trouxe dependência nova: NuGet e npm continuam iguais aos do S3. O cliente da Asaas usa o `Microsoft.Extensions.Http.Resilience` que já existia.

| Peça | Onde | Relação com o DP |
|---|---|---|
| Cliente da API, simulador e trava | `Infrastructure/Integracao/Asaas` (`AsaasClienteHttp`, `SimuladorAsaas`, `GuardaAsaas`); página `/dev/asaas` | O padrão ZapSign e GoHighLevel do DP, como o WhatsApp no S2. **Novo:** a chave vem da escolinha a cada chamada (cifrada no banco, X04), e o pedido de criação não é repetido pela resiliência (D75). |
| Webhook por escolinha | `ProcessadorWebhooksAsaas`; `Api/WebhooksController` (`asaas/{escolinha}`) | O padrão do DP (corpo bruto, 200 na hora, job) na tabela `WebhookRecebido`. **Novo:** a escolinha vem do endereço, e o token é de cada escolinha (D70). |
| Recorrência, régua e emissão | `Domain/Financeiro` (`Cobranca`, `PlanejadorCobrancas`, `ReguaCobranca`); `Application/Financeiro` (`RotinaCobrancas`, `EmissaoCobrancas`) | A régua segue o motor de lembretes do DP (avaliador recorrente com chave única por degrau), sobre o motor de mensagens do S2 (D66, D69). |
| Conciliação e varredura | `ConciliacaoCobrancas`, `VarreduraCobrancas`, `ValidadorOrigemCobranca` | As regras de ouro 4, 5 e 6 do DP. A revalidação na hora do envio usa o validador de origem do S2 (D70). |
| Gateway e agendador de teste | `GatewayAsaasFalso`, `AgendadorFinanceiroDeTeste` | Seguem o `AgendadorDeTeste` do DP. |

## 3. Integrações existentes nos projetos locais

Varredura por **conteúdo** em todo `C:\Users\vinic\Documents\Projetos`, em 26/09/2026:
- **O que foi procurado:** `graph.facebook.com`, `api.asaas.com`, `sandbox.asaas.com`, `phone_number_id`, `whatsapp_business` e similares.
- **Onde:** arquivos `.cs`, `.js`, `.ts`, `.py` e `.php`, fora de `node_modules`, `bin`, `obj` e `vendor`.
- **Duração:** 27 minutos. Uma varredura anterior, só pelo nome dos arquivos, não tinha achado cliente Asaas; esta substitui aquela conclusão.

| Integração | O que existe | Uso previsto |
|---|---|---|
| **Asaas** | <ul><li>**`Projetos\Aplicativos\Asaas API`:** SDK .NET 8 (MIT) com `HttpClient` tipado, `AddAsaasClient()`, DTOs, enums e serviços por domínio (clientes, cobranças por boleto, Pix e cartão, Pix, assinaturas, estornos, webhooks, financeiro, sandbox). Traz também `docs/` em pt-BR, um arquivo por domínio com rotas, payloads e armadilhas, refletindo a API em abril/2026.</li><li>**`Projetos\SaaS\AgenciaAzul\Application\Integracoes\Asaas`:** cliente com cobranças, financeiro, notas e webhooks, e testes de contrato (`Testes\AgenciaAzul.Grafo.Testes\Asaas`).</li></ul> | **Referência no S4, sem reaproveitar o código** (D75, 28/09/2026): <ul><li>o SDK fixa uma chave para o app inteiro (`IOptions.ApiKey`), e aqui cada escolinha tem a sua;</li><li>o cliente da AgenciaAzul recebe a chave por instância, mas usa `HttpClient` estático por ambiente, sem `Http.Resilience`, e traz notas fiscais e financeiro, que o piloto não usa.</li></ul> O S4 escreveu o próprio (`AsaasClienteHttp`), no padrão do DP: opções Simulado/Api, simulador, `FalhaIntegracao`, `Http.Resilience` e webhook bruto. Os endereços da API e a documentação em pt-BR do SDK serviram de referência para a conciliação (CA-7 a CA-9). |
| **Stripe** (extra, X08) | Nenhum projeto usa Stripe em código: não há referência ao SDK `Stripe.net`, a `api.stripe.com` nem a `@stripe/` nos `.csproj`, `packages.config` e `package.json`. | Construído em 26/09/2026 no padrão de integração do DP, sem SDK (D29): cliente HTTP próprio, simulador em `/dev/stripe`, trava de ambiente, webhook bruto + job + varredura. |
| **WhatsApp Cloud API** | Nenhum cliente. Os arquivos antes suspeitos (`ceal.londrina.br`, `LDNA SA 2026`) só geram links `wa.me`. As outras ocorrências da varredura eram falsos positivos: o endpoint `/messages` do GoHighLevel e da API da Anthropic, e bibliotecas `datatables`. | **Construído no S2** (27/09/2026), no padrão de integração do DP e sem SDK (D49): cliente HTTP próprio, simulador em `/dev/whatsapp`, trava de ambiente, webhook bruto + job + varredura. |

## 4. O que NÃO foi herdado, e por quê

| Do DP | Motivo |
|---|---|
| Regra de ouro 8, "todo WhatsApp gera cópia ao atendente" | Violaria R3 e o CA-20 e gastaria crédito (X05) |
| Leitura do banco legado de R&S (`Infrastructure/Legado`, Dapper) | Não existe legado aqui. A carga inicial vem por planilha (S7). |
| ZapSign, GoHighLevel e o e-mail com caixa de saída local | Fora do escopo. As integrações do produto são só Asaas e WhatsApp (§8). |
| Parâmetros em linha única tipada (`ParametrosDp`) | A spec pede catálogo por domínio (X03) |
| Swashbuckle, `FluentValidation.AspNetCore`, pin de `System.Private.Uri` | Sem API pública; pacote descontinuado. Pin desnecessário: com o EF Core 9.0.20, o build não acusa a vulnerabilidade (NU1903) que motivou o pin no DP. |
