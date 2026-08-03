// TDD — RED first. Tests for SlashRegistry (Feature #12).
// Pure logic: no EditorWindow, uses gatherOverride seam for context calls.
using NUnit.Framework;
using System;
using System.Linq;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SlashRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Match_EmptyPrefix_ReturnsAllBuiltins()
        {
            var results = SlashRegistry.Match("");
            Assert.AreEqual(SlashRegistry.Builtins.Length, results.Count);
        }

        [Test]
        public void Match_ExactName_ReturnsSingleMatch()
        {
            var results = SlashRegistry.Match("fix-compile");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("fix-compile", results[0].Name);
        }

        [Test]
        public void Match_PartialPrefix_ReturnsFilteredSet()
        {
            var results = SlashRegistry.Match("fix");
            Assert.IsTrue(results.Count >= 1);
            Assert.IsTrue(results.All(t => t.Name.StartsWith("fix")));
        }

        [Test]
        public void Match_CaseInsensitive_Matches()
        {
            var lower = SlashRegistry.Match("FIX-COMPILE");
            Assert.AreEqual(1, lower.Count);
            Assert.AreEqual("fix-compile", lower[0].Name);
        }

        [Test]
        public void Match_UnknownPrefix_ReturnsEmpty()
        {
            var results = SlashRegistry.Match("zzznomatch");
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Match_SlashOnly_ReturnsAllBuiltins()
        {
            var results = SlashRegistry.Match("/");
            Assert.AreEqual(SlashRegistry.Builtins.Length, results.Count);
        }

        [Test]
        public void Match_NullPrefix_ReturnsAllBuiltins()
        {
            var results = SlashRegistry.Match(null);
            Assert.AreEqual(SlashRegistry.Builtins.Length, results.Count);
        }

        [Test]
        public void Resolve_FixCompile_ContainsPrefillAndErrors()
        {
            var t = SlashRegistry.Builtins.First(b => b.Name == "fix-compile");
            var result = SlashRegistry.Resolve(t, _ => "[ERRORS]");
            StringAssert.Contains(t.Prefill, result);
            StringAssert.Contains("[ERRORS]", result);
        }

        [Test]
        public void Resolve_AddComponent_ContainsSelectionSummary()
        {
            var t = SlashRegistry.Builtins.First(b => b.Name == "add-component");
            var result = SlashRegistry.Resolve(t, _ => "[SEL]");
            StringAssert.Contains(t.Prefill, result);
            StringAssert.Contains("[SEL]", result);
        }

        [Test]
        public void Resolve_NoGather_ReturnsPrefillOnly()
        {
            var t = SlashRegistry.Builtins.First(b => b.Name == "screenshot");
            // screenshot has ContextGather.None
            var result = SlashRegistry.Resolve(t, _ => "[SHOULD_NOT_APPEAR]");
            Assert.AreEqual(t.Prefill, result);
        }

        [Test]
        public void Resolve_WithGatherOverride_UsesInjectedContext()
        {
            var t = SlashRegistry.Builtins.First(b => b.Name == "inspect");
            var result = SlashRegistry.Resolve(t, _ => "injected-ctx");
            StringAssert.Contains("injected-ctx", result);
        }

        [Test]
        public void Resolve_Selection_WhenNull_OmitsSelectionLine()
        {
            var t = SlashRegistry.Builtins.First(b => b.Name == "inspect");
            var result = SlashRegistry.Resolve(t, _ => null);
            // null gather → omit context line, just prefill
            Assert.AreEqual(t.Prefill, result);
        }

        [Test]
        public void Builtins_AllHaveNonEmptyName()
        {
            foreach (var t in SlashRegistry.Builtins)
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Name), $"Template has empty Name");
        }

        [Test]
        public void Builtins_AllHaveNonEmptyPrefill()
        {
            foreach (var t in SlashRegistry.Builtins)
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Prefill), $"'{t.Name}' has empty Prefill");
        }

        [Test]
        public void Builtins_NoDuplicateNames()
        {
            var names = SlashRegistry.Builtins.Select(t => t.Name).ToList();
            var distinct = names.Distinct().ToList();
            Assert.AreEqual(distinct.Count, names.Count, "Duplicate template names found");
        }

        [Test]
        public void Resolve_GatherThrows_FallsBackToContextUnavailable()
        {
            var t = SlashRegistry.Builtins.First(b => b.Name == "fix-compile");
            var result = SlashRegistry.Resolve(t, _ => throw new Exception("boom"));
            StringAssert.Contains(t.Prefill, result);
            StringAssert.Contains("(context unavailable)", result);
        }
    }
}
