using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Decisions.Tests
{
    // The real CommandRegistry.IsMutating(cmd, argsJson) against what the hand-written Python is_write answered (the owner decided on 2026-10-29
    // that Python is the authority): server/tests/golden/decisions/is_write_wire.txt, the wire calls of test_is_write_golden.py's golden with the
    // registry state each was answered under. The registry is filled the way Python fills its sets: a command whose base is "write" is
    // registered mutating, "read" not mutating, "unknown" not at all. What the test cannot say is whether the real registrations of the plugin have
    // those bases: that is test_catalog_vs_csharp.py (KNOWN_DIVERGENT), which reads the Register calls.
    public sealed class MutabilityWireTests
    {
        private static List<(string Base, string Cmd, string Args, bool Want)> Rows() =>
            File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "rows", "is_write_wire.txt"))
                .Where(line => line.Length > 0 && line[0] != '#')
                .Select(line => line.Split('\t'))
                .Select(f => (f[0], f[1], f[2], f[3] == "true"))
                .ToList();

        [Test]
        public void IsMutating_answers_as_Python_does_for_every_wire_call()
        {
            var rows = Rows();
            Assert.That(rows.Count, Is.GreaterThan(250), "the golden is nearly empty: this test would pass without checking");
            CommandRegistry.Clear();
            foreach (var (callBase, cmd, _, _) in rows.Where(r => r.Base != "unknown").GroupBy(r => r.Cmd).Select(g => g.First()))
                CommandRegistry.Register(cmd, _ => "ok", mutating: callBase == "write");

            var problems = rows
                .Where(r => CommandRegistry.IsMutating(r.Cmd, r.Args) != r.Want)
                .Select(r => $"{r.Cmd} {r.Args}: {CommandRegistry.IsMutating(r.Cmd, r.Args)}, Python says {r.Want}")
                .ToList();

            Assert.That(problems, Is.Empty, $"{problems.Count} of {rows.Count} calls differ:\n" + string.Join("\n", problems.Take(20)));
        }
    }
}
