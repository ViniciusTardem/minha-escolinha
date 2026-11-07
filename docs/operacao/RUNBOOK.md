# Runbook — Minha Escolinha

> Como publicar, configurar, ligar as integrações e manter o sistema no ar. Para quem opera o servidor.
> Última atualização: 28/09/2026, depois do S7 (os 81 entregáveis do piloto prontos). Portado do runbook do AlternativaDepartamentoPessoal.

---

## 1. O que vai para o servidor

- **Um site** ASP.NET Core 9 (`src/Web`), publicado *self-contained* para Windows x64: não depende de runtime instalado.
- **Um banco próprio,** `escolinhaesporte` (SQL Server 2019 ou mais novo: o EF gera SQL do nível de compatibilidade 150). As tabelas, inclusive as do Hangfire, são criadas pela aplicação na primeira subida (migrations automáticas).
- **Jobs em segundo plano** (Hangfire) rodam **dentro do site**: sessões de treino, mensagens, mensalidades, convocações, compras de créditos e a guarda dos webhooks (§7).
- **Integrações:** WhatsApp (Cloud API da Meta), Asaas (mensalidades, na conta de cada escolinha) e Stripe (a LDNA vende créditos às escolinhas). Todas nascem em modo **Simulado**: nada sai para fora até a configuração da §4.

## 2. Antes da primeira subida (DBA)

1. Rodar [`sql/01-banco-e-login.sql`](sql/01-banco-e-login.sql) no servidor do banco: cria o banco `escolinhaesporte` e o login `escolinha_app`, liga a leitura sem bloqueio e desliga o `AUTO_CLOSE`.
2. Conferir a saída do fim do script (permissões).
3. **SQL Express:** o teto é de 10 GB por banco (P22). O corpo bruto dos webhooks sai sozinho depois de 90 dias (§7). Acompanhe o tamanho pelo SSMS; perto de 8 GB, é hora de uma edição paga.

## 3. Publicar

```powershell
# Na máquina de build. Instale ANTES o último patch do SDK .NET 9 (o 9.0.100 embarca um runtime antigo).
# O .NET 9 sai de suporte em 10/11/2026 (P24): antes disso, suba o projeto para o .NET 10.
dotnet publish src/Web -c Release -r win-x64 --self-contained true -o C:\publish\minha-escolinha
```

- Copiar a pasta para o site, com o site parado: o executável fica bloqueado enquanto roda.
- O `wwwroot` já vem com o CSS, as fontes e as bibliotecas (nada de CDN; o servidor não roda `npm`).
- O site precisa de **HTTPS**: o cookie de login e o antiforgery exigem conexão segura. Os webhooks da Meta, da Asaas e do Stripe também só chamam endereço https público.
- **Antes de cada publicação, faça backup do banco** (§9).

## 4. Configuração

Tudo por variável de ambiente do site. **Nunca em arquivo versionado:** o `appsettings.Production.json` fica fora do git.

### 4.1 Base

| Variável | Para quê | Exemplo |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (produção) ou `Homologacao` (servidor de teste) | `Production` |
| `ConnectionStrings__ConexaoPadrao` | O banco da §2, com o login `escolinha_app` | ver `sql/01` |
| `GuardaAmbiente__BancosProducao__0__Banco` | Trava de ambiente: fora de `Production`, o site **se recusa** a subir apontando para este banco | `escolinhaesporte` |
| `GuardaAmbiente__BancosProducao__0__Servidor` | (Opcional) o servidor do banco de produção; vazio = qualquer servidor com esse nome de banco | `SERVIDOR\SQLEXPRESS` |
| `Seed__Administrador__Login`, `__Nome`, `__Senha` | O 1º administrador da plataforma (equipe LDNA), só na primeira subida. A senha é trocada no 1º acesso | — |
| `DataProtection__CertificadoArquivo`, `DataProtection__CertificadoSenha` | **Obrigatório em Production** (sem ele o site não sobe). Cifra as chaves Asaas das escolinhas, o cookie de login e a planilha de importação aberta. O `.pfx` fica **fora** da pasta do site, com backup | `D:\certs\minha-escolinha.pfx` |
| `Hangfire__WorkerCount`, `Hangfire__WorkerCountIntegracoes` | (Opcional) quantos jobs rodam ao mesmo tempo: nas filas de mensagens, webhooks e rotinas, e na fila das chamadas à Asaas e ao Stripe, que tem servidor próprio para um parceiro fora do ar não atrasar o resto. O padrão é 2 e 2 | `2` |

