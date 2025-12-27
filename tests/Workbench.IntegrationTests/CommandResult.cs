using System.Diagnostics;
using System.Text.Json;

namespace Workbench.IntegrationTests;

internal sealed record CommandResult(int ExitCode, string StdOut, string StdErr);