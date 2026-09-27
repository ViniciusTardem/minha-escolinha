// Copia a fonte Inter (variável, latin + latin-ext) de node_modules para wwwroot/fonts/inter.
// O resultado é versionado: o servidor não roda npm.
import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const raiz = join(dirname(fileURLToPath(import.meta.url)), "..");
const origem = join(raiz, "node_modules", "@fontsource-variable", "inter", "files");
const destino = join(raiz, "wwwroot", "fonts", "inter");

const arquivos = ["inter-latin-wght-normal.woff2", "inter-latin-ext-wght-normal.woff2"];

mkdirSync(destino, { recursive: true });
for (const arquivo of arquivos) {
  copyFileSync(join(origem, arquivo), join(destino, arquivo));
  console.log(`fonte: ${arquivo}`);
}
