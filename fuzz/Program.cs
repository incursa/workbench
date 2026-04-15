using System.Text;
using SharpFuzz;
using Workbench.Core;

namespace Workbench.Fuzz;

public static class Program
{
    public static void Main(string[] args)
    {
        Fuzzer.OutOfProcess.Run(ConsumeInput);
    }

    private static void ConsumeInput(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var text = Encoding.UTF8.GetString(buffer.ToArray());

        _ = FrontMatter.TryParse(text, out _, out _);
        _ = SpecTraceMarkdown.ParseRequirementClauses(text, out _);
        _ = SchemaValidationService.ValidateCanonicalArtifactJson(
            Path.GetTempPath(),
            Path.Combine(Path.GetTempPath(), "workbench-fuzz.json"),
            text);
    }
}
