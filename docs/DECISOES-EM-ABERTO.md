# Decisões, desvios e pendências — Minha Escolinha

Registro exigido pela seção 12 da especificação ([`especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md`](especificacao/INSTRUCOES-IA-MINHA-ESCOLINHA.md)):

- **Decisões** (12.1): o que foi decidido sem consulta, porque o padrão do DP ou a spec cobrem.
- **Desvios da herança** (12.3): o que não existe no DP e entrou mesmo assim, com motivo e custo de reverter.
- **Pendências** (12.2): o que depende de decisão humana. Enquanto não houver decisão, vale o encaminhamento provisório.
- **Inconsistências da especificação**: pontos em que a própria spec se contradiz ou deixa lacuna.

O progresso por item (12.4) fica em [`BACKLOG.md`](BACKLOG.md).

> Regra: nenhuma lacuna é preenchida com suposição silenciosa (R12). Toda escolha provisória aparece aqui, e a tela a marca como provisória quando o usuário vê o valor.

---

## 1. Decisões tomadas (12.1)

| # | Data | Decisão | Motivo |
|---|---|---|---|
| D01 | 26/09/2026 | Solução `MinhaEscolinha.sln`; namespaces e DLLs `MinhaEscolinha.*`. A pasta continua `EscolinhaEsporte` e o banco continua `escolinhaesporte`. | Nome do produto na spec; a pasta e o banco já existiam. |
| D02 | 26/09/2026 (revista no mesmo dia) | **.NET 9, igual ao DP:** SDK 9.0.100 (`rollForward: latestFeature`), `net9.0`, pacotes Microsoft 9.0.20 e demais versões do DP. A 1ª versão do S0 foi feita em .NET 10 e voltou para o 9 antes do 1º commit. | **Decisão do responsável:** o servidor de produção só tem o runtime 9 por enquanto. Custo aceito: o suporte do .NET 9 termina em **10/11/2026** (ver P24). |
| D03 | 26/09/2026 | SQL Server 2019 Express (compatibilidade 150, `UseCompatibilityLevel(150)`). Os testes rodam no banco `escolinhaesporte_Testes`, recriado a cada execução. | Servidor disponível; sem isso, o EF geraria SQL para a compatibilidade 160 (SQL Server 2022). |
| D04 | 26/09/2026 | Identidade global = `Pessoa` + `Credencial`: login pelo e-mail, BCrypt fator 11, bloqueio de 5 tentativas por 15 min, carimbo de segurança, troca obrigatória de senha. A atuação por escolinha é o `VinculoAtuacao`: papel Gestor, Professor ou Responsável; situação Ativo ou Revogado. | BL-M01-01: uma pessoa, vários papéis e contextos, sem contas separadas. O mecanismo de senha e sessão é o do DP. |
| D05 | 26/09/2026 | A escolinha e o 1º gestor nascem numa gravação só, pela equipe da plataforma (`AdministradorPlataforma`). A senha temporária aparece uma vez e a troca é obrigatória no 1º acesso. | Resolve a circularidade apontada no S0. Se algo falha, nada fica gravado (teste). |
| D06 | 26/09/2026 | A equipe da plataforma não vê dado de escolinha nenhuma sem vínculo explícito de gestor ("Entrar como equipe"). O vínculo fica registrado na auditoria da escolinha e é revogável. | Isolamento entre escolinhas (§3 e §7), sem exceção para a equipe. |
| D07 | 26/09/2026 | Quem atua em mais de uma escolinha escolhe no login e troca pelo menu do usuário. Os vínculos são relidos a cada requisição, então a revogação vale na requisição seguinte. | A §7 exige alternância sem vazamento. O seletor rápido (BL-M01-02) não está no piloto. |
| D08 | 26/09/2026 | Matriz de permissões da §7 em código (`MatrizPermissoes`), com negação por padrão. Toda action declara o acesso (`[Permissao]`, `[Plataforma]`, `[SomenteAutenticado]` ou `[AllowAnonymous]`), e um teste de arquitetura garante isso. O escopo do dado passa por `IResolvedorEscopo`; no S0, sem cadastros, ele sempre nega. | BL-M01-03. O resolvedor real (turmas do professor, filhos do responsável) chega no S1. |
| D09 | 26/09/2026 | Parâmetros de regra (R7) = catálogo em código (`CatalogoParametros`, 26 definições em 5 domínios) mais a tabela chave-valor `ParametroEscolinha`, com o valor gravado em texto invariável. A leitura é tipada por domínio (`IParametros`). A reconciliação no startup, idempotente, grava o padrão que faltar. | A spec pede catálogo agrupado por domínio e legível por qualquer módulo. Ver X03. |
| D10 | 26/09/2026 | Valores iniciais: os sugeridos pela spec (anti-duplicidade 15 dias, silêncio das 21:00 às 07:00, aviso de trial 3 dias antes, bloqueio por inadimplência desligado). Os demais são **provisórios**, marcados no catálogo e na tela (tabela da seção 5). | 12.1 permite decidir o valor inicial; o que a spec não sugere vira pendência P05. |
| D11 | 26/09/2026 | Auditoria genérica: uma tabela `RegistroAlteracao` (entidade, id, campo, antes → depois, autor, data) para toda ação sensível, no lugar de uma tabela de histórico por agregado (o plano citava `EscolinhaHistorico` e `ParametroEscolinhaHistorico`). | A §7 exige autor, data e valor anterior em toda ação sensível. Um mecanismo só atende todas, e o histórico de parâmetros é uma consulta nele. |
| D12 | 26/09/2026 | Modalidade = perfil (nesta versão, só *Coletiva*) mais 6 interruptores (adversário, placar/súmula, classificação, convocação, graduação, apresentação), iniciados pelo perfil e editáveis. Duração padrão do evento: 60 min por modalidade, editável. | BL-M02-02. A duração resolve provisoriamente a pendência P01. |
| D13 | 26/09/2026 | R8 no domínio: `TipoEvento {Jogo}`, `RecursosDoEvento` e `PoliticaPerfil`, que diz o que cada tipo exige ou permite na modalidade. A tabela `Evento` nasce no S5, já genérica, com adversário opcional. | BL-M02-03, sem criar tabela antes da fatia que a usa. |
| D14 | 26/09/2026 | Conta Asaas da escolinha: ambiente (Sandbox ou Produção) e chave de API cifrada, mostrando só os 4 últimos caracteres. A troca pede a senha de quem troca. Conta de Produção só é aceita com o sistema em Production. | BL-M02-10 e R1. A chave é segredo de negócio digitado na tela. Ver X04. |
| D15 | 26/09/2026 | Créditos: `ContaCreditos` (saldo ≥ 0, versão de linha) mais o razão `MovimentoCredito` (lançamentos ±, saldo após, motivo, autor). Só a equipe da plataforma concede ou ajusta. O plano padrão é "Piloto". | BL-M02-11. O consumo por mensagem chega no S2 (BL-M09-04). |
| D16 | 26/09/2026 | Hangfire no mesmo processo e no mesmo banco. Filas `esc_padrao`, `esc_mensagens` e `esc_webhooks` no servidor "esc"; `esc_integracoes` no servidor "esc-integracoes". Painel `/jobs` só para a plataforma. | Mesmo padrão do DP. |
| D17 | 26/09/2026 | Front: Metronic 9 + KTUI + Tailwind 4.1.8 + KeenIcons (só a família filled), prefixo `esc-`. Marca provisória: o azul padrão do Metronic `#1379F0`. O recorte servido fica no git; `docs/metronic/` fica local. | Plano visual aprovado. Guia em [`design/GUIA-DESIGN-SYSTEM.md`](design/GUIA-DESIGN-SYSTEM.md). |
| D18 | 26/09/2026 | Arquivos estáticos por `UseStaticFiles`, como no DP. O `MapStaticAssets` (compressão e fingerprint), previsto no plano, **não** foi adotado no S0. | Nada no S0 depende dele. Reavaliar no S3, pelo desempenho da quadra com sinal ruim. |
| D19 | 26/09/2026 | Erro de caso de uso ligado ao campo: o serviço devolve `ResultadoOperacao.Campo` (`ResultadoOperacao.Invalido(validacao)` já traz o campo), o controller chama `ModelState.AdicionarErro(resultado)` e o `CampoInvalidoTagHelper` marca `aria-invalid`. O formulário em etapas abre na etapa do campo. Em erro, a tela volta com o que foi digitado. | Achado na validação no navegador: o assistente voltava para a etapa 1 e a aba de parâmetros perdia o que tinha sido digitado. |
| D20 | 26/09/2026 | Parâmetro de action que é coleção ou dicionário declara o nome do binding (`[FromForm(Name = ...)]`); um teste de arquitetura garante isso. | Achado na validação: sem o nome, o ASP.NET ligava o dicionário com prefixo vazio e o salvar dos parâmetros falhava em 4 das 5 abas. |
| D21 | 26/09/2026 | CNPJ da escolinha é opcional e validado quando informado. O CPF do gestor é opcional; se informado, não pode ser de outra pessoa com acesso. | Pendência P20; a colisão de CPF entre pessoas é recusada antes de gravar. |
| D22 | 26/09/2026 | Validação de tela do S0 feita por roteiro no navegador (Playwright) em 1280 px, 375 px e 320 px, no claro e no escuro. O teste automatizado de navegador (CA-21) será decidido no S3: `Microsoft.Playwright` nos testes ou roteiro assistido. | Estratégia de teste (12.1). O pacote de navegador seria dependência nova (12.3). |
| D23 | 26/09/2026 | O `esc.toast` escapa o texto antes de passar ao KTToast, que monta a mensagem como HTML. | Achado na revisão do guia: as mensagens levam nomes digitados na tela. A CSP bloqueia script, mas não marcação injetada. |
| D24 | 26/09/2026 | O MVC não põe `[Required]` implícito em `string` não anulável, e as mensagens de conversão do model binding estão em pt-BR (`MensagensValidacao`). Quem valida é o validador da Application. | Achado na validação: o login com a senha vazia mostrava "The Senha field is required." junto com "Informe a senha.". O DP tem o mesmo defeito. |
| D25 | 26/09/2026 | Estado sem enum de domínio (conta vinculada ou não, saldo abaixo do aviso) entra no mapa central como `SinalTela`; a View usa `<selo-status>`, que aceita `class` só de layout. | Regra do plano visual: a cor de status nunca é escolhida na View. |

