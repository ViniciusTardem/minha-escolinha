// Copia as bibliotecas de navegador de node_modules para wwwroot/lib (sem CDN: CSP script-src 'self').
// O resultado é versionado: o servidor não roda npm.
import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");

const bibliotecas = [
  { origem: ["@microsoft", "signalr", "dist", "browser", "signalr.min.js"], destino: ["signalr", "signalr.min.js"] }
];

for (const b of bibliotecas) {
  const destino = join(raiz, "wwwroot", "lib", ...b.destino);
  mkdirSync(dirname(destino), { recursive: true });
  copyFileSync(join(raiz, "node_modules", ...b.origem), destino);
  console.log(`biblioteca: ${b.destino.join("/")}`);
}
