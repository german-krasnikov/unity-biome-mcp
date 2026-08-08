using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Command
{
    public class CommandRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(CommandRegistry.InitDefaults);
            CommandRegistry.Clear();
        }

        [Test]
        public void Register_And_Execute()
        {
            CommandRegistry.Register("test_cmd", args => "result:" + args);
            Assert.AreEqual("result:hello", CommandRegistry.Execute("test_cmd", "hello"));
        }

        [Test]
        public void RegisterAction_ExtractsAction()
        {
            CommandRegistry.RegisterAction("my_tool",
                (action, args) => $"action={action}");
            var args = "{\"action\":\"do_thing\"}";
            Assert.AreEqual("action=do_thing", CommandRegistry.Execute("my_tool", args));
        }

        [Test]
        public void IsMutating_ReturnsTrueForMutating()
        {
            CommandRegistry.Register("safe_cmd", _ => "ok", mutating: false);
            CommandRegistry.Register("dangerous_cmd", _ => "ok", mutating: true);
            Assert.IsFalse(CommandRegistry.IsMutating("safe_cmd"));
            Assert.IsTrue(CommandRegistry.IsMutating("dangerous_cmd"));
        }

        [Test]
        public void IsMutating_ReturnsFalseForUnknown()
        {
            Assert.IsFalse(CommandRegistry.IsMutating("nonexistent"));
        }

        [Test]
        public void Execute_UnknownCommand_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => CommandRegistry.Execute("nonexistent", ""));
        }

        [Test]
        public void IsRegistered_Works()
        {
            Assert.IsFalse(CommandRegistry.IsRegistered("foo"));
            CommandRegistry.Register("foo", _ => "ok");
            Assert.IsTrue(CommandRegistry.IsRegistered("foo"));
        }

        [Test]
        public void Register_SameName_KeepsFirst()
        {
            // Security: duplicate registration is silently rejected (prevents command hijacking).
            CommandRegistry.Register("cmd", _ => "v1");
            CommandRegistry.Register("cmd", _ => "v2");
            Assert.AreEqual("v1", CommandRegistry.Execute("cmd", ""),
                "Duplicate registration must not overwrite the original handler");
        }

        [Test]
        public void GetAllCommands_ReturnsRegistered()
        {
            CommandRegistry.Register("a", _ => "");
            CommandRegistry.Register("b", _ => "");
            CollectionAssert.AreEquivalent(
                new[] { "a", "b" },
                new System.Collections.Generic.List<string>(CommandRegistry.GetAllCommands()));
        }
        [Test]
        public void RegisterAction_MissingAction_ThrowsArgumentException()
        {
            CommandRegistry.RegisterAction("my_tool", (action, args) => action);
            Assert.Throws<System.ArgumentException>(
                () => CommandRegistry.Execute("my_tool", "{}"));
        }

        [Test]
        public void InitDefaults_RegistersPhase26AsMutating()
        {
            CommandRegistry.InitDefaults();
            Assert.IsTrue(CommandRegistry.IsMutating("asset"));
            Assert.IsTrue(CommandRegistry.IsMutating("material"));
            Assert.IsTrue(CommandRegistry.IsMutating("prefab"));
            Assert.IsTrue(CommandRegistry.IsMutating("project_settings"));
            Assert.IsTrue(CommandRegistry.IsMutating("scriptable_object"));
        }

        [Test]
        public void InitDefaults_RegistersMutatingWriteCommands()
        {
            CommandRegistry.InitDefaults();
            // batch is no longer mutating (Phase 31b: per-command guards)
            // scene is no longer mutating (P-414: undo group around save clears isDirty in Unity 6;
            //   mutating actions — new/open/discard/close — record mutation explicitly inside ExecScene)
            foreach (var cmd in new[] { "create_object", "delete_object", "set_property", "set_active",
                "wire_event", "manage_component", "set_material",
                "create_ui", "set_rect", "animator", "particle", "shader", "menu",
                "animation", "references" })
            {
                Assert.IsTrue(CommandRegistry.IsMutating(cmd), $"{cmd} should be mutating");
            }
        }

        [Test]
        public void InitDefaults_RegistersReadCommandsAsNonMutating()
        {
            CommandRegistry.InitDefaults();
            foreach (var cmd in new[] { "ping", "get_version", "get_hierarchy", "get_component",
                "get_components_list", "get_object_detail", "find_objects", "get_console",
                "recompile", "search_scene", "get_enabled_tools", "editor", "inspect",
                "validate_references", "checkpoint", "timeline",
                "scene" /* P-414: per-action mutation; registry flag is false */ })
            {
                Assert.IsFalse(CommandRegistry.IsMutating(cmd), $"{cmd} should NOT be mutating");
            }
        }

        [Test]
        public void InitDefaults_RegistersAllExpectedCommands()
        {
            CommandRegistry.InitDefaults();
            // C7: get_version is intentionally NOT in CommandRegistry — MCPServer fast-path owns it.
            var expected = new[] {
                "ping", "get_enabled_tools", "get_hierarchy", "get_component",
                "get_components_list", "get_object_detail", "find_objects", "get_console",
                "screenshot", "recompile", "search_scene", "editor", "inspect",
                "validate_references", "checkpoint", "run_tests",
                "create_object", "delete_object", "set_property", "set_active", "wire_event",
                "manage_component", "set_material", "batch", "scene", "animation", "timeline",
                "references", "create_ui", "set_rect", "animator", "particle", "shader", "menu",
                "asset", "project_settings", "material", "prefab", "scriptable_object"
            };
            foreach (var cmd in expected)
                Assert.IsTrue(CommandRegistry.IsRegistered(cmd), $"{cmd} should be registered");
        }

        [Test]
        public void Register_RuntimeTrue_IsRuntimeReturnsTrue()
        {
            CommandRegistry.Register("test_rt", _ => "ok", runtime: true);
            Assert.IsTrue(CommandRegistry.IsRuntime("test_rt"));
        }

        [Test]
        public void Register_RuntimeFalse_IsRuntimeReturnsFalse()
        {
            CommandRegistry.Register("test_nrt", _ => "ok");
            Assert.IsFalse(CommandRegistry.IsRuntime("test_nrt"));
        }

        [Test]
        public void MutatingCommand_IsRuntimeFalse()
        {
            CommandRegistry.Register("test_mut_only", _ => "ok", mutating: true, runtime: false);
            Assert.IsTrue(CommandRegistry.IsMutating("test_mut_only"));
            Assert.IsFalse(CommandRegistry.IsRuntime("test_mut_only"));
        }

        [Test]
        public void RuntimeCommand_IsMutatingFalse()
        {
            CommandRegistry.Register("test_rt_only", _ => "ok", mutating: false, runtime: true);
            Assert.IsTrue(CommandRegistry.IsRuntime("test_rt_only"));
            Assert.IsFalse(CommandRegistry.IsMutating("test_rt_only"));
        }

        [Test]
        public void RegisterAll_RuntimeCommands_AreRuntime()
        {
            CommandRegistry.InitDefaults();
            foreach (var cmd in new[] { "invoke_method", "wait_until", "query_state", "move_to", "test_step", "set_runtime_property" })
                Assert.IsTrue(CommandRegistry.IsRuntime(cmd), $"{cmd} should be runtime");
        }

        [Test]
        public void Clear_DoesNotRegisterPluginCommands()
        {
            CommandRegistry.Clear();
            Assert.IsFalse(CommandRegistry.IsRegistered("nonexistent"), "Unknown cmd must not be registered after Clear()");
        }
    }
}