---

## 2. Desvios da herança (12.3)

| # | Data | Desvio | O que motivou | O que foi considerado | Por que a solução do DP não serve | Custo de reverter |
|---|---|---|---|---|---|---|
| X01 | 26/09/2026 | **Multi-escolinha.** `Escolinha_ID` em toda tabela de escolinha. Os mecanismos: <ul><li>filtro global do EF Core, fechado por padrão: sem contexto, zero linhas. No EF 9 há um filtro por entidade e `IgnoreQueryFilters()` ignora todos; outro filtro futuro (ex.: exclusão lógica) precisa ser combinado nesta mesma expressão;</li><li>`EscolinhaInterceptor`, que carimba e valida as gravações;</li><li>`IContextoEscolinha` por requisição e `IExecutorEscolinha` para plataforma, jobs e startup;</li><li>`IgnoreQueryFilters` só nos arquivos de uma lista permitida, garantida por teste.</li></ul> | §3 e §7: uma pessoa atua em várias escolinhas e o dado de uma nunca aparece na outra. | Banco por escolinha: N migrations e N conexões no SQL Express. Schema por escolinha: mesmo problema. | O DP é de um cliente só; não tem conceito de tenant. | **Alto**: atravessa todas as tabelas. É a decisão estrutural do projeto. |
| X02 | 26/09/2026 | **Revertido no mesmo dia:** o projeto chegou a ser feito em .NET 10 e voltou ao .NET 9 do DP (D02). Hoje não há desvio de versão. | — | — | — | Subir para o 10 depois custa pouco: trocar o `net9.0`, o `global.json` e a linha dos pacotes. O filtro por escolinha pode continuar como está. |
| X03 | 26/09/2026 | Parâmetros em catálogo com linhas chave-valor, em vez da linha única tipada do DP (`ParametrosDp`). | A spec exige catálogo por domínio, legível por qualquer módulo, com parâmetros de nível de turma. | Linha tipada com uma coluna por parâmetro. | Cada parâmetro novo exigiria migration e tela nova; não atende o escopo por turma. | Médio. |
| X04 | 26/09/2026 | Uso do ASP.NET Data Protection (`IDataProtector`, propósito `MinhaEscolinha.Segredos.v1`) para cifrar **dado de negócio**: a chave de API da conta Asaas de cada escolinha. | Cada escolinha digita a própria chave na tela, então ela precisa ficar no banco, cifrada. | Chave em variável de ambiente, o que não escala por escolinha; cofre externo, que seria serviço novo. | O DP só guarda segredo do próprio sistema, em configuração. O pacote de chaves no banco já vem do DP; o uso do protetor é novo. | Baixo. Exige o certificado `DataProtection:CertificadoArquivo` em Production; o app não sobe sem ele. |
| X05 | 26/09/2026 | A regra de ouro 8 do DP ("todo WhatsApp gera cópia ao atendente") **não** foi herdada. No lugar dela: nenhum envio fora do catálogo de gatilhos e nenhum destinatário aluno. | R3, R4 e CA-20: só o catálogo da §8.3 dispara, e cada envio consome crédito. | — | Era requisito do cliente do DP. | Nenhum. |
| X06 | 26/09/2026 | `ResultadoOperacao` ganhou `Campo` e `Invalido(validacao)`; a Web ganhou `ModelState.AdicionarErro` e o `CampoInvalidoTagHelper`. | D19. | Deixar o erro só no resumo do topo, como no DP. | No assistente em etapas, o erro sem campo levava a pessoa de volta à etapa 1. | Baixo. |
| X07 | Previsto (S3) | Chamada offline: service worker com escopo `/Chamada/`, fila no IndexedDB e UUID por marcação. | R10 e CA-21: a chamada funciona sem sinal. | — | O DP não tem PWA. | Médio. Será registrado com detalhe ao entrar no S3. |

