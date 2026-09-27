// Lista as classes usadas nas Views, no JS e nos textos de classe do C# que NÃO existem em nenhum CSS carregado pelo layout.
// Precisa voltar vazio (exit 0). Rodar depois de `npm run css`: `npm run verificar-classes`.
//
// Por quê: o styles.css do Metronic é pré-compilado — classe fora dele simplesmente não funciona, sem erro nenhum.
// O esc.css (build complementar) cobre as utilities do Tailwind; o que sobrar aqui é erro de digitação ou classe
// inventada (ex.: kt-btn-success, kt-alert-danger) ou ícone que não existe no KeenIcons.
// Portado do AlternativaDepartamentoPessoal; acrescenta o C# de Infraestrutura (TagHelpers, mapa de selos).
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");

const cssCarregados = [
  "wwwroot/assets/vendors/keenicons/keenicons-filled.css",
  "wwwroot/assets/css/styles.css",
  "wwwroot/css/esc.css",
  "wwwroot/assets/css/custom.css",
];

// Classes que são só gancho de comportamento (sem regra CSS própria).
const permitidasSemCss = new Set(["esc-assistente-ativo"]);

function arquivos(dir, extensao) {
  const saida = [];
  if (!existsSync(dir)) return saida;
  for (const nome of readdirSync(dir)) {
    const caminho = join(dir, nome);
    if (statSync(caminho).isDirectory()) saida.push(...arquivos(caminho, extensao));
    else if (caminho.endsWith(extensao)) saida.push(caminho);
  }
  return saida;
}

function desescapar(nome) {
  return nome
    .replace(/\\([0-9a-fA-F]{1,6}) ?/g, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/\\(.)/g, "$1");
}

function classesDoCss() {
  const conhecidas = new Set();
  const seletor = /\.((?:\\[0-9a-fA-F]{1,6} ?|\\.|[\w-])+)/g;
  for (const arquivo of cssCarregados) {
    const css = readFileSync(join(raiz, arquivo), "utf8");
    for (const m of css.matchAll(seletor)) conhecidas.add(desescapar(m[1]));
  }
  return conhecidas;
}

// Lê o valor de class="..." de uma view Razor: texto literal fora de expressões
// + literais de string dentro de @(...) (ex.: @(ativo ? "kt-badge-success" : "kt-badge-destructive")).
function classesDaView(texto) {
  const tokens = [];
  const inicio = /\bclass\s*=\s*"/g;
  let m;
  while ((m = inicio.exec(texto))) {
    let i = m.index + m[0].length;
    let literal = "";
    while (i < texto.length && texto[i] !== '"') {
      if (texto[i] === "@" && texto[i + 1] === "(") {
        tokens.push(literal);
        literal = "";
        let profundidade = 0;
        i++;
        do {
          const c = texto[i];
          if (c === "(") profundidade++;
          else if (c === ")") profundidade--;
          else if (c === '"') {
            const fim = texto.indexOf('"', i + 1);
            tokens.push(texto.slice(i + 1, fim));
            i = fim;
          }
          i++;
        } while (profundidade > 0 && i < texto.length);
        continue;
      }
      if (texto[i] === "@") {
        // @variavel / @Model.Prop: expressão simples, não é classe
        tokens.push(literal);
        literal = "";
        i++;
        while (i < texto.length && /[\w.]/.test(texto[i])) i++;
        // chamada de método (@Metodo(args)): pula os argumentos
        if (texto[i] === "(") {
          let profundidade = 0;
          do {
            if (texto[i] === "(") profundidade++;
            else if (texto[i] === ")") profundidade--;
            i++;
          } while (profundidade > 0 && i < texto.length);
        }
        continue;
      }
      literal += texto[i++];
    }
    tokens.push(literal);
    inicio.lastIndex = i + 1;
  }
  return tokens.flatMap((t) => t.split(/\s+/)).filter(Boolean);
}

function classesDoJs(texto) {
  const tokens = [];
  for (const m of texto.matchAll(/classList\.(?:add|remove|toggle|contains|replace)\(([^)]*)\)/g))
    for (const s of m[1].matchAll(/["'`]([^"'`]+)["'`]/g)) tokens.push(...s[1].split(/\s+/));
  for (const m of texto.matchAll(/className\s*=\s*["'`]([^"'`]+)["'`]/g)) tokens.push(...m[1].split(/\s+/));
  return tokens.filter(Boolean);
}

// C# (TagHelpers, mapa de selos, catálogo de módulos): tokens kt-*, esc-* e ki-* dentro de literais de string.
function classesDoCsharp(texto) {
  const tokens = [];
  for (const m of texto.matchAll(/"([^"\\\n]*)"/g))
    for (const t of m[1].split(/\s+/)) if (/^(kt|esc|ki)-[a-z0-9-]+$/.test(t)) tokens.push(t);
  return tokens;
}

const conhecidas = classesDoCss();
const faltando = [];

const pastasCsharp = ["Controllers", "Infraestrutura", "Models"].map((p) => join(raiz, p));
const fontes = [
  ...arquivos(join(raiz, "Views"), ".cshtml").map((a) => [a, classesDaView]),
  ...arquivos(join(raiz, "Areas"), ".cshtml").map((a) => [a, classesDaView]),
  ...arquivos(join(raiz, "wwwroot", "js"), ".js").map((a) => [a, classesDoJs]),
  ...pastasCsharp.flatMap((p) => arquivos(p, ".cs")).map((a) => [a, classesDoCsharp]),
];

for (const [arquivo, extrair] of fontes) {
  const usadas = new Set(extrair(readFileSync(arquivo, "utf8")));
  for (const classe of usadas) {
    if (!/^[!\w:\-[\]/.%#()',*&>~+=]+$/.test(classe)) continue; // sobra de expressão Razor
    if (conhecidas.has(classe) || permitidasSemCss.has(classe)) continue;
    faltando.push(`${relative(raiz, arquivo)}: ${classe}`);
  }
}

if (faltando.length) {
  console.error(`Classes sem CSS (${faltando.length}):`);
  for (const f of faltando) console.error(`  ${f}`);
  process.exit(1);
}
console.log("verificar-classes: 0 classes faltando.");
