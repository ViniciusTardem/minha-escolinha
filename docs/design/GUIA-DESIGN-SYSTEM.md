# Guia do Design System — Minha Escolinha

> **Fonte da verdade visual do projeto.** Leia a §0 antes de escrever qualquer markup. O mapa das telas por fatia está em [`MAPA-DE-TELAS.md`](MAPA-DE-TELAS.md); o resumo das regras, no §6 do [`CLAUDE.md`](../../CLAUDE.md).
>
> **Stack:** ASP.NET Core MVC (.NET 9) + Razor · Metronic 9 + KTUI + Tailwind 4.1.8 + KeenIcons (só `ki-filled`).
> **Prefixo do projeto:** `esc-` (classes), `data-esc-` (atributos), `window.esc` (JS), `esc:*` (eventos).
>
> **Como foi verificado (27/09/2026, fatia S1 pronta):** toda classe citada aqui foi conferida no CSS que o navegador recebe (`keenicons-filled.css`, `styles.css`, `css/esc.css` e `custom.css`, em `src/Web/wwwroot`), e todo comportamento, no código. O que ainda não existe está marcado como **planejado** e listado na §14.
>
> **Base:** o guia verificado do Adama/Candeias (`docs/metronic/GUIA-DESIGN-SYSTEM.md`, cópia local fora do git), reescrito para este projeto. O `docs/metronic/GUIA-REFERENCIA-METRONIC-KTUI.md` está superado e tem afirmações erradas: não use.
>
> Decisões ligadas a este guia, em [`../DECISOES-EM-ABERTO.md`](../DECISOES-EM-ABERTO.md):
> - D17 (front) e D18 (arquivos estáticos);
> - D19 e X06 (erro ligado ao campo), D20 (nome de binding);
> - D22 (validação no navegador);
> - D23 (toast escapado), D24 (mensagens de validação em pt-BR), D25 (`SinalTela`);
> - do S1: D30 (valor em reais), D37 (ficha em página com abas), D41 (escopo no que vem junto) e D42 (assistente sem botões).

## Sumário