---

## 3. Pendências (12.2)

Status em 26/09/2026: **todas abertas**, cada uma com o encaminhamento provisório em vigor.

| # | Pendência | O que bloqueia | Encaminhamento provisório em vigor | Fatia |
|---|---|---|---|---|
| P01 | Duração padrão do evento | Detecção de conflito de agenda | Campo na Modalidade: 60 min, editável (D12) | S5 |
| P02 | Categoria de WhatsApp de cada gatilho | Aprovação dos templates pela Meta | Proposta: "utilidade" para todos os gatilhos da §8.3. **Validar com o responsável antes de submeter.** | S2 |
| P03 | Dois responsáveis habilitados respondem | O resultado da convocação | Vale a primeira resposta; o professor pode sobrescrever | S5 |
| P04 | Falta justificada conta para o alerta? | A regra de disparo | Conta em separado e não dispara alerta (parâmetro `presenca.falta_justificada.conta_para_alerta` = desligado) | S3 |
| P05 | Faixas mínima e máxima dos parâmetros | Configurações que inviabilizam a operação | Faixas provisórias no catálogo (seção 5) | S0 (valores) |
| P06 | Comportamento ao zerar créditos (o BL-M09-06 está fora dos 81, mas a §8.2 exige o comportamento) | Envio no S2 | Fallback da §8.2: suspende o WhatsApp, mantém o portal, avisa o gestor e retém as essenciais | S2 |
| P07 | Retenção de dados após o desligamento | O que pode ser apagado | Não apagar nada; não existe exclusão física de aluno | S1 |
| P08 | Atleta de 16 ou 17 anos | Acesso e comunicação | Mediação integral pelo responsável (R4) | S1/S2 |
| P09 | O CA-6 pede o alerta na ficha, mas o BL-M05-10 foi adiado | CA-6 | A ficha do aluno mostra o `AlertaFaltas` e o log das mensagens dele | S3 |
| P10 | O S6 e o CA-15 pedem a visão única, mas o BL-M10-02 foi adiado | CA-15 | A home do portal lista os filhos com vínculo verificado | S6 |
| P11 | "Treino remarcado" está no catálogo de gatilhos, mas o BL-M04-07 foi adiado; o estado "resultado homologado" é inalcançável | Máquinas de estado de sessão e evento | Remarcar = cancelar + criar outra sessão. Os estados existem no enum e ficam sem uso. | S1/S5 |
| P12 | Os alertas de trial ao gestor (§6) não estão no catálogo de gatilhos | Mensagem ao gestor | Sinal na tela, sem mensagem (CA-20) | S1 |
| P13 | A §8.3 diz "Evento alterado → Convocados", o que conflita com R4; o "comunicado de segurança" da §8.2 não existe no catálogo | Destinatários e janela de silêncio | Destinatários = responsáveis dos convocados. Exceção à janela de silêncio só para mudança nas próximas 24 h. | S5 |
| P14 | O número de WhatsApp é da LDNA ou de cada escolinha? Qual provedor? | Configuração do S2 | Uma conta WhatsApp Business da LDNA na Cloud API oficial, com o nome da escolinha no corpo da mensagem | S2 |
| P15 | Opt-in de WhatsApp (política da Meta) | Primeiro envio | Finalidade própria no consentimento; na importação, registrar a declaração do gestor | S1/S7 |
| P16 | Como o responsável ativa o acesso e entra | Portal | Link de uso único + CPF conferido + senha; recuperação manual pela equipe (o BL-M01-09 foi adiado) | S2 |
| P17 | Vencimento, emissão antecipada, pró-rata; pagador sem CPF | Emissão | Dia de vencimento parametrizado (padrão 10); 1ª cobrança confirmada à mão; um pagador por matrícula; sem CPF não há emissão | S4 |
| P18 | Dinheiro em espécie, estorno, chargeback | Conciliação | Fila de divergências; recebimento em dinheiro lançado com autoria | S4 |
| P19 | Transições do evento; quando a convocação vira "sem resposta" | Máquina de estado | Transições manuais; adiado é estado final. A convocação vira "sem resposta" no início do evento, e resposta tardia vai para o histórico. | S5 |
| P20 | O que o professor vê como "o necessário ao treino"; o trial é cobrado?; o CNPJ é obrigatório? | Ficha e cobrança | Alergias, restrições e contato de emergência; o trial entra na chamada e na convocação, mas não é cobrado; CNPJ opcional (D21) | S1/S4 |
| P21 | Dependências externas | S2, S4, S7 e produção | <ul><li>Templates aprovados e número do WhatsApp</li><li>Conta Asaas da escolinha piloto (sandbox e produção)</li><li>Pacote de créditos</li><li>A planilha real da escolinha</li><li>Hospedagem com HTTPS público para os webhooks</li><li>A "Especificação Funcional e Técnica", se existir</li><li>A identidade visual</li></ul> | várias |
| P22 | O SQL Express tem teto de 10 GB | Crescimento | Guardar o corpo bruto dos webhooks por 90 dias; o registro interpretado fica | S2/S4 |
| P23 | Pela matriz da §7, quem é só Responsável veria no menu do painel os módulos de Turmas, Alunos, Agenda e Chamada (o menu usa `Pode(recurso, Ler)`) | Nada hoje: o 1º vínculo de responsável nasce com o convite (S2) | Decidir no S2 se o login de quem é só Responsável vai direto para o portal (S6) | S2 |
| P24 | **O suporte do .NET 9 termina em 10/11/2026**: depois disso, sem correção de segurança. O piloto roda além dessa data. | Segurança do servidor a partir de 10/11/2026 | O app está em .NET 9 porque o servidor só tem o runtime 9. Instalar o Hosting Bundle do .NET 10 no servidor antes de 10/11/2026 e subir o projeto (custo baixo, X02). Outra saída é publicar self-contained, como o DP já faz: o runtime vai junto com o app. | produção |

