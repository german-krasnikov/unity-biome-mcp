using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.TestProject.Command
{
    [TestFixture]
    public class CommandSchemaTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // --- StringDistance ---

        [Test]
        public void Levenshtein_IdenticalStrings_Zero()
        {
            Assert.AreEqual(0, StringDistance.Levenshtein("hello", "hello"));
        }

        [Test]
        public void Levenshtein_OneEdit()
        {
            Assert.AreEqual(1, StringDistance.Levenshtein("cat", "bat"));
            Assert.AreEqual(1, StringDistance.Levenshtein("cat", "cats"));
        }

        [Test]
        public void Levenshtein_EmptyStrings()
        {
            Assert.AreEqual(3, StringDistance.Levenshtein("", "abc"));
            Assert.AreEqual(3, StringDistance.Levenshtein("abc", ""));
            Assert.AreEqual(0, StringDistance.Levenshtein("", ""));
        }

        [Test]
        public void ClosestMatch_FindsSimilar()
        {
            var candidates = new[] { "create_object", "delete_object", "set_property" };
            Assert.AreEqual("create_object", StringDistance.ClosestMatch("creat_object", candidates));
            Assert.AreEqual("set_property", StringDistance.ClosestMatch("set_proprty", candidates));
        }

        [Test]
        public void ClosestMatch_ReturnsNull_WhenTooFar()
        {
            var candidates = new[] { "create_object", "delete_object" };
            Assert.IsNull(StringDistance.ClosestMatch("xyzzy_foobar", candidates));
        }

        // --- CommandSchema.Validate ---

        [Test]
        public void ValidCommand_ReturnsNull()
        {
            Assert.IsNull(CommandValidator.Validate("create_object", "{\"name\":\"A\"}"));
            Assert.IsNull(CommandValidator.Validate("recompile", "{}"));
            Assert.IsNull(CommandValidator.Validate("get_hierarchy", "{}"));
        }

        [Test]
        public void ValidCommand_WithOptional_ReturnsNull()
        {
            Assert.IsNull(CommandValidator.Validate("create_object",
                "{\"name\":\"A\",\"parent\":\"/Root\",\"primitive\":\"Cube\"}"));
        }

        [Test]
        public void UnknownCommand_SuggestsSimilar()
        {
            // "creat_object" is 1 edit from "create_object"
            var err = CommandValidator.Validate("creat_object", "{}");
            Assert.IsNotNull(err);
            StringAssert.Contains("Unknown command", err);
            StringAssert.Contains("Did you mean 'create_object'", err);
        }

        [Test]
        public void UnknownCommand_WildlyDifferent()
        {
            // "move_object" is too far from any command (>3 edits) — no suggestion
            var err = CommandValidator.Validate("move_object", "{}");
            Assert.IsNotNull(err);
            StringAssert.Contains("Unknown command", err);
            Assert.IsFalse(err.Contains("Did you mean"));
        }

        [Test]
        public void MissingRequired_ReportsAll()
        {
            var err = CommandValidator.Validate("get_component", "{\"path\":\"/A\"}");
            Assert.IsNotNull(err);
            StringAssert.Contains("!type", err);
        }

        [Test]
        public void UnknownParam_SuggestsClosest()
        {
            // "valuee" is 1 edit from "value" — should suggest
            var err = CommandValidator.Validate("set_property",
                "{\"path\":\"/A\",\"component\":\"Transform\",\"prop\":\"x\",\"value\":\"1\",\"valuee\":\"extra\"}");
            Assert.IsNotNull(err);
            StringAssert.Contains("?valuee→value", err);
        }

        [Test]
        public void UnknownParam_NoSuggestion_WhenTooFar()
        {
            // "position" is too far from any set_property param (>3 edits)
            var err = CommandValidator.Validate("set_property",
                "{\"path\":\"/A\",\"component\":\"Transform\",\"prop\":\"x\",\"value\":\"1\",\"position\":\"(0,0,0)\"}");
            Assert.IsNotNull(err);
            StringAssert.Contains("?position", err);
            Assert.IsFalse(err.Contains("→"));
        }

        [Test]
        public void UnknownParam_MultipleErrors()
        {
            var err = CommandValidator.Validate("create_object",
                "{\"name\":\"A\",\"pos\":\"(0,0,0)\",\"rotation\":\"(0,0,0)\"}");
            Assert.IsNotNull(err);
            StringAssert.Contains("pos", err);
            StringAssert.Contains("rotation", err);
        }

        [Test]
        public void RegisteredPluginCommands_SkipSchemaValidation()
        {
            CommandRegistry.Register("test_plugin_cmd", args => "ok");
            Assert.IsNull(CommandValidator.Validate("test_plugin_cmd", "{\"anything\":\"goes\"}"));
        }

        [Test]
        public void LegacyCommands_NowRejected()
        {
            // Dead aliases removed (#17) — consolidated under scene/animation/references commands.
            Assert.IsNotNull(CommandValidator.Validate("new_scene", "{}"));
            Assert.IsNotNull(CommandValidator.Validate("open_scene", "{\"path\":\"Assets/test.unity\"}"));
            Assert.IsNotNull(CommandValidator.Validate("get_animation", "{\"path\":\"/Obj\"}"));
        }

        // --- ExtractKeys ---

        [Test]
        public void ExtractKeys_EmptyJson()
        {
            Assert.AreEqual(0, CommandValidator.ExtractKeys("{}").Count);
            Assert.AreEqual(0, CommandValidator.ExtractKeys("").Count);
            Assert.AreEqual(0, CommandValidator.ExtractKeys(null).Count);
        }

        [Test]
        public void ExtractKeys_ParsesFlat()
        {
            var keys = CommandValidator.ExtractKeys("{\"name\":\"A\",\"parent\":\"/Root\"}");
            Assert.AreEqual(2, keys.Count);
            Assert.Contains("name", keys);
            Assert.Contains("parent", keys);
        }

        [Test]
        public void ExtractKeys_ValueContainingKeyName_NotConfused()
        {
            // Value "path=/a" contains substring "path" which is also a real key
            var keys = CommandValidator.ExtractKeys("{\"value\":\"path=/a\",\"path\":\"/real\"}");
            Assert.AreEqual(2, keys.Count);
            Assert.AreEqual("value", keys[0]);
            Assert.AreEqual("path", keys[1]);
        }

        [Test]
        public void ExtractKeys_EscapedQuoteInsideValue()
        {
            var keys = CommandValidator.ExtractKeys("{\"key\":\"val\\\"more\",\"next\":\"x\"}");
            Assert.AreEqual(2, keys.Count);
            Assert.AreEqual("key", keys[0]);
            Assert.AreEqual("next", keys[1]);
        }

        [Test]
        public void ExtractKeys_EscapedBackslashBeforeQuote()
        {
            // Value is val\ (escaped backslash) — JSON: "val\\"
            // The closing " after \\ is NOT escaped
            var keys = CommandValidator.ExtractKeys("{\"key\":\"val\\\\\",\"next\":\"x\"}");
            Assert.AreEqual(2, keys.Count);
            Assert.AreEqual("key", keys[0]);
            Assert.AreEqual("next", keys[1]);
        }

        // --- delete_object schema ---

        [Test]
        public void DeleteObject_WithPath_Valid()
        {
            Assert.IsNull(CommandValidator.Validate("delete_object", "{\"path\":\"/Some/Object\"}"));
        }

        [Test]
        public void DeleteObject_WithId_Valid()
        {
            Assert.IsNull(CommandValidator.Validate("delete_object", "{\"id\":\"12345\"}"));
        }

        [Test]
        public void DeleteObject_NoParams_Valid_AtSchemaLevel()
        {
            // Schema requires neither id nor path — runtime enforces "at least one"
            Assert.IsNull(CommandValidator.Validate("delete_object", "{}"));
        }

        // --- Batch integration ---

        [Test]
        public void Batch_ValidationError_UnknownCommand()
        {
            var result = BatchHelper.Execute("move_object name=A", "continue");
            StringAssert.Contains("[0] err:", result);
            StringAssert.Contains("Unknown command", result);
            StringAssert.Contains("err:1", result);
        }

        [Test]
        public void Batch_ValidationError_MissingRequired()
        {
            var result = BatchHelper.Execute("get_component path=/A", "continue");
            StringAssert.Contains("[0] err:", result);
            StringAssert.Contains("!type", result);
        }

        [Test]
        public void Batch_ValidationError_StopsOnError()
        {
            var result = BatchHelper.Execute("move_object name=A\ncreate_object name=B", "stop");
            StringAssert.Contains("[0] err:", result);
            StringAssert.Contains("[1] skip", result);
        }

        // --- SchemaHelper ---

        [Test]
        public void SchemaHelper_KnownType_ReturnsFields()
        {
            var result = SchemaHelper.GetSchema("Rigidbody");

            StringAssert.Contains("Schema: Rigidbody", result);
            StringAssert.Contains("m_Mass", result);
            StringAssert.Contains("m_UseGravity", result);
        }

        [Test]
        public void SchemaHelper_UnknownType_ReturnsError()
        {
            var result = SchemaHelper.GetSchema("NonExistentType99999");

            StringAssert.Contains("Type not found", result);
        }

        [Test]
        public void SchemaHelper_BoxCollider_HasSizeField()
        {
            var result = SchemaHelper.GetSchema("BoxCollider");

            StringAssert.Contains("Schema: BoxCollider", result);
            StringAssert.Contains("m_Size", result);
        }

        [Test]
        public void SchemaHelper_EmptyTypeName_ReturnsError()
        {
            var result = SchemaHelper.GetSchema("");

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }
    }
}
