// T16: ChangeSetParser unit tests (7 tests). Pure NUnit — no Unity API.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChangeSetParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Parse_Null_ReturnsNull()
            => Assert.That(ChangeSetParser.Parse(null), Is.Null);

        [Test]
        public void Parse_Empty_ReturnsNull()
            => Assert.That(ChangeSetParser.Parse(""), Is.Null);

        [Test]
        public void Parse_NoChangeset_ReturnsNull()
            => Assert.That(ChangeSetParser.Parse("no_changeset"), Is.Null);

        [Test]
        public void Parse_Header_ExtractsIdAndStatus()
        {
            var text = "cs:abc12345 status:finalized ops:0 nc:0 nm:0 nd:0";
            var vm   = ChangeSetParser.Parse(text);
            Assert.That(vm, Is.Not.Null);
            Assert.That(vm.ChangeSetId, Is.EqualTo("abc12345"));
            Assert.That(vm.Status, Is.EqualTo("finalized"));
        }

        [Test]
        public void Parse_OpLine_ExtractsAllFields()
        {
            var text = "cs:abc12345 status:open ops:1 nc:0 nm:1 nd:0\n" +
                       "modify property /Player Health bh:aabb1122 ah:ccdd3344 rev:true";
            var vm = ChangeSetParser.Parse(text);
            Assert.That(vm.Operations.Length, Is.EqualTo(1));
            var op = vm.Operations[0];
            Assert.That(op.Kind,       Is.EqualTo("modify"));
            Assert.That(op.TargetType, Is.EqualTo("property"));
            Assert.That(op.TargetPath, Is.EqualTo("/Player"));
            Assert.That(op.Prop,       Is.EqualTo("Health"));
            Assert.That(op.BeforeHash, Is.EqualTo("aabb1122"));
            Assert.That(op.AfterHash,  Is.EqualTo("ccdd3344"));
            Assert.That(op.Reversible, Is.True);
        }

        [Test]
        public void Parse_OpLine_MissingProp_PropNull()
        {
            var text = "cs:x status:open ops:1 nc:1 nm:0 nd:0\n" +
                       "create scene_object /Sword rev:true";
            var vm = ChangeSetParser.Parse(text);
            Assert.That(vm.Operations[0].Prop, Is.Null);
        }

        [Test]
        public void Parse_OpLine_MissingHashes_HashesNull()
        {
            var text = "cs:x status:open ops:1 nc:1 nm:0 nd:0\n" +
                       "create scene_object /Sword rev:true";
            var vm = ChangeSetParser.Parse(text);
            Assert.That(vm.Operations[0].BeforeHash, Is.Null);
            Assert.That(vm.Operations[0].AfterHash,  Is.Null);
        }

        [Test]
        public void Parse_MultipleOps_AllParsed()
        {
            var text = "cs:x status:open ops:3 nc:1 nm:1 nd:1\n" +
                       "create scene_object /A rev:true\n" +
                       "modify property /B Health rev:true\n" +
                       "delete scene_object /C rev:false";
            var vm = ChangeSetParser.Parse(text);
            Assert.That(vm.Operations.Length, Is.EqualTo(3));
        }
    }
}
