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
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
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

        // dsl-bottlenecks-2026-11 Part A (slice R2): six more tools whose hand valid-list is
        // byte-identical in content and order to ToolActions.Names(cmd) -- pinned here BEFORE the
        // swap in CommandRouter.MediaHandlers.cs / MaterialHelper.cs / SpatialHelper.cs so the
        // same literal proves no observable change after it.
        [Test]
        public void Animator_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("animator", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: get|add_param|add_state|add_transition|" +
                "set_default|remove|add_blend_tree|edit_blend_tree|get_blend_tree|add_layer|remove_layer|" +
                "rename_layer|set_layer_weight|set_layer_blending|set_state_speed|update_transition|" +
                "set_avatar|rename_state|rename_param.",
                ex.Message);
        }

        [Test]
        public void Material_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("material", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: create|get|set|copy|list_properties|" +
                "list_slots|get_errors|list_shaders|set_fields.",
                ex.Message);
        }

        [Test]
        public void Menu_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("menu", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: execute|list.",
                ex.Message);
        }

        [Test]
        public void Particle_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("particle", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: get|create|set|apply|play|stop|pause.",
                ex.Message);
        }

        [Test]
        public void References_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("references", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: get|find_to|remap.",
                ex.Message);
        }

        [Test]
        public void SpatialQuery_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("spatial_query", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: nearest|in_front_of|objects_in_radius|" +
                "bounds_info|raycast|spatial_map|objects_in_polygon.",
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

        // dsl-bottlenecks-2026-11 Part B2: RenderAnalyzer.cs's default arm threw a free-standing
        // interpolated string (its own hand order, no trailing period) instead of calling
        // ErrorHelper.InvalidAction -- so it had no single source for its valid list either. The
        // exception type (InvalidOperationException) is kept: RenderAnalyzerTests.cs asserts it.
        [Test]
        public void RenderAnalyze_UnknownAction_ErrorTextListsAllDeclaredActionsInCatalogOrder()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() =>
                CommandRouter.ExecuteCommand("render_analyze", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: stats|materials|shaders|overdraw|audit|" +
                "compare|batching|lights|shadow_audit|probe_audit|light_optimize|frame_debug.",
                ex.Message);
        }

        // dsl-bottlenecks-2026-11 Part B2: UIHelper.UIToolkit.cs's default arm returned a bare
        // interpolated string, space-separated, with no catalog valid-list at all. UitkElement
        // returns its error as a plain string (never throws for an unknown action), and that
        // string keeps its "err:" prefix -- callers/batch rely on it (UIPanelHost.cs:22,
        // BatchHelper's per-line ok:/err: check) -- only the "Valid: ..." wording now comes from
        // the catalog. A real UIDocument + named child VisualElement is required to reach the
        // action switch at all: with no resolvable path/ref, UitkElement returns a different
        // "err:" (path or ref is required) before ever reaching it.
        [Test]
        public void UitkElement_UnknownAction_ReturnedErrTextListsAllDeclaredActions()
        {
            // Realizing a UIDocument panel for the first time in a project makes Unity
            // materialize a default theme asset under Assets/UI Toolkit/UnityThemes/ -- a real
            // disk write, unconditional even with a themeStyleSheet pre-assigned, outside
            // Assets/TestsTemp/ (so TrackOwnedAsset cannot register it). This test fixture ships
            // inside the plugin (unity-plugin/Editor/Tests) and can run in a project where
            // Assets/UI Toolkit already holds the user's own themes -- the cleanup below records
            // the exact pre-test contents and deletes only what this test newly created, never a
            // pre-existing file, folder, or the folder itself if it already existed.
            RegisterUiToolkitCleanup();

            var go = TrackOwnedObject(new GameObject("UitkElementUnknownActionProbe"));
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = TrackOwnedObject(ScriptableObject.CreateInstance<PanelSettings>());
            var probe = new VisualElement { name = "ProbeChild" };
            document.rootVisualElement.Add(probe);

            var result = CommandRouter.ExecuteCommand("uitk_element",
                "{\"action\":\"" + UnknownAction + "\",\"path\":\"/" + go.name + "\",\"name\":\"ProbeChild\"}");

            Assert.AreEqual(
                "err: Unknown action '__no_such_action__'. Valid: query|get|set_style|add_class|" +
                "remove_class|get_style|enable|disable.",
                result);
        }

        // dsl-bottlenecks-2026-11 Part B3: UIFileHelper.cs's default arm (EditUIFile:120) returned a bare
        // interpolated string naming only the 7 surgical actions its own switch reaches, not the tool's full
        // 12-action vocabulary (read/revert/write/create_uxml/create_uss are intercepted earlier, by
        // ExecUitkFile's if-chain, CommandRouter.MediaHandlers.cs:296-304). UitkFile returns its error as a
        // plain string (never throws), keeping the "err:" prefix callers/batch detect (same contract as
        // UitkElement). EditUIFile requires the target file to exist before reaching the action switch at all
        // (UIFileHelper.cs:104), so this test creates a real file under Assets/TestsTemp first.
        [Test]
        public void UitkFile_UnknownAction_ReturnedErrTextListsAllDeclaredActions()
        {
            const string assetPath = "Assets/TestsTemp/UitkFileUnknownActionProbe.uxml";
            TrackOwnedAsset(assetPath);
            var abs = AbsoluteAssetPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
            File.WriteAllText(abs, "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"/>", System.Text.Encoding.UTF8);

            var result = CommandRouter.ExecuteCommand("uitk_file",
                "{\"action\":\"" + UnknownAction + "\",\"path\":\"" + assetPath + "\"}");

            Assert.AreEqual(
                "err: Unknown action '__no_such_action__'. Valid: read|revert|write|create_uxml|create_uss|" +
                "set-attr|add-class|remove-class|add-element|remove-element|set-rule|remove-rule.",
                result);
        }

        // dsl-bottlenecks-2026-11 Part B (slice R2): BuildHelper.cs's `if (action != "build")` bail-out
        // (line 19-23) sets inner.TrySetResult($"err:invalid action '{action}'") -- no catalog list, lowercase
        // "invalid", no Valid: suffix. Async (TaskCompletionSource) handler: calling BuildHelper.Execute
        // directly resolves `inner` synchronously for this branch (no MainThreadDispatcher.Enqueue reached),
        // so awaiting tcs.Task completes immediately with no polling -- `async Task` + `await`, never
        // the banned blocking `.Result` (AI/testing.md). "err:" prefix kept, no space -- matches this
        // file's own other err: results (line 44) and AsyncBuild's own `r.StartsWith("err:")` check.
        [Test]
        public async Task Build_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var tcs = new TaskCompletionSource<string>();
            BuildHelper.Execute(UnknownAction, null, null, null, false, tcs);
            var result = await tcs.Task;
            Assert.AreEqual("err:Unknown action '__no_such_action__'. Valid: build.", result);
        }

        // dsl-bottlenecks-2026-11 Part B (slice R2): ObjectManager.ManageComponent's else-branch
        // (ObjectManager.cs:242-245) throws $"Invalid action: {action}" -- no catalog list. FindObjectOrThrow
        // (ObjectManager.cs:213) resolves `path` before the action check, so a real GameObject is required.
        [Test]
        public void ManageComponent_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var go = TrackOwnedObject(new GameObject("ManageComponentUnknownActionProbe"));
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("manage_component",
                    "{\"action\":\"" + UnknownAction + "\",\"path\":\"/" + go.name + "\",\"type\":\"Rigidbody\"}"));
            Assert.AreEqual("Unknown action '__no_such_action__'. Valid: add|remove.", ex.Message);
        }

        // dsl-bottlenecks-2026-11 Part B (slice R2): MaterialAuditHelper.cs's default arm (line 27) already
        // returned "err:Unknown action '{action}'. Valid: <catalog-order pipe list>" -- byte-for-byte the
        // catalog's own ACTION order, just hand-written and missing the trailing period that
        // ErrorHelper.InvalidAction always appends. Returns a plain string (never throws); "err:" prefix kept,
        // no space, matching its own prior literal.
        [Test]
        public void MaterialAudit_UnknownAction_ReturnedErrTextListsAllDeclaredActions()
        {
            var result = CommandRouter.ExecuteCommand("material_audit", Args());
            Assert.AreEqual(
                "err:Unknown action '__no_such_action__'. Valid: summary|materials|textures|duplicates|" +
                "compression|recommendations.",
                result);
        }

        // dsl-bottlenecks-2026-11 Part B (slice R2): PackageManagerHelper.cs's switch default (line 38-40)
        // sets inner.TrySetResult($"err:invalid action '{action}'") -- same shape as BuildHelper.cs. The
        // default arm resolves `inner` synchronously inside Execute itself (the MainThreadDispatcher.Enqueue
        // wrapping happens one level up, in AsyncPackage, around the whole Execute call) -- `async Task` +
        // `await tcs.Task` completes immediately, no polling, never the banned blocking `.Result`.
        [Test]
        public async Task Package_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var tcs = new TaskCompletionSource<string>();
            PackageManagerHelper.Execute(UnknownAction, null, null, null, tcs);
            var result = await tcs.Task;
            Assert.AreEqual(
                "err:Unknown action '__no_such_action__'. Valid: list|search|add|remove.",
                result);
        }

        // dsl-bottlenecks-2026-11 Part B (slice R2): Profiling/ProfileRecorder.cs's switch-expression default
        // (line 45) threw $"Unknown profile action: {action}" -- its own hand wording, no catalog list, no
        // quotes around the action. Dispatch(action, args) is reached directly via RegisterAction("profile",
        // ...), same two-arg shape as Part A's "material"/"spatial_query" -- no object resolution first.
        [Test]
        public void Profile_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("profile", Args()));
            Assert.AreEqual(
                "Unknown action '__no_such_action__'. Valid: start|stop|status|analyze|compare|list_sessions.",
                ex.Message);
        }

        // dsl-bottlenecks-2026-11 Part B (slice R2): ObjectManager.Transfer.cs's switch default
        // (ObjectManager.Transfer.cs:81-82) threw $"Invalid action: {action}. Must be move or copy" -- its own
        // hand wording, no catalog list. FindObjectOrThrow (line 37) resolves `sourcePath` before the action
        // switch, so a real GameObject is required.
        [Test]
        public void TransferObject_UnknownAction_ErrorTextListsAllDeclaredActions()
        {
            var go = TrackOwnedObject(new GameObject("TransferObjectUnknownActionProbe"));
            var ex = Assert.Throws<System.ArgumentException>(() =>
                CommandRouter.ExecuteCommand("transfer_object",
                    "{\"action\":\"" + UnknownAction + "\",\"path\":\"/" + go.name + "\"}"));
            Assert.AreEqual("Unknown action '__no_such_action__'. Valid: move|copy.", ex.Message);
        }

        // Proves the cleanup algorithm itself keeps a pre-existing sibling untouched when the
        // new content lands inside an already-existing Assets/UI Toolkit (the real-project shape
        // the owner's safety review asked to prove: these fixtures ship inside the plugin and can
        // run in a project where the user already has their own themes there). Deterministic and
        // self-contained -- it does not depend on the live UIDocument/PanelSettings bootstrap
        // above re-triggering, which was observed not to reliably recreate the default theme once
        // Unity's own project/session state has already resolved one.
        [Test]
        public void UiToolkitCleanup_PreservesPreexistingSibling_RemovesOnlyTheNewSubfolder()
        {
            RegisterUiToolkitCleanup();  // suite's own baseline: Assets/UI Toolkit does not exist yet

            // Simulate a project that already has its own UI Toolkit folder with real user
            // content -- a plain file, not a ScriptableObject asset (ThemeStyleSheet/most
            // UnityEngine.Object types refuse a generic AssetDatabase.CreateAsset; a plain file
            // + ImportAsset proves the same diff logic without that noise).
            var rootAbs = AbsoluteAssetPath(UiToolkitAssetRoot);
            AssetDatabase.CreateFolder("Assets", "UI Toolkit");
            var userAssetPath = UiToolkitAssetRoot + "/UserOwnedTheme.txt";
            File.WriteAllText(AbsoluteAssetPath(userAssetPath), "user owned content");
            AssetDatabase.ImportAsset(userAssetPath);

            // What UitkElement's own cleanup registration would snapshot at this point: the
            // user's pre-existing content, taken as the baseline for the diff.
            var dirsBefore = SnapshotDirectories(rootAbs);
            var filesBefore = SnapshotFiles(rootAbs);

            // Simulate exactly the shape Unity's theme bootstrap produces: a brand-new subfolder
            // (not a loose file) inside the already-existing root.
            var newAssetPath = UiToolkitAssetRoot + "/UnityThemes/UnityDefaultRuntimeTheme.txt";
            Directory.CreateDirectory(AbsoluteAssetPath(UiToolkitAssetRoot + "/UnityThemes"));
            File.WriteAllText(AbsoluteAssetPath(newAssetPath), "simulated default theme");
            AssetDatabase.ImportAsset(newAssetPath);

            DeleteOnlyWhatIsNew(UiToolkitAssetRoot, rootAbs, dirsBefore, filesBefore);

            Assert.IsTrue(AssetDatabase.IsValidFolder(UiToolkitAssetRoot), "the pre-existing root must survive");
            Assert.IsTrue(File.Exists(AbsoluteAssetPath(userAssetPath)), "the pre-existing user asset must survive");
            Assert.IsFalse(Directory.Exists(AbsoluteAssetPath(UiToolkitAssetRoot + "/UnityThemes")),
                "the newly created subfolder must be gone");
        }

        private const string UiToolkitAssetRoot = "Assets/UI Toolkit";

        // Before/after diff instead of TrackOwnedAsset: Unity materializes the default UI Toolkit theme under
        // Assets/UI Toolkit unconditionally (outside Assets/TestsTemp/) the first time a UXML/USS asset is
        // touched, so there is no single path for TrackOwnedAsset to own in advance. Snapshotting before and
        // after removes only entries that were absent before this test -- any pre-existing root/user asset
        // under Assets/UI Toolkit survives.
        private void RegisterUiToolkitCleanup()
        {
            var rootAbs = AbsoluteAssetPath(UiToolkitAssetRoot);
            var dirsBefore = SnapshotDirectories(rootAbs);
            var filesBefore = SnapshotFiles(rootAbs);
            RegisterCleanup(() => DeleteOnlyWhatIsNew(UiToolkitAssetRoot, rootAbs, dirsBefore, filesBefore));
        }

        private static string AbsoluteAssetPath(string assetRelativePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetRelativePath);
        }

        private static HashSet<string> SnapshotDirectories(string absolutePath)
        {
            var found = new HashSet<string>();
            void Walk(string dir)
            {
                if (!Directory.Exists(dir)) return;
                found.Add(dir);
                foreach (var sub in Directory.GetDirectories(dir)) Walk(sub);
            }
            Walk(absolutePath);
            return found;
        }

        private static HashSet<string> SnapshotFiles(string absolutePath) =>
            Directory.Exists(absolutePath)
                ? new HashSet<string>(Directory.GetFiles(absolutePath, "*", SearchOption.AllDirectories))
                : new HashSet<string>();

        // Deletes only what did not exist before this test, diffed against `dirsExisted`/
        // `filesExisted`. A directory absent from `dirsExisted` is deleted wholesale (everything
        // inside a newly created directory must itself be new); a pre-existing directory is only
        // recursed into, never deleted itself, so a sibling the test did not create is untouched.
        private static void DeleteOnlyWhatIsNew(string assetPath, string absolutePath,
            HashSet<string> dirsExisted, HashSet<string> filesExisted)
        {
            if (!Directory.Exists(absolutePath)) return;
            if (!dirsExisted.Contains(absolutePath))
            {
                AssetDatabase.DeleteAsset(assetPath);
                return;
            }
            foreach (var childDir in Directory.GetDirectories(absolutePath))
                DeleteOnlyWhatIsNew(assetPath + "/" + Path.GetFileName(childDir), childDir,
                    dirsExisted, filesExisted);
            foreach (var childFile in Directory.GetFiles(absolutePath))
            {
                if (childFile.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (filesExisted.Contains(childFile)) continue;
                AssetDatabase.DeleteAsset(assetPath + "/" + Path.GetFileName(childFile));
            }
        }
    }
}
