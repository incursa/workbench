using System.Text.Json.Serialization;

namespace Workbench;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true)]
[JsonSerializable(typeof(WorkbenchConfig))]
[JsonSerializable(typeof(DoctorOutput))]
[JsonSerializable(typeof(ScaffoldOutput))]
[JsonSerializable(typeof(ConfigOutput))]
[JsonSerializable(typeof(ItemCreateOutput))]
[JsonSerializable(typeof(ItemListOutput))]
[JsonSerializable(typeof(ItemShowOutput))]
[JsonSerializable(typeof(ItemStatusOutput))]
[JsonSerializable(typeof(ItemCloseOutput))]
[JsonSerializable(typeof(ItemMoveOutput))]
[JsonSerializable(typeof(ItemRenameOutput))]
[JsonSerializable(typeof(ItemImportOutput))]
[JsonSerializable(typeof(ItemSyncOutput))]
[JsonSerializable(typeof(BoardOutput))]
[JsonSerializable(typeof(PromoteOutput))]
[JsonSerializable(typeof(PrOutput))]
[JsonSerializable(typeof(ValidateOutput))]
[JsonSerializable(typeof(DocCreateOutput))]
[JsonSerializable(typeof(DocSyncOutput))]
[JsonSerializable(typeof(DocLinkOutput))]
[JsonSerializable(typeof(DocSummaryOutput))]
[JsonSerializable(typeof(NavSyncOutput))]
[JsonSerializable(typeof(RepoSyncOutput))]
internal partial class WorkbenchJsonContext : JsonSerializerContext
{
}