**Perder o certificado do Data Protection** é perder as chaves Asaas de todas as escolinhas (cada uma vincula a conta de novo), os logins abertos e os lotes de importação abertos (a gestão envia a planilha de novo).

### 4.2 Asaas (mensalidades, R1)

A chave de API é **de cada escolinha**, vinculada por ela na tela Conta de recebimento (fica cifrada no banco). O site só precisa saber que a API está ligada:

| Variável | Para quê |
|---|---|
| `Asaas__Modo` | `Api` (o padrão é `Simulado`) |
| `Asaas__UrlPublica` | O endereço https público do site: o webhook de cada escolinha é `https://<site>/api/v1/webhooks/asaas/{escolinha}`, cadastrado sozinho quando a conta é vinculada |
| `Asaas__EmailAvisosWebhook` | (Opcional) a Asaas avisa este e-mail quando o webhook falha |
| `Asaas__ConfirmarProducao` | `true` em `Production` (sem isso o site não sobe no modo `Api`) |

- **Sandbox primeiro (P41):** fora de `Production`, só a conta Sandbox é aceita ao vincular. O roteiro da prova está na §6.
- As notificações da própria Asaas ficam desligadas no cliente: só o motor fala com a família (R3).
- `Asaas__Modo = Simulado` em `Production`: a emissão fica desligada, com aviso no log, e as mensalidades continuam previstas.

### 4.3 WhatsApp (Cloud API da Meta)

| Variável | Para quê |
|---|---|
| `WhatsApp__Modo` | `Api` (o padrão é `Simulado`) |
| `WhatsApp__IdNumero` | O id do número na Meta |
| `WhatsApp__TokenAcesso` | O token permanente (usuário do sistema do Business Manager) |
| `WhatsApp__SegredoApp` | O segredo do app: confere a assinatura dos webhooks |
| `WhatsApp__TokenVerificacao` | Um texto à escolha, o mesmo cadastrado no painel da Meta |
| `WhatsApp__UrlPublica` | O endereço https público: vai no link do convite |
| `WhatsApp__NumerosTeste__0`, `__1`... | **Fora de Production, obrigatório:** só estes números recebem; os demais são suprimidos, com o motivo |
| `WhatsApp__ConfirmarProducao` | `true` em `Production` |

- No painel da Meta: o webhook é `https://<site>/api/v1/webhooks/whatsapp` (GET de verificação e POST assinado), com o campo `messages` assinado.
- Só sai template **aprovado** pela Meta (tela Plataforma › Templates, da equipe LDNA). A revisão dos textos antes da submissão está em [`TEMPLATES-WHATSAPP.md`](TEMPLATES-WHATSAPP.md).
- `WhatsApp__Modo = Simulado` em `Production`: o canal fica desligado, com aviso no log. As mensagens essenciais ficam retidas, e o portal continua.

### 4.4 Stripe da LDNA (pacotes de créditos)

| Variável | Para quê |
|---|---|
| `Stripe__Habilitado` | `false` desliga a compra de créditos (a equipe concede à mão, em Plataforma › Escolinhas) |
| `Stripe__Modo` | `Producao` em `Production`; `Teste` só em `Homologacao` (em `Production` o site **não sobe** com `Teste`) |
| `Stripe__ChaveSecreta` | `sk_live_`/`rk_live_` em produção; `sk_test_`/`rk_test_` em homologação |
| `Stripe__SegredoWebhook` | O `whsec_` do endpoint |
| `Stripe__UrlPublica` | O endereço https público (a volta do pagamento) |
| `Stripe__ConfirmarProducao` | `true` em `Production` |

