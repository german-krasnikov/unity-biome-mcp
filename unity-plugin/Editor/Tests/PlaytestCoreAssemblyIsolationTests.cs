// TDD for D01 — UnityMCP.Playtest.Core asmdef isolation.
// Verifies that UnityMCP.Playtest.Core has no UnityEngine/UnityEditor references
// (noEngineReferences contract), mirroring the precedent at
// unity-plugin/Editor/Chat/Tests/CLI/ParserAssemblyIsolationTests.cs.
using System;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Playtest.Core;

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
            UnityMCP.Editor.Testing.AssemblyIsolationAssert.HasNoEngineReferences(
                "UnityMCP.Playtest.Core");
        }

        // D07 — no Core type may keep the temporary UnityMCP.Editor namespace D01/D05 left in
        // place; every type here must be UnityMCP.Playtest.Core. Double-red: red the moment any
        // Core type's namespace regresses back to UnityMCP.Editor.
        [Test]
        public void CoreAssembly_ContainsNoUnityMcpEditorNamespace()
        {
            Assert.IsNotNull(_coreAsm, "UnityMCP.Playtest.Core assembly not found.");
            var offenders = _coreAsm.GetTypes()
                .Where(t => t.Namespace == "UnityMCP.Editor")
                .Select(t => t.FullName)
                .ToArray();
            Assert.IsEmpty(offenders,
                "Core types must live in UnityMCP.Playtest.Core, not UnityMCP.Editor: " +
                string.Join(", ", offenders));
        }

        // D07 — Core must never reference back into the assembly it was extracted from
        // (Editor already references Core; the reverse would be circular). Regression guard,
        // not expected to ever go red under normal development — double-red: red if Core ever
        // gains an assembly reference to UnityMCP.Editor.
        [Test]
        public void CoreAssembly_DoesNotReferenceEditorAssembly()
        {
            Assert.IsNotNull(_coreAsm, "UnityMCP.Playtest.Core assembly not found.");
            var refs = _coreAsm.GetReferencedAssemblies().Select(n => n.Name);
            Assert.IsFalse(refs.Contains("UnityMCP.Editor"),
                "UnityMCP.Playtest.Core must not reference UnityMCP.Editor.");
        }

        // D07 — the 9 externally-required Core contract members must be public: StepType,
        // SourcedLine, PlaytestStep, ParseResult, PlaytestHeader, IncludeResolver, the
        // PlaytestParser class itself, and its Compare/SplitTokens static methods (required by
        // the Player, D13/D14). Double-red: red if any of these stays internal.
        [Test]
        public void CoreContract_NineExternalMembers_ArePublic()
        {
            Assert.IsNotNull(_coreAsm, "UnityMCP.Playtest.Core assembly not found.");
            AssertPublicType("UnityMCP.Playtest.Core.StepType");
            AssertPublicType("UnityMCP.Playtest.Core.SourcedLine");
            AssertPublicType("UnityMCP.Playtest.Core.PlaytestStep");
            AssertPublicType("UnityMCP.Playtest.Core.ParseResult");
            AssertPublicType("UnityMCP.Playtest.Core.PlaytestHeader");
            AssertPublicType("UnityMCP.Playtest.Core.IncludeResolver");
            var parserType = AssertPublicType("UnityMCP.Playtest.Core.PlaytestParser");

            var compare = parserType.GetMethod("Compare",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(compare, "PlaytestParser.Compare must be a public static method.");

            var splitTokens = parserType.GetMethod("SplitTokens",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(splitTokens, "PlaytestParser.SplitTokens must be a public static method.");
        }

        private Type AssertPublicType(string fullName)
        {
            var t = _coreAsm.GetType(fullName);
            Assert.IsNotNull(t, $"{fullName} not found in UnityMCP.Playtest.Core.");
            Assert.IsTrue(t.IsPublic, $"{fullName} must be public (found: {(t.IsNotPublic ? "internal" : t.Attributes.ToString())}).");
            return t;
        }
    }
}
