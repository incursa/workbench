import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
const workerModuleUrl = pathToFileURL(path.join(rootDir, "dist", "mcp", "worker.mjs")).href;

export async function loadWorker() {
  return import(workerModuleUrl);
}

export async function callWorker(pathname, { method = "GET", headers = {}, body, env } = {}) {
  const worker = await loadWorker();
  return worker.fetch(
    new Request(`https://example.test${pathname}`, {
      method,
      headers,
      body,
    }),
    env,
  );
}

export async function callJsonRpc(method, params = {}, { pathname = "/mcp", env } = {}) {
  const response = await callWorker(pathname, {
    method: "POST",
    headers: {
      accept: "application/json, text/event-stream",
      "content-type": "application/json",
    },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 1,
      method,
      params,
    }),
    env,
  });

  const text = await response.text();
  return JSON.parse(text);
}
