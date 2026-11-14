using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityMCP.Playtest.Core.SharedTests;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The differential of the reader (step 7.5 R6, Plans/R6_DIFFERENTIAL.md): dsl/playtest/reader.rows holds, for each script of the playtest construct's examples, the verdict that
    // the reader of Core gave (`biome generate` -> `GENERATE playtest-rows reader --surface playtest --target rows`; the format is in the head of the file). This reader reads the same
    // scripts and must say the same, word for word. Replaces the hand-written ExamplesFile/.examples parse this reader used before (PT-5): the rows are printed, never transcribed.
    // The format of a verdict is written twice, in support/reader-rows.mjs (Core) and in Verdict below (P7): a change to one is a change to the other.
    public class RowsTests
    {
        private sealed class Row
        {
            public int At;
            public string Kind, Script, Verdict;
        }

        private static readonly Regex Shape = new Regex(@"^(\S+) ""([^""]*)"" (.+)$", RegexOptions.CultureInvariant);
        private static readonly Regex Code = new Regex(@"\\u([0-9a-f]{4})", RegexOptions.CultureInvariant);

        // \, " and each code unit outside 0x20..0x7e as \u and four hex digits in lower case.
        internal static string Esc(string text)
        {
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                if (c == '\\' || c == '"' || c < 0x20 || c > 0x7e) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // The inverse, strict: every backslash opens \u and four lower-case hex digits, else the file is not what the generator wrote.
        internal static string Unesc(string text)
        {
            if (Code.Replace(text, "").Contains("\\")) throw new FormatException("a backslash that opens no \\uXXXX: " + text);
            return Code.Replace(text, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        }

        // The slots by name (ordinal: UTF-16 code units, as Core orders text), then the flags: ` name="raw"@col`, ` +flag`; columns count from 1.
        private static string Bound(SlotBindings b) =>
            string.Concat(b.Slots.OrderBy(s => s.Key, StringComparer.Ordinal).Select(s => " " + s.Key + "=\"" + Esc(s.Value.Raw) + "\"@" + (s.Value.Col + 1)))
            + string.Concat(b.Flags.OrderBy(f => f, StringComparer.Ordinal).Select(f => " +" + Esc(f)));

        // `none`, or the uses in the order the reader gave them, then its diagnostics, joined by ` ; `. A block says its last line (`..n`) and the slots of each of its lines behind a bar.
        internal static string Verdict(ReadResult r)
        {
            var events = r.Uses.Select(u => "ok " + u.Form.Construct + "@" + (u.Index + 1) + ":" + (u.Col + 1) + (u.Form.IsBlock ? ".." + (u.EndIndex + 1) : "") + Bound(u.Head)
                    + string.Concat(u.Items.Select(i => " |" + Bound(i))))
                .Concat(r.Diagnostics.Select(d => "err " + d.Code + "@" + d.Line + ":" + d.Col + " " + Esc(d.Text))).ToList();
            return events.Count == 0 ? "none" : string.Join(" ; ", events);
        }

        // The file is read as the generator wrote it: lines cut at \n, the blank ones and the comments passed over; a line that is no row throws, so that no row is passed over by mistake.
        private static List<Row> ReadAll()
        {
            var rows = new List<Row>();
            var at = 0;
            var text = File.ReadAllText(Path.Combine(RepoRoot.Find(), "dsl", "playtest", "reader.rows"), Encoding.UTF8);
            if (text.IndexOf('\r') >= 0) throw new InvalidOperationException("reader.rows holds a carriage return: the file is LF only, as the generator wrote it (a checkout that changes line ends spoils it)");
            foreach (var line in text.Split('\n'))
            {
                at++;
                if (line.Length == 0 || line[0] == '#') continue;
                var m = Shape.Match(line);
                if (!m.Success) throw new InvalidOperationException("reader.rows:" + at + ": not a row: " + line);
                rows.Add(new Row { At = at, Kind = m.Groups[1].Value, Script = Unesc(m.Groups[2].Value), Verdict = m.Groups[3].Value });
            }
            return rows;
        }

        // The rows this reader says otherwise about: the line of the file, what Core said, what this reader says (the reader does not throw, so a throw is said as a verdict).
        private static List<string> Diff(IEnumerable<Row> rows)
        {
            var diff = new List<string>();
            foreach (var row in rows)
            {
                string got;
                try { got = Verdict(ReaderHarness.Read(row.Script)); }
                catch (Exception e) { got = "throws " + e.GetType().Name; }
                if (got != row.Verdict) diff.Add("reader.rows:" + row.At + " " + row.Kind + "\n    core: " + row.Verdict + "\n    c#:   " + got);
            }
            return diff;
        }

        [Test]
        public void Read_EveryRow_SaysWhatCoreSaid_AndTheCorpusIsWhole()
        {
            var rows = ReadAll();
            var diff = Diff(rows);
            var groups = rows.Select(r => Regex.Replace(r.Kind, "[0-9]+$", "")).Distinct().Count();
            int Count(Func<Row, bool> by) => rows.Count(by);
            TestContext.WriteLine("rows " + rows.Count + ", SAME " + (rows.Count - diff.Count) + ", DIFF " + diff.Count);
            TestContext.WriteLine("kinds " + groups + ", src " + Count(r => r.Kind == "src") + ", ex " + Count(r => r.Kind == "ex")
                + ", ok " + Count(r => r.Verdict.StartsWith("ok ", StringComparison.Ordinal)) + ", err " + Count(r => r.Verdict.StartsWith("err ", StringComparison.Ordinal)));
            foreach (var d in diff.Take(40)) TestContext.WriteLine(d);
            Assert.That(diff, Is.Empty);
        }

        [Test]
        public void Escape_RoundTrips_AndAStrayBackslashIsRefused()
        {
            var every = new StringBuilder();
            for (var c = 0; c <= 0xffff; c++) every.Append((char)c);
            var text = every.ToString();
            Assert.That(Esc(text).All(c => c >= 0x20 && c <= 0x7e), Is.True, "printable ASCII only");
            Assert.That(Unesc(Esc(text)), Is.EqualTo(text));
            Assert.That(Esc("a\"b\\c"), Is.EqualTo("a\\u0022b\\u005cc"));
            foreach (var stray in new[] { "\\n", "\\x41", "\\u12", "\\u00E9", "\\\\u0041", "a\\" })
                Assert.Throws<FormatException>(() => Unesc(stray), stray);
        }

        // The control: Diff is not blind. Verdicts written from the rules of the plan are SAME, and each one corrupted is one DIFF.
        [Test]
        public void Diff_SeesACorruptedVerdict()
        {
            var known = new Dictionary<string, string>
            {
                ["WAIT 2.5"] = "ok PT_WAIT@1:1 seconds=\"2.5\"@6",
                ["WAIT abc"] = "err PT_NUMBER@1:6 WAIT: \\u0022abc\\u0022 is not a number (seconds)",
                ["WAIT"] = "err PT_SYNTAX@1:1 WAIT: the line ends early",
                ["# a comment is no line"] = "none",
            };
            Row Of(string script, string verdict) => new Row { At = 1, Kind = "src", Script = script, Verdict = verdict };
            foreach (var pair in known)
            {
                Assert.That(Diff(new[] { Of(pair.Key, pair.Value) }), Is.Empty, pair.Key);
                foreach (var wrong in new[] { pair.Value + " ", " " + pair.Value, pair.Value.Replace("@", "@1"), "none", "ok PT_PING@1:1" }.Where(w => w != pair.Value))
                    Assert.That(Diff(new[] { Of(pair.Key, wrong) }), Has.Count.EqualTo(1), pair.Key + " => " + wrong);
            }
        }
    }
}
