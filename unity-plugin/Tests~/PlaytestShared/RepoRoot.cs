using System;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Playtest.Core.SharedTests
{
    /// <summary>The root of the repository: the first folder above the test directory that holds both unity-plugin/ and dsl/constructs/.</summary>
    internal static class RepoRoot
    {
        private static string found;

        public static string Find()
        {
            if (found != null) return found;
            var start = TestContext.CurrentContext.TestDirectory;
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "unity-plugin")) && Directory.Exists(Path.Combine(dir.FullName, "dsl", "constructs")))
                    return found = dir.FullName;
            throw new InvalidOperationException("The root of the repository (a folder with unity-plugin/ and dsl/constructs/) is not above " + start);
        }
    }
}
