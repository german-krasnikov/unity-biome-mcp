// TDD Phase 2.4 — ObjectMutationParser. 10 tests, all RED before implementation.
// Tools: set_property, create_object, delete_object, rename_object, unknown.
// noEngineReferences: true — no Debug.Log, no Unity types.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class ObjectMutationParserTests : UnityMcpTestBase
    {
        private const string SetPropertyJson =
            "{\"path\":\"/Hero\",\"component\":\"Health\",\"prop\":\"maxHealth\",\"value\":\"150\"}";

        // === set_property ===

        [Test]
        public void Parse_SetProperty_ExtractsPath()
        {
            var r = ObjectMutationParser.Parse("set_property", SetPropertyJson);
            Assert.AreEqual("/Hero", r.Path);
            Assert.IsTrue(r.IsValid);
        }

        [Test]
        public void Parse_SetProperty_ExtractsProp()
        {
            var r = ObjectMutationParser.Parse("set_property", SetPropertyJson);
            Assert.AreEqual("maxHealth", r.Property);
        }

        [Test]
        public void Parse_SetProperty_ExtractsValue()
        {
            var r = ObjectMutationParser.Parse("set_property", SetPropertyJson);
            Assert.AreEqual("150", r.Value);
        }

        // === create_object ===

        [Test]
        public void Parse_CreateObject_ExtractsName()
        {
            var r = ObjectMutationParser.Parse("create_object", "{\"name\":\"Enemy\"}");
            Assert.AreEqual("Enemy", r.Name);
            Assert.AreEqual(MutationKind.CreateObject, r.Kind);
        }

        // === delete_object ===

        [Test]
        public void Parse_DeleteObject_ExtractsPath()
        {
            var r = ObjectMutationParser.Parse("delete_object", "{\"path\":\"/Old\"}");
            Assert.AreEqual("/Old", r.Path);
            Assert.AreEqual(MutationKind.DeleteObject, r.Kind);
        }

        // === rename_object ===

        [Test]
        public void Parse_SetName_ExtractsOldAndNew()
        {
            var r = ObjectMutationParser.Parse(
                "rename_object",
                "{\"path\":\"/Obj\",\"old_name\":\"Cube\",\"new_name\":\"Player\"}");
            Assert.AreEqual("Cube",   r.OldName);
            Assert.AreEqual("Player", r.NewName);
            Assert.AreEqual(MutationKind.RenameObject, r.Kind);
        }

        // === error cases ===

        [Test]
        public void Parse_Null_IsValidFalse()
        {
            var r = ObjectMutationParser.Parse("set_property", null);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void Parse_InvalidJson_IsValidFalse()
        {
            var r = ObjectMutationParser.Parse("set_property", "}");
            Assert.IsFalse(r.IsValid);
        }

        // === unknown tool ===

        [Test]
        public void Parse_UnknownTool_KindUnknown()
        {
            var r = ObjectMutationParser.Parse("get_hierarchy", "{}");
            Assert.AreEqual(MutationKind.Unknown, r.Kind);
        }

        // === array value preserved as raw string ===

        [Test]
        public void Parse_ArrayValue_Preserved()
        {
            const string json =
                "{\"path\":\"/O\",\"component\":\"C\",\"prop\":\"list\",\"value\":[1,2,3]}";
            var r = ObjectMutationParser.Parse("set_property", json);
            Assert.AreEqual("[1,2,3]", r.Value);
        }
    }
}
