"""Step 7.5 O0: scripts/playtest_oracle.py and the frozen dsl/playtest/oracle.rows it printed.

The expected numbers are the ones the design (Plans/STEP_7_5_PLAYTEST_GRAMMAR_DESIGN.md, 2.2) and EVAL2 measured by grep, not what the script printed.
The C# snippets are written the way the test files of the plugin are formatted (attribute on its own line).
"""
import subprocess
import sys
from collections import Counter
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import playtest_oracle as oracle  # noqa: E402

ORACLE = REPO_ROOT / "dsl" / "playtest" / "oracle.rows"
# Plans/STEP_7_5_PLAYTEST_GRAMMAR_DESIGN.md 2.2: 19 verbs in 156 commands of 25 files; 156 - INCLUDE - VAL - VAR - SETUP/TEARDOWN = 138 steps
VERBS = {"ASSERT": 49, "INVOKE": 33, "WAIT_UNTIL": 15, "INCLUDE": 11, "ASSERT_CONSOLE_CLEAN": 9, "TIMESCALE": 8, "LOG": 8, "SET": 5, "VAL": 4, "MCP": 4,
         "WAIT": 2, "VAR": 1, "SETUP": 1, "TEARDOWN": 1, "SNAPSHOT": 1, "SECTION": 1, "FOCUS": 1, "FILL": 1, "CLICK": 1}


def rows(kind: str) -> list[list[str]]:
    """The rows of a kind as [kind, where, field, text]; a text that is empty ends its row."""
    lines = ORACLE.read_text(encoding="utf-8").split("\n")
    return [(line.split(" ", 3) + [""])[:4] for line in lines if line.split(" ", 1)[0] == kind]


def test_a_plain_string_literal_argument_is_decoded_and_located():
    src = 'class T\n{\n    void A()\n    {\n        var s = PlaytestParser.Parse("WAIT 1\\nLOG \\"x\\" \\\\ \\u0041");\n    }\n}\n'
    assert oracle.csharp_calls(src) == [(5, None, [], 'WAIT 1\nLOG "x" \\ A')]


def test_only_a_plain_literal_as_the_first_argument_is_a_call_of_the_oracle():
    src = "\n".join([
        "PlaytestParser.Parse(script);", 'PlaytestParser.Parse("A" + "B");', 'PlaytestParser.Parse($"A{x}");', 'PlaytestParser.Parse(@"A");',
        "PlaytestParser.Parse(Export(steps));", '// PlaytestParser.Parse("C");', 'var s = "PlaytestParser.Parse(\\"D\\")";',
        '/* PlaytestParser.Parse("E") */', 'PlaytestParser.Parse("F");', 'PlaytestParser.Parse("G", strict: true);', 'PlaytestParser.Parse(',
        '    "H");'])
    assert [(call[0], call[3]) for call in oracle.csharp_calls(src)] == [(9, "F"), (10, "G"), (11, "H")]


def test_a_call_inside_assert_throws_is_a_reject_with_its_exception_and_nothing_else_is():
    src = "\n".join([
        "Assert.Throws<System.ArgumentException>(() =>", '    PlaytestParser.Parse("A"));', 'Assert.Throws<ArgumentException>(() => { var r = PlaytestParser.Parse("B"); });',
        'Assert.DoesNotThrow(() => PlaytestParser.Parse("C"));', 'var r = PlaytestParser.Parse("D");',
        'Assert.Throws<ArgumentException>(() => PlaytestParser.Parse("E")); var q = PlaytestParser.Parse("F");'])
    assert [(call[3], call[1]) for call in oracle.csharp_calls(src)] == [
        ("A", "ArgumentException"), ("B", "ArgumentException"), ("C", None), ("D", None), ("E", "ArgumentException"), ("F", None)]


def test_the_step_types_of_a_call_are_the_assertions_after_it_in_its_own_test():
    src = "\n".join([
        "[Test]", "public void A()", "{", '    var s = PlaytestParser.Parse("WAIT 1\\nLOG x");', "    Assert.AreEqual(StepType.Wait, s[0].Type);",
        "    Assert.That(s[1].Type, Is.EqualTo(StepType.Log));", "    var made = new PlaytestStep { Type = StepType.Snapshot };", "}", "",
        "[Test]", "public void Other()", "{", "    Assert.AreEqual(StepType.Click, Make().Type);", "}", "",
        "[Test]", "public void B()", "{", '    Assert.AreEqual(StepType.Set, PlaytestParser.Parse("SET a b c 1")[0].Type);', '    var s = PlaytestParser.Parse("LOG y");',
        "    Assert.AreEqual(1, s.Count);", '    var t = PlaytestParser.Parse("WAIT 2");', "    Assert.AreEqual(StepType.Wait, t[0].Type);", "}"])
    assert [(call[3], call[2]) for call in oracle.csharp_calls(src)] == [
        ("WAIT 1\nLOG x", ["Wait", "Log"]), ("SET a b c 1", ["Set"]), ("LOG y", []), ("WAIT 2", ["Wait"])]


def test_a_row_text_is_one_line_and_keeps_its_trailing_space():
    assert oracle.escape("a\\b\n\r\t\x01\u2028 ") == "a\\\\b\\n\\r\\t\\x01\\u2028\\x20"


def test_the_oracle_holds_the_25_files_156_commands_and_19_verbs_of_the_design():
    commands = rows("line")
    assert len({where.rpartition(":")[0] for _, where, _, _ in commands}) == 25
    assert Counter(verb for _, _, verb, _ in commands) == VERBS
    assert sum(VERBS.values()) == 156
    assert sum(n for verb, n in VERBS.items() if verb not in {"INCLUDE", "VAL", "VAR", "SETUP", "TEARDOWN"}) == 138


def test_the_oracle_holds_the_289_literal_calls_and_273_literals_of_eval2():
    calls = rows("accept") + rows("reject")
    assert len(calls) == 289
    assert len({text for _, _, _, text in calls}) == 273
    assert {exception for _, _, exception, _ in rows("reject")} == {"ArgumentException"}


def test_the_oracle_is_what_the_script_prints_at_its_commit():
    if subprocess.run(["git", "cat-file", "-e", f"{oracle.BASE}^{{commit}}"], cwd=REPO_ROOT, capture_output=True).returncode:
        pytest.skip(f"{oracle.BASE[:8]} is not in this clone")
    assert ORACLE.read_text(encoding="utf-8") == oracle.render()
