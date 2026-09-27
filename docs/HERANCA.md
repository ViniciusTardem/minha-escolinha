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
| **Mensagens e integrações existentes** | Padrão completo: `Opcoes<Parceiro>` com Habilitado e Modo (Simulado/Api), trava de ambiente no startup, simulador com página `/dev/<parceiro>` e estado em arquivo, webhook bruto + job + varredura, `FalhaIntegracao` retentável × definitiva, cliente com `Http.Resilience`. Aplicado ao ZapSign e ao GoHighLevel. | Asaas (S4) e WhatsApp Cloud API (S2) seguirão esse padrão, em modo Simulado até haver credencial. **Existem clientes Asaas em outros projetos da LDNA, a reaproveitar no S4; não existe cliente de WhatsApp Cloud API** (seção 3). |
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

## 3. Integrações existentes nos projetos locais

Varredura por **conteúdo** em todo `C:\Users\vinic\Documents\Projetos`, em 26/09/2026:
- **O que foi procurado:** `graph.facebook.com`, `api.asaas.com`, `sandbox.asaas.com`, `phone_number_id`, `whatsapp_business` e similares.
- **Onde:** arquivos `.cs`, `.js`, `.ts`, `.py` e `.php`, fora de `node_modules`, `bin`, `obj` e `vendor`.
- **Duração:** 27 minutos. Uma varredura anterior, só pelo nome dos arquivos, não tinha achado cliente Asaas; esta substitui aquela conclusão.

| Integração | O que existe | Uso previsto |
|---|---|---|
| **Asaas** | <ul><li>**`Projetos\Aplicativos\Asaas API`:** SDK .NET 8 (MIT) com `HttpClient` tipado, `AddAsaasClient()`, DTOs, enums e serviços por domínio (clientes, cobranças por boleto, Pix e cartão, Pix, assinaturas, estornos, webhooks, financeiro, sandbox). Traz também `docs/` em pt-BR, um arquivo por domínio com rotas, payloads e armadilhas, refletindo a API em abril/2026.</li><li>**`Projetos\SaaS\AgenciaAzul\Application\Integracoes\Asaas`:** cliente com cobranças, financeiro, notas e webhooks, e testes de contrato (`Testes\AgenciaAzul.Grafo.Testes\Asaas`).</li></ul> | **Reaproveitar no S4**, como manda a §3 da spec. A escolha entre referenciar o SDK ou portar o cliente da AgenciaAzul fica para o início do S4 e sai registrada em 12.1 ou 12.3. Em qualquer caso, o cliente fica atrás do padrão do DP: opções Simulado/Api, simulador, `FalhaIntegracao`, `Http.Resilience` e webhook bruto. A pasta `docs/` do SDK é leitura obrigatória antes da conciliação (CA-7 a CA-9). |
| **WhatsApp Cloud API** | Nenhum cliente. Os arquivos antes suspeitos (`ceal.londrina.br`, `LDNA SA 2026`) só geram links `wa.me`. As outras ocorrências da varredura eram falsos positivos: o endpoint `/messages` do GoHighLevel e da API da Anthropic, e bibliotecas `datatables`. | Construir no S2, no padrão de integração do DP |

## 4. O que NÃO foi herdado, e por quê

| Do DP | Motivo |
|---|---|
| Regra de ouro 8, "todo WhatsApp gera cópia ao atendente" | Violaria R3 e o CA-20 e gastaria crédito (X05) |
| Leitura do banco legado de R&S (`Infrastructure/Legado`, Dapper) | Não existe legado aqui. A carga inicial vem por planilha (S7). |
| ZapSign, GoHighLevel e o e-mail com caixa de saída local | Fora do escopo. As integrações do produto são só Asaas e WhatsApp (§8). |
| Parâmetros em linha única tipada (`ParametrosDp`) | A spec pede catálogo por domínio (X03) |
| Swashbuckle, `FluentValidation.AspNetCore`, pin de `System.Private.Uri` | Sem API pública; pacote descontinuado. Pin desnecessário: com o EF Core 9.0.20, o build não acusa a vulnerabilidade (NU1903) que motivou o pin no DP. |
