using NUnit.Framework;
using UnityMCP.Editor.Decisions;

namespace UnityMCP.Decisions.Tests
{
    // The facts that MutabilityFacts.Read states about a wire call (dsl/decisions/mutability.contract), as the Python side states them. The
    // switch arguments are absent, off (the literal false) or on (anything else); `enable` only needs to be there; an action is a read when the
    // generated ActionReads says so. The wire calls themselves (and the rule) are MutabilityWireTests.
    public sealed class MutabilityFactsTests
    {
        [TestCase("{}", "absent")]
        [TestCase("{\"abort_on_fail\":\"false\"}", "off")]
        [TestCase("{\"abort_on_fail\":false}", "off")]
        [TestCase("{\"abort_on_fail\":\"true\"}", "on")]
        [TestCase("{\"abort_on_fail\":true}", "on")]
        [TestCase("{\"abort_on_fail\":\"future\"}", "on")]
        [TestCase("{\"abort_on_fail\":\"\"}", "on")]
        [TestCase("{\"abort_on_fail\":null}", "absent")]
        [TestCase("{\"other\":\"false\",\"abort_on_fail\":\"true\"}", "on")]
        public void A_switch_is_absent_off_or_on(string argsJson, string expected)
        {
            var facts = MutabilityFacts.Read("wait_until", argsJson, "write");
            Assert.That(facts.ArgsAbortOnFail, Is.EqualTo(expected));
        }

        [Test]
        public void Every_switch_is_read_by_its_own_key()
        {
            var facts = MutabilityFacts.Read("x", "{\"clear\":\"false\",\"reset\":\"true\",\"fix\":\"false\",\"abort_on_fail\":\"true\"}", "write");
            Assert.That((facts.ArgsClear, facts.ArgsReset, facts.ArgsFix, facts.ArgsAbortOnFail), Is.EqualTo(("off", "on", "off", "on")));
        }

        [TestCase("{}", false)]
        [TestCase("{\"enable\":\"true\"}", true)]
        [TestCase("{\"enable\":\"false\"}", true)]
        [TestCase("{\"enable\":\"\"}", true)]
        [TestCase("{\"enable\":null}", false)]
        public void Enable_only_needs_to_be_there(string argsJson, bool expected) =>
            Assert.That(MutabilityFacts.Read("editor", argsJson, "write").ArgsHasEnable, Is.EqualTo(expected));

        [TestCase("editor", "{\"action\":\"state\"}", true)]
        [TestCase("editor", "{\"action\":\"project_path\"}", true)]
        [TestCase("editor", "{\"action\":\"play\"}", false)]
        [TestCase("editor", "{\"action\":\"STATE\"}", false)]
        [TestCase("editor", "{}", false)]
        [TestCase("navmesh", "{\"action\":\"status\"}", true)]
        [TestCase("navmesh_query", "{\"action\":\"status\"}", true)]
        [TestCase("navmesh", "{\"action\":\"bake\"}", false)]
        [TestCase("get_hierarchy", "{\"action\":\"get\"}", false)]
        [TestCase("uitk_file", "{\"action\":\"read\"}", true)]
        public void An_action_is_a_read_when_the_catalog_says_so(string cmd, string argsJson, bool expected) =>
            Assert.That(MutabilityFacts.Read(cmd, argsJson, "write").CallActionRead, Is.EqualTo(expected));

        [Test]
        public void The_call_is_passed_through_and_asks_as_a_registry_query_does()
        {
            var facts = MutabilityFacts.Read("scene", "{\"action\":\"list\"}", "write");
            Assert.That((facts.CallCmd, facts.CallBase, facts.ArgsAction, facts.CallUnknownIsWrite), Is.EqualTo(("scene", "write", "list", false)));
            Assert.That(MutabilityFacts.Read(null, null, "unknown").CallCmd, Is.EqualTo(""));
            Assert.That(MutabilityFacts.Read("x", null, "unknown").ArgsAction, Is.EqualTo(""));
        }
    }
}
