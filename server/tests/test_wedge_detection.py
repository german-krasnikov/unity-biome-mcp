"""Tests for FM-26 detection: parse_build_failure + classify_failure_currency.

P12: parse_build_failure — parse Tundra/Csc/reload markers from Editor.log text.
P13: classify_failure_currency — is the last reload-terminal a failure?
P14: crosscheck_error_on_disk — does cs_error still match on disk?
P15: detect_wedge — compose P12+P13+P14 consulting both Editor.log files.
G28: multi-block currency — only post-last-success block is current.
G32: Windows prev-log path.
G33: partial reload (started, no Mono-success) → current.
"""

import sys
import tempfile
from pathlib import Path

import pytest

FIXTURES = Path(__file__).parent / "fixtures"
WEDGE_LOG = FIXTURES / "fm26_reload_wedge.log"
CLEAN_LOG = FIXTURES / "fm26_reload_clean.log"


# ---------------------------------------------------------------------------
# P12 — parse_build_failure
# ---------------------------------------------------------------------------

def test_parse_build_failure_detects_tundra_failed():
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert result.tundra_failed is True


def test_parse_build_failure_detects_failed_dll():
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert any("UnityMCP.Editor.Chat.Tests" in d for d in result.failed_dlls)


def test_parse_build_failure_detects_editor_compiler_errors():
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert result.reload_aborted is True


def test_parse_build_failure_detects_glued_reload_failed():
    """The GLUED line Reloading assemblies failed.Asset Pipeline Refresh must be detected.

    A4: re.search substring, no ^ anchor — line is NOT newline-separated.
    """
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert result.reload_failed is True


def test_parse_build_failure_captures_cs_errors():
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert any("CS0535" in e for e in result.cs_errors)
    assert any("ISyncOps.StartTickPump" in e for e in result.cs_errors)


def test_parse_build_failure_no_reloaded_ok_in_wedge():
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert result.reloaded_ok is False


def test_parse_build_failure_detects_mono_success_in_clean_log():
    from unity_mcp.editor_log_parser import parse_build_failure
    text = CLEAN_LOG.read_text(encoding="utf-8", errors="replace")
    result = parse_build_failure(text)
    assert result.reloaded_ok is True
    assert result.tundra_failed is False
    assert result.reload_failed is False
    assert result.reload_aborted is False


def test_parse_build_failure_crlf_normalized():
    """CRLF-normalized text must still parse correctly."""
    from unity_mcp.editor_log_parser import parse_build_failure
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    crlf = text.replace("\n", "\r\n")
    result = parse_build_failure(crlf)
    assert result.tundra_failed is True
    assert result.reload_failed is True


def test_parse_build_failure_never_raises():
    from unity_mcp.editor_log_parser import parse_build_failure
    result = parse_build_failure("")
    assert result.tundra_failed is False


# ---------------------------------------------------------------------------
# P13 — classify_failure_currency
# ---------------------------------------------------------------------------

def test_classify_failure_currency_wedge_log_is_current():
    """Real incident log: last terminal is failure → CURRENT (the C1 bug fix)."""
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    text = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "current"


def test_classify_failure_currency_clean_log_is_stale():
    """Clean log: Mono-success follows failure terminal → STALE."""
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    text = CLEAN_LOG.read_text(encoding="utf-8", errors="replace")
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "stale"


def test_classify_failure_currency_stale_with_later_success():
    """Fail block THEN later build-START THEN Mono-success → stale."""
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    wedge = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    success = "\nMono: successfully reloaded assembly\n"
    text = wedge + success
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "stale"


def test_classify_failure_currency_inert_requested_does_not_make_stale():
    """Inert post-wedge [ScriptCompilation] Requested + 0.000s lines = PROOF of wedge, not a new build-START.

    A3: these lines AFTER failure terminal → still CURRENT, never STALE.
    """
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    wedge = WEDGE_LOG.read_text(encoding="utf-8", errors="replace")
    # Simulate what the real log has post-wedge (A3: these are inert no-ops)
    inert = (
        "\n[ScriptCompilation] Requested synchronous compilation\n"
        "script compilation time: 0.000\n"
        "[ScriptCompilation] Requested synchronous compilation\n"
        "script compilation time: 0.000\n"
    )
    text = wedge + inert
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "current"


