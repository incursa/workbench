import assert from "node:assert/strict";
import test from "node:test";
import { callJsonRpc } from "./_helpers.mjs";

test("search_docs ranks an exact title match first", async () => {
  const response = await callJsonRpc("tools/call", {
    name: "search_docs",
    arguments: {
      query: "overview",
      kind: "guide",
      group: "any",
      max_results: 5,
    },
  });

  assert.equal(response.result.structuredContent.results[0].uri, "workbench://overview");
});

test("search_docs filters by group", async () => {
  const response = await callJsonRpc("tools/call", {
    name: "search_docs",
    arguments: {
      query: "layout",
      kind: "any",
      group: "reference",
      max_results: 5,
    },
  });

  const results = response.result.structuredContent.results;
  assert.equal(results[0].uri, "workbench://reference/layout");
  assert.ok(results.every((result) => result.group === "reference"));
});

test("search_docs filters by kind", async () => {
  const response = await callJsonRpc("tools/call", {
    name: "search_docs",
    arguments: {
      query: "verification",
      kind: "spec",
      group: "any",
      max_results: 5,
    },
  });

  assert.equal(response.result.structuredContent.results[0].uri, "workbench://specs/verification-index");
});

test("search_docs returns prioritized results for an empty query", async () => {
  const response = await callJsonRpc("tools/call", {
    name: "search_docs",
    arguments: {
      query: "",
      kind: "any",
      group: "any",
      max_results: 3,
    },
  });

  assert.equal(response.result.structuredContent.results[0].uri, "workbench://overview");
});
