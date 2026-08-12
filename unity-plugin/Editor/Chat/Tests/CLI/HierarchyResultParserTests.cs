// TDD T2.2a — HierarchyResultParser: text-tree parse from get_hierarchy output.
//
// Real C# format (from HierarchySerializer.AppendIndent):
//   Root: "Name $HexRef"           (no connector prefix)
//   Depth 1: "│  └─ Name $HexRef" or "   └─ Name $HexRef"
//   Depth N: N × 3-char groups + connector
//
// Double-red requirement:
//   A — corrupt any Assert → test RED
//   B — stub Parse() to return [] → all structure/field tests RED
//
// Data: REAL C# serializer format strings with Cyrillic, escaped slashes,
// inactive markers, hidden counts, multi-scene headers, and truncation.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class HierarchyResultParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Sentinel inputs ────────────────────────────────────────────────────

        [Test]
        public void Parse_Empty_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, HierarchyResultParser.Parse("").Length);
        }

        [Test]
        public void Parse_Null_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, HierarchyResultParser.Parse(null).Length);
        }

        [Test]
        public void Parse_NoChange_ReturnsEmptyArray()
        {
            Assert.AreEqual(0, HierarchyResultParser.Parse("NO_CHANGE").Length);
        }

        // ── Root nodes (depth 0) ───────────────────────────────────────────────

        [Test]
        public void Parse_SingleRootNode_NameHexRefDepth()
        {
            // "Main Camera $A1B2C3" — real single-scene root output
            var nodes = HierarchyResultParser.Parse("Main Camera $A1B2C3");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual("Main Camera", nodes[0].Name,   "Name stripped of hex ref");
            Assert.AreEqual("$A1B2C3",     nodes[0].HexRef, "Hex ref preserved with $");
            Assert.AreEqual(0,              nodes[0].Depth,  "Root depth is 0");
            Assert.IsFalse(nodes[0].IsInactive, "Not inactive");
        }

        [Test]
        public void Parse_InactiveRoot_IsInactiveTrue()
        {
            // "Enemy $C3D4E5 !" — inactive marker after hex ref
            var nodes = HierarchyResultParser.Parse("Enemy NPC $C3D4E5 !");
            Assert.AreEqual(1, nodes.Length);
            Assert.IsTrue(nodes[0].IsInactive, "! suffix must set IsInactive");
            Assert.AreEqual("Enemy NPC", nodes[0].Name);
        }

        [Test]
        public void Parse_HiddenDescendants_HiddenCountParsed()
        {
            // "World $E5F6G7 +12" — depth-limited node with 12 hidden descendants
            var nodes = HierarchyResultParser.Parse("World $E5F6G7 +12");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual(12, nodes[0].HiddenCount, "+12 must be parsed as HiddenCount");
        }

        // ── Children (depth 1) — real format: 3-char ancestor group + connector ─

        [Test]
        public void Parse_ChildOfLastRoot_Depth1()
        {
            // "   └─ Weapon $G7H8I9" — single root's child (parent was last → 3 spaces)
            var nodes = HierarchyResultParser.Parse("   └─ Weapon $G7H8I9");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual(1, nodes[0].Depth, "3-space prefix = depth 1");
            Assert.AreEqual("Weapon", nodes[0].Name);
            Assert.AreEqual("$G7H8I9", nodes[0].HexRef);
        }

        [Test]
        public void Parse_ChildOfNonLastRoot_Depth1()
        {
            // "│  ├─ Shield $J0K1L2" — child of non-last root (│   prefix)
            var nodes = HierarchyResultParser.Parse("│  ├─ Shield $J0K1L2");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual(1, nodes[0].Depth, "│   prefix = depth 1");
            Assert.AreEqual("Shield", nodes[0].Name);
        }

        // ── Grandchildren (depth 2) ────────────────────────────────────────────

        [Test]
        public void Parse_Grandchild_Depth2()
        {
            // "│  │  └─ Barrel $I9J0K1" — two ancestor groups + connector
            var nodes = HierarchyResultParser.Parse("│  │  └─ Barrel $I9J0K1");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual(2, nodes[0].Depth, "Two │   groups = depth 2");
            Assert.AreEqual("Barrel", nodes[0].Name);
        }

        // ── Deep nesting ───────────────────────────────────────────────────────

        [Test]
        public void Parse_DeepNesting_Depth5()
        {
            // Five │   groups before the connector → depth 5
            var nodes = HierarchyResultParser.Parse("│  │  │  │  │  └─ VeryDeep $X1Y2Z3");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual(5, nodes[0].Depth, "Five ancestor groups → depth 5");
            Assert.AreEqual("VeryDeep", nodes[0].Name);
        }

        // ── Scene headers ──────────────────────────────────────────────────────

        [Test]
        public void Parse_SceneHeader_IsSceneHeaderTrue()
        {
            var nodes = HierarchyResultParser.Parse("[SampleScene]");
            Assert.AreEqual(1, nodes.Length);
            Assert.IsTrue(nodes[0].IsSceneHeader, "Line matching [Name] → IsSceneHeader");
            Assert.AreEqual("SampleScene", nodes[0].SceneName);
        }

        [Test]
        public void Parse_MultiScene_AllHeadersAndNodes()
        {
            // Realistic multi-scene output: 2 headers + 2 objects = 4 nodes
            var input =
                "[SampleScene]\n" +
                "Main Camera $AABBCC\n" +
                "\n" +
                "[AdditiveScene]\n" +
                "Enemy $DDEEFF !\n";

            var nodes = HierarchyResultParser.Parse(input);
            Assert.AreEqual(4, nodes.Length, "2 headers + 2 game objects");
            Assert.IsTrue(nodes[0].IsSceneHeader,   "nodes[0] = SampleScene header");
            Assert.AreEqual("SampleScene", nodes[0].SceneName);
            Assert.AreEqual("Main Camera", nodes[1].Name);
            Assert.AreEqual(0, nodes[1].Depth, "Root in scene = depth 0");
            Assert.IsTrue(nodes[2].IsSceneHeader, "nodes[2] = AdditiveScene header");
            Assert.AreEqual("AdditiveScene", nodes[2].SceneName);
            Assert.IsTrue(nodes[3].IsInactive, "Enemy is inactive");
        }

        // ── Components ─────────────────────────────────────────────────────────

        [Test]
        public void Parse_ComponentsPresent_ExtractsComponents()
        {
            // "Player [Health,Rigidbody] $Z9W8V7" — components between name and hex ref
            var nodes = HierarchyResultParser.Parse("Player [Health,Rigidbody] $Z9W8V7");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual("Player",            nodes[0].Name);
            Assert.AreEqual("Health,Rigidbody",  nodes[0].Components);
            Assert.AreEqual("$Z9W8V7",           nodes[0].HexRef);
        }

        // ── Real-world data: Cyrillic and special chars ────────────────────────

        [Test]
        public void Parse_CyrillicName_ParsedCorrectly()
        {
            // Cyrillic name in a real scene — common in localized projects
            var nodes = HierarchyResultParser.Parse("Главный Герой $A0B1C2 !");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual("Главный Герой", nodes[0].Name, "Cyrillic name preserved");
            Assert.IsTrue(nodes[0].IsInactive, "Inactive flag parsed");
        }

        [Test]
        public void Parse_NameWithEscapedSlash_NamePreservedRaw()
        {
            // "Day\/Night $C2D3E4" — escaped slash in GameObject name
            // Display keeps raw form; navigation uses hex ref, not path.
            var nodes = HierarchyResultParser.Parse("Day\\/Night $C2D3E4");
            Assert.AreEqual(1, nodes.Length);
            Assert.AreEqual("Day\\/Night", nodes[0].Name, "Escaped slash kept as-is");
            Assert.AreEqual("$C2D3E4", nodes[0].HexRef);
        }

        // ── Truncation ─────────────────────────────────────────────────────────

        [Test]
        public void Parse_TruncationSentinel_Skipped()
        {
            // Sentinel appended by HierarchySerializer when MAX_NODES reached
            var input =
                "Main Camera $AABBCC\n" +
                "... truncated at 3000 nodes. Use filter/root/depth to narrow.\n";
            var nodes = HierarchyResultParser.Parse(input);
            Assert.AreEqual(1, nodes.Length, "Sentinel line must be skipped");
            Assert.AreEqual("Main Camera", nodes[0].Name);
        }

        [Test]
        public void Parse_TruncatedMidLine_PartialLineSkipped()
        {
            // 2000-char limit (T0.1) can cut result mid-line.
            // Partial line has no valid hex ref → TryParseNode returns false → skipped.
            var input = "Main Camera $AABBCC\nPlayer $DDEEFF\n│  └─ WeaponSys"; // cut mid
            var nodes = HierarchyResultParser.Parse(input);
            Assert.AreEqual(2, nodes.Length, "Partial last line must be silently skipped");
            Assert.AreEqual("Main Camera", nodes[0].Name);
            Assert.AreEqual("Player",      nodes[1].Name);
        }

        [Test]
        public void Parse_TruncatedMidHexRef_NodeAppearsWithPartialRef()
        {
            // 2000-char limit (T0.1) can slice inside the hex digits.
            // "Directional Light $ABCD" where full ref was "$ABCDEF01" is valid: " $" IS present.
            // TryParseNode creates the node with the partial ref — user sees it,
            // NavBindingHelper.Attach logs a warning on click, no crash.
            var nodes = HierarchyResultParser.Parse("Directional Light $ABC");
            Assert.AreEqual(1, nodes.Length, "Node with partial hex ref must appear, not be skipped");
            Assert.AreEqual("Directional Light", nodes[0].Name);
            StringAssert.StartsWith("$", nodes[0].HexRef, "HexRef must retain $ prefix");
        }

        // ── Complex scene (covers all required cases together) ─────────────────

        [Test]
        public void Parse_ComplexScene_AllFieldsCorrect()
        {
            // Multi-scene: Cyrillic, escaped slash, inactive, hidden count, children.
            // Constructed from the exact format HierarchySerializer.Serialize() produces.
            var input =
                "[GameScene]\n" +
                "Главный Персонаж $A1B2C3\n" +           // root, cyrillic
                "│  ├─ Оружие $D4E5F6 !\n" +              // depth-1, cyrillic, inactive
                "│  └─ Камера $G7H8I9 +5\n" +             // depth-1, hidden count
                "Day\\/Night $J0K1L2\n" +                  // root, escaped slash
                "\n" +
                "[AdditiveScene]\n" +
                "Enemy $M3N4O5\n";

            var nodes = HierarchyResultParser.Parse(input);
            Assert.AreEqual(7, nodes.Length, "2 headers + 5 objects");

            // Scene header
            Assert.IsTrue(nodes[0].IsSceneHeader);
            Assert.AreEqual("GameScene", nodes[0].SceneName);

            // Root with cyrillic name
            Assert.AreEqual("Главный Персонаж", nodes[1].Name);
            Assert.AreEqual(0,        nodes[1].Depth);
            Assert.AreEqual("$A1B2C3", nodes[1].HexRef);

            // Depth-1 inactive child
            Assert.AreEqual("Оружие",  nodes[2].Name);
            Assert.AreEqual(1,         nodes[2].Depth);
            Assert.IsTrue(nodes[2].IsInactive);

            // Depth-1 child with hidden count
            Assert.AreEqual("Камера", nodes[3].Name);
            Assert.AreEqual(1,         nodes[3].Depth);
            Assert.AreEqual(5,         nodes[3].HiddenCount);

            // Root with escaped slash
            Assert.AreEqual("Day\\/Night", nodes[4].Name);
            Assert.AreEqual("$J0K1L2",     nodes[4].HexRef);

            // Second scene header
            Assert.IsTrue(nodes[5].IsSceneHeader);
            Assert.AreEqual("AdditiveScene", nodes[5].SceneName);

            // Object in additive scene
            Assert.AreEqual("Enemy",   nodes[6].Name);
            Assert.AreEqual("$M3N4O5", nodes[6].HexRef);
        }
    }
}
