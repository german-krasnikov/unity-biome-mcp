"""Pure parsing functions for Unity Editor.log.

No IO side-effects — all functions take strings/paths and return data.
Used by editor_log.py for corroboration logic.
"""
import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

# Matches: Assets/Foo.cs(12,5): error CS0117: message
_RE_ERROR = re.compile(r"^(.*?)\((\d+),(\d+)\):\s+(error CS\d+:.*)")
_STRONG_BROKEN = "All compiler errors have to be fixed"
_OUTPUT_MARKER = "##### Output"
# Unity 6 Bee failure-diagnostic marker — only present in FAILED compiles
_FAILURE_HEADER = "## Script Compilation Error for:"
# Lines starting with these strings end the ##### Output section
_SECTION_BOUNDARIES = ("##### ", "## ", "*** ")


def get_editor_log_path(env_override: str | None = None) -> Path | None:
    """Return Editor.log path. env_override or UNITY_MCP_EDITOR_LOG env wins.

    When env override points to a non-existent path, warns to stderr (user explicitly
    asked for corroboration; silent degradation would be confusing).
    """
    raw = env_override or os.environ.get("UNITY_MCP_EDITOR_LOG")
    if raw:
        p = Path(raw)
        if not p.exists():
            print(f"warning: UNITY_MCP_EDITOR_LOG path does not exist: {raw}", file=sys.stderr)
        return p  # return even if not exist; caller decides

    home = Path.home()
    platform = sys.platform
    if platform == "darwin":
        p = home / "Library" / "Logs" / "Unity" / "Editor.log"
    elif platform == "win32":
        local = os.environ.get("LOCALAPPDATA", "")
        p = Path(local) / "Unity" / "Editor" / "Editor.log" if local else None
    else:
        p = home / ".config" / "unity3d" / "Editor.log"

    if p is None:
        return None
    return p if p.exists() else None


def parse_compile_errors_from_log(log_path: Path, max_bytes: int = 256_000) -> list[str]:
    """Read FULL log file, find LATEST Csc FAILURE block, return error lines.

    Anchors on '## Script Compilation Error for:' (Unity 6 Bee failure marker).
    A successful compile never writes this header, so a parallel assembly that
    succeeds AFTER a failure cannot cause a false-negative.

    max_bytes is kept as a parameter for the legacy ExitCode path only (tail heuristic).
    For the primary Bee path we always read the full file — the header may be outside
    the last 256 KB (large log with many assemblies after the failure).

    Returns [] if no failure header found (clean).
    Never raises.
    """
    try:
        with open(log_path, encoding="utf-8", errors="replace") as f:
            text = f.read()
    except (OSError, PermissionError):
        return []

    # Anchor: last failure header in the FULL file.
    # If not found → no failed compile → fall back to legacy format using only the tail.
    header_pos = text.rfind(_FAILURE_HEADER)
    if header_pos == -1:
        # Fall back to legacy format (no ## Script Compilation Error header):
        # check for ##### ExitCode with nonzero value + ##### Output.
        # Legacy path is a tail heuristic so we still honor max_bytes here.
        tail = text[max(0, len(text) - max_bytes):]
        return _parse_legacy_format(tail)

    # The ##### Output block with actual errors lives BEFORE the header.
    # Find the last ##### Output that precedes the header.
    output_pos = text.rfind(_OUTPUT_MARKER, 0, header_pos)
    if output_pos == -1:
        return _collect_strong_broken(text[:header_pos])

    # Collect error lines from ##### Output up to the header (or next section boundary)
    output_content = text[output_pos + len(_OUTPUT_MARKER):header_pos]
    return _collect_errors(output_content)


def _parse_legacy_format(text: str) -> list[str]:
    """Fallback: parse old-format logs without the ## Script Compilation Error header.

    rfind picks the LAST ExitCode block — on legacy single-assembly Csc that's the only
    block, so this is correct. Unity 6 never reaches this path (it always writes the
    '## Script Compilation Error for:' header), so multi-assembly ambiguity is not
    a concern here; single-assembly legacy is an accepted trade-off.
    """
    exit_marker = "##### ExitCode"
    exit_pos = text.rfind(exit_marker)
    if exit_pos == -1:
        return []

    after = text[exit_pos + len(exit_marker):]
    exit_code_str = next((l.strip() for l in after.splitlines() if l.strip()), "")
    try:
        if int(exit_code_str) == 0:
            return []
    except ValueError:
        pass  # treat as failure

    output_start = after.find(_OUTPUT_MARKER)
    if output_start == -1:
        return []
    return _collect_errors(after[output_start + len(_OUTPUT_MARKER):])


def _collect_errors(output_content: str) -> list[str]:
    """Extract CS error lines from an Output block body."""
    errors: list[str] = []
    for line in output_content.splitlines():
        if any(line.startswith(b) for b in _SECTION_BOUNDARIES):
            break
        stripped = line.strip()
        m = _RE_ERROR.match(stripped)
        if m:
            filepath, lineno, col, rest = m.groups()
            errors.append(f"{filepath}:{lineno}:{col}: {rest}")
            continue
        if _STRONG_BROKEN in stripped:
            errors.append(
                "compile broken: All compiler errors have to be fixed"
                " [no-cs-code] DO NOT GUESS — run get_console"
            )
    return errors


def _collect_strong_broken(text_before_header: str) -> list[str]:
    """Check for STRONG_BROKEN signal before the failure header."""
    for line in text_before_header.splitlines():
        if _STRONG_BROKEN in line:
            return [
                "compile broken: All compiler errors have to be fixed"
                " [no-cs-code] DO NOT GUESS — run get_console"
            ]
    return []


