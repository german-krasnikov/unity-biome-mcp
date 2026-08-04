"""Reload wedge detection from Editor.log on disk.

Depends on editor_log_parser for log path discovery and build failure parsing.
"""
import re
from dataclasses import dataclass, field
from pathlib import Path

from .editor_log_parser import (
    classify_failure_currency,
    get_editor_log_path,
    get_editor_prev_log_path,
    parse_build_failure,
)


@dataclass
class WedgeReport:
    """Result of detect_wedge — describes the current wedge type and its evidence."""
    kind: str                         # 'build-failed-wedge' | 'stale-cache'
    cs_errors: list[str] = field(default_factory=list)
    failed_dlls: list[str] = field(default_factory=list)
    log_path: "Path | None" = None


def detect_wedge(
    log_path: "Path | None" = None,
    project_path: "Path | None" = None,
) -> "WedgeReport | None":
    """Pure disk authority: detect a reload wedge without needing TCP.

    M4: consults BOTH Editor.log AND Editor-prev.log; takes the most-recent
    reload-terminal across both (incident often rolls to -prev.log).

    Returns WedgeReport when a wedge is detected, None when clean.
    Refines to 'stale-cache' when EVERY cs_error crosschecks as stale-on-disk.
    """
    # Resolve log paths
    primary = log_path or get_editor_log_path()
    prev = None
    if primary is not None:
        prev_candidate = primary.parent / "Editor-prev.log"
        if prev_candidate.exists():
            prev = prev_candidate
    else:
        prev = get_editor_prev_log_path()

    best_text: str | None = None
    best_path: Path | None = None

    def _read(p: "Path | None") -> "tuple[str, Path] | None":
        if p is None or not p.exists():
            return None
        try:
            return p.read_text(encoding="utf-8", errors="replace"), p
        except OSError:
            return None

    primary_data = _read(primary)
    prev_data = _read(prev)

    if primary_data is None and prev_data is None:
        return None

    # Pick the log that contains a CURRENT failure (content-based selection).
    # Unity rotates logs by writing Editor-prev.log FIRST then opening a fresh
    # Editor.log, so Editor.log is always newer by mtime — mtime cannot be used.
    # Content wins; mtime is only a tiebreaker when both or neither are "current".
    if primary_data and prev_data:
        primary_bf = parse_build_failure(primary_data[0])
        prev_bf = parse_build_failure(prev_data[0])
        primary_currency = classify_failure_currency(primary_data[0], primary_bf)
        prev_currency = classify_failure_currency(prev_data[0], prev_bf)
        if primary_currency == "current":
            best_text, best_path = primary_data
        elif prev_currency == "current":
            best_text, best_path = prev_data
        else:
            # Neither is current — fall back to mtime (prefer newer)
            primary_mtime = primary.stat().st_mtime if primary and primary.exists() else 0
            prev_mtime = prev.stat().st_mtime if prev and prev.exists() else 0
            if prev_mtime > primary_mtime:
                best_text, best_path = prev_data
            else:
                best_text, best_path = primary_data
    elif primary_data:
        best_text, best_path = primary_data
    else:
        best_text, best_path = prev_data  # type: ignore[assignment]

    bf = parse_build_failure(best_text)
    currency = classify_failure_currency(best_text, bf)

    if currency != "current":
        return None

    # We have a current failure — build the report
    report = WedgeReport(
        kind="build-failed-wedge",
        cs_errors=bf.cs_errors,
        failed_dlls=bf.failed_dlls,
        log_path=best_path,
    )

    # Refine to stale-cache if EVERY cs_error crosschecks as stale-on-disk
    if bf.cs_errors and _all_errors_stale_on_disk(bf.cs_errors):
        report.kind = "stale-cache"

    return report


def _all_errors_stale_on_disk(cs_error_lines: list[str]) -> bool:
    """Return True iff every CS error line crosschecks as stale-on-disk."""
    # Parse each error line: "path(line,col): error CS####: msg"
    _RE_ERR_LINE = re.compile(r"^(.*?)\((\d+),\d+\):\s+error CS\d+:.*'(\w+)'[^']*'[^.]+\.(\w+)\(\)'")
    for line in cs_error_lines:
        m = _RE_ERR_LINE.match(line.strip())
        if not m:
            return False  # can't parse → ambiguous → real error wins
        file_path, lineno, type_name, member = m.groups()
        result = crosscheck_error_on_disk({
            "file": file_path,
            "line": int(lineno),
            "member": member,
            "type_name": type_name,
        })
        if result != "stale-on-disk":
            return False
    return True


def crosscheck_error_on_disk(cs_error: dict) -> str:
    """Check if a CS error's missing member still exists on disk.

    C2 fix: the named member token must appear inside the BRACE-SCOPE of the NAMED type,
    not anywhere in the file (StartTickPump appears 16×).

    Returns:
        'stale-on-disk'  — member found inside the named type's brace-scope → error is fixed
        'matches'        — member NOT found in scope → real error (or any ambiguity → real wins)

    On ANY parse ambiguity / missing-file → 'matches' (real error wins, never hide live errors).
    """
    file_path = cs_error.get("file", "")
    member = cs_error.get("member", "")
    type_name = cs_error.get("type_name", "")

    if not (file_path and member and type_name):
        return "matches"

    try:
        text = Path(file_path).read_text(encoding="utf-8", errors="replace")
    except (OSError, PermissionError):
        return "matches"

    # Find the brace-scope of the NAMED type.
    # Deliberate: matches only `class` — CS0535 on a `struct`/`record` falls through
    # to "matches" (real error wins), which is safe per C2.
    type_match = re.search(
        r"\bclass\s+" + re.escape(type_name) + r"\b[^{]*\{",
        text,
    )
    if not type_match:
        # Type not found → ambiguous → real error wins
        return "matches"

    # Walk forward from the type's opening brace to find matching closing brace
    start = type_match.end() - 1  # position of the '{'
    depth = 0
    end = len(text)
    for i in range(start, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                break

    scope_text = text[start:end]
    # Check each line in scope: member must appear on a non-comment code line
    # to count as "implemented" (a comment mentioning the name is NOT an implementation).
    for line in scope_text.splitlines():
        stripped = line.strip()
        if stripped.startswith("//") or stripped.startswith("*"):
            continue
        if re.search(r"\b" + re.escape(member) + r"\b", stripped):
            return "stale-on-disk"
    return "matches"