No painel do Stripe: o endpoint `https://<site>/api/v1/webhooks/stripe`, com os 4 eventos de Checkout que a tela Plataforma › Pacotes de créditos lista. Recomendado um sandbox próprio para a Minha Escolinha (P27).

### 4.5 E-mail (nova senha e termos de consentimento)

O e-mail serve para duas coisas: o link de nova senha de quem entra com e-mail, e o link dos termos de consentimento para o 1º responsável aceitar (D111), que sai pelo motor de mensagens. Não é canal dos outros avisos: esses são do WhatsApp e do portal. Sem e-mail (caixa local em `Production`, ou responsável sem e-mail no cadastro), os dois funcionam pela gestão: o link de nova senha e o dos termos ficam na ficha, para a gestão repassar.

| Variável | Para quê |
|---|---|
| `Email__Modo` | `CaixaLocal` (padrão: grava o e-mail em disco, nada sai) ou `Smtp`. Em `Production`, `CaixaLocal` desliga o e-mail, com aviso no log: a recuperação fica só com o link da gestão |
| `Email__SmtpHost`, `Email__SmtpPorta` (587), `Email__SmtpSsl` (true) | O servidor SMTP |
| `Email__SmtpUsuario`, `Email__SmtpSenha` | A conta do SMTP (a senha só em variável de ambiente) |
| `Email__Remetente` | O endereço que aparece como remetente (obrigatório com `Smtp`) |
| `Email__RedirecionarPara` | **Fora de `Production` é obrigatório com `Smtp`:** todo e-mail vai para este endereço. Em `Production` é ignorado |
| `Email__UrlPublica` | O endereço https do site: vai no link de nova senha. Em `Production`, o log avisa se for http ou localhost |
| `Email__Pasta` | A pasta da caixa local (padrão `%LOCALAPPDATA%\MinhaEscolinha\emails`) |

## 5. Primeira subida

1. Iniciar o site. No log (`logs/startup-*.log` e `logs/app-*.log`, na pasta do site): "Aplicando migrations…", os parâmetros reconciliados e os templates gravados.
2. `https://<site>/health` deve responder **Healthy** (banco e jobs).
3. Entrar com o administrador do `Seed__Administrador__*`, trocar a senha e seguir o roteiro de entrada da escolinha ([`PLANO.md`](../PLANO.md) §4.1):
   1. Plataforma › Nova escolinha, com o 1º gestor (a senha temporária aparece uma vez), e os créditos de mensagem;
   2. a gestão cadastra dados, modalidade, locais, professores e turmas, e confere os parâmetros (os provisórios estão marcados);
   3. Conta de recebimento: a chave Asaas da escolinha;
   4. Importação: a planilha dos alunos (baixar o modelo, enviar, mapear, conferir e confirmar);
   5. Mensalidades: confirmar a primeira de cada matrícula;
   6. Convites: o convite em lote às famílias e aos professores; a fila de Vínculos confirma quem ativou.
4. O roteiro de validação está em [`ROTEIRO-HOMOLOGACAO.md`](ROTEIRO-HOMOLOGACAO.md).

## 6. Provar as integrações num servidor de teste (`Homologacao`)

### 6.1 Asaas sandbox (P41)