# ---------------------------------------------------------------------------
# G28 — multi-block currency: only last post-success block is current
# ---------------------------------------------------------------------------

def test_multiblock_only_last_post_success_is_current():
    """Two failure blocks with Mono-success between them.

    The block BEFORE the success is stale; the block AFTER is current.
    classify_failure_currency must return 'current' (there IS a failure after last success).
    """
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    block1 = (
        "Editor compiler errors found. Will not reload assemblies.\n"
        "Reloading assemblies failed.Asset Pipeline Refresh (id=aaa)\n"
    )
    success = "Mono: successfully reloaded assembly\n"
    block2 = (
        "Editor compiler errors found. Will not reload assemblies.\n"
        "Reloading assemblies failed.Asset Pipeline Refresh (id=bbb)\n"
    )
    text = block1 + success + block2
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "current"


def test_multiblock_only_success_at_end_is_stale():
    """Two failure blocks, Mono-success is LAST → stale."""
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    block1 = "Editor compiler errors found. Will not reload assemblies.\n"
    block2 = "Reloading assemblies failed.Asset Pipeline Refresh (id=aaa)\n"
    success = "Mono: successfully reloaded assembly\n"
    text = block1 + block2 + success
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "stale"


# ---------------------------------------------------------------------------
# G33 — partial reload (started, no Mono-success) → current
# ---------------------------------------------------------------------------

def test_partial_reload_no_mono_is_current():
    """Reload started but no Mono-success terminal → CURRENT (wedged)."""
    from unity_mcp.editor_log_parser import parse_build_failure, classify_failure_currency
    text = (
        "Reloading assemblies after forced synchronous recompile.\n"
        "Editor compiler errors found. Will not reload assemblies.\n"
    )
    bf = parse_build_failure(text)
    assert classify_failure_currency(text, bf) == "current"


# ---------------------------------------------------------------------------
# P14 — crosscheck_error_on_disk
# ---------------------------------------------------------------------------

def test_crosscheck_matches_when_member_only_in_comment():
    """Member token present only inside a // comment in the type body → 'matches' (real error).

    The type exists on disk but the member is absent as executable code; the crosscheck
    must NOT treat a comment mention as a real implementation.
    """
    from unity_mcp.editor_log import crosscheck_error_on_disk

    with tempfile.NamedTemporaryFile(suffix=".cs", mode="w", delete=False, encoding="utf-8") as f:
        f.write(
            "public class MockSyncOpsForResume {\n"
            "    // StartTickPump missing here\n"
            "}\n"
        )
        cs_path = f.name

    error = {
        "file": cs_path,
        "line": 1,
        "member": "StartTickPump",
        "type_name": "MockSyncOpsForResume",
    }
    result = crosscheck_error_on_disk(error)
    assert result == "matches"


def test_crosscheck_error_stale_when_member_present_in_type_scope():
    """Member IS present inside the named type's brace scope → 'stale-on-disk'."""
    from unity_mcp.editor_log import crosscheck_error_on_disk

    with tempfile.NamedTemporaryFile(suffix=".cs", mode="w", delete=False, encoding="utf-8") as f:
        f.write(
            "public class MockSyncOpsForResume {\n"
            "    public void StartTickPump() { }\n"
            "}\n"
        )
        cs_path = f.name

    error = {
        "file": cs_path,
        "line": 1,
        "member": "StartTickPump",
        "type_name": "MockSyncOpsForResume",
    }
    result = crosscheck_error_on_disk(error)
    assert result == "stale-on-disk"


def test_crosscheck_error_c2_sibling_type_stays_matches():
    """C2 trap: member in a SIBLING type (not the named one) → 'matches' (real error wins).

    StartTickPump appears 16× in real code — must NOT downgrade to stale-on-disk.
    """
    from unity_mcp.editor_log import crosscheck_error_on_disk

    with tempfile.NamedTemporaryFile(suffix=".cs", mode="w", delete=False, encoding="utf-8") as f:
        f.write(
            "public interface ISyncOps { void StartTickPump(); }\n"
            "public class MockSyncOpsForResume { }\n"  # MISSING the method
        )
        cs_path = f.name

    error = {
        "file": cs_path,
        "line": 2,
        "member": "StartTickPump",
        "type_name": "MockSyncOpsForResume",
    }
    result = crosscheck_error_on_disk(error)
    # Method exists in sibling type but NOT in MockSyncOpsForResume → real error → matches
    assert result == "matches"


