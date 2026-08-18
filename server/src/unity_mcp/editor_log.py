"""Out-of-band compile verifier: reads Unity's Editor.log from disk.

Does NOT depend on the C# plugin being compilable — pure disk IO.
Used by get_compile_errors / await_compile to corroborate "clean" responses
when the plugin dll may be stale.

Unity 6 (Bee/Csc) log format — FAILED compile writes this header line:
    ## Script Compilation Error for: Csc Library/Bee/.../UnityMCP.Editor.dll (+2 others)

A successful compile writes NO such header. We rfind this header as the failure anchor
instead of rfind(ExitCode) to avoid being fooled by a parallel assembly that later
succeeds (ExitCode=0 would be the last ExitCode, giving a false-negative).

The errors live in the ##### Output block BEFORE the header (lines 79707-79733 in real log).
"""

from pathlib import Path  # noqa: TC003

from .editor_log_freshness import (  # noqa: F401 — re-export
    check_dll_freshness,
    find_plugin_source_dir,
    find_plugin_source_files,
)
from .editor_log_parser import (  # noqa: F401 — re-export for back-compat
    BuildFailure,
    classify_failure_currency,
    get_editor_log_path,
    get_editor_prev_log_path,
    parse_build_failure,
    parse_compile_errors_from_log,
)
from .editor_log_wedge import (  # noqa: F401 — re-export
    WedgeReport,
    crosscheck_error_on_disk,
    detect_wedge,
)

# Module-level cached state for centralized corroboration
_cor_project_path = None
_cor_log_path = None
_cor_source_dirs = None   # legacy — kept for back-compat with tests that patch it
_cor_source_files = None  # RC-4: scoped list (excludes Chat/Tests asmdefs)


def corroborate_compile_status(
    csharp_response: str,
    project_path: Path | None = None,
    log_path: Path | None = None,
    source_dirs: list[Path] | None = None,
    source_files: list[Path] | None = None,
    compile_status: str = "",
) -> str:
    """Corroborate a "clean" C# response against Editor.log.

    Override C#'s "clean" ONLY when BOTH signals present:
      - log has error lines (stale failure block)
      - dll is definitively stale (fresh is False)

    3rd-signal gate (P2): stale log CS errors are only resurrected when
    compile_status == "idle-failed" — prevents false positives from old Bee
    blocks after a fix has been applied but the log block lingers.

    A fresh or undeterminable dll means the log error is stale → trust C#.

    source_files (preferred): scoped list from find_plugin_source_files() — excludes Chat/Tests.
    source_dirs (legacy): rglobs dirs (may pick up Chat/Tests asmdefs).
    """
    if "error CS" in csharp_response:
        return csharp_response

    # No log path → graceful pass-through (CI without Unity, tests with mocked _send).
    if log_path is None:
        return csharp_response

    # Prefer source_files (scoped); fall back to source_dirs; then auto-detect (legacy).
    if source_files is not None:
        fresh = (
            check_dll_freshness(project_path, source_files=source_files)
            if project_path is not None
            else None
        )
    else:
        effective_source_dirs = source_dirs if source_dirs is not None else find_plugin_source_dir()
        fresh = (
            check_dll_freshness(project_path, source_dirs=effective_source_dirs)
            if project_path is not None
            else None
        )

    log_errors = parse_compile_errors_from_log(log_path)

    if log_errors and fresh is False:
        # 3rd-signal gate: only resurrect stale log block when C# confirms idle-failed.
        # Without this, a lingering Bee failure block from a previous run causes FP.
        if compile_status and compile_status != "idle-failed":
            # compile_status says we're not in a failed state → log block is stale, trust C#
            pass
        else:
            # Both signals confirmed (+ optional idle-failed agreement) → genuine stale dll.
            return "[editor.log - dll stale]\n" + "\n".join(log_errors)

    if fresh is False and compile_status != "idle":
        # Stale dll but no log errors → soft warn only.
        # Suppressed when compile_status="idle": Unity compiled clean this session, log is stale.
        return csharp_response + "\n[warn: UnityMCP.Editor.dll may be stale - consider recompiling]"

    return csharp_response


def init_corroboration(port: int | None = None) -> None:
    """Autodetect + cache project/log paths and scoped plugin source files once at startup.

    UNITY_MCP_PROJECT_PATH override wins over port-file autodetect.
    Idempotent — safe to call from multiple register() functions.
    port: Unity TCP port used to look up project path from port file (RC-3).
    """
    global _cor_project_path, _cor_log_path, _cor_source_files
    from .compile_state import CompileStateProbe  # lazy import — avoid circular at module load
    _cor_project_path = CompileStateProbe.autodetect_project_path(port=port)
    _cor_log_path = get_editor_log_path()
    # RC-4: use scoped file list (excludes Chat/Tests asmdefs) — not the wide rglob dir list
    _cor_source_files = find_plugin_source_files()


def corroborate(csharp_response: str, compile_status: str = "") -> str:
    """Corroborate using cached project/log/source_files set by init_corroboration()."""
    return corroborate_compile_status(
        csharp_response,
        _cor_project_path,
        _cor_log_path,
        source_files=_cor_source_files,
        compile_status=compile_status,
    )


async def get_corroborated_errors(send, compile_status: str = "") -> str:
    """Shared helper: get compile errors from C#, corroborate, strip clean sentinel.

    Sentinel-strip lives in exactly one place (P3). Both sync.py and code_intel.py
    import this. Returns "" when C# says "No compilation errors" and log agrees.
    compile_status: passed to corroborate to gate soft-warn (e.g. "idle" suppresses it).
    """
    try:
        csharp = await send("get_compile_errors", {})
    except ConnectionError:
        return ""
    out = corroborate(csharp, compile_status=compile_status)
    # Strip the clean sentinel — it's not an error payload.
    if csharp.strip() == "No compilation errors" and out == csharp:
        return ""
    return out
