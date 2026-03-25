using Workbench.Core;

namespace Workbench.Tests;

[TestClass]
public class SpecTraceMarkdownTests
{
    [TestMethod]
    public void ParseRequirementClauses_ValidClauseAndTrace_ParsesSuccessfully()
    {
        var body = """
            ## REQ-WB-0001 Deterministic output
            The service MUST return deterministic output.

            Trace:
            - Implemented By:
              - WI-WB-0001
            - Verified By:
              - VER-WB-0001

            Notes:
            - Keep this stable.
            """;

        var clauses = SpecTraceMarkdown.ParseRequirementClauses(body, out var errors);

        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
        Assert.HasCount(1, clauses);
        Assert.AreEqual("REQ-WB-0001", clauses[0].RequirementId);
        Assert.AreEqual("MUST", clauses[0].NormativeKeyword);
        Assert.IsNotNull(clauses[0].Trace);
        var trace = clauses[0].Trace;
        Assert.IsNotNull(trace);
        Assert.IsTrue(trace.ContainsKey("Implemented By"));
        Assert.AreEqual("WI-WB-0001", trace["Implemented By"][0]);
    }

    [TestMethod]
    public void ParseRequirementClauses_InvalidTraceShape_ReportsErrors()
    {
        var body = """
            ## REQ-WB-0002 Invalid trace example
            The system MUST keep quality evidence local.

            Trace:
            - Unknown Label:
              - WI-WB-0002
            - Implemented By:
            WI-WB-0002
            """;

        var clauses = SpecTraceMarkdown.ParseRequirementClauses(body, out var errors);

        Assert.IsEmpty(clauses);
        Assert.IsTrue(errors.Any(error => error.Contains("Trace label 'Unknown Label' is not canonical.", StringComparison.Ordinal)));
        Assert.IsTrue(errors.Any(error => error.Contains("Trace values for 'Implemented By' must use bullet items.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ParseRequirementClauses_NotesBeforeTrace_ReportsOrderingError()
    {
        var body = """
            ## REQ-WB-0003 Notes order
            The service MUST enforce order.

            Notes:
            - note first
            Trace:
            - Implemented By:
              - WI-WB-0003
            """;

        _ = SpecTraceMarkdown.ParseRequirementClauses(body, out var errors);

        Assert.IsTrue(errors.Any(error => error.Contains("Notes must follow Trace.", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ParseRequirementClauses_MissingNormativeKeyword_ReportsError()
    {
        var body = """
            ## REQ-WB-0004 Missing keyword
            This clause has no approved keyword.
            """;

        _ = SpecTraceMarkdown.ParseRequirementClauses(body, out var errors);

        Assert.IsTrue(errors.Any(error => error.Contains("must contain exactly one approved normative keyword", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ExtractSection_ReturnsExpectedBodyAndStopsAtNextHeading()
    {
        var body = """
            # SPEC-WB-1000 - Example

            ## Purpose
            Purpose line.

            ## Scope
            Scope line one.
            Scope line two.

            ## Context
            Context line.
            """;

        var section = SpecTraceMarkdown.ExtractSection(body, "Scope");

        Assert.AreEqual("Scope line one.\nScope line two.", section);
    }

    [TestMethod]
    public void ExtractRequirementBlocks_ReturnsOnlyRequirementSections()
    {
        var body = """
            # Header

            ## Purpose
            Purpose text.

            ## REQ-WB-0100 First requirement
            The tool MUST keep canonical IDs.

            ## REQ-WB-0101 Second requirement
            The tool SHOULD keep deterministic outputs.

            ## Context
            Context text.
            """;

        var blocks = SpecTraceMarkdown.ExtractRequirementBlocks(body);

        StringAssert.Contains(blocks, "## REQ-WB-0100 First requirement", StringComparison.Ordinal);
        StringAssert.Contains(blocks, "## REQ-WB-0101 Second requirement", StringComparison.Ordinal);
        Assert.IsFalse(blocks.Contains("## Purpose", StringComparison.Ordinal));
        Assert.IsFalse(blocks.Contains("## Context", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GetCanonicalArtifactType_MapsKnownAliases()
    {
        Assert.AreEqual("specification", SpecTraceMarkdown.GetCanonicalArtifactType("spec"));
        Assert.AreEqual("specification", SpecTraceMarkdown.GetCanonicalArtifactType("specification"));
        Assert.AreEqual("architecture", SpecTraceMarkdown.GetCanonicalArtifactType("architecture"));
        Assert.AreEqual("work_item", SpecTraceMarkdown.GetCanonicalArtifactType("work-item"));
        Assert.AreEqual("work_item", SpecTraceMarkdown.GetCanonicalArtifactType("work item"));
        Assert.AreEqual("verification", SpecTraceMarkdown.GetCanonicalArtifactType("verification"));
        Assert.IsNull(SpecTraceMarkdown.GetCanonicalArtifactType("runbook"));
    }
}
