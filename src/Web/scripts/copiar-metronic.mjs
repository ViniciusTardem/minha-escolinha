// Copia do framework de referência (docs/metronic, fora do git) só o que o app serve, para wwwroot/assets (versionado).
// - styles.css, core.bundle.js, layouts/demo1.js e ktui.min.js: byte a byte (intocáveis).
// - KeenIcons: só a família "filled" (a única usada): o CSS dos ícones cai de 232 KB para 82 KB por carga (conta no
//   celular da quadra) e as outras três famílias (~2 MB de fontes) nem são copiadas. O woff vai antes do ttf no @font-face.
// Sem docs/metronic (máquina que não tem a referência), o script avisa e não mexe no que já está versionado.
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const web = join(dirname(fileURLToPath(import.meta.url)), "..");
const origem = join(web, "..", "..", "docs", "metronic", "assets");
const destino = join(web, "wwwroot", "assets");

if (!existsSync(origem)) {
  console.warn(`metronic: referência não encontrada em ${origem} — mantendo o recorte já versionado em wwwroot/assets.`);
  process.exit(0);
}

const copiar = (relativo) => {
  const alvo = join(destino, relativo);
  mkdirSync(dirname(alvo), { recursive: true });
  copyFileSync(join(origem, relativo), alvo);
  console.log(`metronic: ${relativo}`);
};

copiar(join("css", "styles.css"));
copiar(join("js", "core.bundle.js"));
copiar(join("js", "layouts", "demo1.js"));
copiar(join("vendors", "ktui", "ktui.min.js"));

// KeenIcons: o bundle tem quatro blocos em sequência (duotone, filled, outline, solid), cada um começando no @font-face.
const css = readFileSync(join(origem, "vendors", "keenicons", "styles.bundle.css"), "utf8");
const inicio = css.lastIndexOf("@font-face", css.indexOf("'keenicons-filled'"));
const fim = css.lastIndexOf("@font-face", css.indexOf("'keenicons-outline'"));
if (inicio < 0 || fim <= inicio) {
  console.error("metronic: não encontrei o bloco ki-filled no styles.bundle.css do KeenIcons.");
  process.exit(1);
}
let filled = css.slice(inicio, fim);
filled = filled.replace(
  /src:\s*url\('fonts\/keenicons-filled\.ttf\?(\w+)'\) format\('truetype'\),\s*url\('fonts\/keenicons-filled\.woff\?\w+'\) format\('woff'\),\s*url\('[^']+'\) format\('svg'\);/,
  (_, versao) => `src:\n    url('fonts/keenicons-filled.woff?${versao}') format('woff'),\n    url('fonts/keenicons-filled.ttf?${versao}') format('truetype');`
);
if (!filled.includes("keenicons-filled.woff")) {
  console.error("metronic: o @font-face do ki-filled mudou de formato — revise o script.");
  process.exit(1);
}
const pastaIcones = join(destino, "vendors", "keenicons");
mkdirSync(join(pastaIcones, "fonts"), { recursive: true });
writeFileSync(
  join(pastaIcones, "keenicons-filled.css"),
  "/* KeenIcons - somente a familia filled (gerado por scripts/copiar-metronic.mjs a partir de docs/metronic). */\n" + filled
);
console.log("metronic: vendors/keenicons/keenicons-filled.css");
copiar(join("vendors", "keenicons", "fonts", "keenicons-filled.woff"));
copiar(join("vendors", "keenicons", "fonts", "keenicons-filled.ttf"));
