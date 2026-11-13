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
            Assert.That(ToolActions.Names("material"), Is.Empty);
            Assert.That(ToolActions.IsKnown("material", "get"), Is.False);
        }

        // Independent literal expectation, smallest of the dsl-bottlenecks-2026-11 Q1 batch: 5 actions
        // (ScriptableObjectHelper.cs:12,16-22, ScriptableObjectHelper.Execute's switch).
        private static readonly string[] ScriptableObjectActions =
        {
            "create", "get", "set", "list_types", "find",
        };

        [Test]
        public void Names_of_scriptable_object_is_the_5_actions_in_dispatch_order()
        {
            Assert.That(ToolActions.Names("scriptable_object"), Is.EqualTo(ScriptableObjectActions));
        }

        // Independent literal expectation, largest of the dsl-bottlenecks-2026-11 Q1 batch: 24 actions
        // (CommandRouter.MediaHandlers.cs:175-201, ExecTimelineConsolidated's switch -- "edit" plus the 18 names
        // OR'd into the same arm as "edit", plus set_clip_in/get_bindings/preview/get/create).
        private static readonly string[] TimelineActions =
        {
            "get", "create", "edit", "add_track", "remove_track", "add_clip", "remove_clip",
            "set_binding", "set_timing", "mute", "unmute", "lock", "unlock", "rename_track",
            "reorder_track", "duplicate_clip", "add_marker", "remove_marker", "set_track_offset",
            "set_duration", "add_sub_track", "set_clip_in", "get_bindings", "preview",
        };

        [Test]
        public void Names_of_timeline_is_the_24_actions_in_dispatch_order()
        {
            Assert.That(ToolActions.Names("timeline"), Is.EqualTo(TimelineActions));
        }

        // dsl-bottlenecks-2026-11 Part B3: uitk_file is the first tool whose ACTION name contains a hyphen
        // (CONSTRUCT TOOL's pattern was widened to admit one) -- an independent literal check that the
        // generated table returns it verbatim, not split or re-cased by anything downstream.
        [Test]
        public void Names_of_uitk_file_returns_a_hyphenated_action_name_verbatim()
        {
            Assert.That(ToolActions.Names("uitk_file"), Does.Contain("set-attr"));
        }
    }
}