def test_crosscheck_error_unknown_when_file_missing():
    """Missing file → 'matches' (real error wins on ambiguity per C2)."""
    from unity_mcp.editor_log import crosscheck_error_on_disk

    error = {
        "file": "/nonexistent/path/Foo.cs",
        "line": 1,
        "member": "StartTickPump",
        "type_name": "MockSyncOpsForResume",
    }
    result = crosscheck_error_on_disk(error)
    assert result == "matches"


# ---------------------------------------------------------------------------
# P15 — detect_wedge + get_editor_prev_log_path
# ---------------------------------------------------------------------------

def test_detect_wedge_returns_none_for_clean_log(tmp_path):
    """Clean log (Mono-success present) → detect_wedge returns None."""
    from unity_mcp.editor_log import detect_wedge

    log = tmp_path / "Editor.log"
    log.write_text(CLEAN_LOG.read_text(encoding="utf-8"), encoding="utf-8")
    result = detect_wedge(log_path=log)
    assert result is None


def _make_wedge_log_with_missing_member(tmp_path: Path) -> "tuple[Path, Path]":
    """Create a self-contained wedge log + cs source where member is MISSING.

    Returns (log_path, cs_path) — the cs file does NOT have StartTickPump implemented.
    """
    cs = tmp_path / "ResumeGateTests.cs"
    cs.write_text(
        "public class MockSyncOpsForResume : ISyncOps {\n"
        "    // StartTickPump intentionally not implemented\n"
        "}\n",
        encoding="utf-8",
    )
    wedge_text = (
        f"{cs}(188,48): error CS0535: 'MockSyncOpsForResume' does not implement"
        " interface member 'ISyncOps.StartTickPump()'\n"
        "*** Tundra build failed (2.05 seconds), 28 items updated, 946 evaluated\n"
        "## Script Compilation Error for: Csc Library/Bee/artifacts/UnityMCP.Editor.Chat.Tests.dll (+2 others)\n"
        "Editor compiler errors found. Will not reload assemblies.\n"
        "Reloading assemblies failed.Asset Pipeline Refresh (id=abc)\n"
    )
    log = tmp_path / "Editor.log"
    log.write_text(wedge_text, encoding="utf-8")
    return log, cs


def test_detect_wedge_returns_build_failed_wedge_for_wedge_log(tmp_path):
    """Wedge log (member NOT in source) → detect_wedge returns 'build-failed-wedge'."""
    from unity_mcp.editor_log import detect_wedge

    log, _ = _make_wedge_log_with_missing_member(tmp_path)
    result = detect_wedge(log_path=log)
    assert result is not None
    assert result.kind == "build-failed-wedge"


def test_detect_wedge_includes_cs_errors_in_report(tmp_path):
    """WedgeReport must carry the CS error(s) from the wedge log."""
    from unity_mcp.editor_log import detect_wedge

    log, _ = _make_wedge_log_with_missing_member(tmp_path)
    result = detect_wedge(log_path=log)
    assert result is not None
    assert any("CS0535" in e for e in result.cs_errors)


def test_detect_wedge_includes_failed_dlls(tmp_path):
    """WedgeReport must list failed DLLs."""
    from unity_mcp.editor_log import detect_wedge

    log, _ = _make_wedge_log_with_missing_member(tmp_path)
    result = detect_wedge(log_path=log)
    assert result is not None
    assert any("UnityMCP.Editor.Chat.Tests" in d for d in result.failed_dlls)


def test_detect_wedge_stale_cache_when_all_errors_fixed(tmp_path):
    """All CS errors crosscheck as stale-on-disk → kind='stale-cache'."""
    from unity_mcp.editor_log import detect_wedge

    # Create a .cs file that HAS the missing member implemented
    cs = tmp_path / "ResumeGateTests.cs"
    cs.write_text(
        "public class MockSyncOpsForResume {\n"
        "    public void StartTickPump() { }\n"
        "}\n",
        encoding="utf-8",
    )

    # Build a minimal wedge log pointing to the temp cs file
    wedge_text = (
        f"{cs}(188,48): error CS0535: 'MockSyncOpsForResume' does not implement"
        " interface member 'ISyncOps.StartTickPump()'\n"
        "*** Tundra build failed (2.05 seconds), 28 items updated, 946 evaluated\n"
        "## Script Compilation Error for: Csc Library/Bee/artifacts/UnityMCP.Editor.Chat.Tests.dll (+2 others)\n"
        "Editor compiler errors found. Will not reload assemblies.\n"
        "Reloading assemblies failed.Asset Pipeline Refresh (id=abc)\n"
    )
    log = tmp_path / "Editor.log"
    log.write_text(wedge_text, encoding="utf-8")

    result = detect_wedge(log_path=log)
    assert result is not None
    assert result.kind == "stale-cache"


