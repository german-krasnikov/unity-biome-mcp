using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Decisions.Tests
{
    // The generated ActionReads.IsRead (Editor/Generated/ActionReads.g.cs, printed from the READ lines of dsl/tools.biome) against the
    // read actions that middleware_types.ACTION_READS stated before the catalog printed them: server/tests/golden/decisions/action_reads.txt,
    // one line ('command', 'action') per pair. The same file is the oracle of the Python side (test_action_reads_golden.py).
    // The table is keyed by tool name; navmesh is the wire command that the tool navmesh_query sends, and is mapped by its caller.
    public sealed class ActionReadsTests
    {
        private static readonly Regex Pair = new Regex(@"^\('(\w+)', '(\w+)'\)$");

        private static List<(string Cmd, string Action)> Golden() =>
            File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "rows", "action_reads.txt"))
                .Select(line => Pair.Match(line))
                .Where(match => match.Success && match.Groups[1].Value != "navmesh")
                .Select(match => (match.Groups[1].Value, match.Groups[2].Value))
                .ToList();

        [Test]
        public void Every_pair_of_the_golden_is_a_read()
        {
            var golden = Golden();
            Assert.That(golden.Count, Is.GreaterThan(40), "the golden is nearly empty: this test would pass without checking");
            foreach (var (cmd, action) in golden)
                Assert.That(ActionReads.IsRead(cmd, action), Is.True, $"{cmd} {action}");
        }

        [Test]
        public void No_other_pair_is_a_read()
        {
            var golden = Golden();
            var commands = golden.Select(pair => pair.Cmd).Distinct().ToList();
            var actions = golden.Select(pair => pair.Action).Distinct().Concat(new[] { "", "future_action", "write", "set" }).ToList();
            foreach (var cmd in commands)
                foreach (var action in actions)
                    Assert.That(ActionReads.IsRead(cmd, action), Is.EqualTo(golden.Contains((cmd, action))), $"{cmd} {action}");
        }

        [Test]
        public void An_action_in_another_case_and_an_unknown_command_are_not_reads()
        {
            foreach (var (cmd, action) in Golden())
                Assert.That(ActionReads.IsRead(cmd, action.ToUpperInvariant()), Is.False, $"{cmd} {action.ToUpperInvariant()}");
            Assert.That(ActionReads.IsRead("nonexistent_cmd", "get"), Is.False);
            Assert.That(ActionReads.IsRead("get_hierarchy", "get"), Is.False);
            Assert.That(ActionReads.IsRead(null, "get"), Is.False);
            Assert.That(ActionReads.IsRead("editor", null), Is.False);
        }
    }
}