1. Criar a conta em https://sandbox.asaas.com e gerar a chave de API (prefixo `$aact_hmlg_`). **A sandbox manda e-mail e SMS de verdade:** use só dados de teste, nunca de famílias reais.
2. Cadastrar uma chave Pix na conta (a aleatória, EVP, serve), na sandbox e na conta real de cada escolinha. Sem chave, a Asaas ainda gera o Pix da cobrança, mas ele só vale até as 23:59 do dia, por uma instituição parceira (e a Asaas avisa que isso vai acabar). O sistema guarda a validade: o portal esconde o Pix vencido, oferece a fatura, e a rotina das 06:30 busca outro.
3. Site com `Asaas__Modo = Api` e `Asaas__UrlPublica` https; na escolinha de teste, Conta de recebimento → vincular a chave Sandbox (o webhook é cadastrado sozinho).
4. Confirmar uma mensalidade em Mensalidades e esperar a emissão (ou "Atualizar agora"): a cobrança aparece na sandbox, com a referência da mensalidade.
5. Pagar sem dinheiro: no painel da sandbox, ou por `POST /v3/sandbox/payments/{id}/confirmPaymentReceipt`. O aviso `PAYMENT_RECEIVED` chega pelo webhook, a mensalidade passa a paga sozinha, a régua para e a família recebe a confirmação.
6. Vencida: `POST /v3/sandbox/payments/{id}/expirePayment` e a rotina do dia seguinte (06:30) manda o degrau "depois do vencimento".
7. Webhook parado: se o endereço falhar repetidas vezes, a Asaas interrompe a fila (penalização). Reative no painel ou por `POST /v3/webhooks/{id}/tryAgain`; a varredura de hora em hora (`mensalidades-varredura`) recupera o que faltou.

### 6.2 WhatsApp com número de teste (P34)

**A conta na Meta** (uma vez, no portfólio de negócios da LDNA, P14):
1. No App Dashboard da Meta, criar um app com o caso de uso de WhatsApp ("Connect with customers through WhatsApp").
2. Em API Setup, usar o número de teste da Meta: o id dele (Phone number ID) vai em `WhatsApp__IdNumero`. Cadastrar ali os celulares que vão receber (a Meta aceita até 5, e cada um confirma por código); os mesmos vão em `WhatsApp__NumerosTeste__0`, `__1`...
3. Token permanente: em Business Settings, criar um usuário do sistema, dar a ele o app e a conta do WhatsApp, e gerar o token com as permissões `business_management`, `whatsapp_business_messaging` e `whatsapp_business_management`. Ele vai em `WhatsApp__TokenAcesso`. O token temporário da tela API Setup expira em horas: não use.
4. O segredo do app (App settings › Basic) vai em `WhatsApp__SegredoApp`; ele confere a assinatura de cada webhook.
5. Subir o site com `WhatsApp__Modo = Api` e as variáveis da §4.3. Depois, na configuração do WhatsApp no app: o endereço de callback `https://<site>/api/v1/webhooks/whatsapp`, o token de verificação igual a `WhatsApp__TokenVerificacao` (a Meta chama o site para conferir) e a assinatura do campo `messages`.
6. Os templates: criar no WhatsApp Manager os da revisão [`TEMPLATES-WHATSAPP.md`](TEMPLATES-WHATSAPP.md) (ao menos os quatro da prova) e, aprovados, marcar como "Aprovado" em Plataforma › Templates de WhatsApp.

**A prova:**
1. Convidar uma pessoa com um número de teste e ativar o acesso pelo link que chega no celular.
2. Responder a uma mensagem e conferir a resposta e o log em Mensagens.
3. Convocar um aluno cujo responsável tenha o número de teste, tocar em "Vai" no celular e conferir o painel do jogo mudando sozinho.

**Para produção** (só com aprovação humana, spec §2.3): um número de telefone próprio da LDNA registrado na conta (a Meta orienta, no registro, o caso de um número que já está no aplicativo do WhatsApp), com o nome de exibição aprovado pela Meta; o id dele no lugar do número de teste, e `WhatsApp__ConfirmarProducao = true`.

## 7. Manter no ar