def test_detect_wedge_consults_prev_log_when_primary_is_clean(tmp_path):
    """M4: detect_wedge consults Editor-prev.log when Editor.log is clean but prev.log has
    a current wedge. Selection is content-based: 'current' log wins over 'stale' log.
    """
    from unity_mcp.editor_log import detect_wedge

    cs = tmp_path / "ResumeGateTests.cs"
    cs.write_text(
        "public class MockSyncOpsForResume : ISyncOps {\n"
        "    // StartTickPump intentionally missing\n"
        "}\n",
        encoding="utf-8",
    )
    wedge_text = (
        f"{cs}(188,48): error CS0535: 'MockSyncOpsForResume' does not implement"
        " interface member 'ISyncOps.StartTickPump()'\n"
        "Editor compiler errors found. Will not reload assemblies.\n"
        "Reloading assemblies failed.Asset Pipeline Refresh (id=abc)\n"
    )

    log = tmp_path / "Editor.log"
    log.write_text(CLEAN_LOG.read_text(encoding="utf-8"), encoding="utf-8")
    prev = tmp_path / "Editor-prev.log"
    prev.write_text(wedge_text, encoding="utf-8")

    result = detect_wedge(log_path=log)
    assert result is not None
    assert result.kind == "build-failed-wedge"


def test_detect_wedge_consults_prev_log_when_primary_is_newer_but_clean(tmp_path):
    """Production reality: Unity rotates logs — writes prev.log FIRST then opens fresh
    Editor.log, so Editor.log is ALWAYS newer. detect_wedge must select by CONTENT
    (which log has a current wedge), not by mtime. When Editor.log is newer but clean
    and Editor-prev.log is older but has the wedge, the wedge must be returned.
    """
    import time
    from unity_mcp.editor_log import detect_wedge

    cs = tmp_path / "ResumeGateTests.cs"
    cs.write_text(
        "public class MockSyncOpsForResume : ISyncOps {\n"
        "    // StartTickPump intentionally missing\n"
        "}\n",
        encoding="utf-8",
    )
    wedge_text = (
        f"{cs}(188,48): error CS0535: 'MockSyncOpsForResume' does not implement"
        " interface member 'ISyncOps.StartTickPump()'\n"
        "Editor compiler errors found. Will not reload assemblies.\n"
        "Reloading assemblies failed.Asset Pipeline Refresh (id=abc)\n"
    )

    # Write wedge to prev.log FIRST (older mtime — as Unity does in production)
    prev = tmp_path / "Editor-prev.log"
    prev.write_text(wedge_text, encoding="utf-8")

    # Write clean log AFTER (newer mtime — as Unity does: opens fresh Editor.log)
    time.sleep(0.02)
    log = tmp_path / "Editor.log"
    log.write_text(CLEAN_LOG.read_text(encoding="utf-8"), encoding="utf-8")

    # Editor.log is newer but clean; prev.log is older but has the wedge.
    # Content-based selection must return the wedge from prev.log.
    result = detect_wedge(log_path=log)
    assert result is not None, "detect_wedge must find the wedge in the older Editor-prev.log"
    assert result.kind == "build-failed-wedge"


# ---------------------------------------------------------------------------
# G32 — Windows prev-log path (monkeypatch sys.platform)
# ---------------------------------------------------------------------------

def test_prev_log_path_windows(monkeypatch):
    """get_editor_prev_log_path returns Windows path on win32."""
    from unity_mcp import editor_log_parser
    monkeypatch.setenv("LOCALAPPDATA", "C:\\Users\\user\\AppData\\Local")
    monkeypatch.setattr(sys, "platform", "win32")
    path = editor_log_parser.get_editor_prev_log_path()
    assert path is not None
    assert "Editor-prev.log" in str(path)
    assert "AppData" in str(path) or "Local" in str(path)
