// T2.5: Common test helpers for all tool card renderer test fixtures.
// Eliminates ~100 lines of identical setup across ScreenshotCardTests,
// HierarchyCardTests, and BashCardTests.
//
// Pattern:
//   public sealed class MyCardTests : ToolCardTestBase { ... }
//
// Provides:
//   AssertRegistered      — resolves and type-checks a registered renderer
//   AssertOnStartIsNoop   — verifies OnStart does not modify the chip
//   AssertGrouperBypass   — verifies two chips bypass the tool-group foldout
using System;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    public abstract class ToolCardTestBase : UnityMcpTestBase
    {
        /// <summary>
        /// Resolves <paramref name="toolName"/> from the registry and asserts the renderer
        /// exists and is an instance of <paramref name="expectedType"/>.
        /// RED if the [InitializeOnLoad] registration is removed.
        /// </summary>
        protected static void AssertRegistered(string toolName, Type expectedType)
        {
            var renderer = ToolCardRendererRegistry.Resolve(toolName);
            Assert.IsNotNull(renderer,
                $"{expectedType.Name} must be registered for '{toolName}' via [InitializeOnLoad]");
            Assert.IsInstanceOf(expectedType, renderer,
                $"Resolved renderer must be {expectedType.Name}");
        }

        /// <summary>
        /// Calls OnStart on a fresh chip and asserts it remains empty.
        /// </summary>
        protected static void AssertOnStartIsNoop(IToolCardRenderer card, string toolName)
        {
            var chip = new VisualElement();
            var rec  = new ToolCallRecord(toolName, "id-s", null);
            card.OnStart(chip, rec);
            Assert.AreEqual(0, chip.childCount, "OnStart must be a no-op — chip must remain empty");
        }

        /// <summary>
        /// Appends two tool chips for <paramref name="toolName"/> and asserts both appear as
        /// card-chip elements, not absorbed into a tool-group foldout.
        /// RED if the card renderer is unregistered.
        /// </summary>
        protected static void AssertGrouperBypass(string toolName, string id1, string id2)
        {
            var container  = new VisualElement();
            var registry   = ChatBlockRendererFactory.CreateDefault(null, null);
            var transcript = new ChatTranscript(container, registry);

            transcript.AppendToolChip(toolName, ok: true, toolId: id1);
            transcript.AppendToolChip(toolName, ok: true, toolId: id2);
            transcript.FinalizeAssistant();

            var cardChips = container.Query(className: "card-chip").ToList();
            Assert.AreEqual(2, cardChips.Count,
                $"Both {toolName} chips must bypass the grouper and appear as card-chip elements");

            var foldout = container.Q<Foldout>(className: "tool-group");
            if (foldout != null)
                Assert.IsNull(foldout.Q(className: "card-chip"),
                    "No card-chip may reside inside a collapsed tool-group foldout");
        }
    }
}
