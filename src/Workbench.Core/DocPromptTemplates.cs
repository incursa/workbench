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

                ## REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> <short title>
                The system shall ...

                Trace:
                - Satisfied By:
                  - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Implemented By:
                  - WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
                - Test Refs:
                  - <test reference>
                - Code Refs:
                  - <code reference>
                - Related:
                  - <artifact or requirement ID>

                Notes:
                - Optional clarification that narrows interpretation without changing the requirement.
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

                ## Purpose

                ## Requirements Satisfied
                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Design Summary

                ## Key Components

                ## Data and State Considerations

                ## Edge Cases and Constraints

                ## Alternatives Considered
                - <alternative and reason rejected>

                ## Risks
                - <risk or follow-up>

                ## Open Questions
                - <question>
                """,
            "guide" => """
                # <title>

                ## Purpose

                ## Requirements Satisfied

                ## Design summary

                ## Key components

                ## Data and state considerations

                ## Edge cases and constraints

                ## Alternatives considered
                -

                ## Risks
                -

                ## Open questions
                - <question>
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
                related_artifacts:
                  - SPEC-<DOMAIN>[-<GROUPING>...]
                ---

                # WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> - <Work Item Title>

                Use one of the approved work-item statuses: `planned`, `in_progress`, `blocked`, `complete`, `cancelled`, or `superseded`.

                ## Summary

                ## Requirements Addressed
                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Design Inputs
                - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                ## Planned Changes

                ## Out of Scope
                - <item>

                ## Verification Plan

                ## Completion Notes

                ## Trace Links

                Addresses:
                - REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>

                Uses Design:
                - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
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
