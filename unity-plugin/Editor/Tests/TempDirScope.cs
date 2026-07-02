using System;
using System.IO;

namespace UnityMCP.Editor.Tests
{
    // M10 (ROI reliability sprint): shared temp-directory helper replacing the
    // Path.GetTempPath()+Guid+CreateDirectory/Delete boilerplate duplicated across 9 NUnit
    // files. Supports both shapes seen in the wild:
    //   class-level: [SetUp] _scope = new TempDirScope("prefix"); [TearDown] _scope.Dispose();
    //   inline:      using var scope = new TempDirScope("prefix");
    internal sealed class TempDirScope : IDisposable
    {
        public string Path { get; }

        public TempDirScope(string prefix = "mcp_test")
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup — never fail a test's teardown over a stray temp dir.
            }
        }
    }
}
