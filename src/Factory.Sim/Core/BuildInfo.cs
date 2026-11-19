using System.Reflection;

namespace Factory.Sim;

/// <summary>
/// The game build this code was compiled as, stamped into every save. The build embeds it as an assembly
/// attribute (see <c>Factory.Sim.csproj</c>): <c>Factory-&lt;date&gt;-&lt;commit&gt;</c>, the same name
/// <c>package.sh</c> writes to <c>VERSION.txt</c>, or <c>dev</c> when git wasn't available.
/// </summary>
public static class BuildInfo
{
    /// <summary>The build version string, or <c>dev</c>.</summary>
    public static string Version { get; } = typeof(BuildInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "FactoryBuildVersion")?.Value is { Length: > 0 } v ? v : "dev";
}
