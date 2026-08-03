// TDD RED → GREEN: ToLlmPayload must emit full Path in inline @-mentions, not DisplayName.
// UI display (ToDisplayText) stays unchanged — short name only.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipPayloadFullPathTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // ── helpers ─────────────────────────────────────────────────────────

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        private static string Payload(ChipData chip, string text = "что это?", ChipConfig cfg = null)
        {
            var msg = ChipTextInterleaver.Build(text, new List<PositionedChip> { PC(chip, 0) });
            return ChipTextInterleaver.ToLlmPayload(msg, cfg ?? new ChipConfig());
        }

        // ── Bug repro ────────────────────────────────────────────────────────

        // The exact user-confirmed bug: GridPlayer (hierarchy) + CommandRouterTests (script)
        // sent as "@GridPlayer @CommandRouterTests что это?" — both must use full path.
        [Test]
        public void UserScenario_GridPlayerAndScript_InlineMentionsUseFullPath()
        {
            var gridPlayer = H("/GridPlayer", "GridPlayer", 42);
            var tests      = S("Assets/Tests/Editor/CommandRouterTests.cs", "CommandRouterTests");
            var positioned = new List<PositionedChip> { PC(gridPlayer, 0), PC(tests, 0) };
            var msg        = ChipTextInterleaver.Build("что это?", positioned);
            var payload    = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());

            // Full paths in inline @-mentions
            StringAssert.Contains("@/GridPlayer", payload);
            StringAssert.Contains("@Assets/Tests/Editor/CommandRouterTests.cs", payload);

            // Bare short-name standalone mention must NOT be present as an inline token.
            // (The bracket line [hierarchy:/GridPlayer#42] also contains "GridPlayer" — exclude it.)
            // We check that "@GridPlayer " (name + space) doesn't appear before the bracket block.
            var textPart = payload.Split('\n')[0]; // first line = plain text portion
            StringAssert.DoesNotContain("@GridPlayer ", textPart);
            StringAssert.DoesNotContain("@CommandRouterTests ", textPart);
        }

        // ── Per-kind: inline @-mention must use Path ─────────────────────────

        [Test]
        public void Kind_Hierarchy_NestedPath_UsesFullPath()
        {
            var chip    = H("/Canvas/Panel/Header/Title", "Title", 1);
            var payload = Payload(chip);
            StringAssert.Contains("@/Canvas/Panel/Header/Title", payload);
        }

        [Test]
        public void Kind_Script_ProjectRelative_UsesFullPath()
        {
            var chip    = S("Assets/Tests/Editor/CommandRouterTests.cs", "CommandRouterTests");
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Tests/Editor/CommandRouterTests.cs", payload);
        }

        [Test]
        public void Kind_Folder_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Folder, "Assets/Scripts/Player", "Player", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Scripts/Player", payload);
        }

        [Test]
        public void Kind_ExternalFile_AbsolutePath_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Asset, "/Users/dev/Documents/design.pdf", "design.pdf", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@/Users/dev/Documents/design.pdf", payload);
        }

        [Test]
        public void Kind_Prefab_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Prefab, "Assets/Prefabs/Enemy.prefab", "Enemy", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Prefabs/Enemy.prefab", payload);
        }

        [Test]
        public void Kind_Material_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Material, "Assets/Materials/Rock.mat", "Rock", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Materials/Rock.mat", payload);
        }

        [Test]
        public void Kind_Scene_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Scene, "Assets/Scenes/Main.unity", "Main", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Scenes/Main.unity", payload);
        }

        [Test]
        public void Kind_Texture_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Texture, "Assets/Textures/Sky.png", "Sky", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Textures/Sky.png", payload);
        }

        [Test]
        public void Kind_ScriptableObject_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.ScriptableObject, "Assets/Data/Config.asset", "Config", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Data/Config.asset", payload);
        }

        [Test]
        public void Kind_PackageAsset_PackagePath_UsesFullPath()
        {
            var chip    = S("Packages/com.unity.mcp/Editor/Foo.cs", "Foo");
            var payload = Payload(chip);
            StringAssert.Contains("@Packages/com.unity.mcp/Editor/Foo.cs", payload);
        }

        [Test]
        public void Kind_GenericAsset_ProjectRelative_UsesFullPath()
        {
            var chip    = new ChipData(ChipKindKeys.Asset, "Assets/Data/GameConfig.asset", "GameConfig", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@Assets/Data/GameConfig.asset", payload);
        }

        // ── Edge cases ───────────────────────────────────────────────────────

        // Duplicate display names with different paths → both full paths, not "@Enemy @Enemy"
        [Test]
        public void Edge_DuplicateDisplayName_BothFullPaths()
        {
            var enemy1     = H("/World/Enemy", "Enemy", 1);
            var enemy2     = H("/UI/Enemy",    "Enemy", 2);
            var positioned = new List<PositionedChip> { PC(enemy1, 0), PC(enemy2, 0) };
            var msg        = ChipTextInterleaver.Build("check", positioned);
            var payload    = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());

            StringAssert.Contains("@/World/Enemy", payload);
            StringAssert.Contains("@/UI/Enemy",    payload);

            // Neither bare "@Enemy" (as standalone token) should appear in the text portion
            var textPart = payload.Split('\n')[0];
            StringAssert.DoesNotContain("@Enemy ", textPart);
        }

        // Root-level object: path "/MainCamera" → "@/MainCamera"
        [Test]
        public void Edge_RootLevelLeadingSlash_PathPreserved()
        {
            var chip    = H("/MainCamera", "MainCamera", -1);
            var payload = Payload(chip);
            StringAssert.Contains("@/MainCamera", payload);
        }

        // Name with spaces
        [Test]
        public void Edge_NameWithSpaces_FullPathPreserved()
        {
            var chip    = H("/UI Canvas/Main Camera", "Main Camera", -7);
            var payload = Payload(chip);
            StringAssert.Contains("@/UI Canvas/Main Camera", payload);
        }

        // Special characters in path
        [Test]
        public void Edge_SpecialCharsInPath_FullPathPreserved()
        {
            var chip    = H("/Objects/Boss_01 (Clone)", "Boss_01 (Clone)", 99);
            var payload = Payload(chip);
            StringAssert.Contains("@/Objects/Boss_01 (Clone)", payload);
        }

        // depth=none chip: context block suppressed, but inline @-mention still uses full path
        [Test]
        public void Edge_DepthNone_InlineStillUsesFullPath()
        {
            var chip = S("Assets/Scripts/Foo.cs", "Foo");
            var msg  = ChipTextInterleaver.Build("fix", new List<PositionedChip> { PC(chip, 0) });
            var cfg  = new ChipConfig { ScriptDepth = "none" };
            var payload = ChipTextInterleaver.ToLlmPayload(msg, cfg);

            StringAssert.Contains("@Assets/Scripts/Foo.cs", payload);
            Assert.IsFalse(payload.Contains("[script:"), "depth=none suppresses context block");
        }

        // No chips: plain text passes through unchanged
        [Test]
        public void Edge_NoChips_PlainTextUnchanged()
        {
            var msg     = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            Assert.AreEqual("hello world", payload);
        }

        // Empty path falls back to DisplayName (guard: chip with no path)
        [Test]
        public void Edge_EmptyPath_FallsBackToDisplayName()
        {
            var chip    = new ChipData(ChipKindKeys.Hierarchy, "", "OrphanObj", 0);
            var payload = Payload(chip);
            StringAssert.Contains("@OrphanObj", payload);
        }

        // ── GREEN GUARD: ToDisplayText must still use short name ─────────────

        [Test]
        public void DisplayText_StillUsesShortName_HierarchyChip()
        {
            var chip = H("/Environment/Enemies/GridPlayer", "GridPlayer", 42);
            var msg  = ChipTextInterleaver.Build("check", new List<PositionedChip> { PC(chip, 0) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            // Display must show short name, NOT full path
            StringAssert.Contains("@GridPlayer", display);
            StringAssert.DoesNotContain("@/Environment", display);
        }

        [Test]
        public void DisplayText_StillUsesShortName_ScriptChip()
        {
            var chip    = S("Assets/Scripts/Player/PlayerController.cs", "PlayerController");
            var msg     = ChipTextInterleaver.Build("fix", new List<PositionedChip> { PC(chip, 3) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("fix @PlayerController", display);
        }
    }
}
