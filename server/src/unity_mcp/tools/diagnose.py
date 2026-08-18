"""Python wrapper for the C# 'diagnose' command.

Sends the diagnose TCP command, parses the text wire-format, applies the
§3 anti-hallucination protocol, and returns ONE typed verdict string:

  CLEAN-LIVE          — all signals green, MVID determined, no errors
  FAIL:<CS>         — compile errors found (CS#### code or 'unknown')
  STALE-DOMAIN        — MVID unchanged after intended recompile (expected_compile=True)
  WEDGE-ENGINE        — iscompiling=true + cn_active=false + stamp_frozen
  WEDGE-STATE         — sync_state=compiling but compile=idle (state wedge)
  BUILD-FAILED-WEDGE  — log shows failed reload + guard keeps rejecting
  STALE-CACHE         — disk-fixed CS error not yet reimported
  TESTS-INVISIBLE     — Tests dll unknown(missing) → testables gap
  REBUILDING          — all dlls missing → mid-rebuild
  NO-OP               — idle-never, idle-stale, or MVID frozen (no compile expected)
  UNKNOWN             — connection error or UNDETERMINED stamp

Priority order: first match wins (see _verdict).
"""

from dataclasses import dataclass

from unity_mcp.editor_log import WedgeReport  # noqa: TC001

from ._common import bind

_send = None


@dataclass
class _DiagnoseFields:
    """Parsed fields from the diagnose wire-format response."""
    mvid: str = ""
    stamp: str = ""
    compile: str = ""          # e.g. "idle", "idle-failed", "idle-never", "idle-stale"
    sync_state: str = ""       # first field of sync= line
    iscompiling: bool = False
    cn_active: bool = False
    started: bool = False
    stamp_frozen: bool = False
    errors: str = ""
    log: str = ""
    dlls: str = ""             # raw dlls= token string (P16/A6)
    guard_rejected: bool = False   # P16: Unity busy guard text received
    reload_failed: bool = False    # C10: in-process reload failure (authoritative)
    main_mvid: str = ""        # F3/F5: main-asmdef MVID (absent=not loaded)
    all_errors: str = ""       # FIX-1: cross-asmdef compile errors with explicit CS codes
    is_really_compiling: bool = False  # MCPServer event-driven flag (accurate, no stale latch)


_KNOWN_KEYS = frozenset(
    ["mvid=", "stamp=", "compile=", "sync=", "iscompiling=", "dlls=", "errors=", "log=",
     "main_mvid=", "reload_failed=", "all_errors=", "isReallyCompiling="]
)

# Guard-reject signal substrings (Unity is compiling, guard blocked the command)
_GUARD_PHRASES = ("Unity is compiling", "Retry in")


def _parse_diagnose(text: str) -> _DiagnoseFields:
    """Parse the diagnose wire-format (one key=value per line).

    The 'errors=' block may span multiple lines but ends when a new known key
    starts (e.g. 'log=').

    P16: if the text looks like a guard-reject message (not a normal wire response),
    sets guard_rejected=True and stamp=UNDETERMINED so the caller can detect the busy state.
    """
    f = _DiagnoseFields()

    # Detect guard-reject text (Unity busy reply, not a wire-format response)
    stripped_text = text.strip()
    if any(phrase in stripped_text for phrase in _GUARD_PHRASES) and "compile=" not in stripped_text:
        f.guard_rejected = True
        f.stamp = "UNDETERMINED"
        return f

    errors_lines: list[str] = []
    in_errors = False
    all_errors_lines: list[str] = []
    in_all_errors = False

    for line in text.splitlines():
        if in_errors:
            # Exit errors block when a known key starts
            if any(line.startswith(k) for k in _KNOWN_KEYS):
                in_errors = False
            else:
                errors_lines.append(line)
                continue

        if in_all_errors:
            if any(line.startswith(k) for k in _KNOWN_KEYS):
                in_all_errors = False
            else:
                all_errors_lines.append(line)
                continue

        if line.startswith("mvid="):
            f.mvid = line[5:].strip()
        elif line.startswith("stamp="):
            f.stamp = line[6:].strip()
        elif line.startswith("compile="):
            # "compile=idle|8.2" or "compile=idle-failed|4.1" → take state before '|'
            raw = line[8:].split()[0] if line[8:].split() else ""
            f.compile = raw.split("|")[0]
        elif line.startswith("sync="):
            # "sync=ready  epoch=3" → first token after '='
            rest = line[5:].strip()
            f.sync_state = rest.split()[0] if rest else ""
        elif line.startswith("iscompiling="):
            # Line may be "iscompiling=true  cn_active=false  started=true  stamp_frozen=false"
            for part in line.split():
                k, _, v = part.partition("=")
                if k == "iscompiling":
                    f.iscompiling = v.lower() == "true"
                elif k == "cn_active":
                    f.cn_active = v.lower() == "true"
                elif k == "started":
                    f.started = v.lower() == "true"
                elif k == "stamp_frozen":
                    f.stamp_frozen = v.lower() == "true"
        elif line.startswith("main_mvid="):
            f.main_mvid = line[10:].strip()
        elif line.startswith("reload_failed="):
            f.reload_failed = line[14:].strip().lower() == "true"
        elif line.startswith("isReallyCompiling="):
            f.is_really_compiling = line[18:].strip().lower() == "true"
        elif line.startswith("dlls="):
            f.dlls = line[5:].strip()
        elif line.startswith("errors="):
            rest = line[7:]
            if rest:
                errors_lines.append(rest)
            in_errors = True  # errors block may span multiple lines
        elif line.startswith("log="):
            f.log = line[4:].strip()
        elif line.startswith("all_errors="):
            rest = line[11:]
            if rest:
                all_errors_lines.append(rest)
            in_all_errors = True

    f.errors = "\n".join(errors_lines).strip()
    f.all_errors = "\n".join(all_errors_lines).strip()
    return f


