// TDD tests for Step 1.3 — parser asmdef isolation.
// Verifies that UnityMCP.Editor.Chat.Parsers has no UnityEngine/UnityEditor references
// and contains the expected stub types.
using System;
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ParserAssemblyIsolationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private System.Reflection.Assembly _parsersAsm;

        [SetUp]
        public void SetUp()
        {
            _parsersAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "UnityMCP.Editor.Chat.Parsers");
        }

        // ISO-1: assembly is present in the AppDomain
        [Test]
        public void ParsersAssembly_IsLoaded()
        {
            Assert.IsNotNull(_parsersAsm,
                "UnityMCP.Editor.Chat.Parsers assembly not found in AppDomain. " +
                "Check that UnityMCP.Editor.Chat.CLI.asmdef references it.");
        }

        // ISO-2: no reference to UnityEngine or UnityEditor (noEngineReferences contract)
        [Test]
        public void ParsersAssembly_HasNoUnityEngineOrEditorReferences()
        {
            UnityMCP.Editor.Testing.AssemblyIsolationAssert.HasNoEngineReferences(
                "UnityMCP.Editor.Chat.Parsers");
        }

        // ISO-3: the three Phase-2/3 stubs are present
        [Test]
        public void ParsersAssembly_ContainsCodeEditArgsParser()
        {
            if (_parsersAsm == null) Assert.Ignore("Assembly not loaded — see ISO-1");
            var t = _parsersAsm.GetType("UnityMCP.Editor.Chat.Parsers.CodeEditArgsParser");
            Assert.IsNotNull(t, "CodeEditArgsParser not found in parsers assembly");
        }

        [Test]
        public void ParsersAssembly_ContainsObjectMutationParser()
        {
            if (_parsersAsm == null) Assert.Ignore("Assembly not loaded — see ISO-1");
            var t = _parsersAsm.GetType("UnityMCP.Editor.Chat.Parsers.ObjectMutationParser");
            Assert.IsNotNull(t, "ObjectMutationParser not found in parsers assembly");
        }

        [Test]
        public void ParsersAssembly_ContainsTodoTaskParser()
        {
            if (_parsersAsm == null) Assert.Ignore("Assembly not loaded — see ISO-1");
            var t = _parsersAsm.GetType("UnityMCP.Editor.Chat.Parsers.TodoTaskParser");
            Assert.IsNotNull(t, "TodoTaskParser not found in parsers assembly");
        }
    }
}
