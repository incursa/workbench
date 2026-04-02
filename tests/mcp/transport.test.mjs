import assert from "node:assert/strict";
import test from "node:test";
import { callJsonRpc, callWorker } from "./_helpers.mjs";

test("GET /mcp renders the markdown-first docs index", async () => {
  const response = await callWorker("/mcp");
  assert.equal(response.status, 200);

  const html = await response.text();
  assert.match(html, /Workbench Docs MCP/i);
  assert.match(html, /search_docs/i);
  assert.match(html, /content\//i);
  assert.match(html, /workbench:\/\/file\/\{path\}/i);
  assert.match(html, /\/workbench\/mcp\/resource\//i);
});

test("POST /workbench/mcp initializes stable MCP server metadata", async () => {
  const response = await callJsonRpc("initialize", {
    protocolVersion: "2024-11-05",
    clientInfo: { name: "test-client", version: "1.0.0" },
    capabilities: {},
  }, {
    pathname: "/workbench/mcp",
  });

  assert.equal(response.jsonrpc, "2.0");
  assert.equal(response.id, 1);
  assert.equal(response.result.serverInfo.name, "workbench-docs-mcp");
  assert.equal(response.result.serverInfo.version, "0.1.0");
});

test("resources/list exposes markdown resources and a file template", async () => {
  const resources = await callJsonRpc("resources/list", {});
  const templates = await callJsonRpc("resources/templates/list", {});

  const uris = resources.result.resources.map((resource) => resource.uri);
  assert.ok(uris.includes("workbench://overview"));
  assert.ok(uris.includes("workbench://install"));
  assert.ok(uris.includes("workbench://guides/search"));
  assert.ok(uris.includes("workbench://reference/layout"));
  assert.ok(uris.includes("workbench://specs/public-surface"));
  assert.equal(templates.result.resourceTemplates.length, 1);
  assert.equal(templates.result.resourceTemplates[0].uriTemplate, "workbench://file/{path}");
});

test("resources/read returns canonical markdown and file-template content", async () => {
  const canonical = await callJsonRpc("resources/read", {
    uri: "workbench://overview",
  });

  assert.equal(canonical.result.contents.length, 1);
  assert.match(canonical.result.contents[0].text, /Workbench Docs MCP/i);

  const fileTemplate = await callJsonRpc("resources/read", {
    uri: "workbench://file/overview.md",
  });

  assert.equal(fileTemplate.result.contents.length, 1);
  assert.match(fileTemplate.result.contents[0].text, /Workbench Docs MCP/i);
});

test("GET /docs/mcp honors MCP_PATH_PREFIX overrides", async () => {
  const response = await callWorker("/docs/mcp", {
    env: {
      MCP_PATH_PREFIX: "/docs",
    },
  });
  assert.equal(response.status, 200);

  const html = await response.text();
  assert.match(html, /Workbench Docs MCP/i);
  assert.match(html, /\/docs\/mcp\/resource\//i);
});

test("resource page renders browsable HTML for prefixed markdown source paths", async () => {
  const response = await callWorker(`/workbench/mcp/resource/${encodeURIComponent("workbench://file/overview.md")}`);
  assert.equal(response.status, 200);

  const html = await response.text();
  assert.match(html, /Overview/i);
  assert.match(html, /Source/i);
  assert.match(html, /Back to the index/i);
});

test("tools/list exposes only search_docs", async () => {
  const tools = await callJsonRpc("tools/list", {});
  assert.deepEqual(
    tools.result.tools.map((tool) => tool.name),
    ["search_docs"],
  );
});
