import path from "node:path";
import { fileURLToPath } from "node:url";
import esbuild from "esbuild";

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(scriptDir, "..");
const outDir = path.join(rootDir, "dist", "mcp");

await import("./generate-mcp.mjs");

await esbuild.build({
  entryPoints: [path.join(rootDir, "src", "mcp", "worker.ts")],
  bundle: true,
  format: "esm",
  platform: "browser",
  target: ["es2022"],
  sourcemap: true,
  outfile: path.join(outDir, "worker.mjs"),
  logLevel: "info",
});
