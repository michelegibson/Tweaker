using System.Runtime.CompilerServices;

namespace Tweaker.IntegrationTests.Helpers;

public static class TestContext
{
    /// <summary>
    /// Resolves the solution root directory by walking up from this source file's
    /// compile-time path. Anchoring on [CallerFilePath] (rather than
    /// AppContext.BaseDirectory) is what lets us keep working when the test bin
    /// folder lives outside the repo — e.g. when TWEAKER_LOCAL_BUILD_ROOT
    /// redirects output to %LOCALAPPDATA%\Tweaker-dev\build\... on a
    /// network-share repo, where no parent of the bin dir contains Tweaker.sln.
    /// </summary>
    public static string SolutionDir => FindSolutionDir();

    private static string FindSolutionDir([CallerFilePath] string callerPath = "")
    {
        var dir = Path.GetDirectoryName(callerPath);
        while (dir != null && !File.Exists(Path.Combine(dir, "Tweaker.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }

        return dir ?? throw new InvalidOperationException(
            "Could not find Tweaker.sln walking up from " + callerPath);
    }
}
