using Workbench.Core;

namespace Workbench.ProviderTestHost;

internal static class Program
{
    public static Task Main()
    {
        return new ResourceProviderProtocolHost(new ProviderTestImplementation())
            .RunAsync(Console.In, Console.Out);
    }
}