# ---------------------------------------------------------------------------
# P12 — BuildFailure dataclass + parse_build_failure
# ---------------------------------------------------------------------------

# Markers for reload-terminal events (A4: re.search substring, no ^/$ anchors)
_TUNDRA_FAILED = "*** Tundra build failed"
_SCRIPT_COMP_ERROR = "## Script Compilation Error for:"
_RELOAD_ABORTED = "Editor compiler errors found. Will not reload assemblies."
_RELOAD_FAILED = "Reloading assemblies failed."  # substring; GLUED line has no newline before Asset Pipeline
_MONO_SUCCESS = "Mono: successfully reloaded assembly"

# CS error anywhere in the log text (not inside a ##### Output block necessarily)
_RE_CS_ERROR_INLINE = re.compile(r"error (CS\d+):")
# Extract DLL name from ## Script Compilation Error for: Csc .../Foo.dll
_RE_FAILED_DLL = re.compile(r"## Script Compilation Error for:.*?/([\w.]+\.dll)")


@dataclass
class BuildFailure:
    """Result of parse_build_failure — pure data, no IO."""
    tundra_failed: bool = False
    reload_failed: bool = False       # "Reloading assemblies failed." substring present
    reload_aborted: bool = False      # "Editor compiler errors found. Will not reload assemblies."
    reloaded_ok: bool = False         # "Mono: successfully reloaded assembly" present
    failed_dlls: list[str] = field(default_factory=list)
    cs_errors: list[str] = field(default_factory=list)
    block_offset: int = -1            # offset of the LAST reload-terminal event


def parse_build_failure(text: str) -> BuildFailure:
    """Parse authoritative FM-26 markers from Editor.log text.

    A4: re.search substring match only, no ^ / $ anchors.
    CRLF-normalized before any search.
    Never raises.
    """
    # CRLF → LF normalisation
    text = text.replace("\r\n", "\n").replace("\r", "\n")

    bf = BuildFailure()
    bf.tundra_failed = bool(re.search(re.escape(_TUNDRA_FAILED), text))
    bf.reload_aborted = bool(re.search(re.escape(_RELOAD_ABORTED), text))
    bf.reload_failed = bool(re.search(re.escape(_RELOAD_FAILED), text))
    bf.reloaded_ok = bool(re.search(re.escape(_MONO_SUCCESS), text))

    bf.failed_dlls = _RE_FAILED_DLL.findall(text)

    # Collect CS error codes and their surrounding line context
    bf.cs_errors = [line.strip() for line in text.splitlines() if re.search(r"error CS\d+:", line)]

    # block_offset = position of the last reload-terminal event
    last = -1
    for marker in (_RELOAD_ABORTED, _RELOAD_FAILED):
        pos = text.rfind(marker)
        if pos > last:
            last = pos
    bf.block_offset = last

    return bf


# ---------------------------------------------------------------------------
# P13 — classify_failure_currency (A3 currency rule)
# ---------------------------------------------------------------------------

def classify_failure_currency(text: str, build_failure: BuildFailure) -> str:
    """Return 'current' if the last reload-terminal is a failure with no Mono-success after it.

    A3 currency rule:
    - CURRENT iff last reload-TERMINAL is a FAILURE and no Mono-success follows it.
    - Build-START anchors (## Script Compilation Error) ONLY scope WHICH block.
    - Inert post-wedge [ScriptCompilation] Requested + 0.000s lines = proof of wedge, NOT new build-START.

    G28: multi-block — only the block after the last Mono-success matters.
    G33: reload started but no Mono-success terminal → CURRENT.
    """
    # CRLF → LF
    text = text.replace("\r\n", "\n").replace("\r", "\n")

    # Find last reload-terminal failure position
    last_failure = -1
    for marker in (_RELOAD_ABORTED, _RELOAD_FAILED):
        pos = text.rfind(marker)
        if pos > last_failure:
            last_failure = pos

    if last_failure == -1:
        # No failure terminal at all
        return "stale"

    # Find last Mono-success position.
    # Inert post-wedge "[ScriptCompilation] Requested + script compilation time: 0.000s"
    # lines contain no Mono-success, so the rfind below naturally stays after the failure
    # terminal — no explicit branch needed (A3: they are proof of wedge, not new build-START).
    last_success = text.rfind(_MONO_SUCCESS)

    # CURRENT iff failure is after (or there is no) success
    if last_success == -1 or last_failure > last_success:
        return "current"
    return "stale"


# ---------------------------------------------------------------------------
# G32 — get_editor_prev_log_path (sibling to get_editor_log_path)
# ---------------------------------------------------------------------------

def get_editor_prev_log_path(env_override: str | None = None) -> Path | None:
    """Return Editor-prev.log path for forensic incident lookup (M4).

    Mirrors get_editor_log_path but resolves to Editor-prev.log.
    G32: covers win32 (%LOCALAPPDATA%) + darwin + linux.
    """
    # Allow explicit override (primarily for tests)
    if env_override:
        return Path(env_override)

    home = Path.home()
    platform = sys.platform
    if platform == "darwin":
        p = home / "Library" / "Logs" / "Unity" / "Editor-prev.log"
    elif platform == "win32":
        local = os.environ.get("LOCALAPPDATA", "")
        p = Path(local) / "Unity" / "Editor" / "Editor-prev.log" if local else None
    else:
        p = home / ".config" / "unity3d" / "Editor-prev.log"

    if p is None:
        return None
    return p  # caller checks existence
