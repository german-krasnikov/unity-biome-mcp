using System;
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Testing
{
    /// <summary>
    /// Reusable assertion for asmdef isolation tests: verifies a named assembly is
    /// loaded and references neither UnityEngine nor UnityEditor (the
    /// noEngineReferences contract). A missing assembly fails rather than
    /// vacuously passing.
    /// </summary>
    public static class AssemblyIsolationAssert
    {
        public static void HasNoEngineReferences(string assemblyName)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            Assert.IsNotNull(asm, $"{assemblyName} assembly not found in AppDomain.");

            foreach (var r in asm.GetReferencedAssemblies())
            {
                Assert.IsFalse(r.Name.StartsWith("UnityEngine", StringComparison.Ordinal),
                    $"{assemblyName} must not reference UnityEngine (found: {r.Name})");
                Assert.IsFalse(r.Name.StartsWith("UnityEditor", StringComparison.Ordinal),
                    $"{assemblyName} must not reference UnityEditor (found: {r.Name})");
            }
        }
    }
}
