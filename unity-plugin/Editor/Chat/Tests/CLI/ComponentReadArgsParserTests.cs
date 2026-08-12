// TDD T2.4 — ComponentReadArgsParser: parse argsJson for get_component, inspect,
//             get_components_list.
//
// Double-red requirement:
//   A — corrupt any Assert → test RED
//   B — remove field extraction in parser → corresponding field tests RED
//
// Data: real-world tool calls — actual hierarchy paths, transient IDs, Cyrillic,
//       long paths, whitespace-padded paths, type alias. No synthetic "a"/"b" data.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class ComponentReadArgsParserTests : UnityMcpTestBase
    {
        // ── get_component ──────────────────────────────────────────────────────

        [Test]
        public void Parse_GetComponent_ExtractsPathAndType()
        {
            // Real call: read Transform on the hero prefab instance
            var json = "{\"path\":\"/Hero/Body\",\"type\":\"Transform\"}";
            var r = ComponentReadArgsParser.Parse("get_component", json);
            Assert.AreEqual(ReadToolKind.GetComponent, r.Kind);
            Assert.IsTrue(r.IsValid, "Valid when path and type present");
            Assert.AreEqual("/Hero/Body", r.Path);
            Assert.AreEqual("Transform", r.ComponentType);
        }

        [Test]
        public void Parse_GetComponent_WithCompress_StillExtractsFields()
        {
            // Real call: Rigidbody with compress flag
            var json = "{\"path\":\"/Player\",\"type\":\"Rigidbody\",\"compress\":\"true\"}";
            var r = ComponentReadArgsParser.Parse("get_component", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("/Player", r.Path);
            Assert.AreEqual("Rigidbody", r.ComponentType);
        }

        [Test]
        public void Parse_GetComponent_MissingType_IsInvalid()
        {
            var json = "{\"path\":\"/Player\"}";
            var r = ComponentReadArgsParser.Parse("get_component", json);
            Assert.IsFalse(r.IsValid, "Missing type makes get_component invalid");
        }

        [Test]
        public void Parse_GetComponent_MissingPath_IsInvalid()
        {
            var json = "{\"type\":\"Transform\"}";
            var r = ComponentReadArgsParser.Parse("get_component", json);
            Assert.IsFalse(r.IsValid, "Missing path makes get_component invalid");
        }

        // ── Null / empty args ──────────────────────────────────────────────────

        [Test]
        public void Parse_NullArgs_IsInvalid()
        {
            var r = ComponentReadArgsParser.Parse("get_component", null);
            Assert.IsFalse(r.IsValid, "Null argsJson must be invalid");
        }

        [Test]
        public void Parse_EmptyJson_IsInvalid()
        {
            var r = ComponentReadArgsParser.Parse("get_component", "{}");
            Assert.IsFalse(r.IsValid, "Empty JSON object must be invalid");
        }

        // ── inspect ────────────────────────────────────────────────────────────

        [Test]
        public void Parse_Inspect_SinglePath()
        {
            // Real call: inspect a single scene object
            var json = "{\"paths\":\"/Player\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.AreEqual(ReadToolKind.Inspect, r.Kind);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(1, r.Paths.Length);
            Assert.AreEqual("/Player", r.Paths[0]);
            Assert.IsNull(r.Components, "No component filter when components/type absent");
        }

        [Test]
        public void Parse_Inspect_MultiplePaths()
        {
            // Real call: bulk inspect for comparison
            var json = "{\"paths\":\"/Player,/Enemy/Boss,/Environment/Terrain\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(3, r.Paths.Length);
            Assert.AreEqual("/Player", r.Paths[0]);
            Assert.AreEqual("/Enemy/Boss", r.Paths[1]);
            Assert.AreEqual("/Environment/Terrain", r.Paths[2]);
        }

        [Test]
        public void Parse_Inspect_PathsWithWhitespace_AreTrimmed()
        {
            // Real call with spaces around commas (user-entered or programmatic)
            var json = "{\"paths\":\" /Player , /Enemy \"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(2, r.Paths.Length);
            Assert.AreEqual("/Player", r.Paths[0]);
            Assert.AreEqual("/Enemy", r.Paths[1]);
        }

        [Test]
        public void Parse_Inspect_ComponentsFilter()
        {
            // Real call: inspect only Rigidbody and Collider
            var json = "{\"paths\":\"/Player\",\"components\":\"Rigidbody,BoxCollider\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.IsNotNull(r.Components);
            Assert.AreEqual(2, r.Components.Length);
            Assert.AreEqual("Rigidbody", r.Components[0]);
            Assert.AreEqual("BoxCollider", r.Components[1]);
        }

        [Test]
        public void Parse_Inspect_TypeAliasForComponents()
        {
            // Real call: "type" is accepted as alias for "components" by ExecInspect
            var json = "{\"paths\":\"/Player\",\"type\":\"MeshRenderer\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.IsNotNull(r.Components, "'type' must be treated as components alias");
            Assert.AreEqual(1, r.Components.Length);
            Assert.AreEqual("MeshRenderer", r.Components[0]);
        }

        [Test]
        public void Parse_Inspect_ComponentsTakesPriorityOverType()
        {
            // When both present, "components" wins (mirrors ExecInspect priority)
            var json = "{\"paths\":\"/Player\",\"components\":\"Rigidbody\",\"type\":\"Transform\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(1, r.Components.Length);
            Assert.AreEqual("Rigidbody", r.Components[0],
                "'components' field must take priority over 'type' alias");
        }

        [Test]
        public void Parse_Inspect_CyrillicPath_Preserved()
        {
            // Hierarchy paths can contain Unicode (localised projects)
            var json = "{\"paths\":\"/Игрок/Тело\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("/Игрок/Тело", r.Paths[0],
                "Cyrillic path must survive JSON decoding unchanged");
        }

        [Test]
        public void Parse_Inspect_VeryLongPath_PreservedFully()
        {
            // Deeply nested object — path > 200 chars, must not be truncated by parser
            var deepPath = "/World/Zone_A/Building_01/Floor_03/Corridor_West/Room_12/Furniture/Desk_Large/Drawer_Left/Item_Pencil_Set_01";
            Assert.Greater(deepPath.Length, 100, "Test precondition: path is long");
            var json = "{\"paths\":\"" + deepPath + "\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(deepPath, r.Paths[0], "Parser must not truncate long paths");
        }

        [Test]
        public void Parse_Inspect_NoPaths_IsInvalid()
        {
            var json = "{\"components\":\"Transform\"}";
            var r = ComponentReadArgsParser.Parse("inspect", json);
            Assert.IsFalse(r.IsValid, "Inspect without paths is invalid");
        }

        // ── get_components_list ────────────────────────────────────────────────

        [Test]
        public void Parse_GetComponentsList_HexId()
        {
            // Real call: hex transient ID from get_hierarchy
            var json = "{\"id\":\"$3E8\"}";
            var r = ComponentReadArgsParser.Parse("get_components_list", json);
            Assert.AreEqual(ReadToolKind.GetComponentsList, r.Kind);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("$3E8", r.ObjectId);
        }

        [Test]
        public void Parse_GetComponentsList_DecimalId()
        {
            // Legacy decimal format
            var json = "{\"id\":\"#123\"}";
            var r = ComponentReadArgsParser.Parse("get_components_list", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("#123", r.ObjectId);
        }

        [Test]
        public void Parse_GetComponentsList_LargeHexId()
        {
            // Larger transient ID from a real session
            var json = "{\"id\":\"$2B678\"}";
            var r = ComponentReadArgsParser.Parse("get_components_list", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("$2B678", r.ObjectId);
        }

        [Test]
        public void Parse_GetComponentsList_MissingId_IsInvalid()
        {
            var json = "{}";
            var r = ComponentReadArgsParser.Parse("get_components_list", json);
            Assert.IsFalse(r.IsValid, "Missing id makes get_components_list invalid");
        }

        // ── Unknown tool name ──────────────────────────────────────────────────

        [Test]
        public void Parse_UnknownToolName_IsInvalid()
        {
            var r = ComponentReadArgsParser.Parse("get_property", "{\"path\":\"/A\"}");
            Assert.IsFalse(r.IsValid, "Unrecognised tool name must yield invalid result");
            Assert.AreEqual(ReadToolKind.Unknown, r.Kind);
        }
    }
}