---

## 4. Inconsistências da especificação (registradas, sem inventar regra)

| # | Onde | O que acontece | Tratamento |
|---|---|---|---|
| I01 | §8.2 × backlog | O fallback de créditos zerados é exigido, mas o BL-M09-06 não está entre os 81 | P06 |
| I02 | CA-6 × backlog | O alerta na ficha é exigido, mas o BL-M05-10 foi adiado | P09 |
| I03 | S6 / CA-15 × backlog | A visão única é exigida, mas o BL-M10-02 foi adiado | P10 |
| I04 | §8.3 × backlog | "Treino remarcado" é gatilho, mas a remarcação (BL-M04-07) foi adiada | P11 |
| I05 | §6 × §8.3 | Os alertas de trial ao gestor não estão no catálogo fechado de gatilhos | P12 |
| I06 | §8.3 × R4 | "Evento alterado → Convocados" poria o aluno como destinatário | P13 |
| I07 | §7 × backlog | A §7 exige alternância entre escolinhas, mas o BL-M01-02 não está no piloto | D07: escolha no login e troca pelo menu |
| I08 | BL-M01-12, BL-M09-13, BL-M10-12 | Três itens para a mesma mediação do responsável (R4) | Implementada uma vez no motor de mensagens (S2), com teste repetido em S2, S5 e S6 |
| I09 | BL-M03-10 | "liga apenas documentos de campeonato": campeonato é Fase 3 (§1.5) | Fora do escopo; nada a construir |
| I10 | 12.2 × documento | "Usar os padrões deste documento como faixa inicial", mas o documento não define faixas | P05: faixas provisórias |