def _first_cs(errors: str) -> str:
    """Extract first CS#### code from error text. Returns '' if not found."""
    import re
    m = re.search(r"error (CS\d+)", errors)
    return m.group(1) if m else ""


def _first_cs_from_all(all_errors: str) -> str:
    """Extract first CS#### from all_errors= format 'AsmName:CS####:file:line: msg'."""
    import re
    m = re.search(r":(CS\d+):", all_errors)
    return m.group(1) if m else ""


def _parse_dlls(dlls_str: str) -> list[tuple[str, str]]:
    """Parse dlls= token string into [(name, status), ...].

    Format: 'Name1:ticks:status,Name2:ticks:status(detail),...'
    Status is the third colon-delimited field (may contain parens).
    """
    result = []
    for token in dlls_str.split(","):
        parts = token.split(":")
        if len(parts) >= 3:
            name = parts[0]
            status = parts[2]  # 'fresh' | 'stale' | 'unknown(missing)' | 'unknown(no-src)'
            result.append((name, status))
    return result


def _verdict(
    fields: _DiagnoseFields,
    prev_mvid: str = "",
    wedge: WedgeReport | None = None,
    expected_compile: bool = True,
) -> str:
    """Apply §3 protocol priority order. Returns ONE verdict string.

    Priority (first match wins) — spec §2:
      1.  errors= has CS codes                        → FAIL:<CS>           [ground truth, always wins]
      2.  stamp UNDETERMINED                          → UNKNOWN
      3.  build-failed-wedge log                      → BUILD-FAILED-WEDGE    [before WEDGE-ENGINE: different remedy]
      4.  stale-cache log                             → STALE-CACHE
      5.  Tests dll unknown(missing)                  → TESTS-INVISIBLE
      6.  ALL dlls unknown(missing)                   → REBUILDING
      7.  WEDGE-ENGINE fingerprint                    → WEDGE-ENGINE
      8.  WEDGE-STATE fingerprint                     → WEDGE-STATE
      9.  idle-failed                                 → FAIL:<CS|unknown>
      9.5 iscompiling + idle-never + stale-dlls       → STALE-TRANSIENT       [package-resolve transient]
      9.7 prod dll :stale                             → FAIL:stale-dll      [before idle-never to avoid masking]
      10. idle-never / idle-stale                     → NO-OP
      11. prev_mvid + frozen + expected               → STALE-DOMAIN          [gated on expected_compile, A5]
      12. prev_mvid + frozen + !expected              → NO-OP                 [cache-hit is clean, A5]
      13. log errors                                  → FAIL:<log>
      14. stamp set                                   → CLEAN-LIVE
      15. fallthrough                                 → UNKNOWN
    """
    # 1. Compile errors — in-memory C# capture, ground truth, always wins
    if "error CS" in fields.errors or fields.all_errors:
        cs = _first_cs(fields.errors) or _first_cs_from_all(fields.all_errors)
        return f"FAIL:{cs}" if cs else "FAIL:unknown"

    # 2. Undetermined stamp → can't assert domain identity
    if fields.stamp == "UNDETERMINED":
        return "UNKNOWN"

    # 3. Build-failed-wedge: log authority OR C10 in-process reload_failed signal.
    #    reload_failed=true is authoritative even when wedge=None (log rolled/stale).
    #    Must precede WEDGE-ENGINE: wrong remedy otherwise (restart vs reimport).
    _build_failed_wedge = (
        (wedge is not None and wedge.kind == "build-failed-wedge"
         and (fields.iscompiling or fields.guard_rejected))
        or fields.reload_failed
    )
    if _build_failed_wedge:
        dlls_info = (
            ", ".join(wedge.failed_dlls)
            if wedge is not None and wedge.failed_dlls
            else "unknown"
        )
        return (
            f"BUILD-FAILED-WEDGE: reload failed on {dlls_info} — "
            "reimport the file: package (sync), do NOT restart"
        )

    # 4. Stale-cache (disk already fixed, domain not reimported)
    if wedge is not None and wedge.kind == "stale-cache":
        return "STALE-CACHE: stale-on-disk — reimport the file: package"

    # 5/6. dlls= tokens (A6): evaluated before fingerprint wedges
    parsed_dlls = _parse_dlls(fields.dlls) if fields.dlls else []
    if parsed_dlls:
        all_missing = all("missing" in status for _, status in parsed_dlls)
        tests_missing = any(
            "Tests" in name and "missing" in status
            for name, status in parsed_dlls
        )

        if all_missing:         # slot 6: ALL missing → mid-rebuild (superset of tests-missing)
            return "REBUILDING"
        if tests_missing:       # slot 5: only Tests missing → testables gap
            return "TESTS-INVISIBLE"

    # 7. Engine wedge: iscompiling=true + cn_active=false + stamp_frozen
    # Also fires when is_really_compiling=false (MCPServer never saw compilationStarted)
    # — catches stale isCompiling latch even when CompileNotifier.IsCompiling=true.
    stale_latch = fields.iscompiling and not fields.is_really_compiling and fields.stamp_frozen
    if (fields.iscompiling and not fields.cn_active and fields.stamp_frozen) or stale_latch:
        return "WEDGE-ENGINE"

    # 8. State wedge: sync says compiling but compile=idle (real reload, not retry)
    if fields.sync_state == "compiling" and fields.compile == "idle":
        return "WEDGE-STATE"

    # 9. idle-failed — checked before MVID match; SessionState may wipe errors on reconnect
    if fields.compile == "idle-failed":
        cs = _first_cs(fields.errors) or _first_cs_from_all(fields.all_errors)
        if cs:
            return f"FAIL:{cs}"
        # No CS code found — idle-failed flag may be stale or from non-MCP assembly.
        # Corroborate with dll freshness and log before declaring failure.
        has_stale_dll = parsed_dlls and any(s == "stale" for _, s in parsed_dlls)
        has_log_errors = fields.log not in ("clean", "absent", "")
        if has_stale_dll or has_log_errors or fields.reload_failed:
            return "FAIL:unknown"
        # No corroborating evidence — fall through to remaining slots

    # 9.5. iscompiling + idle-never + stale-dlls = package-resolve transient state
    if fields.iscompiling and fields.compile == "idle-never" and \
            parsed_dlls and any(s == "stale" for _, s in parsed_dlls):
        return "STALE-TRANSIENT"

    # 9.7. Prod dll stale — must precede idle-never so stale is never masked by NO-OP
    # Gate on compile != "idle": stale DLL during idle means domain just reloaded clean;
    # only warn when something is actively wrong (compiling/failed/etc).
    if parsed_dlls and any(status == "stale" for _, status in parsed_dlls) \
            and fields.compile != "idle":
        return "FAIL:stale-dll"

    # 10. Never compiled / self-cleared stale → NO-OP
    if fields.compile in ("idle-never", "idle-stale"):
        return "NO-OP"

    # 11/12. MVID check gated on expected_compile (A5)
    if prev_mvid and fields.mvid and fields.mvid == prev_mvid:
        if expected_compile:
            return "STALE-DOMAIN"   # slot 11: compile was expected, MVID froze → stale
        return "NO-OP"          # slot 12: cache-hit / no compile expected → clean

    # 13. Log errors
    if fields.log not in ("clean", "absent", ""):
        return f"FAIL:{fields.log}"

    # 15. All green
    if fields.stamp and fields.stamp != "UNDETERMINED":
        return "CLEAN-LIVE"

    return "UNKNOWN"  # 16. fallthrough