- **Keep-alive (obrigatório):** o site ocioso é desligado pelo servidor, e aí param os jobs. No IIS: *Idle Time-out* = 0 e *Start Mode* = AlwaysRunning no pool. Em hospedagem sem acesso ao IIS: um monitor externo chamando `https://<site>/health` a cada 5 minutos.
- **Jobs** (`/jobs`, só a equipe da plataforma), no horário de Brasília (o fuso é explícito, o servidor pode estar em outro):

  | Job | Quando | O quê |
  |---|---|---|
  | `sessoes-gerar-horizonte` | 03:15 | as sessões de treino até o fim do mês seguinte |
  | `webhooks-limpar-corpo` | 04:30 | apaga o corpo bruto dos webhooks encerrados há mais de 90 dias; a linha fica (P22) |
  | `importacao-descartar-esquecidos` | 04:45 | descarta o lote de importação aberto há mais de 7 dias e apaga a planilha cifrada (P53) |
  | `mensalidades-rotina` | 06:30 | projeção, vencidas, régua, emissões e o Pix do portal |
  | `mensalidades-varredura` | de hora em hora (:40) | confere na Asaas o que o webhook não trouxe |
  | `mensagens-varredura` | a cada 5 min | solta as retidas quando o saldo volta e reenfileira as vencidas |
  | `convocacoes-rotina` | a cada 15 min | lembrete a quem não respondeu e "sem resposta" no início do jogo |
  | `stripe-varredura-compras` | a cada 20 min | as compras de créditos pendentes |
- **Logs:** a pasta `logs` do site (14 dias de startup, 30 de aplicação). O CPF nunca vai inteiro para o log.
- **Créditos de mensagem:** o painel da gestão avisa o saldo baixo; sem saldo, as mensagens essenciais ficam retidas até a recarga: a equipe concede em Plataforma › Escolinhas, ou a gestão compra um pacote em Créditos de mensagem (Stripe).

## 8. Quando algo dá errado

| Sintoma | O que fazer |
|---|---|
| Site não sobe | Ver `logs/startup-*.log`. Causas comuns: connection string; trava de ambiente (banco de produção fora de `Production`); certificado do Data Protection ausente em `Production`; `Stripe__Modo = Teste` em `Production`; `Api` sem as chaves obrigatórias ou sem o `ConfirmarProducao`. |
| Usuários deslogados depois de reiniciar | As chaves do Data Protection: conferir a tabela `DataProtectionKeys` e o certificado (precisa ser o mesmo). |
| Mensagens paradas em "retida" | Saldo de créditos zerado (painel da gestão) ou WhatsApp em `Simulado` em `Production`. |
| Mensalidade não sai para a Asaas | A tela Mensalidades diz o motivo na linha: conta não vinculada, pagador sem CPF, recusa da Asaas. A recusa definitiva não se repete sozinha: corrija o dado e use o botão "Tentar emitir de novo" da linha (as setas). A chave desabilitada ou revogada volta ao vincular a chave nova. |
| Pagamento não concilia | Webhook da Asaas: `Asaas__UrlPublica` https, a fila penalizada no painel da Asaas (§6.1), a varredura de hora em hora. A mensalidade com valor diferente fica na fila de Divergências, de propósito. |
| Painel do jogo não atualiza sozinho | Um proxy que bloqueia WebSocket faz o SignalR cair para long polling (funciona, mais lento); a tela também busca de novo a cada minuto. |
| Chamada não abre sem sinal no celular | O service worker só registra com HTTPS válido; o aparelho precisa ter aberto a lista do dia com sinal antes. |
| E-mail dos termos não chega | Na ficha do aluno, aba Consentimentos: a situação do e-mail no motor, com o motivo. "Suprimida" com "E-mail desligado" é caixa local em `Production`: configure o SMTP ou copie o link da ficha. Falha do SMTP é tentada de novo pela fila `esc_mensagens`. |
| Link de nova senha não chega | Ver o job na fila `esc_integracoes` (`/jobs`): a falha do SMTP é tentada 5 vezes. Conferir `Email__*`. Em `Production` com `CaixaLocal`, o e-mail está desligado: a gestão gera o link na ficha da pessoa (botão "Link de nova senha"). Quem entra só pelo CPF sempre recebe o link da gestão. |
| Lote de importação "não encontrado" | O certificado do Data Protection mudou: descarte e envie a planilha de novo. |

## 9. Voltar uma versão

Publicar a versão anterior por cima. **Atenção às migrations:** a versão nova pode ter alterado o banco, e voltar o código sem voltar o banco pode falhar. Por isso o backup antes de cada publicação (§3).
