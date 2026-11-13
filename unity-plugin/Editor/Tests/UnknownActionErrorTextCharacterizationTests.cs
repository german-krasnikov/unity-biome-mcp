// dsl-bottlenecks-2026-11 Part A characterization: pins the exact "Unknown action" error text
// ErrorHelper.InvalidAction emits for each of the 7 tools whose hand valid-list (an inline
// new[] {...} literal, or a named `static readonly string[] ValidActions` field) is being
// replaced by ToolActions.Names(cmd) in this slice — one fact in one place instead of a hand
// copy that can drift from the catalog's ACTION declaration (dsl/tools.biome).
//
// Dispatches a real "__no_such_action__" action through CommandRouter.ExecuteCommand exactly as
// the TCP bridge would (RegisterAll in SetUp, as AnimationHandlerArgsCharacterizationTests does),
// and asserts the full exception message against a literal string. The literal must stay
// byte-identical before and after the Part A swap: that is the whole point of this fixture.
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UnknownActionErrorTextCharacterizationTests : UnityMcpTestBase
    {
        private const string UnknownAction = "__no_such_action__";

        [SetUp]
        public void SetUp()
        {
            CommandRouter.RegisterAll();
        }

        private static string Args() => "{\"action\":\"" + UnknownAction + "\"}";

        [Test]
        public void Animation_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("animation", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: get|create|edit|add_key|remove_key|" +
                "remove_curve|set_keys|set_loop|set_wrap|set_framerate|preview|get_clip_path|" +
                "get_events|add_event|remove_event.",
                ex.Message);
        }

        [Test]
        public void Scene_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("scene", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: new|open|save|discard|open_additive|" +
                "close|set_active|list|save_copy.",
                ex.Message);
        }

        [Test]
        public void Timeline_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("timeline", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: get|create|edit|add_track|remove_track|" +
                "add_clip|remove_clip|set_binding|set_timing|mute|unmute|lock|unlock|rename_track|" +
                "reorder_track|duplicate_clip|add_marker|remove_marker|set_track_offset|set_duration|" +
                "add_sub_track|set_clip_in|get_bindings|preview.",
                ex.Message);
        }

        [Test]
        public void Shader_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("shader", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: get|create|set|graph_get|graph_create|" +
                "graph_node|graph_edge|graph_add_property|graph_remove_property|graph_rename_property|" +
                "graph_get_layout|graph_set_layout|graph_auto_layout.",
                ex.Message);
        }

        [Test]
        public void Asset_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                CommandRouter.ExecuteCommand("asset", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: find|get_info|create|move|validate_move|" +
                "duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|" +
                "import_package|read_text|write_text|reimport.",
                ex.Message);
        }

        [Test]
        public void ScriptableObject_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("scriptable_object", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: create|get|set|list_types|find.",
                ex.Message);
        }

        [Test]
        public void Prefab_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("prefab", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: save|create_variant|instantiate|apply|" +
                "revert|get_overrides|unpack|edit.",
                ex.Message);
        }

        // dsl-bottlenecks-2026-11 Part B1: EditorStateHelper.cs's hand valid-list omits
        // "mutation_mode", which ExecEditor (CommandRouter.MediaHandlers.cs) really dispatches via
        // its own if-chain before delegating to EditorStateHelper.Control. Swapping the hand list
        // for ToolActions.Names("editor") is a behaviour change: the printed text gains
        // "mutation_mode" (inserted right after "state", the catalog's own ACTION order).
        [Test]
        public void Editor_UnknownAction_ErrorTextListsAllDeclaredActionsIncludingMutationMode()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("editor", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: state|mutation_mode|play|pause|stop|" +
                "select|project_path.",
                ex.Message);
        }
    }
}
