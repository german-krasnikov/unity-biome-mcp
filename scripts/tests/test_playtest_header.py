"""B18: scripts/playtest_header.py — the single Python .playtest header scanner.

test_scan_matches_csharp_scanner_on_all_fixture_files is the parity gate: every
`.playtest` fixture is independently re-scanned by `_oracle_scan` below (a second,
separately-written transcription of PlaytestHeaderScanner.cs's semantics, not a call
into scan() itself) and the two results must agree.
"""
import pathlib
import re
import sys
from dataclasses import dataclass, field

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import playtest_header as ph  # noqa: E402

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
FIXTURE_ROOT = REPO_ROOT / "unity-test-project" / "Assets"


def test_scan_returns_needs_tags_expect_suite_only():
    text = (
        "# @needs editmode playmode\n"
        "# @tags smoke slow\n"
        "# @expect steps=4 failed=1\n"
        "# @suite-only\n"
        "LOG hi\n"
    )

    header = ph.scan(text)

    assert header.needs_editmode is True
    assert header.needs_playmode is True
    assert header.tags == ["slow", "smoke"]
    assert header.expect_steps == 4
    assert header.expect_failed == 1
    assert header.suite_only is True


def test_scan_returns_needs_player():
    """`@needs player` (E05): a Python-only CI fan-out selection token — Unity's
    own DSL execution has no such concept, so it is not mirrored to C#'s
    PlaytestHeaderScanner (unlike editmode/playmode, which gate Editor execution)."""
    header = ph.scan("# @needs player\nLOG hi\n")

    assert header.needs_player is True
    assert header.needs_editmode is False


def test_scan_merges_multiple_directive_lines():
    text = (
        "# @needs editmode\n"
        "LOG something\n"
        "# @needs playmode\n"
        "# @tags a\n"
        "# @tags b c\n"
        "# @tags a\n"  # duplicate — must not appear twice
    )

    header = ph.scan(text)

    assert header.needs_editmode is True
    assert header.needs_playmode is True
    assert header.tags == ["a", "b", "c"]


def test_scan_ignores_plain_comments():
    text = "# this is just a comment\n# another one, no @ directive\nLOG hi\n"

    header = ph.scan(text)

    assert header == ph.Header()


# ── Parity oracle: an independent transcription of PlaytestHeaderScanner.cs ──────

@dataclass
class _OracleHeader:
    needs_editmode: bool = False
    needs_playmode: bool = False
    tags: list = field(default_factory=list)
    expect_steps: object = None
    expect_failed: object = None
    suite_only: bool = False


_ORACLE_LINE = re.compile(r"^#\s*@(\w+)\s*(.*)$")


def _oracle_scan(text: str) -> _OracleHeader:
    header = _OracleHeader()
    if not text:
        return header
    tag_set = set()
    for line in text.split("\n"):
        m = _ORACLE_LINE.match(line.strip())
        if not m:
            continue
        directive, rest = m.group(1).lower(), m.group(2).strip()
        if directive == "needs":
            for tok in rest.split(" "):
                if tok == "editmode":
                    header.needs_editmode = True
                if tok == "playmode":
                    header.needs_playmode = True
        elif directive == "tags":
            for tok in rest.split(" "):
                if tok:
                    tag_set.add(tok)
        elif directive == "expect":
            for tok in rest.split(" "):
                if "=" not in tok:
                    continue
                k, _, v = tok.partition("=")
                try:
                    n = int(v)
                except ValueError:
                    continue
                if k == "steps":
                    header.expect_steps = n
                elif k == "failed":
                    header.expect_failed = n
        elif directive == "suite":
            header.suite_only = True
    header.tags = sorted(tag_set)
    return header


def test_scan_matches_csharp_scanner_on_all_fixture_files():
    fixtures = sorted(FIXTURE_ROOT.rglob("*.playtest"))
    assert fixtures, "expected at least one .playtest fixture under unity-test-project/Assets"

    for path in fixtures:
        text = path.read_text(encoding="utf-8")
        expected = _oracle_scan(text)
        actual = ph.scan(text)

        assert actual.needs_editmode == expected.needs_editmode, path
        assert actual.needs_playmode == expected.needs_playmode, path
        assert actual.tags == expected.tags, path
        assert actual.expect_steps == expected.expect_steps, path
        assert actual.expect_failed == expected.expect_failed, path
        assert actual.suite_only == expected.suite_only, path
