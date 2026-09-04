"""The single Python `# @directive` header scanner for `.playtest` scripts.

Mirrors `unity-plugin/Editor/PlaytestHeaderScanner.cs` (`@needs`, `@tags`, `@expect`,
`@suite-only`). Pure text in, pure data out — no file I/O here; a caller reading a
`.playtest` from disk must pass `encoding="utf-8"` explicitly at that one read site
(`.claude/skills/encoding.md`, standards #17).

This is the only Python header scanner in the repo (B18). B19, C08, C18 and E05
import it — a second ad-hoc `@`-line regex in Python is a review rejection.
"""
import re
from dataclasses import dataclass, field

_DIRECTIVE_LINE = re.compile(r"^#\s*@(\w+)\s*(.*)$")

_NEEDS = "needs"
_TAGS = "tags"
_EXPECT = "expect"
_SUITE = "suite"
_NEEDS_EDITMODE = "editmode"
_NEEDS_PLAYMODE = "playmode"
_EXPECT_STEPS = "steps"
_EXPECT_FAILED = "failed"


@dataclass
class Header:
    needs_editmode: bool = False
    needs_playmode: bool = False
    tags: list[str] = field(default_factory=list)
    expect_steps: int | None = None
    expect_failed: int | None = None
    suite_only: bool = False


def scan(text: str) -> Header:
    """Scan every `# @directive` line (directives may appear anywhere in the
    script, not only in a leading comment block — matches the C# scanner).
    INCLUDE-d content is never scanned (documented MVP constraint, same as C#)."""
    header = Header()
    if not text:
        return header

    tags: set[str] = set()
    for raw_line in text.split("\n"):
        match = _DIRECTIVE_LINE.match(raw_line.strip())
        if not match:
            continue
        key = match.group(1).lower()
        rest = match.group(2).strip()
        _apply_directive(header, tags, key, rest)

    header.tags = sorted(tags)  # dedup + deterministic order, mirrors HashSet+Sort
    return header


def _apply_directive(header: Header, tags: set[str], key: str, rest: str) -> None:
    if key == _NEEDS:
        for token in rest.split():
            lowered = token.lower()
            if lowered == _NEEDS_EDITMODE:
                header.needs_editmode = True
            elif lowered == _NEEDS_PLAYMODE:
                header.needs_playmode = True
    elif key == _TAGS:
        tags.update(rest.split())
    elif key == _EXPECT:
        for pair in rest.split():
            pkey, sep, pval = pair.partition("=")
            if not sep:
                continue
            digits = pval[1:] if pval[:1] == "-" else pval
            if not digits.isdigit():
                continue
            if pkey.lower() == _EXPECT_STEPS:
                header.expect_steps = int(pval)
            elif pkey.lower() == _EXPECT_FAILED:
                header.expect_failed = int(pval)
    elif key == _SUITE:
        header.suite_only = True
    # else: unknown directive — silently ignored, never an error (forward-compat).