async def diagnose(prev_mvid: str = "", expected_compile: bool = True) -> str:
    """Read Unity compile/reload fact-signals atomically; returns typed verdict. For scene symptom analysis, use `debug`. For runtime component state, use `debug_animator` or `debug_physics`.

    prev_mvid: MVID from before a sync operation. When provided, enables STALE-DOMAIN
    detection (unchanged MVID after intended recompile). Pass '' for standalone probing.

    expected_compile: True when a compile was explicitly triggered (default).
    False for Bee cache-hit / will_compile=false / reverted-edit probes — prevents
    false STALE-DOMAIN on legitimately-frozen MVID (A5/G27).

    Returns: CLEAN-LIVE / FAIL:<CS> / STALE-DOMAIN / WEDGE-ENGINE / WEDGE-STATE /
             BUILD-FAILED-WEDGE / STALE-CACHE / TESTS-INVISIBLE / REBUILDING /
             NO-OP / UNKNOWN
    """
    try:
        raw = await _send("diagnose", {})
    except (ConnectionError, OSError, TimeoutError):
        return "UNKNOWN"

    fields = _parse_diagnose(raw)

    # Consult the log-based wedge detector (pure disk, works even when TCP is wedged)
    wedge = None
    try:
        from unity_mcp.editor_log import detect_wedge
        wedge = detect_wedge()
    except Exception:
        pass  # disk read failure → no wedge info, degrade gracefully

    return _verdict(fields, prev_mvid=prev_mvid, wedge=wedge,
                    expected_compile=expected_compile)


def register(mcp, send, args):
    bind(globals(), send, args)
    from ._annotations import RO as _RO
    mcp.tool(annotations=_RO)(diagnose)
