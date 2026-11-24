namespace Workbench.Core;

/// <summary>Shared runtime invariants for change plans carried by the provider protocol.</summary>
internal static class ResourceProviderContractValidator
{
    public static ProviderDiagnostic? ValidateChange(
        ResourceChange change,
        string resourceType,
        string resourceSet,
        string expectedAuthorityEpoch)
    {
        if (string.IsNullOrWhiteSpace(change.ChangeId))
        {
            return Error("provider.change.id.required", "Every change requires a non-empty change ID.", null);
        }

        if (!string.Equals(change.Address.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(change.Address.ResourceSet, resourceSet, StringComparison.OrdinalIgnoreCase))
        {
            return Error(
                "provider.plan.scope.mismatch",
                "Every planned change must belong to the request resource type and set.",
                change.ChangeId);
        }

        if (change.PayloadSha256 is not null && !IsSha256(change.PayloadSha256))
        {
            return Error(
                "provider.change.payload-digest.invalid",
                "A change payload digest must be 64 lowercase hexadecimal characters.",
                change.ChangeId);
        }

        if (change.Kind is ResourceChangeKind.Create or ResourceChangeKind.Update or ResourceChangeKind.Publish
            && (change.Content is null || change.PayloadSha256 is null))
        {
            return Error(
                "provider.change.payload.required",
                $"Change kind '{change.Kind}' requires content and its payload digest.",
                change.ChangeId);
        }

        if (change.Kind == ResourceChangeKind.Create && !string.IsNullOrWhiteSpace(change.ExpectedVersionToken))
        {
            return Error(
                "provider.change.create.version-token.invalid",
                "Create changes must not carry an expected version token.",
                change.ChangeId);
        }

        if (change.Kind is ResourceChangeKind.Update or ResourceChangeKind.Delete or ResourceChangeKind.TransferAuthority
            && string.IsNullOrWhiteSpace(change.ExpectedVersionToken))
        {
            return Error(
                "provider.concurrency.version-token.required",
                $"Change '{change.ChangeId}' requires an expected version token.",
                change.ChangeId);
        }

        if (change.Kind == ResourceChangeKind.Delete
            && (change.Content is not null || change.PayloadSha256 is not null))
        {
            return Error(
                "provider.change.delete.payload.invalid",
                "Delete changes must not carry content or a payload digest.",
                change.ChangeId);
        }

        if (change.Kind != ResourceChangeKind.TransferAuthority)
        {
            return change.AuthorityTransfer is null
                ? null
                : Error(
                    "provider.authority-transfer.unexpected",
                    "Only transferAuthority changes may carry an authority-transfer record.",
                    change.ChangeId);
        }

        ResourceAuthorityTransfer? transfer = change.AuthorityTransfer;
        if (transfer is null)
        {
            return Error(
                "provider.authority-transfer.required",
                "A transferAuthority change requires the complete approved authority-transfer record.",
                change.ChangeId);
        }

        if (!string.Equals(transfer.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(transfer.ResourceSet, resourceSet, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(transfer.FromAuthorityEpoch, expectedAuthorityEpoch, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(transfer.TransferId)
            || string.IsNullOrWhiteSpace(transfer.FromProviderId)
            || string.IsNullOrWhiteSpace(transfer.ToProviderId)
            || string.Equals(transfer.FromProviderId, transfer.ToProviderId, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(transfer.ToAuthorityEpoch)
            || string.Equals(transfer.FromAuthorityEpoch, transfer.ToAuthorityEpoch, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(transfer.SourceVersion)
            || string.IsNullOrWhiteSpace(transfer.TargetVersion)
            || string.IsNullOrWhiteSpace(transfer.ApprovedBy)
            || string.IsNullOrWhiteSpace(transfer.ApprovalReference)
            || !IsSha256(transfer.PayloadSha256)
            || !string.Equals(change.PayloadSha256, transfer.PayloadSha256, StringComparison.Ordinal))
        {
            return Error(
                "provider.authority-transfer.coordinates.invalid",
                "Authority transfer coordinates must match the planned scope, source epoch, and payload digest and name distinct old and new authorities.",
                change.ChangeId);
        }

        return null;
    }

    public static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static ProviderDiagnostic Error(string code, string message, string? target)
    {
        return new ProviderDiagnostic(code, ProviderDiagnosticSeverity.Error, message, target);
    }
}