---

## 5. Valores iniciais dos parâmetros (catálogo em 26/09/2026)

"Provisório" = a spec não sugere o valor; ele foi escolhido para o piloto e deve ser **confirmado com a escolinha**. A tela marca esses parâmetros com o selo "provisório" enquanto a escolinha não muda o valor.

| Domínio | Chave | Parâmetro | Padrão | Faixa | Provisório | Origem |
|---|---|---|---|---|:--:|---|
| Presença | `presenca.faltas.regra` | Regra do alerta de faltas | Faltas no mês | — | sim | RF-PRE-002/003 |
| Presença | `presenca.faltas.limite` | Faltas para disparar o alerta | 3 faltas | 1–30 | sim | RF-PRE-003, CA-PRE-01 |
| Presença | `presenca.alerta.tom` | Tom da mensagem de faltas | Cordial | — | sim | RF-PRE-002 |
| Presença | `presenca.alerta.janela_antiduplicidade_dias` | Janela anti-duplicidade | 15 dias | 1–90 | não (spec) | RF-PRE-011 |
| Presença | `presenca.risco.frequencia_minima` | Frequência mínima | 60 % | 10–100 | sim | RF-PRE-005 |
| Presença | `presenca.risco.janela_dias` | Janela de risco | 30 dias | 7–180 | sim | RF-PRE-005 |
| Presença | `presenca.falta_justificada.conta_para_alerta` | Falta justificada conta para o alerta | desligado | — | sim | P04 |
| Financeiro | `financeiro.multa.percentual` | Multa por atraso | 2 % | 0–10 | sim | RF-FIN-009 |
| Financeiro | `financeiro.juros.percentual_mes` | Juros ao mês | 1 % ao mês | 0–10 | sim | RF-FIN-009 |
| Financeiro | `financeiro.vencimento.dia` | Dia do vencimento | 10 | 1–28 | sim | RF-FIN-004 |
| Financeiro | `financeiro.emissao.dias_antes_vencimento` | Emissão antes do vencimento | 10 dias | 1–30 | sim | RF-FIN-004 |
| Financeiro | `financeiro.regua.dias_antes` | Lembretes antes do vencimento | 3 dias antes | 1–30 | sim | RF-FIN-004 |
| Financeiro | `financeiro.regua.no_dia` | Lembrete no dia do vencimento | ligado | — | sim | RF-FIN-004 |
| Financeiro | `financeiro.regua.dias_depois` | Cobranças após o vencimento | 1, 5, 10 dias depois | 1–60 | sim | RF-FIN-004 |
| Convocação | `convocacao.disparo.modo` | Quando a convocação sai | Na hora do disparo | — | sim | RF-CON-003 |
| Convocação | `convocacao.disparo.janela` | Janela de disparo | 08:00 às 20:00 | — | sim | RF-CON-003 |
| Convocação | `convocacao.lembrete.horas_antes` | Lembrete a quem não respondeu | 24 horas antes | 1–168 | sim | RF-CON-005 |
| Convocação | `convocacao.inadimplencia.sinalizar` | Sinalizar inadimplência na convocação | ligado | — | sim | RF-CON-016 |
| Convocação | `convocacao.inadimplencia.dias_atraso` | Atraso que sinaliza | 10 dias | 1–90 | sim | RF-CON-016 |
| Convocação | `convocacao.inadimplencia.bloquear` | Bloquear convocação de inadimplente | desligado | — | não (R9) | RF-CON-017 |
| Mensagem | `mensagem.silencio.janela` | Janela de silêncio | 21:00 às 07:00 | — | não (spec) | RF-MSG-009 |
| Mensagem | `mensagem.creditos.aviso_saldo_abaixo_de` | Aviso de saldo baixo | 50 créditos | 0–100000 | sim | RF-MSG-006 |
| Matrícula | `matricula.trial.dias_aviso_antes` | Aviso do fim do trial | 3 dias antes | 1–30 | não (spec §6) | Spec §6 |
| Turma | `turma.mensalidade` | Mensalidade (valor inicial das turmas) | sem padrão | 0–100000 | — | RF-FIN-001 |
| Turma | `turma.capacidade` | Capacidade de vagas | 20 vagas | 1–200 | sim | RF-ALU-013 |
| Turma | `turma.minimo_confirmados` | Mínimo de confirmados por jogo | 7 atletas | 1–30 | sim | RF-CON-011 |
