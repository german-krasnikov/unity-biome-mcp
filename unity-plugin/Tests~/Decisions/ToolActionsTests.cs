using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Decisions.Tests
{
    // The generated ToolActions.Names/IsKnown (Editor/Generated/ToolActions.g.cs, printed from the ACTION lines of
    // dsl/tools.biome) against an independently written list of animation's 15 real dispatch actions
    // (CommandRouter.MediaHandlers.cs:152-166, ExecAnimationConsolidated) -- the same vocabulary that
    // server/tests/test_action_vocabulary_parity.py checks from the Python side.
    public sealed class ToolActionsTests
    {
        private static readonly string[] AnimationActions =
        {
            "get", "create", "edit", "add_key", "remove_key", "remove_curve",
            "set_keys", "set_loop", "set_wrap", "set_framerate", "preview",
            "get_clip_path", "get_events", "add_event", "remove_event",
        };

        [Test]
        public void Names_of_animation_is_the_15_actions_in_dispatch_order()
        {
            Assert.That(ToolActions.Names("animation"), Is.EqualTo(AnimationActions));
        }

        [Test]
        public void IsKnown_is_true_for_every_declared_action()
        {
            foreach (var action in AnimationActions)
                Assert.That(ToolActions.IsKnown("animation", action), Is.True, action);
        }

        [Test]
        public void IsKnown_is_case_sensitive()
        {
            foreach (var action in AnimationActions)
                Assert.That(ToolActions.IsKnown("animation", action.ToUpperInvariant()), Is.False, action);
        }

        [Test]
        public void IsKnown_is_false_for_an_unknown_tool_and_an_unknown_action()
        {
            Assert.That(ToolActions.IsKnown("nonexistent_tool", "get"), Is.False);
            Assert.That(ToolActions.IsKnown("animation", "bogus_action"), Is.False);
            Assert.That(ToolActions.Names("nonexistent_tool"), Is.Empty);
        }

        [Test]
        public void A_tool_without_any_ACTION_line_has_no_names()
        {
            Assert.That(ToolActions.Names("scene"), Is.Empty);
            Assert.That(ToolActions.IsKnown("scene", "list"), Is.False);
        }
    }
}
