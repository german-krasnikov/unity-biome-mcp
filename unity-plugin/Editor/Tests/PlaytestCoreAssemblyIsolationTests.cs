// TDD for D01 — UnityMCP.Playtest.Core asmdef isolation.
// Verifies that UnityMCP.Playtest.Core has no UnityEngine/UnityEditor references
// (noEngineReferences contract), mirroring the precedent at
// unity-plugin/Editor/Chat/Tests/CLI/ParserAssemblyIsolationTests.cs.
using System;
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestCoreAssemblyIsolationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private System.Reflection.Assembly _coreAsm;

        [SetUp]
        public void SetUp()
        {
            _coreAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "UnityMCP.Playtest.Core");
        }

        // ISO-1: assembly is present in the AppDomain
        [Test]
        public void CoreAssembly_IsLoaded()
        {
            Assert.IsNotNull(_coreAsm,
                "UnityMCP.Playtest.Core assembly not found in AppDomain. " +
                "Check that UnityMCP.Editor.asmdef references it.");
        }

        // ISO-2: no reference to UnityEngine or UnityEditor (noEngineReferences contract).
        // Double-red: also fails if noEngineReferences is ever flipped back to false,
        // since the assembly would then pick up implicit UnityEngine/UnityEditor references.
        [Test]
        public void CoreAssembly_HasNoUnityEngineOrEditorReferences()
        {
            if (_coreAsm == null) Assert.Ignore("Assembly not loaded — see CoreAssembly_IsLoaded");
            foreach (var r in _coreAsm.GetReferencedAssemblies())
            {
                Assert.IsFalse(r.Name.StartsWith("UnityEngine", StringComparison.Ordinal),
                    $"UnityMCP.Playtest.Core must not reference UnityEngine (found: {r.Name})");
                Assert.IsFalse(r.Name.StartsWith("UnityEditor", StringComparison.Ordinal),
                    $"UnityMCP.Playtest.Core must not reference UnityEditor (found: {r.Name})");
            }
        }
    }
}
