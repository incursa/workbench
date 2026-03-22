// Prompt templates for AI-generated docs.
// Invariants: template headings align with doc type conventions.
namespace Workbench.Core;

internal static class DocPromptTemplates
{
    public static string BuildTemplate(string docType)
    {
        return docType.Trim().ToLowerInvariant() switch
        {
            "spec" or "specification" => """
                ---
                artifact_id: SPEC-<DOMAIN>[-<GROUPING>...]
                artifact_type: specification
                title: <Specification Title>
                domain: <domain>
                capability: <capability-or-concern>
                status: draft
                owner: <team-or-role>
                tags:
                  - <tag>
                related_artifacts:
                  - <artifact-id>
                ---

                # SPEC-<DOMAIN>[-<GROUPING>...] - <Specification Title>

                ## Purpose

                ## Scope

                ## Context

                ## REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> <Requirement Title>
                The system MUST <direct, testable behavior>.

                Trace:
                - Satisfied By:
                  - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Implemented By:
                  - WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Verified By:
                  - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Derived From:
                  - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Supersedes:
                  - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Source Refs:
                  - <external reference>
                - Test Refs:
                  - <test reference>
                - Code Refs:
                  - <code reference>
                - Related:
                  - <artifact or requirement ID>

                Notes:
                - Optional clarification that narrows interpretation without changing the requirement.

                ## Open Questions

                - <question>

                ## Authoring Rules

                - Every requirement clause must contain exactly one approved normative keyword in all caps: `MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `SHOULD`, `SHOULD NOT`, or `MAY`.
                - The standard uses BCP 14-style uppercase requirement language inspired by RFC 2119 and RFC 8174; only uppercase approved forms carry normative meaning, and lowercase forms are plain English.
                - The keyword does not need to be the first word, but it must appear in the clause and it must be the only approved keyword in that clause.
                - The clause should express one obligation, rule, or constraint and should usually be a single sentence.
                - Each specification Markdown file contains one specification and one or more related requirement clauses.
                - `Trace` and `Notes` are optional.
                - `Derived From` and `Supersedes` capture requirement lineage; `Source Refs` captures external upstream material.
                - The clause is the normative content. Do not bury it under a required metadata block.
                - If you add richer local metadata, keep it clearly optional and do not place it between the requirement heading and the clause.
                - Front matter describes the document as a whole, not individual requirements.
                - `Test Refs` and `Code Refs` stay implementation-specific. The standard does not prescribe a framework or comment syntax.
                """,
            "architecture" => """
                ---
                artifact_id: ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                artifact_type: architecture
                title: <Architecture or Design Title>
                domain: <domain>
                status: draft
                owner: <team-or-role>
                satisfies:
                  - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                related_artifacts:
                  - SPEC-<DOMAIN>[-<GROUPING>...]
                ---

                # ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> - <Architecture or Design Title>

                Use one of the approved architecture statuses: `draft`, `proposed`, `approved`, `implemented`, `verified`, `superseded`, or `retired`.

                ## Purpose

                State how this design satisfies the named requirements.

                ## Requirements Satisfied

                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Design Summary

                Summarize the chosen design and the core mechanism that satisfies the requirement set.

                ## Key Components

                - <component or concept>
                - <component or concept>

                ## Data and State Considerations

                Describe the state, data, and ordering rules that materially affect requirement satisfaction.

                ## Edge Cases and Constraints

                Call out boundary cases, failure paths, retries, or invariants that matter to the requirements.

                ## Alternatives Considered

                - <alternative and reason rejected>

                ## Risks

                - <risk or follow-up>

                ## Open Questions

                - <question>
                """,
            "verification" => """
                ---
                artifact_id: VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                artifact_type: verification
                title: <Verification Title>
                domain: <domain>
                status: planned
                owner: <team-or-role>
                verifies:
                  - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                related_artifacts:
                  - SPEC-<DOMAIN>[-<GROUPING>...]
                  - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                  - WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                ---

                # VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> - <Verification Title>

                Use one of the approved verification statuses: `planned`, `passed`, `failed`, `blocked`, `waived`, or `obsolete`.

                ## Scope

                State what is being verified.

                ## Requirements Verified

                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Verification Method

                Describe the method in tooling-agnostic terms, such as execution, inspection, analysis, or manual review.

                ## Preconditions

                - <precondition>

                ## Procedure or Approach

                Describe the steps or approach used to verify the requirement set.

                ## Expected Result

                Describe the expected outcome in plain language.

                ## Evidence

                - <evidence or test reference>

                ## Status

                The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

                planned

                ## Related Artifacts

                - SPEC-<DOMAIN>[-<GROUPING>...]
                - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                """,
            "work_item" or "work-item" => """
                ---
                artifact_id: WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                artifact_type: work_item
                title: <Work Item Title>
                domain: <domain>
                status: planned
                owner: <team-or-role>
                addresses:
                  - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                design_links:
                  - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                verification_links:
                  - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                related_artifacts:
                  - SPEC-<DOMAIN>[-<GROUPING>...]
                ---

                # WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> - <Work Item Title>

                Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

                ## Summary

                State the implementation work in plain language.

                ## Requirements Addressed

                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Design Inputs

                - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Planned Changes

                Describe the code, configuration, or operational changes to be made.

                ## Out of Scope

                - <item>

                ## Verification Plan

                State how the work will be proven and link the verification artifact.

                ## Completion Notes

                Optional implementation notes, deviations, or follow-up items.

                ## Trace Links

                Addresses:

                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                Uses Design:

                - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                Verified By:

                - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                """,
            "doc" => """
                # <title>

                ## Summary

                ## Scope

                ## Context

                ## Notes
                """,
            _ => """
                # <title>

                ## Notes
                """
        };
    }
}
