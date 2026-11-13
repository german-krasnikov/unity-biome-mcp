"""Reload wedge detection from Editor.log on disk.

Depends on editor_log_parser for log path discovery and build failure parsing.
"""
import re
from dataclasses import dataclass, field
from pathlib import Path

from .decisions.generated.editorlog_logic import (
    ErrorOnDiskInput,
    LogPickInput,
    WedgeKindInput,
    error_on_disk,
    log_pick,
    wedge_kind,
)

from .editor_log_parser import (
    classify_failure_currency,
    get_editor_log_path,
    get_editor_prev_log_path,
    parse_build_failure,
)


# Wire values of the wedgeKind verdicts (the logic uses camelCase).
_WEDGE_WIRE = {"buildFailedWedge": "build-failed-wedge", "staleCache": "stale-cache"}


@dataclass
class WedgeReport:
    """Result of detect_wedge — describes the current wedge type and its evidence."""
    kind: str                         # 'build-failed-wedge' | 'stale-cache'
    cs_errors: list[str] = field(default_factory=list)
    failed_dlls: list[str] = field(default_factory=list)
    log_path: Path | None = None


def detect_wedge(
    log_path: Path | None = None,
    project_path: Path | None = None,
) -> WedgeReport | None:
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

    def _read(p: Path | None) -> tuple[str, Path] | None:
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

    # Content picks the log (dsl/decisions/editorlog.logic logPick); mtime is only the tiebreaker fact.
    # Unity writes Editor-prev.log FIRST then opens a fresh Editor.log, so Editor.log is always newer by mtime.
    primary_currency = prev_currency = "stale"
    prev_newer = False
    if primary_data and prev_data:
        primary_currency = classify_failure_currency(primary_data[0], parse_build_failure(primary_data[0]))
        prev_currency = classify_failure_currency(prev_data[0], parse_build_failure(prev_data[0]))
        prev_newer = prev.stat().st_mtime > primary.stat().st_mtime
    pick = log_pick(LogPickInput(
        log_has_primary=primary_data is not None,
        log_has_prev=prev_data is not None,
        log_primary_currency=primary_currency,
        log_prev_currency=prev_currency,
        log_prev_newer=prev_newer,
    ))
    best_text, best_path = prev_data if pick == "prev" else primary_data  # type: ignore[misc]

    bf = parse_build_failure(best_text)
    currency = classify_failure_currency(best_text, bf)

    unparsable, still_missing = _judge_errors(bf.cs_errors) if currency == "current" else (0, 0)
    kind = wedge_kind(WedgeKindInput(
        log_currency=currency,
        wedge_cs_errors=len(bf.cs_errors),
        wedge_unparsable=unparsable,
        wedge_still_missing=still_missing,
    ))
    if kind == "clean":
        return None
    return WedgeReport(
        kind=_WEDGE_WIRE[kind],
        cs_errors=bf.cs_errors,
        failed_dlls=bf.failed_dlls,
        log_path=best_path,
    )


_RE_ERR_LINE = re.compile(r"^(.*?)\((\d+),\d+\):\s+error CS\d+:.*'(\w+)'[^']*'[^.]+\.(\w+)\(\)'")


def _judge_errors(cs_error_lines: list[str]) -> tuple[int, int]:
    """(unparsable, still_missing) counts over the CS error lines: the facts of wedgeKind."""
    unparsable = still_missing = 0
    for line in cs_error_lines:
        m = _RE_ERR_LINE.match(line.strip())
        if not m:
            unparsable += 1  # can't parse -> ambiguous -> real error wins
            continue
        file_path, _lineno, type_name, member = m.groups()
        if crosscheck_error_on_disk({"file": file_path, "member": member, "type_name": type_name}) != "stale-on-disk":
            still_missing += 1
    return unparsable, still_missing


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

    complete = bool(file_path and member and type_name)
    text = ""
    readable = False
    if complete:
        try:
            text = Path(file_path).read_text(encoding="utf-8", errors="replace")
            readable = True
        except (OSError, PermissionError):
            pass

    # Brace-scope of the NAMED type. Deliberate: only `class` — CS0535 on a `struct`/`record`
    # stays "matches" (real error wins), which is safe per C2.
    scope_text = ""
    type_match = re.search(r"\bclass\s+" + re.escape(type_name) + r"\b[^{]*\{", text) if readable else None
    if type_match:
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

    # The member must appear on a non-comment code line (a comment mentioning the name is NOT an implementation).
    member_in_code = any(
        re.search(r"\b" + re.escape(member) + r"\b", stripped)
        for stripped in (line.strip() for line in scope_text.splitlines())
        if not stripped.startswith(("//", "*"))
    )
    return error_on_disk(ErrorOnDiskInput(
        err_complete=complete,
        err_file_readable=readable,
        err_type_found=type_match is not None,
        err_member_in_code=member_in_code,
    )).replace("staleOnDisk", "stale-on-disk")
