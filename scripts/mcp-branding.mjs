export const displayName = "Workbench Docs MCP";
export const serverName = "workbench-docs-mcp";
export const namespace = "workbench";
export const packageName = "@incursa/workbench-docs-mcp";
export const summary = "Deterministic Cloudflare Worker MCP server for Workbench markdown docs.";
export const searchToolName = "search_docs";

export const resourceTemplate = `${namespace}://file/{path}`;

export const groupOrder = ["core", "guides", "reference", "specs", "ai", "examples"];
export const groupLabels = {
  core: "Core",
  guides: "Guides",
  reference: "Reference",
  specs: "Specs",
  ai: "AI",
  examples: "Examples",
};

export const kindLabels = {
  guide: "Guide",
  reference: "Reference",
  spec: "Spec",
  index: "Index",
  example: "Example",
};

export const supportedKinds = ["guide", "reference", "spec", "index", "example"];
export const supportedSearchKinds = [...supportedKinds, "any"];

export const defaultPriorityByGroup = {
  core: 120,
  guides: 100,
  reference: 90,
  specs: 80,
  ai: 70,
  examples: 60,
};

export default {
  displayName,
  serverName,
  namespace,
  packageName,
  summary,
  searchToolName,
  resourceTemplate,
  groupOrder,
  groupLabels,
  kindLabels,
  supportedKinds,
  supportedSearchKinds,
  defaultPriorityByGroup,
};