0. [Regras de ouro](#0-regras-de-ouro)
1. [Referência e o que o app serve](#1-referência-e-o-que-o-app-serve)
2. [Pipeline: CSS, JS, build e CSP](#2-pipeline-css-js-build-e-csp)
3. [Tokens, marca e tema](#3-tokens-marca-e-tema)
4. [Status: `<selo-status>` e o mapa central](#4-status-selo-status-e-o-mapa-central)
5. [As superfícies](#5-as-superfícies)
6. [Componentes](#6-componentes)
7. [Erro de formulário: o padrão do projeto](#7-erro-de-formulário-o-padrão-do-projeto)
8. [Formato pt-BR](#8-formato-pt-br)
9. [Ícones](#9-ícones)
10. [Metronic e KTUI verificados](#10-metronic-e-ktui-verificados)
11. [Acessibilidade, responsividade e modo escuro](#11-acessibilidade-responsividade-e-modo-escuro)
12. [Checklist de aceite de tela](#12-checklist-de-aceite-de-tela)
13. [Armadilhas](#13-armadilhas)
14. [Diferenças entre o plano e o construído](#14-diferenças-entre-o-plano-e-o-construído)

---

## 0. Regras de ouro

1. **Só classe que existe no CSS servido.** O HTML do demo do Metronic tem classes inertes. `npm run build` precisa terminar com `verificar-classes: 0 classes faltando.`
2. **Nada inline nas Views:** nenhum `<script>` inline, nenhum `onclick=` (ou outro `on*=`), nenhum `style=` e nenhuma tag `<style>`. JS vai em arquivo e age por `data-*` e delegação de eventos (CSP `script-src 'self'`).
3. **Cor só por token:** `bg-primary`, `text-mono`, `text-secondary-foreground`, `border-border`... Nunca hex na View; nunca valor literal em `--color-*`.
4. **Cor de status nunca é escolhida na View:** use `<selo-status valor="...">`, que passa pelo mapa central `ApresentacaoStatus` (§4).
5. **Ícone só `ki-filled`**, da lista da §9. As famílias outline, solid e duotone nem são servidas.
6. **Sem emoji** em código, texto de tela, comentário ou commit.
7. **Prefixo `esc-`** em toda classe, atributo e evento próprio. Nunca `me-`: é o margin-end do Tailwind.
8. **Toda tela do painel** começa com `<div class="kt-container-fixed">` e `<cabecalho-pagina ...></cabecalho-pagina>`, sempre com a tag fechada.
9. **Dropdown:** o contêiner leva só `data-kt-dropdown="true"`, sem a classe `kt-dropdown`. **Modal** fica no fim do `<body>` (seção `Modais` do `_Layout`).
10. **Erro de formulário volta para a mesma tela**, com o que foi digitado e o campo marcado. Sucesso é PRG + toast (§7).
11. **Quadra e portal:** alvo de toque de 48 px (`esc-toque`) e a ação principal na barra inferior (§5).
12. **Todo `<button>` declara `type`.** O bloqueio de duplo envio procura `button[type=submit]`.
13. **Formato pt-BR** pelos helpers de `Formatacao` (§8), nunca `ToString()` solto na View.
14. **HTML injetado por AJAX** chama `esc.reinicializarComponentes()`, e leia a limitação da §6.13.

---

## 1. Referência e o que o app serve

| Caminho | O que é | No git? |
|---|---|---|
| `docs/metronic/html/demo1/` | 119 páginas do demo1: abra para copiar a **forma** de um bloco | Não |
| `docs/metronic/assets/` | CSS, JS, vendors e mídia completos do Metronic | Não |
| `src/Web/wwwroot/assets/` | o recorte que o navegador recebe (tabela abaixo) | Sim |
| `src/Web/wwwroot/css/esc.css` | utilities do Tailwind geradas pelo build (§2.3) | Sim |
| `src/Web/wwwroot/fonts/inter/` | Inter variável, latin e latin-ext, em woff2 | Sim |
| `src/Web/wwwroot/lib/signalr/` | SignalR 10: servido, mas nenhum layout carrega ainda (entra no S5) | Sim |
| `src/Web/wwwroot/js/` | `tema.js`, `app.js`, `assistente.js` e `selecao-busca.js` | Sim |
| `src/Web/Estilos/esc.src.css` | a fonte do `esc.css` | Sim |

- **`docs/metronic/` tem 61 MB e licença comercial:** fica fora do git (`.gitignore`). Em outra máquina, copie de `Projetos\SaaS\Adama\docs\metronic`. Sem essa pasta, `npm run metronic` avisa e mantém o recorte já versionado.
- **O recorte, o `esc.css`, as fontes e as bibliotecas vão para o git de propósito:** o servidor não roda npm.
- **Uma classe se valida no CSS servido, nunca no HTML do demo.** Classe que aparece no demo e não está em `wwwroot` não funciona, e não dá erro nenhum.

**O recorte servido** (bytes, 26/09/2026):

| Arquivo | Origem | Bytes |
|---|---|---:|
| `assets/css/styles.css` | Metronic, byte a byte. **Nunca editar.** | 324.886 |
| `assets/js/core.bundle.js` | Metronic, byte a byte | 71.292 |
| `assets/js/layouts/demo1.js` | Metronic, byte a byte (só no painel) | 2.754 |
| `assets/vendors/ktui/ktui.min.js` | Metronic, byte a byte | 316.554 |
| `assets/vendors/keenicons/keenicons-filled.css` | recortado do bundle do KeenIcons (§2.5) | 82.118 |
| `assets/vendors/keenicons/fonts/keenicons-filled.woff` e `.ttf` | Metronic | 431.380 e 431.304 |
| `assets/css/custom.css` | manual | 19.091 |
| `css/esc.css` | `npm run css` | 13.857 |

**Fica de fora:** a mídia do demo (logos, avatares, ilustrações, fundos: não há pasta de mídia servida), ApexCharts (o piloto não tem gráfico), Leaflet, `@form-validation`, clipboard e prismjs. Trazer qualquer um é decisão de arquitetura a registrar (12.3), não decisão de tela.

---

## 2. Pipeline: CSS, JS, build e CSP

### 2.1 CSS: a ordem importa (o último vence)

`Views/Shared/_Cabeca.cshtml` é o `<head>` comum aos cinco layouts:

```cshtml
<meta content="width=device-width, initial-scale=1, viewport-fit=cover" name="viewport" />
<meta name="csrf-token" content="@Antiforgery.GetAndStoreTokens(Context).RequestToken" />
<title>@(ViewData["Title"] ?? "Minha Escolinha") - Minha Escolinha</title>
<link href="~/favicon.svg" rel="icon" type="image/svg+xml" />
<link href="~/assets/vendors/keenicons/keenicons-filled.css" rel="stylesheet" asp-append-version="true" />
<link href="~/assets/css/styles.css" rel="stylesheet" asp-append-version="true" />
<link href="~/css/esc.css" rel="stylesheet" asp-append-version="true" />
<link href="~/assets/css/custom.css" rel="stylesheet" asp-append-version="true" />
<script src="~/js/tema.js" asp-append-version="true"></script>
```

| Ordem | Arquivo | Papel |
|---|---|---|
| 1 | `keenicons-filled.css` | a fonte de ícones; as classes `ki-*` **não** estão no `styles.css` |
| 2 | `styles.css` | o tema inteiro: tokens crus, ponte `--color-*`, todas as classes `kt-*` e as utilities que o demo usa. Intocável |
| 3 | `css/esc.css` | só as utilities que as nossas Views, JS e TagHelpers usam e o `styles.css` não trouxe; a Inter; as variantes `kt-*` |
| 4 | `custom.css` | tokens do projeto, componentes `esc-*` e ajustes do KTUI |

`viewport-fit=cover` habilita a safe-area (quadra e portal). A meta `csrf-token` é lida pelo `esc.fetch` (§6.13).

### 2.2 JS: a ordem de cada layout

| Onde | Ordem |
|---|---|
| `<head>` (todos os layouts) | `js/tema.js`: aplica claro ou escuro antes de pintar |
| fim do `<body>`, todos (`_Scripts.cshtml`) | `assets/js/core.bundle.js` → `assets/vendors/ktui/ktui.min.js` → `js/app.js` |
| só no `_Layout` (painel), depois do `_Scripts` | `assets/js/layouts/demo1.js` → `js/selecao-busca.js` → `js/assistente.js` → seção `Scripts` da tela |
| `_LayoutAutenticacao`, `_LayoutQuadra`, `_LayoutPortal` | `_Scripts` → seção `Scripts` da tela |
| `_LayoutVitrine` | só o `_Scripts` (não renderiza a seção `Scripts`) |

- O `core.bundle.js` traz `KTMenu`, `KTDom`, `KTUtils` e `KTComponents`. O `ktui.min.js` traz os componentes (`KTModal`, `KTDropdown`, `KTDrawer`, `KTTabs`, `KTTooltip`, `KTToast`...). Um complementa o outro.
- O `ktui.min.js` vai em todo layout: é ele que faz o mostrar/ocultar senha, a chave de tema e os toasts.
- **JS de tela é um arquivo próprio** em `wwwroot/js/`, carregado pela seção `Scripts` da View.
- **`assistente.js` e `selecao-busca.js` só existem no painel.** Formulário em etapas ou seleção com busca em outra superfície precisa carregar o script pela seção `Scripts`.

### 2.3 Tailwind 4: `Estilos/esc.src.css` → `wwwroot/css/esc.css`

```css
@layer theme, base, components, utilities;
@import "tailwindcss/theme.css" layer(theme);
@import "tailwindcss/utilities.css" layer(utilities) source(none);

@source "../Views/**/*.cshtml";
@source "../Areas/**/*.cshtml";
@source "../wwwroot/js/**/*.js";
@source "../Infraestrutura/**/*.cs";

@custom-variant dark (&:is(.dark *));
```

- **Sem preflight e sem base:** o `styles.css` já traz os dois. O build gera só as utilities que encontra nas fontes acima, com a mesma versão do Tailwind do bundle do Metronic (4.1.8).
- **Classe utilitária escrita em C# só é gerada se o arquivo estiver em `Infraestrutura/`** (TagHelpers, `Apresentacao`, `CatalogoModulos`). Em `Controllers/` e `Models/`, o Tailwind não enxerga.
- **Ponte de cor** (`@theme inline`): `--color-primary: var(--primary)` e as demais da §3. O arquivo também declara `--color-success`, `--color-warning` e `--color-info`, apontando para `green-500`, `yellow-500` e `violet-500` (a paleta dos `kt-badge-*`). Não existe token cru de sucesso, alerta ou info, e hoje nenhuma utility com esses nomes está no `esc.css`: status continua sendo selo (§4).
- **Escala extra:** `text-2xs` (0,6875rem = 11 px) e `text-2sm` (0,8125rem = 13 px).
- **Inter local:** dois `@font-face` "Inter Variable" (woff2, `font-display: swap`) e `:root { --font-sans: "Inter Variable", ... }` **fora de camada**, para vencer o `--font-sans` do `styles.css`, que não inclui a Inter. Nada de Google Fonts (CSP).
- **Variantes de estado do KTUI registradas** com os mesmos seletores do `styles.css`: `kt-toggle-password-active:`, `kt-dropdown-open:`, `kt-toggle-active:` e `kt-menu-item-active:`. Sem esse registro, só existiriam as combinações que o demo usa. Precisa de outra variante `kt-*`? Copie o seletor do `styles.css` para o `esc.src.css` antes de usar.

| Faça | Não faça |
|---|---|
| `w-(--sidebar-width)` | `w-[--sidebar-width]` (sintaxe do Tailwind 3) |
| `bg-primary/10` | `bg-[#1379F0]/10` |
| `@theme inline { --color-x: var(--x); }` | `@theme { --color-x: #fff; }` (congela o modo escuro) |
| `text-2xs`, `text-2sm` | `text-[11px]`, `text-[13px]` |

### 2.4 `npm run build` (em `src/Web`)

| Passo | Script | O que faz |
|---|---|---|
| `metronic` | `scripts/copiar-metronic.mjs` | copia de `docs/metronic/assets`, byte a byte, `styles.css`, `core.bundle.js`, `demo1.js` e `ktui.min.js`; recorta a família filled do KeenIcons (§2.5) |
| `fontes` | `scripts/copiar-fontes.mjs` | a Inter latin e latin-ext, de `@fontsource-variable/inter` 5.3.0 |
| `bibliotecas` | `scripts/copiar-bibliotecas.mjs` | `signalr.min.js`, de `@microsoft/signalr` 10.0.11 |
| `css` | `tailwindcss -i ./Estilos/esc.src.css -o ./wwwroot/css/esc.css --minify` | as utilities |
| `verificar-classes` | `scripts/verificar-classes.mjs` | falha (exit 1) se alguma classe usada não existe nos quatro CSS carregados |

- **Precisa terminar com `verificar-classes: 0 classes faltando.`** Durante o trabalho, `npm run css:watch`. O resultado vai para o git na mesma entrega da tela.
- **O que o verificador lê:** o `class="..."` das Views e das Areas (o texto literal e os literais de string dentro de `@(...)`); no JS, `classList.add/remove/toggle/contains/replace(...)` e `className = "..."`; no C# de `Controllers/`, `Infraestrutura/` e `Models/`, os tokens `kt-*`, `esc-*` e `ki-*` dentro de string.
- **O que ele não vê:** classe montada em variável no JS (`a.className = CLASSE_OPCAO`), nome de ícone que vem de variável na View (`ki-filled @m.Icone`; por isso o catálogo de módulos, que é C#, é conferido na origem) e utility do Tailwind escrita em C#.

### 2.5 KeenIcons: só a família filled (medido em 26/09/2026)

| Arquivo | Bytes |
|---|---:|
| `styles.bundle.css` original (as quatro famílias) | 232.374 |
| `keenicons-filled.css` servido | 82.118 |
| `keenicons-filled.woff` | 431.380 |
| `keenicons-filled.ttf` | 431.304 |

- **O ganho por carga está no CSS:** cerca de 150 KB a menos em toda página, antes de compressão.
- **A fonte não ficou menor:** o woff e o ttf têm o mesmo tamanho (431 KB). O script põe o woff primeiro no `@font-face` (no bundle original o ttf vinha antes), mas o navegador baixa praticamente os mesmos bytes. A afirmação antiga de "400 KB a menos por carga" não vale.
- **Fora do git:** as outras três famílias (duotone, outline e solid, cerca de 1,5 MB em woff + ttf) e todas as fontes `.svg` (cerca de 4,8 MB).
- **`font-display: block`:** com sinal ruim, o ícone fica invisível até a fonte chegar. Na quadra, ícone nunca é o único rótulo de uma ação.

### 2.6 CSP e cabeçalhos (`Middleware/CabecalhosSegurancaMiddleware.cs`)

```text
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;
font-src 'self' data:; connect-src 'self'; worker-src 'self'; manifest-src 'self'; object-src 'none';
base-uri 'self'; frame-ancestors 'self'; form-action 'self' https://checkout.stripe.com
```

| Diretiva | O que muda na tela |
|---|---|
| `script-src 'self'` | nenhum `<script>` inline e nenhum `on*=`. O próprio `tema.js` é um arquivo por isso |
| `style-src 'self' 'unsafe-inline'` | o KTUI posiciona dropdown e drawer por atributo `style`, então o navegador aceita estilo inline. **A regra do projeto continua: nenhum `style=` nem `<style>` nas Views.** Não há teste automático; é revisão |
| `img-src` e `font-src` com `'self' data:` | nada de CDN, Google Fonts ou imagem externa |
| `connect-src 'self'` | `fetch` e SignalR só na mesma origem |
| `worker-src` e `manifest-src` com `'self'` | já preparam a chamada offline (service worker, S3) |
| `frame-ancestors 'self'` (e `X-Frame-Options: SAMEORIGIN`) | a vitrine põe a quadra e o portal em `<iframe>`; outro site não consegue |
| `form-action 'self' https://checkout.stripe.com` | o POST "Comprar" dos pacotes de créditos redireciona para o Checkout do Stripe, e o navegador aplica o `form-action` também ao redirecionamento. Qualquer outro destino externo de formulário precisa entrar aqui, registrado |

Também vão em toda resposta: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` e `Permissions-Policy` com câmera, microfone e geolocalização desligados.

### 2.7 Arquivos estáticos

`app.UseStaticFiles()` antes da autenticação, e `asp-append-version="true"` em todo `<link>` e `<script>` (hash na query string para renovar o cache). O `MapStaticAssets` previsto no plano (Brotli/gzip e fingerprint) não foi adotado: D18, a reavaliar no S3 pelo desempenho da quadra com sinal ruim.

---

## 3. Tokens, marca e tema

### 3.1 Dois níveis de variável

```css
/* styles.css, nível 1: o token cru, que muda com o tema */
:root { --primary: #1379F0; --background: oklch(1 0 0); /* ... */ }
.dark { --primary: #1379F0; --background: oklch(14.1% 0.005 285.823); /* ... */ }

/* esc.src.css, nível 2: a ponte para as utilities do Tailwind */
@theme inline { --color-primary: var(--primary); --color-background: var(--background); /* ... */ }
```

**Regra:** para trocar a marca, sobrescreva o token **cru** no `custom.css`. Valor literal em `--color-*` rompe a ponte e congela o modo escuro.

### 3.2 Tokens

| Token cru | Utilities que existem hoje | Uso |
|---|---|---|
| `--background` / `--foreground` | `bg-background`, `text-foreground`, `bg-foreground` | fundo e texto da página |
| `--card` / `--card-foreground` | nenhuma (use `kt-card`) | superfície de cartão |
| `--popover` / `--popover-foreground` | nenhuma | dropdown; painel da seleção com busca |
| `--primary` / `--primary-foreground` | `bg-primary`, `bg-primary/10`, `text-primary`, `border-primary`, `text-primary-foreground` | marca |
| `--secondary` / `--secondary-foreground` | `text-secondary-foreground`, `bg-secondary-foreground` | texto de apoio: descrição, rótulo de dado |
| `--muted` / `--muted-foreground` | `bg-muted`, `bg-muted/40`, `bg-muted/70`, `text-muted-foreground` | superfície e texto discretos: dica, meta |
| `--accent` / `--accent-foreground` | `bg-accent`, `hover:bg-accent` | hover e item destacado |
| `--destructive` / `--destructive-foreground` | `text-destructive`, `bg-destructive` | erro, exclusão, falta |
| `--mono` / `--mono-foreground` | `text-mono`, `bg-mono` | título e texto de ênfase |
| `--border` / `--input` / `--ring` | `border-border`, `bg-border`, `border-input` | traços e foco |
| `--radius` | — | raio base (0,5rem) |

A utility que falta nasce no `npm run css` quando uma View passa a usá-la; o verificador confirma.

**Tokens do projeto** (`custom.css`, `:root`):

| Token | Valor | Para quê |
|---|---|---|
| `--esc-foco` | `color-mix(in srgb, var(--primary) 18%, transparent)` | o anel de foco de `kt-input`, `kt-select` e `kt-textarea` |
| `--esc-toque` | `3rem` (48 px) | o alvo mínimo de toque na quadra e no portal |
| `--esc-barra-acao` | `4.75rem` | a altura reservada para a barra de ação da quadra |

### 3.3 Marca provisória

- **`--primary` é `#1379F0`**, o azul padrão do Metronic, **sem sobrescrita**, no claro e no escuro (D17). A identidade visual é pendência (P21).
- **Logo provisório:** o ícone `ki-cup` num quadrado `bg-primary`, mais "Minha Escolinha" em texto (sidebar, header no celular e autenticação). Favicon: `wwwroot/favicon.svg`.
- **Trocar a marca** é sobrescrever os tokens crus no `custom.css`, nos dois temas:

```css
:root { --primary: /* cor da marca */; --primary-foreground: /* texto sobre ela */; }
.dark { --primary: /* ... */; --primary-foreground: /* ... */; }
```

- **Contraste:** texto branco sobre `#1379F0` dá **4,2:1**. Passa no AA para texto grande (24 px, ou 18,66 px em negrito) e para componentes (3:1), mas fica abaixo dos 4,5:1 exigidos para texto normal: é o caso do rótulo de 13 px do `kt-btn-primary` e do `text-primary` em texto pequeno sobre branco. Revisar quando a marca chegar.
- **Armadilha da marca:** `kt-badge-outline kt-badge-primary` **não usa `--primary`**. Usa a paleta `blue` do Tailwind (`--color-blue-50`, `-100` e `-700`; `-950` e `-600` no escuro). Hoje coincide; com outra marca, o selo "Gestor" (§4), o perfil da modalidade e a etiqueta de fatia da página "em breve" continuariam azuis. Ao trocar a marca, sobrescreva essa combinação no `custom.css`. `kt-badge-primary` sólido, `kt-alert-light kt-alert-primary` e `kt-btn-primary` usam o token.

### 3.4 Tema claro e escuro

- **Chave:** `localStorage["kt-theme"]`, a do próprio KTUI, com `light`, `dark` ou `system`. Não existe chave `esc.*`.
- **`tema.js`**, no `<head>`, lê a chave (ou o `data-kt-theme-mode` do `<html>`, que é `light`) e põe a classe `light` ou `dark` no `<html>` antes de pintar. `system` segue o `prefers-color-scheme`.
- **Quem grava a chave** é o `KTThemeSwitch`, no menu da pessoa (`_Header`) e no topo da vitrine:

```html
<input class="kt-switch kt-switch-sm" data-kt-theme-switch-state="dark" data-kt-theme-switch-toggle="true"
       type="checkbox" aria-label="Modo escuro" />
```

- Autenticação, quadra e portal não têm chave de tema: seguem o que a pessoa escolheu no painel (mesma chave, mesmo navegador).
- **Variante `dark:`** = `&:is(.dark *)`, ou seja, qualquer descendente do `<html class="dark">`. Estado fora de componente `kt-*` usa a paleta do Tailwind com par claro e escuro, como o `.esc-conexao-offline` e o `.dark .esc-conexao-offline` do `custom.css`.
- **Para testar:** `localStorage.setItem("kt-theme", "dark"); location.reload();` (e `"light"` para voltar).

---

## 4. Status: `<selo-status>` e o mapa central

### 4.1 Uso

```cshtml
<dd><selo-status valor="Model.Ambiente"></selo-status></dd>     @* ContaRecebimento/Index *@
<td><selo-status valor="m.Tipo"></selo-status></td>             @* Creditos/Index *@
```

- `valor` é uma expressão C# do tipo `Enum`, sem `@`. Valor nulo não renderiza nada.
- `texto="..."` troca só o rótulo; a cor continua sendo a do mapa.
- Saída: `<span class="kt-badge kt-badge-sm kt-badge-outline kt-badge-success">Produção</span>`.
- No painel (`<body class="... esc-tela">`), o selo contornado vira **selo suave**: sem contorno, peso 500.

### 4.2 O mapa central (`Infraestrutura/Apresentacao.cs`)

`ApresentacaoStatus.Selo(Enum)` devolve `(Rotulo, VarianteSelo)`, e `ClasseVariante(VarianteSelo)` devolve a classe:

| `VarianteSelo` | Classe | Cor |
|---|---|---|
| `Sucesso` | `kt-badge-success` | verde |
| `Info` | `kt-badge-info` | **violeta**: o "info" do Metronic é violeta, não azul |
| `Alerta` | `kt-badge-warning` | amarelo |
| `Perigo` | `kt-badge-destructive` | vermelho (`--destructive`) |
| `Primario` | `kt-badge-primary` | azul da paleta, não o token (§3.3) |
| `Neutro` | `kt-badge-secondary` | cinza |

**Enums mapeados hoje (S0, EX-01 e S1):**

| Enum | Valor → rótulo · variante | Onde aparece |
|---|---|---|
| `SituacaoVinculo` | `Ativo` → "Ativo" · Sucesso; `Revogado` → "Revogado" · Neutro | vitrine |
| `AmbienteAsaas` | `Sandbox` → "Sandbox" · Info; `Producao` → "Produção" · Sucesso | Conta de recebimento |
| `TipoMovimentoCredito` | `Concessao` → "Concessão" · Sucesso; `Consumo` → "Consumo" · Neutro; `Estorno` → "Estorno" · Info; `Ajuste` → "Ajuste" · Alerta | Créditos |
| `PapelAtuacao` | `Gestor` → "Gestor" · Primario; `Professor` → "Professor" · Info; `Responsavel` → "Responsável" · Neutro | vitrine |
| `SinalTela` (estados sem enum de domínio, definido em `Apresentacao.cs`) | `ContaRecebimentoVinculada` → "Conta vinculada" · Sucesso; `ContaRecebimentoAusente` → "Sem conta" · Alerta; `SaldoCreditosBaixo` → "Saldo baixo" · Alerta; `PacoteAVenda` → "À venda" · Sucesso; `PacoteForaDeVenda` → "Fora de venda" · Neutro | Plataforma › Escolinhas, Conta de recebimento (`texto="Vinculada"`), Créditos (`texto="abaixo de N"`), Pacotes de créditos |
| `SituacaoCompraCreditos` | `AguardandoPagamento` → "Aguardando pagamento" · Info; `AguardandoConfirmacao` → "Boleto ou Pix emitido" · Alerta; `Paga` · Sucesso; `Expirada` · Neutro; `Falhou` · Perigo | Créditos (compras), Compra de créditos |
| `SituacaoWebhook` | `Recebido` · Info; `Processado` · Sucesso; `Ignorado` · Neutro; `Falhou` · Perigo | Pacotes de créditos (últimos webhooks) |
| `SituacaoAluno` | `Trial` · Info; `Ativo` · Sucesso; `Inativo` · Neutro; `Desistente` · Neutro | Alunos, ficha do aluno, mudar situação, ficha da turma e do responsável |
| `SituacaoMatricula` | `Ativa` · Sucesso; `Encerrada` · Neutro | ficha do aluno (Matrículas) |
| `SituacaoTurma` | `Ativa` · Sucesso; `Encerrada` · Neutro | Turmas, ficha do professor |
| `SituacaoSessao` | `Prevista` · Info; `Realizada` · Sucesso; `Cancelada` · Perigo; `Remarcada` · Alerta (sem uso, P11) | Agenda, ficha da turma, Painel (próximos treinos) |
| `EstadoConsentimento` | `Pendente` · Alerta; `Concedido` → "Autorizado" · Sucesso; `Recusado` · Neutro; `Revogado` · Neutro | ficha do aluno (Consentimentos) |
| `SinalTela`, valores do S1 | `TrialVencendo` → "Trial vencendo" · Alerta; `TrialVencido` → "Trial vencido" · Alerta; `TurmaLotada` → "Lotada" · Alerta; `SemMatricula` → "Sem turma" · Alerta; `ComAcesso` → "Com acesso" · Sucesso; `SemAcesso` → "Sem acesso ainda" · Neutro; `Pagador` → "Pagador" · Primario | Painel, Alunos, fichas do aluno e da turma, Turmas, Professores e Responsáveis |

Valor fora do mapa cai em `valor.ToString()` com a variante Neutro: sem acento e sem cor. **Máquina nova entra inteira no mapa, na mesma entrega:**

1. os casos no `switch` de `ApresentacaoStatus.Selo`, com rótulo em pt-BR e a variante da tabela 4.3;
2. os valores no array `selos` da vitrine (`Views/Design/Index.cshtml`);
3. na View, só `<selo-status>`.

### 4.3 A tabela do plano (Aluno e Sessão prontos; as demais planejadas)

A tabela da §4.2 do plano. **Aluno e Sessão entraram no mapa no S1, como planejados**; o "trial vencido" é o `SinalTela.TrialVencido`, porque o trial vencido continua em trial (§6 da spec). As outras máquinas valem quando cada uma nascer:

| Máquina | Sucesso | Info | Alerta | Perigo | Neutro |
|---|---|---|---|---|---|
| Aluno | ativo | trial | trial vencido | — | inativo, desistente |
| Cobrança | paga, paga em atraso | emitida | divergente (sinal) | vencida | prevista, isenta, cancelada |
| Convocação | confirmada | enviada | sem resposta | recusada | cancelada |
| Evento | realizado, homologado | agendado, confirmado | adiado | cancelado | — |
| Sessão | realizada | prevista | remarcada | cancelada | — |
| Mensagem | entregue, lida | enviada | retida | falhou | pendente, suprimida |
| Presença | presente | — | justificada | falta | não marcada |
| Sinais | — | — | risco de evasão | inadimplência (quando ligada) | — |

- A coluna "secondary / mono" do plano é o `Neutro` (`kt-badge-secondary`). O `VarianteSelo` não tem Mono.
- "Remarcada" (sessão) e "homologado" (evento) existem no domínio, mas ficam sem uso no piloto (P11).

### 4.4 Etiqueta não é status

`kt-badge` escrito direto na View vale só para **etiqueta que não é estado de máquina**:

| Etiqueta | Classes | Onde |
|---|---|---|
| fatia do módulo no menu | `kt-badge kt-badge-sm kt-badge-outline text-2xs` | `_Sidebar` |
| fatia nas próximas entregas | `kt-badge kt-badge-sm kt-badge-outline` | Painel |
| "Fatia S1 do piloto" | `kt-badge kt-badge-outline kt-badge-primary` | página "em breve" |
| perfil da modalidade | `kt-badge kt-badge-sm kt-badge-outline kt-badge-primary` | Modalidades |
| recurso ligado | `kt-badge kt-badge-sm kt-badge-outline kt-badge-success` | Modalidades |
| "provisório" | `kt-badge kt-badge-sm kt-badge-outline kt-badge-warning` | Parâmetros |
| "valor inicial das turmas" | `kt-badge kt-badge-sm kt-badge-outline kt-badge-info` | Parâmetros |

Estado que não tem enum de domínio por trás (a conta existe ou não, o saldo passou do aviso) **também** passa pelo mapa: entra como um valor de `SinalTela`, e a View usa `<selo-status valor="SinalTela...">`. O `texto` troca o rótulo; `class` só acrescenta classe de layout (`self-start`), nunca cor:

```cshtml
<selo-status valor="e.ContaAsaasVinculada ? SinalTela.ContaRecebimentoVinculada : SinalTela.ContaRecebimentoAusente"></selo-status>
<selo-status class="self-start" valor="SinalTela.SaldoCreditosBaixo" texto="@($"abaixo de {Model.AvisoSaldoAbaixoDe}")"></selo-status>
```

Na vitrine, "prevista", "paga", "emitida" e "convocação · Pedro" são escritos à mão porque as máquinas ainda não existem; na tela real, é `<selo-status>`.

---

## 5. As superfícies

| Layout | Quem usa | Classes do `<body>` | Seções | `ViewData` que o layout lê |
|---|---|---|---|---|
| `_LayoutAutenticacao` | todos, fora do sistema | `antialiased flex h-full text-base text-foreground bg-background` | `Scripts` | `Title` |
| `_Layout` (padrão, no `_ViewStart`) | gestor, equipe LDNA, professor no computador | as de cima + `demo1 kt-sidebar-fixed kt-header-fixed esc-tela` | `Estilos`, `Modais`, `Scripts` | `Title`, `ModuloAtivo` |
| `_LayoutQuadra` | professor no celular | `antialiased text-base text-foreground bg-background esc-quadra esc-toque` | `BarraAcao`, `Scripts` | `Title`, `Voltar`, `Pendentes` |
| `_LayoutPortal` | responsável no celular | `antialiased text-base text-foreground esc-portal esc-toque` | `Filhos`, `Scripts` | `Title`, `Escolinha`, `Inicial`, `AbaPortal` |
| `_LayoutVitrine` | desenvolvimento (`/dev/design`) | `antialiased text-base text-foreground esc-tela` | nenhuma | `Title` |

Os cinco ficam em `Views/Shared/` e incluem `_Cabeca` (§2.1), `_Mensagens` (toasts, §6.4) e `_Scripts` (§2.2).

### 5.1 `_LayoutAutenticacao`

- **Telas:** entrar, trocar senha e escolher escolinha; acesso negado e erro **quando não há login** (com login, essas duas usam o `_Layout`). Planejado: a ativação do convite (S2).
- **Composição:** uma coluna centralizada sobre `bg-muted/40`, com o logo provisório, o `@RenderBody()` e a linha "LDNA S/A · Minha Escolinha". Não há painel de marca nem imagem.
- **O cartão** (tela Entrar):

```cshtml
<div class="kt-card max-w-[380px] w-full">
    <form asp-action="Login" asp-route-returnUrl="@ViewData["ReturnUrl"]" method="post"
          class="kt-card-content flex flex-col gap-5 p-8 sm:p-10" novalidate>
        <div class="text-center mb-2.5">
            <h1 class="text-lg font-medium text-mono leading-none mb-2.5">Entrar</h1>
            <p class="text-sm text-secondary-foreground">Use o seu e-mail e a sua senha.</p>
        </div>
        <partial name="_ErrosFormulario" />
        <div class="flex flex-col gap-1">
            <label class="kt-form-label font-normal text-mono" asp-for="Login">E-mail</label>
            <input class="kt-input" asp-for="Login" type="email" inputmode="email" autocomplete="username" autofocus />
        </div>
        @* ... senha (§10.2) e "Manter conectado" (§6.17) ... *@
        <button class="kt-btn kt-btn-primary flex justify-center grow" type="submit">Entrar</button>
    </form>
</div>
```

- Larguras em uso: 380 px (entrar e a vitrine), 400 px (trocar senha) e 460 px (escolher escolinha).
- **Scripts:** só o `_Scripts`. Etapas ou seleção com busca aqui exigem carregar o script pela seção `Scripts` (§2.2).

### 5.2 `_Layout`: o painel (demo1)

```cshtml
<body class="antialiased flex h-full text-base text-foreground bg-background demo1 kt-sidebar-fixed kt-header-fixed esc-tela">
    @* min-w-0: conteúdo largo (tabelas) rola dentro do próprio contêiner em vez de esticar a página. *@
    <div class="flex grow min-w-0">
        <partial name="_Sidebar" />
        <div class="kt-wrapper flex grow flex-col min-w-0">
            <partial name="_Header" />
            <main class="grow pt-5 pb-8" id="content" role="main">
                @RenderBody()
            </main>
            <partial name="_Footer" />
        </div>
    </div>
    <partial name="_Mensagens" />
    <partial name="_Modais" />
    @await RenderSectionAsync("Modais", required: false)
```

**Sidebar (`_Sidebar`).** Sai do `CatalogoModulos`, a mesma fonte da busca rápida, das páginas "em breve" e das próximas entregas do Painel.
- `CatalogoModulos.NoMenu(plataforma, temEscolinha, r => Autorizacao.Pode(r, Operacao.Ler))`: os módulos da plataforma só aparecem para a equipe LDNA; os da escolinha, só com escolinha no contexto e conforme a matriz de permissões.
- Seções, nesta ordem: Painel (sem título) · Operação · Financeiro · Comunicação · Escolinha · Plataforma.
- **Item ativo:** vem de `ViewData["ModuloAtivo"]` (a chave do módulo) ou, na falta dele, do nome do controller. O item ganha `active` e `aria-current="page"`, e o `custom.css` pinta um fundo suave da marca.
- Módulo planejado mostra a fatia num `kt-menu-badge`.
- **Abaixo de `lg` (1024 px), a sidebar é um drawer:** `hidden lg:flex`, `data-kt-drawer="true"` e `[--kt-drawer-enable:true] lg:[--kt-drawer-enable:false]`. Quem abre é o botão `ki-menu` do header (`data-kt-drawer-toggle="#sidebar"`). No desktop, o `#sidebar_toggle` recolhe a sidebar (`data-kt-toggle="body" data-kt-toggle-class="kt-sidebar-collapse"`).

**Header (`_Header`).** É um `kt-header` fixo com `data-kt-sticky`.
- No celular: o logo e o botão do menu.
- A escolinha da sessão e os papéis da pessoa, com link para trocar de escolinha (a partir de `md`). Para a equipe sem escolinha, "Área da plataforma".
- **Busca rápida:** um campo-botão "Buscar telas… Ctrl K" a partir de `lg`; abaixo disso, um botão de lupa (§6.14).
- **Menu da pessoa** (dropdown KTUI): nome e login, modo escuro, trocar de escolinha, área da plataforma (só a equipe), trocar senha e sair.
- **Sino de notificações: planejado (S2).**

**Medidas** (do `styles.css`, regra `.demo1`): `--sidebar-width` de 280 px (80 px recolhida) e `--header-height` de 70 px (60 px abaixo de 1024 px). O `kt-wrapper` recebe o recuo da sidebar e do header pelo próprio `styles.css`. Nunca fixe essas medidas.

**IDs intocáveis:**
- o `demo1.js` procura `#sidebar`, `#sidebar_toggle`, `#sidebar_menu`, `#sidebar_scrollable` e `#header`;
- a sidebar declara `#sidebar_header` e `#sidebar_content` como dependências do `KTScrollable`;
- o `app.js` procura `#modal_busca`, `#busca_termo`, `#busca_resultados`, `#busca_vazio` e os links `#sidebar_menu a[data-esc-busca]`.

Renomear qualquer um quebra o recolhimento, a rolagem do menu ou a busca.

**`esc-tela`, a pele do painel** (herdada do DP): o fundo da página fica levemente acinzentado (`--muted` a 45% sobre `--background`); o `kt-card` ganha fio mais leve, raio de 0,75rem e sombra discreta; o `kt-card-title` fica em 600 e `text-mono`; o cabeçalho de tabela fica tingido; e os selos contornados viram suaves.

**Conteúdo:** cada tela abre `<div class="kt-container-fixed">` e o `<cabecalho-pagina>`. As seções do layout são `Estilos` (no `<head>`; nenhuma tela usa, e componente novo vai para o `custom.css`), `Modais` (fim do `<body>`) e `Scripts`.

### 5.3 `_LayoutQuadra`: o professor em pé, com uma mão e sinal ruim

```cshtml
<body class="antialiased text-base text-foreground bg-background esc-quadra esc-toque">
    <header class="esc-quadra-topo">
        <a class="kt-btn kt-btn-ghost kt-btn-icon" href="@voltar" aria-label="Voltar">
            <i class="ki-filled ki-arrow-left text-lg"></i>
        </a>
        <span class="esc-quadra-titulo">@ViewData["Title"]</span>
        <span class="esc-conexao" data-esc-conexao data-pendentes="@(ViewData["Pendentes"] ?? 0)" role="status" aria-live="polite">
            <i class="ki-filled ki-wifi"></i>
            <span data-esc-conexao-texto>Online</span>
        </span>
    </header>

    <main class="esc-quadra-conteudo" role="main">
        @RenderBody()
    </main>

    @if (IsSectionDefined("BarraAcao"))
    {
        <div class="esc-barra-acao">
            @await RenderSectionAsync("BarraAcao")
        </div>
    }
```

- **Barra superior compacta** (`esc-quadra-topo`, sticky, respeita a `safe-area-inset-top`): voltar (`ViewData["Voltar"]`, padrão `/`), o título com reticências (`esc-quadra-titulo`) e o selo de conexão.
- **Selo de conexão** (`esc-conexao`): o `app.js` atualiza na carga e nos eventos `online` e `offline`. Com sinal: "Online" ou, com pendentes, "Enviando N", e ícone `ki-wifi`. Sem sinal: "Sem sinal · N pendentes", ícone `ki-cloud-change` e a classe `esc-conexao-offline` (âmbar, com par no escuro). O número vem de `data-pendentes`.
- **Barra de ação inferior fixa** (`esc-barra-acao`, seção `BarraAcao`): dois ou três `kt-btn` lado a lado. Cada um é `flex: 1 1 0` com **`min-width: 0`**: sem isso, a barra vazava da tela em 375 px. O rótulo quebra em duas linhas, e o `kt-btn-primary` cresce 1,4 vez. A barra respeita a `safe-area-inset-bottom`, e o conteúdo (`esc-quadra-conteudo`, até 40rem) reserva a altura dela (`--esc-barra-acao`).

```cshtml
@section BarraAcao {
    <button class="kt-btn kt-btn-outline" type="button"><i class="ki-filled ki-check-squared"></i> Todos presentes</button>
    <button class="kt-btn kt-btn-primary" type="button"><i class="ki-filled ki-check"></i> Concluir chamada</button>
}
```

- **Toque (`esc-toque` no `<body>`):** `kt-btn`, `kt-input`, `kt-select` e `kt-textarea` com altura mínima de 48 px e texto de 16 px (o iOS dá zoom em campo com menos de 16 px); `kt-btn-icon` com 48 px de largura; `esc-segmento-opcao` com 48 px. O KTUI puro tem 34 px e texto de 13 px.
- **Planejado:** as telas reais são os controllers `Chamada` (S3) e `Convocacao` (S5). A chamada offline (S3) usa service worker com escopo `/Chamada/` e fila no IndexedDB; a CSP já permite o worker. O `atualizarConexao()` do `app.js` ainda não é público, e a fila offline vai precisar expô-lo para atualizar o selo quando o número de pendentes mudar.

### 5.4 `_LayoutPortal`: o responsável no celular, sem instalar nada

```cshtml
<body class="antialiased text-base text-foreground esc-portal esc-toque">
    <header class="esc-portal-topo">
        @* a escolinha (ViewData["Escolinha"]) e o avatar com a inicial (ViewData["Inicial"]) *@
        @await RenderSectionAsync("Filhos", required: false)
    </header>

    <main class="esc-portal-conteudo" role="main">
        @RenderBody()
    </main>

    <nav class="esc-portal-abas" aria-label="Portal">
        <a class="esc-portal-aba" href="#" aria-current="@Atual("inicio")"><i class="ki-filled ki-home-2"></i>Início</a>
        <a class="esc-portal-aba" href="#" aria-current="@Atual("jogos")"><i class="ki-filled ki-cup"></i>Jogos</a>
        <a class="esc-portal-aba" href="#" aria-current="@Atual("pagamentos")"><i class="ki-filled ki-dollar"></i>Pagamentos</a>
        <a class="esc-portal-aba" href="#" aria-current="@Atual("avisos")"><i class="ki-filled ki-notification-status"></i>Avisos</a>
    </nav>
```

- **Topo** (`esc-portal-topo`, sticky, com safe-area): a escolinha e, pela seção `Filhos`, os chips dos filhos (uma conta só para todos):

```cshtml
@section Filhos {
    <div class="esc-chips" data-esc-escolha role="group" aria-label="Filhos">
        <button class="esc-chip" type="button" aria-pressed="true" data-valor="todos">Todos</button>
        <button class="esc-chip" type="button" aria-pressed="false" data-valor="pedro">Pedro · Sub-11</button>
    </div>
}
```

- **Abas inferiores** (`esc-portal-abas`: fixas, quatro colunas, 64 px de altura, ícone de 22 px). A ativa vem de `ViewData["AbaPortal"]` (`inicio`, `jogos`, `pagamentos` ou `avisos`) e ganha `aria-current="page"`, na cor da marca. Os `href="#"` são provisórios até o S6 (`Areas/Portal`).
- **Conteúdo** (`esc-portal-conteudo`): uma coluna de cartões grandes, até 40rem, que reserva a altura das abas.
- **Resposta Sim/Não** (`esc-resposta`): dois botões em grade, com 48 px e texto de 16 px em peso 600:

```cshtml
<div class="esc-resposta">
    <button class="kt-btn kt-btn-primary" type="button"><i class="ki-filled ki-check"></i> Vai</button>
    <button class="kt-btn kt-btn-outline" type="button"><i class="ki-filled ki-cross"></i> Não vai</button>
</div>
```

- Os chips (`esc-chip`) têm 36 px de altura mínima, abaixo dos 48 px do resto do portal (§14).

### 5.5 `_LayoutVitrine` e `/dev/design`

| Rota | View | Layout |
|---|---|---|
| `/dev/design` | `Design/Index` | `_LayoutVitrine` |
| `/dev/design/quadra` | `Design/Quadra` (uma chamada fictícia) | `_LayoutQuadra` |
| `/dev/design/portal` | `Design/Portal` (dois filhos, uma convocação, as mensalidades) | `_LayoutPortal` |
| `/dev/design/autenticacao` | `Design/Autenticacao` (a ativação do convite) | `_LayoutAutenticacao` |

- **Só em Development** (fora dele, 404) e sem login (`[AllowAnonymous]`).
- **A página principal mostra:** os tokens de cor, os selos de todos os enums mapeados, botões, abas, controle segmentado, alerta, estado vazio, os ícones do domínio e a quadra e o portal em `<iframe class="esc-moldura-celular">` (até 24rem de largura e 44rem de altura).
- **`_LayoutVitrine`** é o miolo do painel (`esc-tela`) sem sidebar e sem header, com a chave de modo escuro no topo. Carrega só o `_Scripts`: não renderiza a seção `Scripts` (uma `@section Scripts` numa view da vitrine dá erro) e não tem `assistente.js` nem `selecao-busca.js`.
- **É onde o visual é conferido antes de virar tela.** Componente ou máquina de estado nova entra na vitrine na mesma entrega. A moldura é uma aproximação: a checagem de 375 e 320 px é no navegador redimensionado (§12).

---

## 6. Componentes

Reuse antes de criar. Componente novo vai para o `custom.css` com prefixo `esc-`, entra na vitrine e neste guia na mesma entrega.

### 6.1 Cabeçalho de página: `<cabecalho-pagina>`

| Atributo | Para quê |
|---|---|
| `titulo` | obrigatório |
| `descricao` | o que a tela faz, numa frase |
| `meta` | uma linha só de texto |
| `voltar-href`, `voltar-texto` | o link "voltar" acima do título (texto padrão: "Voltar") |
| conteúdo interno | as ações, à direita |

```cshtml
<cabecalho-pagina titulo="Modalidades" descricao="Cada modalidade tem um perfil e interruptores que decidem o que os eventos dela têm.">
    @if (podeAlterar)
    {
        <a class="kt-btn kt-btn-primary" asp-action="Nova"><i class="ki-filled ki-plus"></i> Nova modalidade</a>
    }
</cabecalho-pagina>

<cabecalho-pagina titulo="Créditos de mensagem" voltar-href="/Plataforma" voltar-texto="Escolinhas"
                  descricao="..." meta="@($"Saldo atual: {Formatacao.Numero(escolinha?.SaldoCreditos ?? 0)} crédito(s)")"></cabecalho-pagina>
```

- A saída é um `<div class="flex flex-wrap items-center lg:items-end justify-between gap-5 pb-7.5">` com o título em `<h1 class="text-xl font-medium leading-none text-mono">`, a descrição em `text-sm text-secondary-foreground`, a meta em `text-xs text-muted-foreground` e as ações em `flex flex-wrap items-center gap-2.5`. O link "voltar" é um `kt-link text-sm` com `ki-arrow-left`.
- **Sempre feche a tag.** O TagHelper força `TagMode.StartTagAndEndTag` por causa da armadilha do guia-base (TagHelper auto-fechado fazia o conteúdo seguinte virar filho, e o título sumia), mas a regra de View continua: `<cabecalho-pagina ...></cabecalho-pagina>`.
- `meta` aceita só texto; selo ou link vai numa linha logo abaixo do cabeçalho.

### 6.2 Estado vazio: `<estado-vazio>`

```cshtml
<div class="kt-card">
    <estado-vazio icone="ki-grid" titulo="Nenhuma escolinha ainda" texto="Crie a escolinha piloto junto com o gestor dela.">
        <a class="kt-btn kt-btn-primary" asp-action="Nova"><i class="ki-filled ki-plus"></i> Nova escolinha</a>
    </estado-vazio>
</div>
```

- `icone` (padrão `ki-information-2`), `titulo` (obrigatório), `texto` e, como conteúdo interno, a ação. Feche a tag.
- A saída é um círculo `size-16 rounded-full bg-muted/70` com o ícone em `text-3xl text-muted-foreground`, o título em `text-base font-semibold text-mono` e o texto em `text-sm text-secondary-foreground`.
- Vai dentro de um `kt-card`. As páginas inteiras de erro e de acesso negado usam a mesma forma, maior (`size-20`, `text-4xl`), escrita na própria View.
- Sem ilustração: as imagens do demo não são servidas.

### 6.3 Selo de status

Ver a §4.

### 6.4 Toasts

**Do servidor, depois de um redirect (PRG):**

```csharp
TempData[ChavesTempData.Sucesso] = resultado.Mensagem;   // ou ChavesTempData.Erro
return RedirectToAction(nameof(Index));
```

O `_Mensagens.cshtml`, presente em todos os layouts, vira um marcador sem script inline:

```cshtml
<div hidden data-esc-mensagens data-sucesso="@sucesso" data-erro="@erro"></div>
```

Na carga, o `app.js` mostra o sucesso com a variante `success` e o erro com `destructive`, via `KTToast.show({ message, variant })`. Sem o KTUI, cai no `window.alert`.

**Do JS:** `esc.toast("Copiado.", "success")`.
- Variantes que o `styles.css` pinta: `success`, `warning`, `destructive`, `info`, `primary` e `mono`. O KTUI também aceita `error` e `secondary`, mas as classes que eles geram não existem no CSS. O projeto usa `success`, `destructive` e `warning`.
- Posição e duração são as padrão do KTUI: canto superior direito, 4 s. O toast já sai com `role="status"` e `aria-live="polite"`.
- **Escape:** o KTToast monta o toast inserindo a mensagem como HTML (concatena no `innerHTML`), e as mensagens levam nomes digitados na tela ("Modalidade {Nome} cadastrada."). Por isso o `esc.toast` **sempre escapa** o texto antes (`escaparHtml` no `app.js`). Nunca chame `KTToast.show` direto com texto do usuário.

### 6.5 Resumo de erros: `_ErrosFormulario`

```cshtml
<div class="kt-alert kt-alert-light kt-alert-destructive flex items-start gap-3" role="alert">
    <i class="ki-filled ki-information-2 text-lg shrink-0"></i>
    <div class="flex flex-col gap-1 text-sm">
        @foreach (var mensagem in mensagens)
        {
            <span>@mensagem</span>
        }
    </div>
</div>
```

- Entra no começo do conteúdo do formulário: `<partial name="_ErrosFormulario" />`. Só aparece com o ModelState inválido, e sem mensagens repetidas.
- Aviso informativo usa a mesma forma: `kt-alert kt-alert-light kt-alert-info` ou `kt-alert kt-alert-light kt-alert-warning`, com `flex items-start gap-3`, `ki-information-2` e `role="status"` (Parâmetros, Escolinha criada).
- O padrão completo de erro está na §7.

### 6.6 Assistente em etapas (`js/assistente.js`)

```cshtml
<form asp-action="Nova" method="post" novalidate data-esc-assistente>
    <div class="kt-card-header flex-wrap gap-3 py-4">
        <h3 class="kt-card-title">Cadastro</h3>
        <ol class="esc-passos" data-esc-passos></ol>
    </div>
    <div class="kt-card-content flex flex-col gap-5">
        <partial name="_ErrosFormulario" />
        <section class="flex flex-col gap-5" data-esc-passo data-esc-passo-titulo="Escolinha"> ... </section>
        <section class="flex flex-col gap-5" data-esc-passo data-esc-passo-titulo="Modalidade"> ... </section>
        <section class="flex flex-col gap-5" data-esc-passo data-esc-passo-titulo="1º gestor"> ... </section>
    </div>
    <div class="kt-card-footer justify-end gap-2.5">
        <a class="kt-btn kt-btn-ghost me-auto" asp-action="Index">Cancelar</a>
        <button class="kt-btn kt-btn-outline" type="button" data-esc-passo-anterior><i class="ki-filled ki-arrow-left"></i> Voltar</button>
        <button class="kt-btn kt-btn-primary" type="button" data-esc-passo-proximo>Continuar <i class="ki-filled ki-arrow-right"></i></button>
        <button class="kt-btn kt-btn-primary" type="submit" data-esc-passo-final><i class="ki-filled ki-check"></i> Criar escolinha</button>
    </div>
</form>
```

| Atributo | Papel |
|---|---|
| `data-esc-assistente` | no `<form>`; precisa de ao menos duas etapas |
| `data-esc-passo` e `data-esc-passo-titulo` | cada etapa (`<section>`) e o nome dela na lista |
| `data-esc-passos` | a `<ol class="esc-passos">`, preenchida pelo JS |
| `data-esc-passo-anterior`, `data-esc-passo-proximo` | Voltar e Continuar |
| `data-esc-passo-final` | o submit, que só aparece na última etapa |
| `data-esc-passo-erro` | marca a etapa de um erro que não tem campo |

- **Uma etapa por vez.** A lista vira botões `esc-passo` (com `esc-passo-numero` e `esc-passo-titulo`); a etapa atual ganha `esc-passo-atual` e `aria-current="step"`, e as anteriores, `esc-passo-feito`.
- **Continuar valida só os campos da etapa**, com as restrições do HTML (`required`, `maxlength`, `type="email"`) por `checkValidity()` e `reportValidity()`, mesmo com `novalidate` no form. Voltar é livre; clicar numa etapa à frente valida o caminho. Enter num `<input>` avança em vez de enviar antes da hora. O envio final vai para o servidor, que é quem valida de verdade.
- **Onde ele abre:** na etapa de `?passo=N` (contando de 1); senão, na primeira etapa com `[data-esc-passo-erro]` ou com campo `aria-invalid="true"` (§7); senão, na primeira. E põe o foco no campo com erro.
- **Sem JS**, todas as etapas aparecem, e a lista e os botões Voltar e Continuar ficam ocultos (`form:not(.esc-assistente-ativo)` no `custom.css`); o submit continua visível.
- **Assistente no cadastro, página inteira na edição** (Turma): o `<form>` sempre leva `data-esc-assistente`, mas só o cadastro tem Continuar e o envio final. Sem esses dois botões o JS não divide o formulário (D42), e cada etapa mostra o próprio título, `<h4 class="esc-passo-cabecalho">`, que some quando o assistente está ativo (§6.24).
- Só carrega no `_Layout` (§2.2).

### 6.7 Select dependente (`js/assistente.js`)

Pronto no JS, ainda sem uso em tela. O `<select data-esc-depende="#origem" data-esc-sugestao="Texto">` mostra só as `<option data-grupo="...">` cujo grupo é o valor escolhido no `#origem`; opção sem `data-grupo` (como "nenhum") vale sempre. Se a escolhida sumiu, tenta a opção cujo texto é a sugestão. A origem precisa ter `id`.

### 6.8 Abas em pílula (`esc-abas`)

**Com links (aba = rota).** É o caso dos domínios de Parâmetros:

```cshtml
<nav class="esc-abas mb-5" aria-label="Domínios">
    @foreach (var dominio in Model.Dominios)
    {
        <a class="esc-aba" asp-action="Index" asp-route-dominio="@dominio" aria-current="@(dominio == Model.Dominio ? "page" : null)">
            @CatalogoParametros.RotuloDominio(dominio)
        </a>
    }
</nav>
```

**Na mesma página (painéis).** O `app.js` alterna `aria-selected` e o `hidden` dos painéis:

```cshtml
<div class="esc-abas" role="tablist">
    <button class="esc-aba" type="button" role="tab" aria-selected="true" data-esc-aba="#vitrine_aba_1">Presença</button>
    <button class="esc-aba" type="button" role="tab" aria-selected="false" data-esc-aba="#vitrine_aba_2">Financeiro <span class="esc-aba-contador">3</span></button>
</div>
<p class="text-sm text-secondary-foreground" id="vitrine_aba_1">Conteúdo da aba Presença.</p>
<p class="text-sm text-secondary-foreground" id="vitrine_aba_2" hidden>Conteúdo da aba Financeiro.</p>
```

- A aba ativa é a que tem `aria-current="page"` (link) ou `aria-selected="true"` (botão). `esc-aba-contador` é o número ao lado do rótulo.
- Abaixo de 640 px, as abas rolam na horizontal em vez de quebrar em várias linhas.
- As abas do KTUI (`kt-tabs`, `kt-tab-toggle`, `data-kt-tabs`) existem no `styles.css`, mas o projeto usa `esc-abas`.

### 6.9 Controle segmentado e chips (`data-esc-escolha`)

```cshtml
<div class="esc-segmento" data-esc-escolha>
    <button class="esc-segmento-opcao" type="button" aria-pressed="true" data-valor="meus">Minhas turmas</button>
    <button class="esc-segmento-opcao" type="button" aria-pressed="false" data-valor="todos">Todas</button>
</div>
```

- O `app.js` mantém um único `aria-pressed="true"` entre os filhos diretos do grupo e dispara `esc:escolha` no grupo (propaga), com `detail.valor`:

```js
document.addEventListener("esc:escolha", function (evento) {
  // evento.target é o grupo; evento.detail.valor é o data-valor escolhido
});
```

- **P/F da chamada:** as opções ganham `esc-presente` (verde) e `esc-falta` (`--destructive`) quando marcadas; na quadra (`esc-toque`), cada opção tem 48 px. A forma, simplificada da vitrine (`Design/Quadra.cshtml`):

```cshtml
<li class="esc-linha-chamada">
    <span class="esc-linha-chamada-nome">@aluno</span>
    <div class="esc-segmento" data-esc-escolha role="group" aria-label="@($"Presença de {aluno}")">
        <button class="esc-segmento-opcao esc-presente" type="button" aria-pressed="false" data-valor="P">P</button>
        <button class="esc-segmento-opcao esc-falta" type="button" aria-pressed="false" data-valor="F">F</button>
    </div>
</li>
```

- Os **chips dos filhos** do portal (`esc-chips` e `esc-chip`, §5.4) usam o mesmo `data-esc-escolha`.
- O controle guarda só o estado visual: gravar a escolha é da tela, ouvindo `esc:escolha` (planejado para a chamada, S3).

### 6.10 Seleção com busca (`js/selecao-busca.js`)

O "select2" do sistema, sem jQuery. Pronto no JS e no CSS; nenhuma tela do S0 usa, e a vitrine também não mostra. Exemplo de uso:

```html
<select class="kt-select" id="turma" name="turma" data-esc-selecao-busca data-esc-busca-placeholder="Buscar turma…">
    <option value="">Selecione</option>
    ...
</select>
```

- O `<select>` original fica escondido (`esc-selecao-original`) e **continua sendo a fonte**: formulário, filtros e o evento `change`. Em volta dele nasce um `esc-selecao` com um botão `kt-input esc-selecao-botao` (texto em `esc-selecao-texto`) e, ao abrir, o painel `esc-selecao-painel`: busca (`kt-input kt-input-sm`) e lista `esc-selecao-lista` (`role="listbox"`) de `esc-selecao-opcao`. A opção destacada ganha `esc-selecao-ativa`; lista vazia mostra `esc-selecao-vazio` ("Nada encontrado").
- O `<label for>` passa a apontar para o botão. A busca ignora acento.
- **Teclado:** seta para baixo, Enter ou Espaço abrem; setas navegam; Enter escolhe; Esc e Tab fecham.
- **No celular** (abaixo de 640 px): a lista vai até 60% da altura da tela e cada opção tem texto de 16 px.
- Em HTML injetado, `esc.reinicializarComponentes()` aplica o componente. Depois de mudar o valor por código, `window.escSelecaoBusca.atualizar()` redesenha o botão.
- Só carrega no `_Layout` (§2.2).

### 6.11 Confirmação e bloqueio de duplo envio

```cshtml
<form asp-action="EntrarComoEquipe" asp-route-id="@e.Id" method="post"
      data-esc-confirmar="@(e.SouGestor ? null : "Você passa a atuar como gestor desta escolinha (fica registrado na auditoria dela). Continuar?")">
```

- `data-esc-confirmar="Pergunta"` pede confirmação com `window.confirm` antes de enviar. Valor nulo no Razor omite o atributo, e aí não há pergunta.
- **Todo formulário**, confirmado ou não, tem os `button[type=submit]` desabilitados quando o envio segue de fato (bloqueio de duplo envio), marcados com `data-esc-enviando`. Por isso todo botão declara `type`.
- A checagem roda depois de todos os ouvintes do `submit`: se algum cancelou o envio (`preventDefault`, formulário por AJAX), os botões continuam ativos.
- Voltar pelo histórico (página restaurada do cache) reabilita os botões marcados.
- **Planejado:** o modal de confirmação com motivo (desligar aluno, cancelar cobrança).

### 6.12 Copiar (`data-esc-copiar`)

```cshtml
<span class="esc-senha-temporaria text-lg font-semibold text-mono" id="senha-temporaria">@Model.SenhaTemporaria</span>
<button class="kt-btn kt-btn-sm kt-btn-outline" type="button" data-esc-copiar="#senha-temporaria">
    <i class="ki-filled ki-copy"></i> Copiar
</button>
```

Copia o `textContent` do alvo (sem espaços nas pontas) com `navigator.clipboard` e mostra o toast "Copiado.". A API de área de transferência só existe em HTTPS ou localhost; sem ela, o botão não faz nada. `esc-senha-temporaria` é a fonte monoespaçada com `user-select: all`.

### 6.13 `esc.fetch` e `esc.reinicializarComponentes()`

Exemplo de uso (nenhuma tela do S0 usa AJAX ainda):

```js
esc.fetch(form.action, { method: "POST", body: new FormData(form) })
  .then(function (resposta) { return resposta.json(); })
  .then(function (dados) { /* { sucesso, mensagem, ... } */ });
```

`esc.fetch(url, opcoes)` é o `fetch` do projeto:
- manda `X-Requested-With: fetch` e, se a tela não definir outro, `Accept: application/json`;
- em método que não seja GET nem HEAD, manda o token antiforgery no cabeçalho **`RequestVerificationToken`** (lido da meta `csrf-token` do `_Cabeca`; é o nome configurado no `Program.cs`);
- usa `credentials: "same-origin"`;
- **401** (sessão expirada): toast de aviso e ida para `/Conta/Login?returnUrl=` com a página atual.

Com esses cabeçalhos, o servidor responde **JSON em vez de redirect**: 401, 403 (sem permissão, sem escolinha escolhida ou com troca de senha pendente), sempre com `{ sucesso: false, mensagem }`. O 403 fica por conta da tela.

**Depois de injetar HTML, chame `esc.reinicializarComponentes()`.** Hoje ela faz duas coisas: chama o `KTComponents.init()` do `core.bundle.js`, que só reinicia o `KTMenu`, e reaplica a seleção com busca.

> **Limitação:** modal, dropdown, drawer, abas, tooltip e mostrar/ocultar senha do KTUI **não** são reiniciados por ela. Nenhuma tela do S0 injeta HTML, então o problema ainda não apareceu. Até a função ser estendida (planejado, antes da primeira tela com AJAX), chame também o `init()` estático do componente injetado: `KTDropdown.init()`, `KTModal.init()`, `KTDrawer.init()`, `KTTabs.init()`, `KTTooltip.init()`, `KTTogglePassword.init()`. Cada um cria instância só para os elementos novos.

**Página que acompanha sozinha** (primeiro uso: a volta do pagamento, `Views/Creditos/Compra.cshtml` + `js/compra-creditos.js`):
- **Marcador:** a View põe um `<div hidden data-esc-compra-url="..." data-esc-compra-situacao="...">` só enquanto o processo não terminou.
- **Consulta:** o script pergunta a situação por `esc.fetch` a cada 3 s, por até 2 min. Quando a situação muda, recarrega a página (o servidor monta o estado novo); ao fim do prazo, mostra o aviso de que pode fechar.
- **Arquivo próprio:** o script da tela entra pela seção `Scripts` do layout, sempre declarada no nível de cima da View (Razor não aceita `@section` dentro de `@if`; o `if` vai dentro da seção).

### 6.14 Busca rápida (Ctrl+K)

- Abre com **Ctrl+K** (ou Cmd+K) em qualquer lugar, com **`/`** fora de campo de texto, e pelos botões do header (`data-kt-modal-toggle="#modal_busca"`).
- Lista os links do menu (`#sidebar_menu a[data-esc-busca]`): só as telas que a pessoa pode ver. Filtra por título e resumo, ignorando acento. Setas navegam, Enter abre, Esc fecha.
- O modal fica no `_Modais` e só existe no `_Layout`; nos outros layouts, o atalho não faz nada.
- Módulo novo entra na busca sozinho se vier do `CatalogoModulos`: o `_Sidebar` escreve `data-esc-busca`, `data-esc-busca-resumo` e `data-esc-busca-icone` em cada link.
- **Foco no campo:** o KTModal foca o elemento **de dentro** do modal que tem o atributo `data-kt-modal-input-focus`. No `_Modais`, o atributo está na raiz do modal (com um seletor como valor), então o KTUI não o encontra, e quem põe o foco no campo é o `app.js`. Em modal novo, ponha o atributo no próprio `<input>`.

### 6.15 Paginação: `_Paginacao`

```cshtml
@{
    var paginacao = PaginacaoViewModel.De(Model.Movimentos, "movimento(s)", new Dictionary<string, string?>());
}
...
<partial name="_Paginacao" model="paginacao" />
```

É um `kt-card-footer` com "1–20 de 57 movimento(s)" (ou "Nenhum(a) ..." quando a lista está vazia). Com mais de uma página, aparecem "Página N de M" e os botões Anterior e Próxima (`kt-btn kt-btn-sm kt-btn-outline`, com `ki-left` e `ki-right`). Os links mantêm os filtros passados em `Parametros` e levam `?pagina=N` (a página 1 vai sem o parâmetro). O rodapé vai dentro do `kt-card` da lista, logo depois da tabela.

### 6.16 Editor de parâmetro: `_CampoParametro`

Fica em `Views/Parametros/_CampoParametro.cshtml` (não em `Shared`). Cada parâmetro é uma linha da lista:

```cshtml
<ul class="esc-linhas">
    @foreach (var parametro in Model.Parametros)
    {
        <li class="px-5 py-4">
            <partial name="_CampoParametro" model="parametro" />
        </li>
    }
</ul>
```

- **Coluna da esquerda:** o rótulo, com as etiquetas "valor inicial das turmas" (parâmetro de escopo Turma) e "provisório" (valor que a spec não define e a escolinha ainda não mudou); a descrição; e a linha "Padrão: … · faixa … · origem · alterado por … em …".
- **Coluna da direita:** o editor, conforme o tipo do catálogo:

| Tipo | Controle | Nome no formulário |
|---|---|---|
| `Booleano` | `kt-switch` + oculto `false` + "Ligado"/"Desligado" | `valores[chave]` |
| `Escolha` | `kt-select` com as opções do catálogo | `valores[chave]` |
| `Janela` | dois `<input type="time">` (início e fim) | `janelaInicio[chave]` e `janelaFim[chave]`; o controller junta em `HH:mm-HH:mm` |
| `ListaDias` | `kt-input` de texto: "1, 5, 10" | `valores[chave]` |
| `Percentual` e `Moeda` | `kt-input-group` com o `kt-input-addon` "%" ou "R$" | `valores[chave]` |
| `Inteiro` | `kt-input` `type="number"` com mínimo e máximo, e a unidade num `kt-input-addon` | `valores[chave]` |
| `Hora` | **sem editor próprio:** cai no `type="number"`. Nenhum parâmetro do catálogo usa esse tipo hoje; acrescente o caso com `type="time"` antes de usar | — |

- **`ViewData` que ele lê:** `PodeAlterar` (sem ele, tudo fica `disabled`) e `ChavesComErro` (marca `aria-invalid`, §7.3).
- Parâmetro já alterado mostra o link "Ver histórico" (`/Parametros/Historico?chave=...`).
- A action que recebe esses dicionários declara o nome do binding: `[FromForm(Name = "valores")]` (D20).

### 6.17 Formulário, tabela e blocos de dados

**Formulário padrão** (Escolinha › Editar):

```cshtml
<div class="kt-card max-w-[820px]">
    <form asp-action="Editar" method="post" novalidate>
        <div class="kt-card-content flex flex-col gap-5">
            <partial name="_ErrosFormulario" />
            <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
                <div class="flex flex-col gap-1 md:col-span-2">
                    <label class="kt-form-label" asp-for="Nome">Nome da escolinha</label>
                    <input class="kt-input" asp-for="Nome" required maxlength="150" />
                </div>
                ...
            </div>
        </div>
        <div class="kt-card-footer justify-end gap-2.5">
            <a class="kt-btn kt-btn-ghost me-auto" asp-action="Index">Cancelar</a>
            <button class="kt-btn kt-btn-primary" type="submit"><i class="ki-filled ki-check"></i> Salvar</button>
        </div>
    </form>
</div>
```

- `novalidate` no form: quem valida é o servidor (§7). Larguras de cartão em uso: `max-w-[600px]`, `max-w-[640px]`, `max-w-[720px]`, `max-w-[820px]` e `max-w-[900px]`.
- Campo opcional: `<span class="text-muted-foreground">(opcional)</span>` dentro do rótulo. Dica embaixo do campo: `<span class="text-xs text-muted-foreground">`. Separador entre grupos: `<div class="border-t border-border"></div>`.
- Unidade junto do campo: `kt-input-group` + `kt-input-addon` (duração da modalidade, "min").
- **Checkbox com texto** (a classe `kt-checkbox-label` do demo não existe):

```cshtml
<label class="kt-label">
    <input class="kt-checkbox kt-checkbox-sm" asp-for="Lembrarme" type="checkbox" />
    <span class="text-sm text-foreground">Manter conectado neste aparelho</span>
</label>
```

- **Checkbox ou switch sem `asp-for`** (nome montado à mão) precisa do oculto `false` logo depois, porque checkbox desmarcado não é enviado: `<input type="hidden" name="@nome" value="false" />`. Com `asp-for`, o ASP.NET já cuida disso.

**Tabela** (Plataforma › Escolinhas):

```cshtml
<div class="kt-card">
    <div class="kt-card-table kt-scrollable-x-auto">
        <table class="kt-table kt-table-border align-middle text-sm">
            <thead>
                <tr>
                    <th class="min-w-[220px]">Escolinha</th>
                    <th class="min-w-[180px] hidden md:table-cell">Gestores</th>
                    ...
                    <th class="w-[1%] text-end">Ações</th>
                </tr>
            </thead>
```

- Sempre dentro de `kt-card-table kt-scrollable-x-auto`: a tabela rola dentro do cartão, nunca a página. Coluna secundária some com `hidden md:table-cell`.
- Lista vazia vira `<estado-vazio>` dentro do mesmo `kt-card`.

**Blocos de dados** (`custom.css`):

| Classe | Para quê | Onde |
|---|---|---|
| `esc-dados` | um `<dl>` de rótulo e valor em duas colunas (rótulo de 8 a 12rem); abaixo de 640 px, uma coluna | Dados da escolinha, Conta de recebimento, Escolinha criada |
| `esc-linhas` | uma lista com fio entre as linhas | Parâmetros, Painel, Modalidades (interruptores), portal |
| `esc-passo-config`, `esc-passo-config-icone`, `esc-passo-config-texto`, `esc-passo-config-acao`, `esc-passo-config-feito` | um passo da configuração: ícone num círculo, texto e o botão da ação; feito fica verde. Abaixo de 640 px, o botão desce para baixo do texto, alinhado a ele | Painel |
| `esc-senha-temporaria` | a senha que aparece uma vez | Escolinha criada |

```cshtml
<dl class="esc-dados">
    <dt>Nome</dt>
    <dd>@Model.Nome</dd>
    <dt>CNPJ</dt>
    <dd>@Ou(Formatacao.Cnpj(Model.Cnpj))</dd>
</dl>
```

**Cartão de indicador** (Créditos): `<span class="text-xs text-secondary-foreground">Saldo</span>` sobre `<span class="text-3xl font-semibold text-mono">`, dentro de `kt-card-content flex flex-col gap-1`, numa grade `grid grid-cols-1 md:grid-cols-3 gap-5`.

### 6.18 Classes da quadra, do portal e da vitrine

| Classe | O que faz |
|---|---|
| `esc-toque` | no `<body>` da quadra e do portal: alvos de 48 px e campo com texto de 16 px (§5.3) |
| `esc-quadra`, `esc-quadra-topo`, `esc-quadra-titulo`, `esc-quadra-conteudo` | a estrutura do `_LayoutQuadra` (altura mínima `100dvh`) |
| `esc-barra-acao` | a barra de ação inferior fixa |
| `esc-conexao`, `esc-conexao-offline` | o selo de conexão (online / sem sinal) |
| `esc-linha-chamada`, `esc-linha-chamada-nome` | a linha do aluno na chamada: nome com reticências + P/F |
| `esc-presente`, `esc-falta` | a cor do P e do F marcados (`aria-pressed="true"`) |
| `esc-segmento`, `esc-segmento-opcao` | o controle segmentado (§6.9) |
| `esc-portal`, `esc-portal-topo`, `esc-portal-conteudo` | a estrutura do `_LayoutPortal` |
| `esc-chips`, `esc-chip` | os chips dos filhos; o marcado tem `aria-pressed="true"` e a cor da marca |
| `esc-portal-abas`, `esc-portal-aba` | as abas inferiores do portal |
| `esc-resposta` | a grade dos botões Sim/Não |
| `esc-amostra-cor`, `esc-moldura-celular` | só da vitrine: a amostra de token e a moldura de celular do `<iframe>` |

As parciais que o plano previa para essas peças (`_StatusConexao`, `_LinhaChamada`, `_BarraAcaoInferior`, `_AbasPortal`) não foram criadas: o selo e as abas moram nos layouts, a barra é a seção `BarraAcao`, e a linha da chamada é markup com as classes acima (§14).

### 6.19 Linhas repetíveis (`js/linhas.js`)

Horários da turma e faixas de disponibilidade do professor: uma lista que ganha e perde linhas, com o model binding recebendo a lista inteira.

```cshtml
<div class="flex flex-col gap-3" data-esc-linhas data-esc-linhas-prefixo="Horarios">
    <ul class="flex flex-col gap-3" data-esc-linhas-lista>
        <li class="esc-linha-horario" data-esc-linha>
            <input type="hidden" asp-for="Horarios[i].Id" data-esc-linhas-id />
            <select class="kt-select" asp-for="Horarios[i].DiaSemana" aria-label="Dia da semana"> ... </select>
            <input class="kt-input" asp-for="Horarios[i].Inicio" type="time" aria-label="Início" />
            <input class="kt-input" asp-for="Horarios[i].Fim" type="time" aria-label="Fim" />
            <button class="kt-btn kt-btn-icon kt-btn-ghost" type="button" data-esc-linhas-remover hidden aria-label="Retirar horário"><i class="ki-filled ki-trash"></i></button>
        </li>
    </ul>
    <template data-esc-linhas-modelo> <li class="esc-linha-horario" data-esc-linha> ... name="Horarios[__i__].Inicio" ... </li> </template>
    <div><button class="kt-btn kt-btn-sm kt-btn-outline" type="button" data-esc-linhas-adicionar hidden><i class="ki-filled ki-plus"></i> Outro horário</button></div>
</div>
```

- **A linha nova sai do `<template>`** desenhado pelo servidor, com `__i__` no lugar do índice; o JS não monta HTML.
- **A cada mudança, o JS renumera** `name`, `id`, `label[for]` e o valor do oculto `__Invariant`, que o ASP.NET põe nos campos de data e hora (armadilha 26).
- **Retirar a última linha** só a limpa, e o id escondido (`data-esc-linhas-id`) é zerado: a lista nunca some.
- **Sem JS**, as linhas que o servidor desenhou funcionam, inclusive uma linha em branco extra; os botões ficam ocultos. Linha sem horário é ignorada no servidor.
- **`esc-linha-horario`** (`custom.css`): grade de dia, início, fim e botão; abaixo de 480 px, o dia ocupa a linha de cima.

### 6.20 Busca de responsável (`js/busca-responsavel.js`) e `_ResponsavelDoAluno`

O cadastro do aluno e o "vincular responsável" usam a parcial `Shared/_ResponsavelDoAluno` (`<partial name="_ResponsavelDoAluno" for="Responsaveis[i]" view-data="..." />`). Ela junta três coisas:
- **a busca** de quem já é responsável na escolinha (nome, CPF ou celular), por `esc.fetch` em `/Responsaveis/Buscar`; o resultado mostra o CPF mascarado e os filhos;
- **o cadastro novo**, quando ninguém foi escolhido;
- **as marcas do vínculo:** parentesco, pagador, recebe cobrança e recebe notificação.

**Sugestão de cadastro parecido** (mesmo celular ou mesmo nome, D33): o servidor devolve a tela com um alerta e as opções:
- cada candidato tem um botão **"Usar este cadastro"**, um `type="submit"` com `name` = o campo do id e `value` = o candidato. Enquanto a sugestão está na tela, o oculto do id fica `disabled` para não disputar o valor;
- a caixa **"É outra pessoa"** (`ConfirmarNovo`) cria um cadastro novo.

Enter na busca não envia o formulário. **Sem JS**, a busca não aparece: o CPF igual reaproveita sozinho no servidor, e celular ou nome igual viram sugestão.

### 6.21 Endereço: `_CamposEndereco`

`<partial name="_CamposEndereco" model="Model" />` desenha rua, número, complemento, bairro, cidade, UF e CEP para qualquer modelo que implemente `IComEndereco` (local, aluno, responsável). Os nomes ficam na raiz do formulário. No aluno novo, o endereço vem recolhido num `<details class="rounded-xl border border-border">` com "(opcional)" no `<summary>`.

### 6.22 Ocupação de vagas: `progress.esc-ocupacao`

```cshtml
<progress class="esc-ocupacao" value="@Math.Min(t.Ocupadas, t.Capacidade)" max="@t.Capacidade" aria-label="@($"{t.Ocupadas} de {t.Capacidade} vagas ocupadas")"></progress>
```

`<progress>` nativo, porque a CSP não deixa `style=` para uma barra de largura variável. A barra usa `--primary` sobre `--muted`; a turma cheia ganha o selo `SinalTela.TurmaLotada` ao lado, e a barra não muda de cor. Onde: Turmas e ficha da turma.

### 6.23 Item de agenda: `esc-item-agenda`

Uma linha por sessão: a hora numa coluna fixa (`esc-item-agenda-hora`, com números tabulares), a turma com local, professor e alertas no meio, e o selo e as ações à direita. Abaixo de 640 px, a hora sobe para uma linha própria. Onde: Agenda (agrupada por dia, com `Formatacao.DiaPorExtenso`) e Painel (próximos treinos).

Os alertas de agenda são texto com `ki-information-2 text-warning`, um por linha, e não selos: podem ser vários na mesma sessão, e cada um explica o conflito.

### 6.24 Cabeçalho de etapa e filtros recolhidos

- **`esc-passo-cabecalho`:** o título de uma etapa quando o formulário aparece inteiro (edição da turma, ou sem JS). Com o assistente ativo, ele some, porque o nome da etapa já está na lista `esc-passos` (§6.6).
- **Filtros secundários em `<details>`** (Agenda): no celular, os filtros tomavam a primeira tela. Recorte e mês ficam à vista; período, situação e modalidade ficam num `<details>` com `<summary class="cursor-pointer text-sm font-medium text-mono">`, que abre sozinho quando algum desses filtros está em uso.

---

## 7. Erro de formulário: o padrão do projeto

> **Novo neste projeto (D19, X06).** Nasceu na validação do S0 no navegador: o assistente voltava para a etapa 1 depois de um erro, e a aba de parâmetros perdia o que tinha sido digitado.

**A regra:** em erro, a tela volta **com o que foi digitado**, o resumo no topo e **o campo marcado**. Em sucesso, PRG (redirect) + toast. **Nunca redirect em erro**: apaga a digitação.

### 7.1 O fluxo

| Camada | O que faz |
|---|---|
| Serviço (`Application`) | devolve `ResultadoOperacao` com `Campo` opcional. `ResultadoOperacao.Invalido(validacao)` já traz a primeira falha do FluentValidation com o campo dela; `ResultadoOperacao.Erro(mensagem, nameof(Requisicao.Campo))` serve para regra de negócio |
| Controller | `ModelState.AdicionarErro(resultado)` e `return View(requisicao)` |
| `AdicionarErro` (extensão em `Infraestrutura/Apresentacao.cs`) | `modelState.AddModelError(resultado.Campo ?? string.Empty, resultado.Mensagem)` |
| `_ErrosFormulario` | o resumo no topo (`kt-alert-destructive`, `role="alert"`) |
| `CampoInvalidoTagHelper` (`Infraestrutura/TagHelpers.cs`) | todo `input`, `select` e `textarea` com `asp-for` cujo campo tem erro no ModelState ganha `aria-invalid="true"`, sem mexer na View |
| KTUI (`styles.css`) | `[aria-invalid="true"]` pinta a borda de vermelho em `kt-input`, `kt-select`, `kt-textarea`, `kt-checkbox`, `kt-radio` e `kt-switch`; o leitor de tela anuncia o erro |
| `assistente.js` | abre na etapa do campo marcado e põe o foco nele (§6.6) |

### 7.2 Exemplo real: nova escolinha com um e-mail que já tem acesso

**Serviço** (`ServicoPlataforma.CriarEscolinhaComGestorAsync`):

```csharp
var validacao = await validadorNova.ValidateAsync(requisicao, ct);
if (!validacao.IsValid) return ResultadoOperacao.Invalido(validacao);
...
if (existente is not null && !requisicao.ReaproveitarAcessoExistente)
    return ResultadoOperacao.Erro(
        $"O e-mail {login} já tem acesso ao sistema ({existente.Pessoa?.Nome}). Marque \"usar o acesso que já existe\" " +
        "para torná-lo gestor desta escolinha também.", nameof(NovaEscolinhaRequisicao.GestorEmail));
```

**Controller** (`PlataformaController.Nova`, POST):

```csharp
var resultado = await plataforma.CriarEscolinhaComGestorAsync(requisicao, ct);
if (!resultado.Sucesso || resultado.Dados is not EscolinhaCriada criada)
{
    ModelState.AdicionarErro(resultado);
    return View(requisicao);
}
```

**View:** nada especial. `<partial name="_ErrosFormulario" />` e `<input class="kt-input" asp-for="GestorEmail" type="email" required autocomplete="off" />`.

**Resultado:** a tela volta na etapa "1º gestor", com tudo o que foi digitado, a borda vermelha no e-mail, o foco nele e a mensagem no topo.

**Sucesso** (em qualquer controller):

```csharp
TempData[ChavesTempData.Sucesso] = resultado.Mensagem;
return RedirectToAction(nameof(Index));
```

### 7.3 Casos especiais

- **Campo sem `asp-for`: a View marca `aria-invalid` ela mesma.** O caso real é a aba de parâmetros, cujos campos se chamam `valores[chave]`. O serviço devolve `Campo` com a primeira chave com erro e `Dados` com todas; o controller remonta a aba **com os valores digitados** e passa as chaves adiante:

```csharp
ModelState.AdicionarErro(resultado);
return View(nameof(Index), await MontarAsync(dominio, valores, resultado.Dados as IReadOnlyList<string>, ct));
```

```cshtml
@* _CampoParametro.cshtml: "true" ou null (null omite o atributo) *@
var invalido = (ViewData["ChavesComErro"] as IReadOnlyCollection<string>)?.Contains(d.Chave) == true ? "true" : null;
...
<select class="kt-select" id="@id" name="@nome" aria-invalid="@invalido" disabled="@somenteLeitura">
```

- **Erro sem campo** fica só no resumo. Se ele pertence a uma etapa do assistente, marque a `<section data-esc-passo data-esc-passo-erro>` para o assistente abrir nela.
- **Erro depois de redirect** (ação sem tela própria, como "Entrar como equipe"): `TempData[ChavesTempData.Erro]`, que vira toast vermelho.
- **Segredo não volta para a tela:** limpe senha e chave de API antes de devolver a View. Entrar zera a `Senha`; Trocar senha, as três senhas; Vincular conta, a `ChaveApi` e a `SenhaAtual`.
- **Dicionário ou coleção na action** declara o nome do binding: `[FromForm(Name = "valores")]` (D20, com teste de arquitetura). Sem isso, o salvar dos parâmetros falhava em quatro das cinco abas.
- **Campo embrulhado** (`<div class="kt-input">` com o `<input>` dentro, como a senha com o olho): o `aria-invalid` vai para o `<input>` interno, e o KTUI só pinta quem tem a classe `kt-input`. O `custom.css` cobre: `.kt-input:has(> input[aria-invalid="true"])` pinta a borda do embrulho com a mesma cor.
- **Trocar senha:** os três campos são montados por uma função local, sem `asp-for`; a função marca o `aria-invalid` ela mesma (`ViewData.ModelState[nome]?.Errors.Count > 0`).
- **Mensagem só em pt-BR:** o MVC não põe `[Required]` implícito em `string` não anulável (`SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`), porque quem valida é o validador da Application. As mensagens de conversão do model binding (texto num campo numérico) estão em pt-BR (`Infraestrutura/MensagensValidacao.cs`). Antes disso, o login com a senha vazia mostrava "The Senha field is required." junto com "Informe a senha.".

### 7.4 Limites conhecidos

- **Interruptores da modalidade:** sem `asp-for` e sem marcação (erro improvável nesses campos).
- O projeto não usa `kt-form-item` e `kt-form-message`, que mostram a mensagem embaixo do campo; a mensagem fica no resumo.

---

## 8. Formato pt-BR

A cultura da requisição é só pt-BR (`UseRequestLocalization`), e toda data é em horário de Brasília (`DataHoraBrasil`, `IRelogioBrasil`; nunca `DateTime.Now`). Na View, use os helpers de `Formatacao` (`Infraestrutura/Apresentacao.cs`):

| Helper | Entrada → saída |
|---|---|
| `Formatacao.Data(DateTime?)` e `Formatacao.Data(DateOnly?)` | `dd/MM/yyyy`; nulo → "—" |
| `Formatacao.DiaCurto(DateOnly)` | "01/10 (qui)" |
| `Formatacao.DiaPorExtenso(DateOnly)` | "Quinta-feira, 1 de outubro": só a primeira letra maiúscula (armadilha 27) |
| `Formatacao.Mes(DateOnly)` | "Outubro de 2026" |
| `Formatacao.Horario(inicio, fim)` | "18:00–19:00", para `DateTime` ou `TimeOnly` |
| `Formatacao.Cpf(digitos)` | 11 dígitos → "000.000.000-00" |
| `Formatacao.Idade(anos)` | "1 ano", "9 anos" |
| `Formatacao.DataHora(DateTime?)` | `dd/MM/yyyy HH:mm` (relógio de 24 h); nulo → "—" |
| `Formatacao.TempoRelativo(quando, agora)` | "agora", "há 5 min", "há 3 h", "ontem", "há 4 dias" e, a partir de 7 dias, `dd/MM/yyyy`. Os dois em horário de Brasília. Ainda sem uso em tela |
| `Formatacao.Telefone(e164)` | "+5543987654321" → "(43) 98765-4321"; com 10 dígitos, "(43) 3333-4444"; outro formato volta como veio |
| `Formatacao.Cnpj(digitos)` | 14 dígitos → "00.000.000/0000-00" |
| `Formatacao.Cep(digitos)` | "86010000" → "86010-000" |
| `Formatacao.Numero(int)` | separador de milhar: "1.234" |
| `Formatacao.Moeda(decimal)` | "R$ 1.234,50" |
| `Formatacao.Parametro(definicao, valor, seVazio)` | valor de parâmetro como o gestor lê (abaixo) |

`Formatacao.Parametro` recebe o valor gravado em texto invariável e devolve:
- Booleano: "ligado" ou "desligado";
- Escolha: o rótulo da opção;
- Janela: "21:00 às 07:00";
- ListaDias: "1,5,10" vira "1, 5, 10", mais a unidade: "1, 5, 10 dias depois";
- os demais: ponto vira vírgula, mais a unidade: "2,5 %", "1 % ao mês", "30 dias".

É o mesmo texto no editor e no histórico ("antes → depois").

**Valor em reais digitado** (mensalidade, D30): o campo é `type="text" inputmode="decimal"` dentro de um `kt-input-group` com o addon "R$", e mostra `Moeda.ParaCampo(valor)` ("1.500,00"). Em erro, volta com o que foi digitado (`ModelState[...].AttemptedValue`). Quem lê é o `DecimalBrasileiroModelBinder`, que aceita "150", "150,50", "1.500,00", "R$ 1.500,00" e "150.50". Nunca use `type="number"` para dinheiro.

---

## 9. Ícones

```html
<i class="ki-filled ki-calendar text-lg text-muted-foreground"></i>
```

- **Só `ki-filled`.** Tamanho e cor vêm de utilities (`text-lg`, `text-primary`, `text-muted-foreground`), nunca de CSS próprio.
- Ícone decorativo não precisa de rótulo; **botão só com ícone precisa de `aria-label`**.
- A família servida tem 565 ícones. Para conferir um nome: `grep -c "\.ki-cup\.ki-filled" src/Web/wwwroot/assets/vendors/keenicons/keenicons-filled.css` (zero = não existe).

### 9.1 Domínio (a lista validada do plano)

| Conceito | Ícone | Uso hoje |
|---|---|---|
| painel | `ki-element-11` | menu (Painel); ícone padrão da busca rápida |
| alunos | `ki-people` | menu (Alunos, em breve) |
| responsáveis | `ki-users` | só a vitrine |
| professores | `ki-teacher` | só a vitrine |
| turmas | `ki-category` | menu (Turmas, em breve) |
| agenda | `ki-calendar` | menu (Agenda, em breve); cartão de jogo da vitrine |
| chamada | `ki-check-squared` | menu (Chamada, em breve); "Todos presentes" na vitrine |
| jogos | `ki-cup` | menu (Jogos e convocações, em breve); aba Jogos do portal; **logo provisório** |
| convocação | `ki-flag` | só a vitrine |
| confirmações | `ki-double-check` | só a vitrine |
| mensagens | `ki-message-text-2` | menu (Mensagens, em breve) |
| WhatsApp | `ki-whatsapp` | só a vitrine |
| avisos | `ki-notification-status` | aba Avisos do portal |
| financeiro | `ki-dollar` | menu (Mensalidades, em breve); aba Pagamentos do portal |
| cobrança | `ki-bill` | só a vitrine |
| cartão | `ki-credit-cart` | só a vitrine. O nome é `credit-cart` mesmo |
| créditos | `ki-wallet` | menu (Créditos); Plataforma; Painel |
| configuração | `ki-setting-2` | só a vitrine |
| parâmetros | `ki-setting-3` | menu (Parâmetros); Painel |
| locais | `ki-geolocation` | só a vitrine |
| mapa | `ki-map` | só a vitrine |
| saúde | `ki-heart` | só a vitrine |
| consentimento | `ki-shield-tick` | só a vitrine |
| acesso | `ki-key` | menu da pessoa (Trocar senha) |
| permissões | `ki-security-user` | só a vitrine |
| vínculo verificado | `ki-user-tick` | só a vitrine |
| importação | `ki-file-up` | menu (Importação, em breve) |
| modelo para baixar | `ki-file-down` | só a vitrine |
| online (conexão) | `ki-wifi` | selo de conexão da quadra, com sinal |
| sem sinal / sincronizar | `ki-cloud-change` | selo de conexão da quadra, sem sinal (o `app.js` troca o ícone) |
| horário, histórico | `ki-time` | Parâmetros (botão e histórico vazio) |
| início do portal | `ki-home-2` | aba Início do portal; "Ir para o início" na página de erro |
| copiar (Pix) | `ki-copy` | copiar a senha temporária |
| risco | `ki-chart-line-down` | só a vitrine |

### 9.2 Estrutura (menu e cabeçalho), fora da lista do plano

| Conceito | Ícone | Uso hoje |
|---|---|---|
| dados da escolinha, trocar de escolinha | `ki-home-3` | menu, header, Escolher escolinha, Painel |
| modalidades | `ki-medal-star` | menu, Modalidades, Painel |
| conta de recebimento | `ki-bank` | menu, Conta de recebimento, Painel |
| plataforma, escolinhas | `ki-grid` | menu, header, Escolher escolinha, Plataforma |
| jobs (Hangfire) | `ki-timer` | menu (não aparece na vitrine) |
| pacotes de créditos | `ki-package` | menu, Pacotes de créditos, vitrine vazia de pacotes |
| comprar | `ki-basket` | botão "Comprar" dos pacotes |
| pagar com cartão; simulador do Stripe | `ki-credit-cart` | simulador (`/dev/stripe`) |
| boleto | `ki-bill` | simulador (`/dev/stripe`) |
| editar item da lista | `ki-notepad-edit` | Pacotes de créditos |
| pagamento confirmado | `ki-check-circle` | Compra de créditos |
| aguardando confirmação | `ki-time` | Compra de créditos |

### 9.3 Interface

| Ação | Ícone | | Ação | Ícone |
|---|---|---|---|---|
| voltar | `ki-arrow-left` | | continuar | `ki-arrow-right` |
| salvar, confirmar, passo feito | `ki-check` | | item de lista de recursos | `ki-check-circle` |
| novo | `ki-plus` | | editar | `ki-pencil` |
| excluir | `ki-trash` | | fechar; "não vai" | `ki-cross` |
| mais ações | `ki-dots-vertical` | | buscar | `ki-magnifier` |
| busca vazia; página não encontrada | `ki-search-list` | | seta de select | `ki-down` |
| página anterior | `ki-left` | | próxima página; abrir linha | `ki-right` |
| abrir menu (celular) | `ki-menu` | | recolher sidebar | `ki-black-left-line` |
| mostrar senha | `ki-eye` | | ocultar senha | `ki-eye-slash` |
| sair | `ki-exit-right` | | entrar como equipe | `ki-entrance-left` |
| modo escuro | `ki-moon` | | acesso negado | `ki-lock-2` |
| aviso, erro, estado vazio padrão | `ki-information-2` | | filtrar (Agenda) | `ki-filter` |
| encerrar turma | `ki-archive` | | cancelar sessão | `ki-cross-circle` |
| trocar o responsável escolhido | `ki-arrows-circle` | | revogar consentimento | `ki-shield-cross` |

### 9.4 Não existem (conferidos no CSS servido; não usar)

- **Da lista do plano:** `profile-user`, `send`, `whistle`, `football`, `soccer`, `ball`, `tshirt`, `warning-2`, `information-5`, `mobile`.
- **Nomes que parecem óbvios e também não existem:** `warning`, `alert`, `danger`, `trophy`, `smartphone`, `sync`, `refresh`, `wifi-off`, `cloud-off`, `money`, `pix`, `qr-code`, `check-double` (use `double-check`), `user-check` (use `user-tick`) e `check-square` (use `check-squared`).
- **Nenhuma outra família:** `ki-outline`, `ki-solid` e `ki-duotone` não são servidas.

---

## 10. Metronic e KTUI verificados

O `styles.css` servido tem 291 classes `kt-*` (mais 62 utilities com variante de estado, como `kt-menu-item-active:text-primary`). As listas abaixo são as que interessam, com as variantes **exaustivas**: variante que não está aqui não existe.

### 10.1 Classes `kt-*`

- **Botão:** `kt-btn` + `kt-btn-primary`, `kt-btn-secondary`, `kt-btn-outline`, `kt-btn-ghost`, `kt-btn-destructive`, `kt-btn-mono`, `kt-btn-dim`; tamanho e forma: `kt-btn-sm`, `kt-btn-lg`, `kt-btn-icon`. **Não existem** variantes de sucesso, alerta, info, tracejado ou claro, nem tamanho extra pequeno.
- **Cartão:** `kt-card`, `kt-card-header`, `kt-card-title`, `kt-card-heading`, `kt-card-description`, `kt-card-toolbar`, `kt-card-content`, `kt-card-table`, `kt-card-footer`, `kt-card-group`, `kt-card-grid`, `kt-card-border`, `kt-card-accent`, `kt-card-rounded-t`, `kt-card-rounded-b`.
- **Tabela:** `kt-table`, `kt-table-border` (e `kt-table-border-t`, `-b`, `-s`, `-e`), `kt-table-wrapper`, `kt-table-col`, `kt-table-col-label`, `kt-table-col-sort`, `kt-table-cell-center`, `kt-table-highlight`.
- **Formulário:** `kt-input` (e `kt-input-sm`, `kt-input-lg`, `kt-input-ghost`), `kt-textarea` (e `-sm`, `-lg`), `kt-select` (e `-sm`, `-lg`), `kt-input-group`, `kt-input-addon` (e `-sm`, `-lg`, `-icon`), `kt-form`, `kt-form-item`, `kt-form-label`, `kt-form-control`, `kt-form-control-inline`, `kt-form-description`, `kt-form-message`, `kt-form-actions`, `kt-checkbox` (e `-sm`, `-lg`, `-mono`), `kt-radio` (e `-sm`, `-lg`, `-mono`), `kt-switch` (e `-sm`, `-lg`), `kt-label`, `kt-label-secondary`.
- **Badge:** `kt-badge` + cor (`kt-badge-primary`, `-secondary`, `-success`, `-warning`, `-info`, `-destructive`, `-mono`, `-light`) + estilo (`kt-badge-outline`, `-ghost`, `-stroke`, `-dot`, `-btn`, `-disabled`) + tamanho (`kt-badge-xs`, `-sm`, `-lg`).
- **Alerta:** `kt-alert` + cor (`kt-alert-primary`, `-success`, `-warning`, `-info`, `-destructive`, `-mono`) + estilo (`kt-alert-light`, `-outline`) + tamanho (`kt-alert-sm`, `-lg`) + peças (`kt-alert-icon`, `-title`, `-content`, `-description`, `-close`, `-actions`, `-toolbar`).
- **Link:** `kt-link` (e `-sm`, `-lg`, `-mono`, `-inverse`, `-dashed`, `-underline`, `-underlined`, `-disabled`).
- **Avatar:** `kt-avatar`, `kt-avatar-image`, `kt-avatar-fallback`, `kt-avatar-indicator`, `kt-avatar-status` (e `-online`, `-away`, `-busy`, `-offline`). O projeto usa a inicial num `kt-avatar-fallback` (não há foto servida).
- **Outros:** `kt-kbd` (e `-xs`, `-sm`, `-outline`), `kt-progress` (e `-primary`, `-success`, `-warning`, `-info`, `-destructive`, `-mono`) com `kt-progress-indicator`, `kt-separator`, `kt-separator-vertical`, `kt-scrollable-x-auto`, `kt-scrollable-y-auto`, `kt-scrollable-y-hover`, `kt-container-fixed`, `kt-container-fluid`, `kt-skeleton`, e as peças de breadcrumb, paginação, rating, popover, tooltip, toast, menu, modal, drawer, dropdown, accordion, datatable, select e abas.

### 10.2 Componentes KTUI (sempre por atributo, nunca por classe)

| Componente | Atributo raiz | Peças |
|---|---|---|
| `KTModal` | `data-kt-modal="true"` | `data-kt-modal-toggle="#id"`, `data-kt-modal-dismiss` |
| `KTDropdown` | `data-kt-dropdown="true"` | `data-kt-dropdown-toggle`, `data-kt-dropdown-menu`, `-placement`, `-trigger`, `-offset` |
| `KTDrawer` | `data-kt-drawer="true"` | `data-kt-drawer-toggle="#id"`, `data-kt-drawer-dismiss`, `data-kt-drawer-class` |
| `KTTabs` | `data-kt-tabs="true"` | `data-kt-tab-toggle="#painel"` |
| `KTTooltip` | `data-kt-tooltip="true"` | `data-kt-tooltip-content`, `-placement`, `-trigger` |
| `KTTogglePassword` | `data-kt-toggle-password="true"` | `data-kt-toggle-password-trigger` |
| `KTToggle` | `data-kt-toggle="body"` | `data-kt-toggle-class` |
| `KTSticky` | `data-kt-sticky="true"` | `-name`, `-class` |
| `KTScrollable` | `data-kt-scrollable="true"` | `-dependencies`, `-height`, `-offset`, `-wrappers` |
| `KTThemeSwitch` | `data-kt-theme-switch-toggle` | `data-kt-theme-switch-state` |
| `KTAccordion`, `KTStepper`, `KTSelect`, `KTDataTable`, `KTDatepicker`, `KTImageInput`, `KTScrollspy`, `KTCollapse`, `KTDismiss`, `KTScrollto`, `KTReparent` | idem, por atributo | |

**Em uso hoje:** dropdown (menu da pessoa), modal (busca rápida), drawer (sidebar no celular), toggle (recolher a sidebar), sticky (header), scrollable (menu), mostrar/ocultar senha e a chave de tema. O projeto usa as próprias abas (`esc-abas`) e a própria seleção com busca (`data-esc-selecao-busca`) no lugar das do KTUI; listas são renderizadas no servidor com `_Paginacao`, sem o `KTDataTable`.

**As três pegadinhas:**

1. **Dropdown sem a classe no contêiner** (`_Header`). Com `class="kt-dropdown"` no contêiner, o menu aparece aberto na carga:

```cshtml
<div class="shrink-0 ms-1" data-kt-dropdown="true" data-kt-dropdown-offset="10px, 10px"
     data-kt-dropdown-placement="bottom-end" data-kt-dropdown-trigger="click">
    <button class="flex items-center gap-2.5 cursor-pointer" data-kt-dropdown-toggle="true" type="button" aria-label="Menu da pessoa">...</button>
    <div class="kt-dropdown-menu w-[270px]" data-kt-dropdown-menu="true">...</div>
</div>
```

2. **Modal no fim do `<body>`**, nunca dentro do `kt-container-fixed`: o `_Modais` e a seção `Modais` do `_Layout` existem para isso.

```cshtml
<div class="kt-modal" data-kt-modal="true" id="modal_busca">
    <div class="kt-modal-content max-w-[600px] top-[12%]">
        <div class="kt-modal-header py-4 px-5 gap-2"> ...
            <button class="kt-btn kt-btn-sm kt-btn-icon kt-btn-dim shrink-0" data-kt-modal-dismiss="true" type="button" aria-label="Fechar">
                <i class="ki-filled ki-cross"></i>
            </button>
        </div>
        <div class="kt-modal-body p-0 pb-3"> ... </div>
        <div class="kt-modal-footer py-3 px-5 justify-start gap-4 text-xs text-muted-foreground"> ... </div>
    </div>
</div>
```

3. **HTML injetado não se inicializa sozinho:** §6.13.

**Mostrar/ocultar senha** (Entrar):

```cshtml
<div class="kt-input" data-kt-toggle-password="true">
    <input asp-for="Senha" type="password" autocomplete="current-password" placeholder="Sua senha" />
    <button class="kt-btn kt-btn-sm kt-btn-ghost kt-btn-icon bg-transparent! -me-1.5" data-kt-toggle-password-trigger="true" type="button" aria-label="Mostrar ou ocultar a senha">
        <span class="kt-toggle-password-active:hidden"><i class="ki-filled ki-eye text-muted-foreground"></i></span>
        <span class="hidden kt-toggle-password-active:block"><i class="ki-filled ki-eye-slash text-muted-foreground"></i></span>
    </button>
</div>
```

O `kt-input` é um contêiner flex, e o `<input>` de dentro herda o estilo. A mesma forma serve para ícone dentro do campo. Veja o limite de erro em campo embrulhado na §7.4.

### 10.3 Onde copiar a forma (demo1) e o que reusar no projeto

| Quero fazer | Referência em `docs/metronic/html/demo1/` | Reuse no projeto |
|---|---|---|
| shell do painel | `index.html` | `_Layout` |
| entrar | `authentication/branded/sign-in.html` (só a estrutura) | `Conta/Login` |
| erro 403, 404, 500 | `authentication/error-404.html`, `error-500.html` | `Erro/Index`, `Conta/AcessoNegado` |
| tabela de dados | `network/user-table/saas-users.html` | `Plataforma/Index`, `Creditos/Index` |
| formulário simples | `account/home/settings-plain.html` | `Escolinha/Editar` |
| perfil de empresa | `account/home/company-profile.html` | `Escolinha/Index` |
| configuração por seções | `account/home/settings-sidebar.html` | `Parametros/Index` (com `esc-abas`) |
| integração com conta externa | `account/integrations.html` | `ContaRecebimento` |
| histórico de lançamentos | `account/billing/history.html` | `Creditos/Index` |
| linha do tempo e log | `account/activity.html` | `Parametros/Historico` |
| cartões de pessoa ou item | `network/user-cards/team-crew.html` | `Modalidades/Index` |
| ficha de pessoa (S1) | `public-profile/profiles/crm.html` | — |
| importação (S7) | `account/members/import-members.html` | — |
| estado vazio | — (a ilustração do demo não é servida) | `<estado-vazio>` |
| formulário em etapas | — | `Plataforma/Nova` (§6.6) |

---

## 11. Acessibilidade, responsividade e modo escuro

**Acessibilidade**
- Botão só com ícone tem `aria-label` (voltar, fechar, abrir menu, recolher menu, buscar, mostrar senha, menu da pessoa).
- O selo de conexão tem `role="status"` e `aria-live="polite"`; o resumo de erros, `role="alert"`; os toasts do KTUI já saem com `role="status"`.
- Campo com erro tem `aria-invalid="true"` (§7). Grupo de escolha tem `role="group"` e `aria-label` (P/F de cada aluno, chips dos filhos).
- **Foco visível:** `kt-input`, `kt-select` e `kt-textarea` ganham borda da marca e anel `--esc-foco`; `a.kt-btn` ganha contorno de 2 px da marca no `:focus-visible`. Não remova `outline` sem repor.
- O `custom.css` corrige o `:visited` dos botões que são `<a>` (sem isso, o rótulo sumia sobre o fundo).
- Contraste da marca: §3.3.

**Responsividade**
- A virada do shell é em `lg` (1024 px): abaixo dela, a sidebar vira drawer.
- **A página nunca rola na horizontal.** Os wrappers do `_Layout` levam `min-w-0` (não remova); tabela larga fica em `kt-card-table kt-scrollable-x-auto`; coluna secundária usa `hidden md:table-cell`; texto longo em linha flex leva `min-w-0` e `truncate`.
- Abaixo de 640 px: `esc-dados` vira uma coluna, `esc-abas` rola na horizontal, e a seleção com busca abre maior.
- Quadra e portal: largura útil de até 40rem, safe-area respeitada (`viewport-fit=cover` + `env(safe-area-inset-*)`), altura mínima `100dvh`.
- Texto de campo com 16 px só na quadra e no portal (`esc-toque`); no painel, o `kt-input` tem 13 px.

**Modo escuro:** §3.4. Todo componente `esc-*` usa token ou par claro/escuro; confira toda tela nos dois temas.

---

## 12. Checklist de aceite de tela

**Build e código**
- [ ] `npm run build` terminou com `verificar-classes: 0 classes faltando.` e o resultado entrou na entrega.
- [ ] `dotnet build` sem aviso.
- [ ] Nenhum `style=`, `<style>`, `<script>` inline ou `on*=` na View.
- [ ] Toda classe existe no CSS servido; nenhuma do anti-catálogo (§13).
- [ ] Ícones só `ki-filled`, da lista da §9.
- [ ] Nenhum emoji.

**Cor e texto**
- [ ] Cor só por token; nenhum hex; cor fixa só em selo.
- [ ] Status por `<selo-status>`; máquina nova mapeada inteira no `ApresentacaoStatus` e na vitrine.
- [ ] Formato pt-BR pelos helpers de `Formatacao`: `dd/MM/aaaa`, relógio de 24 h, `R$ 0,00`.
- [ ] Botão só com ícone tem `aria-label`; todo `<button>` tem `type`.

**Estrutura**
- [ ] `kt-container-fixed` + `<cabecalho-pagina ...></cabecalho-pagina>` fechado.
- [ ] Modal no fim do `<body>` (seção `Modais`).
- [ ] Depois de HTML injetado: `esc.reinicializarComponentes()` e o `init()` do componente KTUI injetado (§6.13).
- [ ] IDs do shell, `data-kt-*` e `data-esc-*` preservados.

**Estados**
- [ ] Vazio (`<estado-vazio>`), carregando, erro e sem sinal tratados.
- [ ] Erro volta com o que foi digitado, o campo marcado e o assistente na etapa certa; sucesso é PRG + toast (§7).
- [ ] Dado de saúde nunca é renderizado para quem não pode vê-lo: a checagem é no view model, não no CSS.

**No navegador (1280 px, 375 px e 320 px; claro e escuro)**
- [ ] Sem rolagem horizontal da página: `document.documentElement.scrollWidth <= document.documentElement.clientWidth`.
- [ ] **Essa checagem não pega elemento `position: fixed`** (barra de ação, abas do portal, toasts). Confira que nenhum elemento passa da largura da tela, colando no console:

```js
[...document.querySelectorAll("body *")].filter(function (el) {
  var r = el.getBoundingClientRect();
  if (!r.width || (r.right <= innerWidth + 0.5 && r.left >= -0.5)) return false;
  for (var p = el.parentElement; p && p !== document.body; p = p.parentElement)
    if (getComputedStyle(p).overflowX !== "visible") return false; // cortado por um contêiner que rola
  return true;
});
```

- [ ] Item flex dentro de barra fixa tem `min-width: 0`. Foi o defeito da barra da quadra em 375 px, corrigido no `custom.css` (`.esc-barra-acao > .kt-btn`).
- [ ] Modo escuro: `localStorage.setItem("kt-theme", "dark"); location.reload();`, e depois `"light"`.
- [ ] Quadra e portal: alvos de 48 px, campo com texto de 16 px, ação principal na barra inferior, nada escondido atrás da barra ou das abas.
- [ ] Aba Network sem 404 e sem requisição para outro domínio; console sem erro de CSP.

**Documentação**
- [ ] Componente ou máquina nova na vitrine (`/dev/design`) e neste guia; tela nova no [`MAPA-DE-TELAS.md`](MAPA-DE-TELAS.md).

---

## 13. Armadilhas

Herdadas do guia-base e confirmadas neste CSS:

1. **`class="link"`** e **`kt-checkbox-label`**: o demo usa, mas não existem. Use `kt-link` e um `<span>` com utilities.
2. **`kt-toolbar`, `kt-footer`, `kt-tab`, `kt-card-body`**: aparecem em markup de exemplo, sem regra CSS. Componha com utilities e com as peças da §10.1.
3. **Variante inventada:** botão de sucesso, alerta ou info não existe (`kt-btn` só tem as variantes da §10.1), e alerta "danger" também não (é `kt-alert-destructive`).
4. **Mídia do demo, ApexCharts e Leaflet** não são servidos.
5. **`w-[--var]`** é sintaxe do Tailwind 3. Use `w-(--var)`.
6. **Valor literal em `--color-*`** congela o modo escuro (§3.1).
7. **Combinação de variante `kt-*` não registrada** não gera CSS, sem erro. Registre no `esc.src.css` (§2.3).
8. **TagHelper auto-fechado** engolia o conteúdo seguinte. Sempre feche `<cabecalho-pagina>` e `<estado-vazio>` (§6.1).
9. **Página rolando na horizontal por causa de tabela larga:** os `min-w-0` dos wrappers do `_Layout` são o que segura. Nunca os remova.
10. **`class="kt-dropdown"` no contêiner** do dropdown deixa o menu aberto na carga (§10.2).
11. **Modal dentro do `kt-container-fixed`** fica com o fundo recortado.

Achadas neste projeto:

12. **Elemento `position: fixed` é invisível à checagem de rolagem:** a barra da quadra vazava em 375 px e o `scrollWidth` não denunciava. Item flex em barra fixa leva `min-width: 0` (§12).
13. **Utility do Tailwind escrita em C# fora de `Infraestrutura/`** não é gerada nem conferida (§2.3 e §2.4).
14. **Classe montada em variável no JS** não passa pelo verificador; o Tailwind a gera se o texto literal estiver no arquivo, mas confira no navegador.
15. **`kt-badge-outline kt-badge-primary` usa a paleta azul, não `--primary`** (§3.3).
16. **O "info" do Metronic é violeta** (`kt-badge-info`, `kt-alert-info`).
17. **O KTToast insere a mensagem como HTML:** use sempre o `esc.toast`, que escapa (§6.4). As variantes `error` e `secondary` do KTToast geram classes que o CSS não tem; use `destructive` e `mono`.
18. **`esc.reinicializarComponentes()` só reinicia o `KTMenu`** e a seleção com busca (§6.13).
19. **O bloqueio de duplo envio só reconhece `button[type=submit]` com o `type` escrito** (§6.11). Formulário cancelado ou enviado por AJAX mantém os botões ativos.
20. **`assistente.js` e `selecao-busca.js` só carregam no `_Layout`**, e o `_LayoutVitrine` não renderiza a seção `Scripts` (§2.2 e §5.5).
21. **Campo embrulhado em `div.kt-input`:** o KTUI não pinta a borda de erro; quem pinta é a regra `:has` do `custom.css` (§7.3). Não remova essa regra.
22. **Redirect depois de erro apaga o que foi digitado.** Devolva a View (§7).
23. **Dicionário na action sem nome de binding** recebe o formulário inteiro (D20).
24. **O tipo de parâmetro `Hora` não tem editor** (§6.16).
25. **Prefixo `me-`** colide com o margin-end do Tailwind. O prefixo do projeto é `esc-`.

Achadas no S1:

26. **O ASP.NET põe um oculto `__Invariant` nos campos de data e hora**, com o nome do campo no valor. Ao clonar ou renumerar linhas, renumere também esse valor; senão o binding deixa de ler aquele campo no formato invariável do navegador (§6.19).
27. **`capitalize` (CSS) põe maiúscula em toda palavra:** "Quinta-Feira, 1 De Outubro". Use os helpers de `Formatacao`, que só sobem a primeira letra.
28. **O binder padrão de `decimal` em pt-BR lê "150.50" como 15.050.** O `DecimalBrasileiroModelBinder` já está registrado para `decimal` e `decimal?` (D30).
29. **O assistente sem Continuar e sem envio final escondia as etapas** e deixava o formulário sem saída; hoje o JS não liga (D42).
30. **Dado que vem junto também passa pelo escopo:** irmão de outra turma, outros filhos do responsável, pagador e valores (D41). Filtrar só a lista principal não basta.

---

## 14. Diferenças entre o plano e o construído

Comparação entre a seção 4 do plano aprovado (4.1 a 4.8) e o código. Feita na fatia S0 (26/09/2026) e revista na S1 (27/09/2026).

### 14.1 Previsto no plano e diferente, parcial ou ainda não construído

| # | Plano | Construído | Situação |
|---|---|---|---|
| 1 | Arquivos estáticos por `MapStaticAssets`, com Brotli/gzip e fingerprint (4.1) | `UseStaticFiles` + `asp-append-version` | Decisão D18; reavaliar no S3 |
| 2 | KeenIcons filled: "cerca de 2 MB a menos no total e uns 400 KB a menos por carga" (4.1) | medido: o CSS caiu de 232 KB para 82 KB por carga; a fonte continua com 431 KB (woff e ttf do mesmo tamanho); ficaram fora do git cerca de 1,5 MB de woff/ttf das outras famílias e cerca de 4,8 MB de `.svg` | Corrigido neste guia (§2.5) |
| 3 | JS nesta ordem: tema, core, ktui, demo1 (só no painel), `app.js`, arquivo por tela (4.1) | core, ktui, `app.js` em todos; só no painel, depois: demo1, `selecao-busca.js`, `assistente.js`; por fim a seção `Scripts` | Diferença de ordem (sem efeito conhecido); os dois scripts de formulário só existem no painel |
| 4 | Chaves de `localStorage` `esc.*` (4.1) | nenhuma; o tema usa a chave do KTUI, `kt-theme` | Não há estado de navegador próprio ainda |
| 5 | Tudo sob `window.esc` (4.1) | exceção: `window.escSelecaoBusca` | Global fora do prefixo |
| 6 | `_LayoutAutenticacao` com base no `authentication/branded`, "cor + formas em CSS" (4.3) | cartão central sobre `bg-muted/40` com o logo; sem painel de marca e sem formas | Mais simples que o previsto |
| 7 | Header do painel com "sino" (4.3) | não existe | Planejado (S2) |
| 8 | Quadra: "a chamada funciona offline" (4.3) | só a CSP pronta (`worker-src`); o selo de conexão lê `data-pendentes`, mas a função que o atualiza não é pública | Planejado (S3) |
| 9 | Portal: alvos de 48 px (4.3) | botões Sim/Não com 48 px (`esc-resposta`), abas com 64 px; **chips dos filhos com 36 px** | Chips abaixo do mínimo |
| 10 | Portal em `Areas/Portal` (3.1, 4.3) | `_LayoutPortal` em `Views/Shared`; as abas apontam para `#`; a área não existe | Planejado (S6); o build já varre `Areas/` |
| 11 | Componentes do DP: ficha lateral única (drawer com versão e resposta 409) (4.4) | **não adotada no S1:** a ficha é página com abas, com URL própria; a versão de linha vai num campo do formulário, e o conflito vira mensagem | Decisão D37 |
| 12 | Menu de ações ⋮ (4.4) | não construído; a vitrine mostra só o botão com `ki-dots-vertical`. No S1, as ações ficam no cabeçalho da ficha | Planejado |
| 13 | Modal de confirmação com motivo (4.4) | a confirmação é `window.confirm` (`data-esc-confirmar`). No S1, desligar, encerrar e cancelar são páginas próprias, com o motivo no formulário e os efeitos explicados | Planejado |
| 14 | Seleção com busca (com painel maior no celular) e select dependente (4.4) | construídos no JS e no CSS; nenhuma tela usa, e a vitrine não mostra | Prontos, sem uso |
| 15 | `_CampoParametro` (4.4) | construído em `Views/Parametros/`, não em `Shared` | Pronto; o tipo `Hora` não tem editor |
| 16 | Quadra: `_StatusConexao`, `_LinhaChamada`, `_BarraAcaoInferior` (4.4) | não são parciais: selo no `_LayoutQuadra`, classes `esc-linha-chamada*`, seção `BarraAcao` | Forma diferente, mesma função |
| 17 | Convocação: `_CartaoJogo`, `_SelecaoConvocados`, `_PainelConfirmacoes` (4.4) | não existem; a vitrine do portal mostra a forma do cartão de jogo | Planejado (S5) |
| 18 | Portal: `_CartaoFilho`, `_CartaoConvocacao`, `_CartaoCobranca`, `_AbasPortal` (4.4) | não são parciais: abas no layout; os cartões só como markup na vitrine | Planejado (S6) |
| 19 | Vitrine com "os selos de todas as máquinas" e "as 4 superfícies" (4.4) | selos de todos os enums do mapa central (S0, EX-01 e S1, por `Enum.GetValues`); quadra, portal e autenticação em páginas próprias; o painel aparece só como miolo (`_LayoutVitrine`, sem sidebar e header, porque a vitrine não exige login); o ícone `ki-timer` não está na vitrine | Parcial, por construção |
| 20 | Nova escolinha em etapas: dados → modalidade → 1º gestor → **revisão** (4.5) | três etapas, sem revisão | Sem a etapa de revisão |
| 21 | Mapa de status com a coluna "secondary / mono" (4.2) | `VarianteSelo` tem `Neutro` (`kt-badge-secondary`) e **`Primario`**, que o plano não previa (usado no papel Gestor); não tem Mono | Diferença de variantes |
| 22 | "Cor de status nunca é escolhida na View" (4.2) | cumprido: os três estados sem enum de domínio ("Conta vinculada"/"Sem conta", "Vinculada", "abaixo de N") passam pelo `SinalTela` do mapa central | Resolvido em 26/09/2026 |
| 23 | Ícones: "sem sinal → `wifi`; sincronizar → `cloud-change`" (4.7) | o selo usa `ki-wifi` com sinal e troca para `ki-cloud-change` sem sinal | Diferença de uso |
| 24 | Ícone "configuração → `setting-2`" (4.7) | nenhum item de menu usa; a seção Escolinha usa `home-3`, `medal-star`, `setting-3`, `bank`, `wallet` e `file-up` | `setting-2` só na vitrine |
| 25 | Resposta do responsável "Sim/Não" (4.3) | os rótulos da vitrine são "Vai" e "Não vai" | Texto a confirmar no S6 |
| 26 | `reinicializarComponentes()` depois de todo HTML injetado (4.1, 4.6) | a função existe, mas só reinicia o `KTMenu` e a seleção com busca; os componentes do KTUI precisam do próprio `init()` | Estender antes da primeira tela com AJAX |

### 14.2 Construído e não citado no plano

| Item | Onde |
|---|---|
| Padrão de erro ligado ao campo: `ResultadoOperacao.Campo`, `ModelState.AdicionarErro`, `CampoInvalidoTagHelper` e a abertura do assistente na etapa do erro (D19, X06) | §7 |
| Helpers `Formatacao` (datas, telefone, CNPJ, CEP, número, parâmetro) | §8 |
| `esc-tela`, a pele do painel (fundo suave, cartões com fio leve, selos suaves) | §5.2 |
| `esc-dados`, `esc-linhas`, `esc-passo-config*` e `esc-senha-temporaria` | §6.17 |
| `_Paginacao` + `PaginacaoViewModel` | §6.15 |
| Busca rápida (Ctrl+K e `/`) a partir do `CatalogoModulos` | §6.14 |
| `_LayoutVitrine` (quinto layout, só da vitrine) | §5.5 |
| `data-esc-enviar-ao-mudar` (envia o formulário ao mudar um filtro), ainda sem uso | `app.js` |
| Evento `esc:escolha` do controle segmentado e dos chips | §6.9 |
| `esc.fetch` tratando 401 (toast e volta ao login com `returnUrl`) e o servidor respondendo JSON a quem manda `X-Requested-With` | §6.13 |
| Tokens `--esc-foco`, `--esc-toque` e `--esc-barra-acao`; o anel de foco da marca; a correção do `:visited` dos botões-link | §3.2 e §11 |
| Ponte `--color-success`, `--color-warning` e `--color-info` no `esc.src.css` | §2.3 |
| Ícones de estrutura (`home-3`, `medal-star`, `bank`, `grid`, `timer`) e de interface | §9.2 e §9.3 |
| Classes `esc-amostra-cor` e `esc-moldura-celular` (vitrine) | §6.18 |
| Linhas repetíveis (`linhas.js`, `esc-linha-horario`) | §6.19 |
| Busca de responsável (`busca-responsavel.js`) e a parcial `_ResponsavelDoAluno`, com a sugestão de cadastro parecido | §6.20 |
| `_CamposEndereco` | §6.21 |
| `progress.esc-ocupacao`, `esc-item-agenda`, `esc-passo-cabecalho` e filtros em `<details>` | §6.22 a §6.24 |
| Helpers `Formatacao` de dia, mês, horário, CPF e idade; `DecimalBrasileiroModelBinder` | §8 |

### 14.3 Achados técnicos da revisão do guia

**Corrigidos em 26/09/2026, antes do 1º commit:**

| Achado | Correção |
|---|---|
| O KTToast insere a mensagem como HTML, e as mensagens levam nomes digitados | `esc.toast` escapa sempre (§6.4) |
| Campo em `div.kt-input` sem borda de erro; Trocar senha sem `aria-invalid` | Regra `:has` no `custom.css` e marcação na função local (§7.3) |
| O bloqueio de duplo envio travava formulário cancelado ou enviado por AJAX | Checagem depois dos ouvintes, reabilitação ao voltar pelo histórico (§6.11) |
| Três selos de estado escritos na View | `SinalTela` no mapa central (§4.4) |
| "The Senha field is required." em inglês, junto com a mensagem do validador | `[Required]` implícito desligado e mensagens do model binding em pt-BR (§7.3) |

**Em aberto:**
- O `data-kt-modal-input-focus` do `_Modais` está na raiz do modal, onde o KTUI não o procura; o foco da busca vem do `app.js` (§6.14).
- O contraste do azul provisório (4,2:1) não atende o AA em texto pequeno (§3.3). Resolver quando chegar a identidade visual.
- O menu usa `Pode(recurso, Ler)`: pela matriz atual, uma pessoa só com o papel Responsável veria no painel os módulos "em breve" de Turmas, Alunos, Agenda e Chamada. Ninguém tem esse papel ainda: o 1º vínculo de responsável nasce com o convite, no S2. Decidir no S2 se o painel manda o responsável para o portal (P23).
