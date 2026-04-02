import { McpServer, ResourceTemplate } from "@modelcontextprotocol/sdk/server/mcp.js";
import { WebStandardStreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/webStandardStreamableHttp.js";
import * as z from "zod/v4";
import resourcesManifest from "../../dist/mcp/resources.json";
import searchIndex from "../../dist/mcp/search-index.json";

type ResourceRecord = {
  uri: string;
  title: string;
  kind: string;
  searchKind: string;
  summary: string;
  body: string;
  sourcePaths: string[];
  mimeType: string;
  aliases: string[];
  relatedUris: string[];
  group: string;
  priority: number;
  includeInSearch?: boolean;
  tags?: string[];
  searchText: string;
};

type SearchIndexEntry = Pick<
  ResourceRecord,
  | "uri"
  | "title"
  | "kind"
  | "searchKind"
  | "summary"
  | "sourcePaths"
  | "aliases"
  | "relatedUris"
  | "group"
  | "priority"
  | "includeInSearch"
  | "tags"
  | "searchText"
> & { excerpt: string };

const packageName = resourcesManifest.packageName ?? "@incursa/workbench-docs-mcp";
const packageVersion = resourcesManifest.packageVersion ?? "0.0.0";
const serverName = resourcesManifest.serverName ?? "workbench-docs-mcp";
const displayName = resourcesManifest.displayName ?? "Workbench Docs MCP";
const namespace = resourcesManifest.namespace ?? "workbench";
const defaultPathPrefix = "/workbench";
const resources = resourcesManifest.resources as ResourceRecord[];
const resourceMap = new Map(resources.map((resource) => [resource.uri, resource]));
const sourcePathMap = new Map<string, ResourceRecord>();
const searchEntries = searchIndex as SearchIndexEntry[];

type WorkerEnv = {
  MCP_PATH_PREFIX?: string;
};

for (const resource of resources) {
  for (const sourcePath of resource.sourcePaths) {
    const normalized = sourcePath.replace(/\\/g, "/").replace(/^\.\//, "").replace(/^\//, "");
    sourcePathMap.set(normalized, resource);
    sourcePathMap.set(`content/${normalized}`, resource);
    sourcePathMap.set(normalized.replace(/\.md$/, ""), resource);
  }
}

function normalizeText(value: string) {
  return String(value ?? "")
    .replace(/\r\n/g, "\n")
    .replace(/\u00a0/g, " ")
    .replace(/[ \t]+\n/g, "\n")
    .replace(/\n{3,}/g, "\n\n")
    .replace(/[ \t]{2,}/g, " ")
    .trim()
    .toLowerCase();
}

function escapeHtml(value: string) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function titleCase(value: string) {
  return value
    .split(/[-_/]+/g)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function groupLabel(group: string) {
  return (
    {
      core: "Core",
      guides: "Guides",
      reference: "Reference",
      specs: "Specs",
      ai: "AI",
      examples: "Examples",
    }[group] ?? titleCase(group)
  );
}

function groupOrder(group: string) {
  return (
    {
      core: 0,
      guides: 1,
      reference: 2,
      specs: 3,
      ai: 4,
      examples: 5,
    }[group] ?? 99
  );
}

function code(text: string) {
  return `<code>${escapeHtml(text)}</code>`;
}

function normalizeSourcePath(filePath: string) {
  const value = String(filePath ?? "");
  const stripped = value.startsWith(`${namespace}://file/`) ? value.slice(`${namespace}://file/`.length) : value;
  return stripped
    .replace(/\\/g, "/")
    .replace(/^\.\//, "")
    .replace(/^\//, "")
    .replace(/^(?:content\/)+/, "")
    .replace(/\/+/g, "/");
}

function extractExcerpt(text: string, tokens: string[]) {
  const source = normalizeText(text);
  if (!source) {
    return "";
  }

  const firstMatch = tokens.map((token) => source.indexOf(token)).find((index) => index >= 0);
  if (firstMatch == null || firstMatch < 0) {
    return source.slice(0, 220);
  }

  const start = Math.max(0, firstMatch - 80);
  return source.slice(start, start + 220);
}

function normalizePathPrefix(value: string | undefined) {
  const raw = String(value ?? "").trim();
  if (!raw) {
    return defaultPathPrefix;
  }

  if (raw === "/") {
    return "/";
  }

  let prefix = raw.startsWith("/") ? raw : `/${raw}`;
  prefix = prefix.replace(/\/+$/, "");
  return prefix || "/";
}

function stripPathPrefix(pathname: string, prefix: string) {
  if (!prefix || prefix === "/") {
    return pathname;
  }

  if (pathname === prefix) {
    return "/";
  }

  if (pathname.startsWith(`${prefix}/`)) {
    const stripped = pathname.slice(prefix.length);
    return stripped || "/";
  }

  return pathname;
}

function joinPath(prefix: string, pathname: string) {
  const normalizedPrefix = !prefix || prefix === "/" ? "" : prefix.replace(/\/+$/, "");
  const normalizedPath = pathname.startsWith("/") ? pathname : `/${pathname}`;
  return `${normalizedPrefix}${normalizedPath}` || "/";
}

function normalizeRequestForRouting(request: Request, env: WorkerEnv | undefined) {
  const pathPrefix = normalizePathPrefix(env?.MCP_PATH_PREFIX);
  const url = new URL(request.url);
  const strippedPath = stripPathPrefix(url.pathname, pathPrefix);
  if (strippedPath !== url.pathname) {
    url.pathname = strippedPath;
    return {
      request: new Request(url.toString(), request),
      pathPrefix,
      url,
    };
  }

  return {
    request,
    pathPrefix,
    url,
  };
}

function renderDocsIndexHtml(pathPrefix: string) {
  const publicMcpBasePath = joinPath(pathPrefix, "/mcp");
  const grouped = new Map<string, ResourceRecord[]>();
  for (const resource of resources) {
    const bucket = grouped.get(resource.group) ?? [];
    bucket.push(resource);
    grouped.set(resource.group, bucket);
  }

  const sections = [...grouped.entries()]
    .sort((left, right) => groupOrder(left[0]) - groupOrder(right[0]) || left[0].localeCompare(right[0]))
    .map(([group, entries]) => {
      const cards = entries
        .slice()
        .sort((left, right) => right.priority - left.priority || left.title.localeCompare(right.title))
        .map(
          (resource) => `
            <a class="card" href="${joinPath(pathPrefix, `/mcp/resource/${encodeURIComponent(resource.uri)}`)}">
              <strong>${escapeHtml(resource.title)}</strong>
              <span>${escapeHtml(resource.summary)}</span>
              <small>${escapeHtml(resource.uri)}</small>
            </a>`,
        )
        .join("");

      return `
        <section class="section">
          <div class="section-head">
            <h2>${escapeHtml(groupLabel(group))}</h2>
            <p>${entries.length} resources</p>
          </div>
          <div class="grid">${cards}</div>
        </section>`;
    })
    .join("");

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(displayName)}</title>
  <style>
    :root {
      color-scheme: light dark;
      --bg: #07111f;
      --panel: rgba(17, 27, 49, 0.82);
      --line: rgba(158, 177, 200, 0.18);
      --text: #e8f0fb;
      --muted: #9eb1c8;
      --accent: #7dd3fc;
      --shadow: 0 28px 60px rgba(2, 6, 23, 0.45);
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      min-height: 100vh;
      font-family: Inter, "Segoe UI", system-ui, -apple-system, sans-serif;
      background:
        radial-gradient(circle at top left, rgba(125, 211, 252, 0.16), transparent 32%),
        radial-gradient(circle at right 12%, rgba(167, 139, 250, 0.12), transparent 24%),
        linear-gradient(180deg, var(--bg), #0c1830);
      color: var(--text);
    }
    a { color: inherit; text-decoration: none; }
    main {
      width: min(1160px, calc(100vw - 32px));
      margin: 0 auto;
      padding: 40px 0 64px;
    }
    .hero, .section {
      border: 1px solid var(--line);
      background: var(--panel);
      backdrop-filter: blur(18px);
      border-radius: 24px;
      box-shadow: var(--shadow);
    }
    .hero {
      display: grid;
      grid-template-columns: 1.2fr 0.8fr;
      gap: 18px;
      padding: 24px;
    }
    .panel {
      padding: 18px;
      border-radius: 18px;
      border: 1px solid rgba(255, 255, 255, 0.06);
      background: rgba(255, 255, 255, 0.04);
    }
    h1 {
      margin: 0;
      font-size: clamp(2.4rem, 5vw, 4.4rem);
      line-height: .94;
      max-width: 10ch;
    }
    p { line-height: 1.6; }
    .lead { color: var(--muted); max-width: 66ch; }
    .meta-grid, .grid, .tools {
      display: grid;
      gap: 14px;
    }
    .meta-grid { grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }
    .section {
      margin-top: 20px;
      padding: 20px;
    }
    .section-head {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: 12px;
      margin-bottom: 14px;
    }
    .section-head h2, .section-head p { margin: 0; }
    .section-head p { color: var(--muted); }
    .grid {
      grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
    }
    .card {
      display: grid;
      gap: 10px;
      min-height: 150px;
      padding: 16px;
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 18px;
      background: rgba(255, 255, 255, 0.04);
      color: inherit;
    }
    .card strong { color: var(--accent); }
    .card span, .card small, .meta p, .tool p { color: var(--muted); }
    .card small, code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
    code {
      background: rgba(125, 211, 252, 0.12);
      border: 1px solid rgba(125, 211, 252, 0.18);
      border-radius: 999px;
      padding: 0.15rem 0.4rem;
    }
    pre {
      margin: 12px 0 0;
      padding: 14px 16px;
      border-radius: 14px;
      overflow: auto;
      background: rgba(2, 6, 23, 0.55);
      border: 1px solid rgba(255, 255, 255, 0.08);
      white-space: pre-wrap;
    }
    @media (max-width: 860px) {
      .hero { grid-template-columns: 1fr; }
      .section-head { flex-direction: column; align-items: start; }
      main { width: min(1160px, calc(100vw - 22px)); padding-top: 20px; }
    }
  </style>
</head>
<body>
  <main>
    <section class="hero">
      <div>
        <p class="lead" style="text-transform:uppercase;letter-spacing:.16em;color:var(--accent);margin:0 0 12px">${escapeHtml(displayName)}</p>
        <h1>Static markdown, deterministic MCP.</h1>
        <p class="lead">${escapeHtml(resourcesManifest.summary ?? "A deterministic Cloudflare Worker MCP docs server.")}</p>
        <div class="panel" style="margin-top:18px">
          <strong>Endpoints</strong>
          <p>${code(`GET ${publicMcpBasePath}`)}, ${code(`POST ${publicMcpBasePath}`)}, and ${code(`GET ${publicMcpBasePath}/resource/<uri>`)}.</p>
        </div>
      </div>
      <div class="meta-grid">
        <div class="panel"><strong>Package</strong><p>${escapeHtml(packageName)}</p></div>
        <div class="panel"><strong>Version</strong><p>${escapeHtml(packageVersion)}</p></div>
        <div class="panel"><strong>Tool</strong><p>${code("search_docs")}</p></div>
        <div class="panel"><strong>Template</strong><p>${code(`${namespace}://file/{path}`)}</p></div>
      </div>
    </section>

    <section class="section">
      <div class="section-head">
        <h2>How it works</h2>
        <p>Front matter becomes MCP metadata at build time.</p>
      </div>
      <div class="tools">
        <div class="panel"><strong>Authoring</strong><p>Edit markdown files in ${code("content/")}.</p></div>
        <div class="panel"><strong>Build</strong><p>Compile the files into ${code("dist/mcp/*.json")}.</p></div>
        <div class="panel"><strong>Search</strong><p>Use the single dynamic tool to search compiled docs.</p></div>
      </div>
    </section>

    <section class="section">
      <div class="section-head">
        <h2>Resources</h2>
        <p>${resources.length} static docs.</p>
      </div>
      ${sections}
    </section>
  </main>
</body>
</html>`;
}

function renderResourcePage(resource: ResourceRecord, pathPrefix: string) {
  const publicMcpBasePath = joinPath(pathPrefix, "/mcp");
  const aliases = resource.aliases.length ? resource.aliases.map((alias) => `<code>${escapeHtml(alias)}</code>`).join(" ") : "<span>None</span>";
  const related = resource.relatedUris.length
    ? `<ul>${resource.relatedUris
        .map((uri) => {
          const relatedResource = resourceMap.get(uri);
          const title = relatedResource?.title ?? uri;
          return `<li><a href="${joinPath(pathPrefix, `/mcp/resource/${encodeURIComponent(uri)}`)}">${escapeHtml(title)}</a></li>`;
        })
        .join("")}</ul>`
    : "<p>No related resources.</p>";

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeHtml(resource.title)} - ${escapeHtml(displayName)}</title>
  <style>
    :root {
      color-scheme: light dark;
      --bg: #07111f;
      --panel: rgba(17, 27, 49, 0.82);
      --line: rgba(158, 177, 200, 0.18);
      --text: #e8f0fb;
      --muted: #9eb1c8;
      --accent: #7dd3fc;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: Inter, "Segoe UI", system-ui, -apple-system, sans-serif;
      background: linear-gradient(180deg, var(--bg), #0c1830);
      color: var(--text);
    }
    main {
      width: min(980px, calc(100vw - 32px));
      margin: 0 auto;
      padding: 40px 0 64px;
    }
    article, section {
      border: 1px solid var(--line);
      background: var(--panel);
      backdrop-filter: blur(18px);
      border-radius: 24px;
      padding: 24px;
      box-shadow: 0 28px 60px rgba(2, 6, 23, 0.45);
    }
    section { margin-top: 20px; }
    p, li { color: var(--muted); line-height: 1.6; }
    code, pre { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
    code {
      background: rgba(125, 211, 252, 0.12);
      border: 1px solid rgba(125, 211, 252, 0.18);
      border-radius: 999px;
      padding: 0.15rem 0.4rem;
    }
    pre {
      margin: 12px 0 0;
      padding: 14px 16px;
      border-radius: 14px;
      overflow: auto;
      background: rgba(2, 6, 23, 0.55);
      border: 1px solid rgba(255, 255, 255, 0.08);
      white-space: pre-wrap;
    }
    a { color: var(--accent); }
    .meta {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      margin-top: 16px;
    }
    .meta div {
      padding: 14px 16px;
      border-radius: 16px;
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.06);
    }
    .meta strong {
      display: block;
      margin-bottom: 6px;
      color: var(--accent);
      font-size: 0.76rem;
      letter-spacing: .12em;
      text-transform: uppercase;
    }
  </style>
</head>
<body>
  <main>
    <article>
      <p style="text-transform:uppercase;letter-spacing:.16em;color:var(--accent);margin:0 0 12px">${escapeHtml(resource.group)}</p>
      <h1>${escapeHtml(resource.title)}</h1>
      <p>${escapeHtml(resource.summary)}</p>
      <div class="meta">
        <div><strong>URI</strong><code>${escapeHtml(resource.uri)}</code></div>
        <div><strong>Source</strong><code>${escapeHtml(resource.sourcePaths.join(", "))}</code></div>
        <div><strong>Kind</strong><code>${escapeHtml(resource.kind)}</code></div>
      </div>
    </article>

    <section>
      <h2>Aliases</h2>
      <p>${aliases}</p>
    </section>

    <section>
      <h2>Body</h2>
      <pre>${escapeHtml(resource.body)}</pre>
    </section>

    <section>
      <h2>Related resources</h2>
      ${related}
      <p><a href="${publicMcpBasePath}">Back to the index</a></p>
    </section>
  </main>
</body>
</html>`;
}

function lookupResourceFromUri(uri) {
  return resourceMap.get(uri) ?? null;
}

function lookupResourceFromFilePath(filePath) {
  const normalized = normalizeSourcePath(filePath);
  return sourcePathMap.get(normalized) ?? sourcePathMap.get(normalized.replace(/\.md$/, "")) ?? null;
}

function tokenMatches(entry, token) {
  const title = normalizeText(entry.title);
  const summary = normalizeText(entry.summary);
  const aliases = normalizeText(entry.aliases.join(" "));
  const tags = normalizeText((entry.tags ?? []).join(" "));
  const haystack = normalizeText(entry.searchText);
  const sourcePaths = normalizeText(entry.sourcePaths.join(" "));
  const uri = normalizeText(entry.uri);

  if (title === token) return 500;
  if (uri === token) return 450;
  if (aliases.includes(token)) return 220;
  if (tags.includes(token)) return 180;
  if (title.includes(token)) return 160;
  if (summary.includes(token)) return 120;
  if (sourcePaths.includes(token)) return 100;
  if (haystack.includes(token)) return 80;
  return 0;
}

function searchDocs(args) {
  const query = normalizeText(args.query ?? "");
  const tokens = query.split(/\s+/g).filter(Boolean);
  const kind = args.kind ?? "any";
  const group = args.group ?? "any";
  const includeUnsearchable = args.include_unsearchable ?? false;
  const maxResults = Math.min(Math.max(args.max_results ?? 8, 1), 20);

  const filtered = searchEntries.filter((entry) => {
    if (!includeUnsearchable && entry.includeInSearch === false) {
      return false;
    }
    if (kind !== "any" && entry.searchKind !== kind && entry.kind !== kind) {
      return false;
    }
    if (group !== "any" && entry.group !== group) {
      return false;
    }
    return true;
  });

  const scored = filtered
    .map((entry) => {
      const title = normalizeText(entry.title);
      const summary = normalizeText(entry.summary);
      const haystack = normalizeText(entry.searchText);
      let score = (entry.priority ?? 0) * 10;

      if (!query) {
        score += 10;
      } else {
        if (normalizeText(entry.uri) === query) score += 4000;
        if (title === query) score += 3200;
        if (title.startsWith(query)) score += 800;
        if (haystack.includes(query)) score += 500;
        if (summary.includes(query)) score += 240;

        for (const token of tokens) {
          const tokenScore = tokenMatches(entry, token);
          score += tokenScore;
          if (!tokenScore) {
            score -= 4;
          }
        }
      }

      return {
        ...entry,
        score,
        excerpt: entry.excerpt || extractExcerpt(entry.searchText, tokens),
      };
    })
    .sort((left, right) => right.score - left.score || right.priority - left.priority || left.title.localeCompare(right.title) || left.uri.localeCompare(right.uri));

  return {
    query: args.query,
    kind,
    group,
    include_unsearchable: includeUnsearchable,
    max_results: maxResults,
    results: scored.slice(0, maxResults).map((entry) => ({
      uri: entry.uri,
      title: entry.title,
      kind: entry.kind,
      searchKind: entry.searchKind,
      group: entry.group,
      summary: entry.summary,
      sourcePaths: entry.sourcePaths,
      score: entry.score,
      excerpt: entry.excerpt,
      relatedUris: entry.relatedUris,
      tags: entry.tags ?? [],
    })),
    starterSuggestions: scored.slice(0, 3).map((entry) => ({
      uri: entry.uri,
      title: entry.title,
      kind: entry.kind,
      searchKind: entry.searchKind,
      group: entry.group,
    })),
  };
}

function registerResources(server) {
  for (const resource of resources) {
    server.registerResource(
      resource.title,
      resource.uri,
      {
        description: resource.summary,
        mimeType: resource.mimeType,
      },
      async () => ({
        contents: [
          {
            uri: resource.uri,
            mimeType: resource.mimeType,
            text: resource.body,
          },
        ],
      }),
    );
  }

  server.registerResource(
    "file-template",
    new ResourceTemplate(`${namespace}://file/{path}`, {
      list: async () => ({
        resources: resources.map((resource) => ({
          uri: `${namespace}://file/${resource.sourcePaths[0]}`,
          name: resource.title,
          title: resource.title,
          description: resource.summary,
          mimeType: resource.mimeType,
        })),
      }),
    }),
    {
      description: "static markdown file template",
      mimeType: "text/markdown; charset=utf-8",
    },
    async (uri: URL, variables: Record<string, string>) => {
      const filePath = variables.path ?? decodeURIComponent(uri.pathname.split("/").pop() ?? "");
      const resource = lookupResourceFromFilePath(filePath) ?? lookupResourceFromUri(uri.toString());
      if (!resource) {
        throw new Error(`Unknown resource: ${filePath}`);
      }
      return {
        contents: [
          {
            uri: resource.uri,
            mimeType: resource.mimeType,
            text: resource.body,
          },
        ],
      };
    },
  );
}

function registerTools(server) {
  server.registerTool(
    "search_docs",
    {
      description: resourcesManifest.searchTool?.description ?? "Search the compiled Workbench docs.",
      inputSchema: {
        query: z.string().describe("Search text"),
        kind: z.enum(["guide", "reference", "spec", "index", "example", "any"]).default("any"),
        group: z.string().default("any"),
        include_unsearchable: z.boolean().default(false),
        max_results: z.number().int().positive().max(20).default(8),
      },
      outputSchema: {
        query: z.string(),
        kind: z.string(),
        group: z.string(),
        include_unsearchable: z.boolean(),
        max_results: z.number(),
        results: z.array(
          z.object({
            uri: z.string(),
            title: z.string(),
            kind: z.string(),
            searchKind: z.string(),
            group: z.string(),
            summary: z.string(),
            sourcePaths: z.array(z.string()),
            score: z.number(),
            excerpt: z.string(),
            relatedUris: z.array(z.string()),
            tags: z.array(z.string()),
          }),
        ),
        starterSuggestions: z.array(
          z.object({
            uri: z.string(),
            title: z.string(),
            kind: z.string(),
            searchKind: z.string(),
            group: z.string(),
          }),
        ),
      },
    },
    async (args) => {
      const result = searchDocs(args);
      return {
        content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
        structuredContent: result,
      };
    },
  );
}

function createServer() {
  const server = new McpServer({ name: serverName, version: packageVersion }, { capabilities: { logging: {} } });
  registerResources(server);
  registerTools(server);
  return server;
}

async function handleMcpRequest(request) {
  const server = createServer();
  const transport = new WebStandardStreamableHTTPServerTransport({
    sessionIdGenerator: undefined,
    enableJsonResponse: true,
  });

  await server.connect(transport);
  return transport.handleRequest(request);
}

function findResourceForRequest(url) {
  if (url.pathname === "/mcp" || url.pathname === "/") {
    return null;
  }

  const resourcePrefix = "/mcp/resource/";
  const rawResourcePrefix = "/resource/";
  let encodedUri = "";

  if (url.pathname.startsWith(resourcePrefix)) {
    encodedUri = url.pathname.slice(resourcePrefix.length);
  } else if (url.pathname.startsWith(rawResourcePrefix)) {
    encodedUri = url.pathname.slice(rawResourcePrefix.length);
  } else if (url.searchParams.has("uri")) {
    encodedUri = url.searchParams.get("uri") ?? "";
  }

  if (!encodedUri) {
    return null;
  }

  const decodedUri = decodeURIComponent(encodedUri);
  return lookupResourceFromUri(decodedUri) ?? lookupResourceFromFilePath(decodedUri);
}

export async function fetch(request: Request, env?: WorkerEnv) {
  const routed = normalizeRequestForRouting(request, env);
  const { request: routedRequest, pathPrefix, url } = routed;

  if (routedRequest.method === "POST" && url.pathname === "/mcp") {
    return handleMcpRequest(routedRequest);
  }

  if (routedRequest.method === "GET" && (url.pathname === "/mcp" || url.pathname === "/")) {
    return new Response(renderDocsIndexHtml(pathPrefix), {
      headers: { "content-type": "text/html; charset=utf-8" },
    });
  }

  if (routedRequest.method === "GET") {
    const resource = findResourceForRequest(url);
    if (resource) {
      return new Response(renderResourcePage(resource, pathPrefix), {
        headers: { "content-type": "text/html; charset=utf-8" },
      });
    }
  }

  return new Response("Not found", { status: 404 });
}
