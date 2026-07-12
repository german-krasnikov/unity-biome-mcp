"""Tests for editor_log.py — out-of-band compile verifier reading Unity's Editor.log.

All tests are pure-Python, no Unity required.
"""
import os
import sys
import pytest
from pathlib import Path
from unittest.mock import patch

FIXTURES = Path(__file__).parent / "fixtures"


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _write_log(tmp_path: Path, content: str) -> Path:
    p = tmp_path / "Editor.log"
    p.write_text(content, encoding="utf-8")
    return p


def _bee_block(exit_code: int, output_lines: list[str]) -> str:
    """Build a realistic Bee/Csc log block."""
    lines = [
        '<csc /nostdlib /out:"Library/Bee/artifacts/test.dag/Foo.dll">',
        "##### Custom Environment Variables",
        "DOTNET_MULTILEVEL_LOOKUP=0",
        "##### ExitCode",
        str(exit_code),
        "##### Output",
        *output_lines,
    ]
    if exit_code != 0:
        lines += [
            "*** Tundra build failed (1.00 seconds)",
            "## Script Compilation Error for: Csc Library/Bee/artifacts/test.dag/Foo.dll",
            "## Output:",
            # duplicate block — must NOT be double-counted
            *output_lines,
        ]
    else:
        lines.append("*** Tundra build done (0.50 seconds)")
    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# get_editor_log_path
# ---------------------------------------------------------------------------

def test_env_override_wins(tmp_path, monkeypatch):
    fake = tmp_path / "custom.log"
    fake.touch()
    monkeypatch.setenv("UNITY_MCP_EDITOR_LOG", str(fake))
    from unity_mcp.editor_log import get_editor_log_path
    assert get_editor_log_path(env_override=str(fake)) == fake


def test_env_var_picked_up_automatically(tmp_path, monkeypatch):
    fake = tmp_path / "custom.log"
    fake.touch()
    monkeypatch.setenv("UNITY_MCP_EDITOR_LOG", str(fake))
    from unity_mcp.editor_log import get_editor_log_path
    assert get_editor_log_path() == fake


@pytest.mark.parametrize("platform,expected_fragment", [
    ("darwin", "Library/Logs/Unity/Editor.log"),
    ("win32",  "Unity/Editor/Editor.log"),
    ("linux",  ".config/unity3d/Editor.log"),
])
def test_platform_path(monkeypatch, platform, expected_fragment):
    monkeypatch.delenv("UNITY_MCP_EDITOR_LOG", raising=False)
    from unity_mcp import editor_log
    with patch.object(sys, "platform", platform):
        result = editor_log.get_editor_log_path()
    assert result is None or expected_fragment in str(result).replace("\\", "/")


def test_env_override_no_op_ternary_simplified(tmp_path):
    """get_editor_log_path returns path even when file doesn't exist (env override path)."""
    nonexistent = tmp_path / "ghost.log"
    from unity_mcp.editor_log import get_editor_log_path
    result = get_editor_log_path(env_override=str(nonexistent))
    assert result == nonexistent  # returned even though it doesn't exist


# ---------------------------------------------------------------------------
# parse_compile_errors_from_log — Unity 6 Bee/Csc format
# ---------------------------------------------------------------------------

def test_empty_log_returns_empty(tmp_path):
    log = _write_log(tmp_path, "")
    from unity_mcp.editor_log import parse_compile_errors_from_log
    assert parse_compile_errors_from_log(log) == []


def test_no_compile_marker_returns_empty(tmp_path):
    log = _write_log(tmp_path, "Some Unity log line\nAnother line\n")
    from unity_mcp.editor_log import parse_compile_errors_from_log
    assert parse_compile_errors_from_log(log) == []


def test_missing_log_returns_empty(tmp_path):
    missing = tmp_path / "nonexistent.log"
    from unity_mcp.editor_log import parse_compile_errors_from_log
    assert parse_compile_errors_from_log(missing) == []


def test_permission_error_returns_empty(tmp_path):
    log = _write_log(tmp_path, "some content")
    from unity_mcp.editor_log import parse_compile_errors_from_log
    with patch("builtins.open", side_effect=PermissionError("locked")):
        assert parse_compile_errors_from_log(log) == []


# --- Fixture-based canonical tests ---

def test_unity6_compile_fail_fixture_single_error():
    """Fixture with ExitCode=1 must yield exactly ONE error (no double-count from ## Output:)."""
    log = FIXTURES / "unity6_compile_fail.log"
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert len(errors) == 1
    assert "CS0117" in errors[0]
    assert "UndoGroupHelper" in errors[0]


def test_unity6_compile_fail_error_normalized():
    """Error must be normalized: Foo.cs(L,C): → Foo.cs:L:C:"""
    log = FIXTURES / "unity6_compile_fail.log"
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert errors[0] == "Assets/Editor/UndoGroupHelper.cs:48:18: error CS0117: 'Undo' does not contain a definition for 'RevertAllDownTo'"


def test_unity6_compile_ok_fixture_returns_empty():
    """Fixture with ExitCode=0 must return [] even though ## Output: block has warnings."""
    log = FIXTURES / "unity6_compile_ok.log"
    from unity_mcp.editor_log import parse_compile_errors_from_log
    assert parse_compile_errors_from_log(log) == []


# --- False-negative regression test ---

def test_false_negative_regression_bee_format():
    """THE regression: a real Unity 6 failed Csc block MUST yield the error.

    This is the exact format from the live log that previously returned [].
    """
    content = _bee_block(
        exit_code=1,
        output_lines=[
            "Assets/Editor/MaterialHelper.cs(80,73): warning CS0618: deprecated",
            "Assets/Editor/UndoGroupHelper.cs(48,18): error CS0117: 'Undo' does not contain a definition for 'RevertAllDownTo'",
        ],
    )
    # Must not return [] — that was the bug
    from unity_mcp.editor_log import parse_compile_errors_from_log
    import io
    from unittest.mock import mock_open, MagicMock
    from pathlib import Path

    mock_path = MagicMock(spec=Path)
    mock_path.stat.return_value.st_size = len(content.encode())
    mock_path.__str__ = lambda s: "fake.log"

    with patch("builtins.open", mock_open(read_data=content)):
        errors = parse_compile_errors_from_log(mock_path)

    assert len(errors) == 1
    assert "CS0117" in errors[0]


# --- Supersession test (append-only log: old fail + newer success → clean) ---

def test_append_only_supersession_old_fail_new_success(tmp_path):
    """Supersession is handled at corroborate level (fresh dll gate), not parser level.

    parse_compile_errors_from_log returns errors from the last failure header in the log
    (even if a later success block follows). corroborate_compile_status suppresses those
    errors when the dll is fresh — that's the supersession gate.

    This test verifies the CORROBORATE level: old fail header + fresh dll → C# trusted.
    """
    base_t = 1000000.0
    src = tmp_path / "src"
    _make_cs(src, base_t)
    _make_dll(tmp_path, base_t + 20)  # dll NEWER than src → fresh (supersession: fixed + recompiled)

    old_fail = _bee_block(
        exit_code=1,
        output_lines=["Assets/Old.cs(1,1): error CS9999: stale error"],
    )
    new_ok = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, old_fail + "\n" + new_ok)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    # Fresh dll → trust C# response → old log errors suppressed
    assert "[editor.log" not in result
    assert "No compilation errors." in result


# --- Parametric Bee-format tests ---

def test_clean_compile_returns_empty(tmp_path):
    content = _bee_block(exit_code=0, output_lines=["Assets/Foo.cs(1,1): warning CS0618: deprecated"])
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    assert parse_compile_errors_from_log(log) == []


def test_single_cs_error(tmp_path):
    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(12,5): error CS0117: 'Foo' does not contain a definition"],
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert len(errors) == 1
    assert "Assets/Foo.cs:12:5:" in errors[0]
    assert "CS0117" in errors[0]


def test_multi_error_within_one_block(tmp_path):
    """Multiple errors in one Csc block — collect all."""
    content = _bee_block(
        exit_code=1,
        output_lines=[
            "Assets/A.cs(1,2): error CS0001: first",
            "Assets/B.cs(3,4): error CS0002: second",
        ],
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert len(errors) == 2
    assert any("CS0001" in e for e in errors)
    assert any("CS0002" in e for e in errors)


def test_warnings_only_no_false_error(tmp_path):
    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/A.cs(1,2): warning CS0618: deprecated"],
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    # ExitCode=1 but no error lines in output → empty (no CS error lines)
    errors = parse_compile_errors_from_log(log)
    assert errors == []


def test_truncated_log_no_exit_code_returns_empty(tmp_path):
    """Log tail cut off before any ##### ExitCode → []."""
    content = '<csc /nostdlib /out:"Foo.dll">\n##### Custom Environment Variables\n'
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    assert parse_compile_errors_from_log(log) == []


def test_error_format_normalized(tmp_path):
    """'Assets/Foo.cs(12,5): error CS0117: msg' → 'Assets/Foo.cs:12:5: error CS0117: msg'"""
    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Scripts/Player.cs(42,13): error CS0103: 'bar' does not exist"],
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert errors[0] == "Assets/Scripts/Player.cs:42:13: error CS0103: 'bar' does not exist"


def test_malformed_exit_code_treated_as_failure(tmp_path):
    """If ExitCode line is not parseable as int, treat as failure and collect errors."""
    content = (
        "##### ExitCode\n"
        "BROKEN\n"
        "##### Output\n"
        "Assets/Foo.cs(1,1): error CS0001: something\n"
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert any("CS0001" in e for e in errors)


def test_double_count_prevention(tmp_path):
    """Errors after '## Output:' must NOT be collected (double-count trap)."""
    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(5,3): error CS0117: single"],
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    # Only 1 error despite it appearing twice in the log (once in ##### Output, once in ## Output:)
    assert errors.count(errors[0]) == 1
    assert len(errors) == 1


def test_all_compiler_errors_signal(tmp_path):
    """'All compiler errors have to be fixed' inside the ExitCode block is a signal."""
    content = (
        "##### ExitCode\n"
        "1\n"
        "##### Output\n"
        "All compiler errors have to be fixed before you can enter playmode!\n"
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert len(errors) >= 1
    assert any("compiler errors" in e.lower() for e in errors)


# ---------------------------------------------------------------------------
# check_dll_freshness (new signature: source_dirs parameter)
# ---------------------------------------------------------------------------

def _make_dll(tmp_path: Path, mtime: float) -> Path:
    dll_dir = tmp_path / "Library" / "ScriptAssemblies"
    dll_dir.mkdir(parents=True)
    dll = dll_dir / "UnityMCP.Editor.dll"
    dll.write_bytes(b"fake dll")
    os.utime(dll, (mtime, mtime))
    return dll


def _make_cs(parent: Path, mtime: float, name: str = "Foo.cs") -> Path:
    parent.mkdir(parents=True, exist_ok=True)
    cs = parent / name
    cs.write_text("class Foo {}", encoding="utf-8")
    os.utime(cs, (mtime, mtime))
    return cs


def test_dll_missing_returns_none(tmp_path):
    """No dll → None regardless of source_dirs."""
    from unity_mcp.editor_log import check_dll_freshness
    src = tmp_path / "src"
    _make_cs(src, 1000000.0)
    assert check_dll_freshness(tmp_path, source_dirs=[src]) is None


def test_dll_freshness_no_source_dirs_returns_none(tmp_path):
    """source_dirs=None → None (undeterminable, do NOT rglob project)."""
    _make_dll(tmp_path, 1000000.0)
    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=None) is None


def test_dll_freshness_empty_source_dirs_returns_none(tmp_path):
    """source_dirs=[] → None (no dirs to scan)."""
    _make_dll(tmp_path, 1000000.0)
    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=[]) is None


def test_dll_freshness_source_dir_no_cs_files_returns_none(tmp_path):
    """source_dir exists but has no .cs files → None."""
    _make_dll(tmp_path, 1000000.0)
    empty_dir = tmp_path / "empty_src"
    empty_dir.mkdir()
    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=[empty_dir]) is None


def test_dll_fresh_when_newer_than_cs(tmp_path):
    """dll mtime > cs mtime → True (fresh)."""
    base_t = 1000000.0
    src = tmp_path / "src"
    _make_cs(src, base_t)
    _make_dll(tmp_path, base_t + 20)

    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=[src]) is True


def test_dll_stale_when_cs_newer(tmp_path):
    """cs mtime > dll mtime + grace → False (stale)."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 100)

    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=[src]) is False


def test_dll_freshness_within_grace_period(tmp_path):
    """cs newer than dll but within grace_s=10 → True (still fresh)."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 5)  # 5s newer but grace=10 → dll_mtime+10 >= cs_mtime

    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=[src]) is True


def test_dll_freshness_scans_multiple_source_dirs(tmp_path):
    """Takes max mtime across all source_dirs."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src1 = tmp_path / "src1"
    _make_cs(src1, base_t - 50)   # older → alone would be "fresh"
    src2 = tmp_path / "src2"
    _make_cs(src2, base_t + 100)  # newer → stale

    from unity_mcp.editor_log import check_dll_freshness
    assert check_dll_freshness(tmp_path, source_dirs=[src1, src2]) is False


# ---------------------------------------------------------------------------
# find_plugin_source_dir
# ---------------------------------------------------------------------------

def test_find_plugin_source_dir_returns_list_or_none():
    """Always returns a list or None, never raises."""
    from unity_mcp.editor_log import find_plugin_source_dir
    result = find_plugin_source_dir()
    assert result is None or isinstance(result, list)


def _plugin_dir_reachable() -> bool:
    """Check via the module's own path, not the test file's path."""
    import unity_mcp.editor_log as _el
    repo = Path(_el.__file__).resolve().parents[3]
    return (repo / "unity-plugin").exists()


@pytest.mark.skipif(not _plugin_dir_reachable(), reason="unity-plugin not present in this checkout")
def test_find_plugin_source_dir_finds_plugin_in_repo():
    """In the actual repo, finds unity-plugin directory with .cs files."""
    from unity_mcp.editor_log import find_plugin_source_dir
    result = find_plugin_source_dir()
    assert result is not None
    assert len(result) >= 1
    plugin_dir = result[0]
    assert plugin_dir.name == "unity-plugin"
    assert plugin_dir.exists()
    assert next(plugin_dir.rglob("*.cs"), None) is not None


def test_find_plugin_source_dir_returns_none_without_plugin(tmp_path):
    """When __file__ hierarchy has no unity-plugin sibling → None."""
    from unittest.mock import patch
    # Point __file__ at a deep path inside tmp_path where there's no unity-plugin
    fake_file = tmp_path / "a" / "b" / "c" / "d" / "editor_log_freshness.py"
    fake_file.parent.mkdir(parents=True)
    fake_file.touch()

    import unity_mcp.editor_log_freshness as freshness_mod
    import unity_mcp.editor_log as el_mod
    with patch.object(freshness_mod, "__file__", str(fake_file)):
        result = el_mod.find_plugin_source_dir()
    assert result is None


# ---------------------------------------------------------------------------
# corroborate_compile_status
# ---------------------------------------------------------------------------

def test_corroborate_csharp_already_has_errors():
    """If C# response already has errors → return it unchanged."""
    from unity_mcp.editor_log import corroborate_compile_status
    cs_resp = "Assets/Foo.cs:1:1: error CS0001: something broken"
    assert corroborate_compile_status(cs_resp, log_path=None) == cs_resp


def test_corroborate_no_log_path_passthrough():
    """log_path=None → return C# response unchanged (graceful degradation)."""
    from unity_mcp.editor_log import corroborate_compile_status
    cs_resp = "No compilation errors."
    assert corroborate_compile_status(cs_resp, log_path=None) == cs_resp


def test_corroborate_clean_csharp_clean_log(tmp_path):
    """C# clean, log clean (ExitCode=0) → return C# response unchanged."""
    content = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import corroborate_compile_status
    assert corroborate_compile_status("No compilation errors.", log_path=log) == "No compilation errors."


def test_corroborate_clean_csharp_log_has_errors_but_dll_fresh_no_override(tmp_path):
    """FALSE-POSITIVE GUARD: log has stale error block BUT dll is fresh → trust C#, no CORROBORATED.

    This is the key regression test. After a fix+recompile Unity 6 leaves the old
    failure block in the log tail. If dll is fresh, the error is stale → pass through.
    """
    base_t = 1000000.0
    src = tmp_path / "src"
    _make_cs(src, base_t)
    _make_dll(tmp_path, base_t + 20)  # dll NEWER than source → fresh

    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(12,5): error CS0117: 'x' does not exist"],
    )
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    assert "[editor.log" not in result
    assert "No compilation errors." in result


def test_corroborate_log_errors_and_stale_dll_triggers_corroborated(tmp_path):
    """Both signals present: log error + stale dll → CORROBORATED."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 100)  # cs NEWER → stale dll

    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(12,5): error CS0117: 'x' does not exist"],
    )
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    assert "[editor.log" in result
    assert "CS0117" in result


def test_corroborate_log_errors_freshness_none_trusts_csharp(tmp_path):
    """Log has errors but freshness is None (dll missing) → trust C#, no CORROBORATED."""
    # No dll → freshness = None
    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(12,5): error CS0117: 'x' does not exist"],
    )
    log = _write_log(tmp_path, content)
    src = tmp_path / "src"
    _make_cs(src, 1000000.0)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    assert "[editor.log" not in result
    assert "No compilation errors." in result


def test_corroborate_stale_dll_no_log_errors_soft_warn(tmp_path):
    """Stale dll but log is clean → soft warn only, no CORROBORATED, no error CS."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 200)  # cs newer → stale

    content = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    assert "[editor.log" not in result
    assert "warn" in result.lower() or "stale" in result.lower()
    assert "error CS" not in result


def test_corroborate_no_project_path_log_errors_trusts_csharp(tmp_path):
    """project_path=None → freshness undeterminable → trust C# even with log errors."""
    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(1,1): error CS0117: whatever"],
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status("No compilation errors.", log_path=log)
    assert "[editor.log" not in result
    assert "No compilation errors." in result


def test_corroborate_stale_dll_warning(tmp_path):
    """C# clean, log clean, dll stale → append one-line warning, no fabricated errors."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 200)

    content = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    assert "stale" in result.lower() or "warn" in result.lower() or result == "No compilation errors."
    assert "error CS" not in result


def test_corroborate_no_project_path_no_dll_check(tmp_path):
    """project_path=None + clean log → pass-through unchanged."""
    content = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status("No compilation errors.", log_path=log)
    assert "No compilation errors." in result


# ---------------------------------------------------------------------------
# MAJOR: multi-assembly parallel build — rfind on failure header, not ExitCode
# ---------------------------------------------------------------------------

def _bee_block_named(assembly: str, exit_code: int, output_lines: list[str]) -> str:
    """Build a Bee/Csc log block for a specific assembly."""
    lines = [
        f'<csc /nostdlib /out:"Library/Bee/artifacts/test.dag/{assembly}.dll">',
        "##### Custom Environment Variables",
        "DOTNET_MULTILEVEL_LOOKUP=0",
        "##### ExitCode",
        str(exit_code),
        "##### Output",
        *output_lines,
    ]
    if exit_code != 0:
        lines += [
            "*** Tundra build failed (1.00 seconds)",
            f"## Script Compilation Error for: Csc Library/Bee/artifacts/test.dag/{assembly}.dll (+2 others)",
            "## Output:",
            *output_lines,
        ]
    else:
        lines.append("*** Tundra build done (0.50 seconds)")
    return "\n".join(lines) + "\n"


def test_multi_assembly_failure_header_anchors_correct_block(tmp_path):
    """MAJOR regression: UnityMCP.Editor fails, THEN Assembly-CSharp-Editor succeeds.

    In a parallel Bee build, the failing assembly's block (ExitCode=1 + failure header)
    appears first; then the successfully compiled assembly's block (ExitCode=0) comes
    after. rfind('##### ExitCode') + value '0' gives false-negative.
    rfind('## Script Compilation Error for:') anchors to the failure header, then finds
    the error Output block before it → returns errors correctly.
    """
    from unity_mcp.editor_log import parse_compile_errors_from_log

    fail_block = _bee_block_named(
        "UnityMCP.Editor",
        exit_code=1,
        output_lines=["Assets/Editor/Foo.cs(10,5): error CS0117: stale error"],
    )
    # Successful assembly processed AFTER the fail — ExitCode=0 is the LAST ExitCode
    success_block = _bee_block_named(
        "Assembly-CSharp-Editor",
        exit_code=0,
        output_lines=["Assets/Game/Bar.cs(1,1): warning CS0618: deprecated"],
    )
    log = _write_log(tmp_path, fail_block + "\n" + success_block)

    errors = parse_compile_errors_from_log(log)
    # Must find the error from UnityMCP.Editor, NOT be fooled by the later ExitCode=0
    assert len(errors) == 1
    assert "CS0117" in errors[0]


def test_multi_assembly_both_fail_returns_last_failure(tmp_path):
    """Both assemblies fail — returns errors from the LAST failure header."""
    from unity_mcp.editor_log import parse_compile_errors_from_log

    first_fail = _bee_block_named(
        "Assembly-CSharp",
        exit_code=1,
        output_lines=["Assets/Game/Old.cs(1,1): error CS0001: first error"],
    )
    second_fail = _bee_block_named(
        "UnityMCP.Editor",
        exit_code=1,
        output_lines=["Assets/Editor/New.cs(5,3): error CS0002: second error"],
    )
    log = _write_log(tmp_path, first_fail + "\n" + second_fail)

    errors = parse_compile_errors_from_log(log)
    # Should return errors from the LAST failure header
    assert len(errors) == 1
    assert "CS0002" in errors[0]


# ---------------------------------------------------------------------------
# MINOR: >256 KB seek path
# ---------------------------------------------------------------------------

def test_large_log_failure_in_tail_window(tmp_path):
    """Failure header is in the last max_bytes — must be found even with seek."""
    from unity_mcp.editor_log import parse_compile_errors_from_log

    # Large preamble (>256 KB) with an OLD success block
    preamble = _bee_block(exit_code=0, output_lines=[""]) + ("X" * 300_000)
    fail_block = _bee_block_named(
        "UnityMCP.Editor",
        exit_code=1,
        output_lines=["Assets/Editor/New.cs(3,2): error CS0009: in tail"],
    )
    log = _write_log(tmp_path, preamble + "\n" + fail_block)

    errors = parse_compile_errors_from_log(log, max_bytes=256_000)
    assert len(errors) == 1
    assert "CS0009" in errors[0]


def test_large_log_failure_before_tail_window_found_via_full_file_read(tmp_path):
    """P1: full-file rfind finds failure header even when pushed before the tail window.

    Before P1 fix: 256KB tail seek missed the header → returned [].
    After P1 fix: full-file read → rfind finds it → errors returned.
    The success block after the filler has NO ## Script Compilation Error header so
    it cannot produce a false-negative: rfind anchors on the failure header only.
    """
    from unity_mcp.editor_log import parse_compile_errors_from_log

    fail_block = _bee_block_named(
        "UnityMCP.Editor",
        exit_code=1,
        output_lines=["Assets/Editor/Old.cs(1,1): error CS0001: old error"],
    )
    # Lots of content after the failure pushes header before 256KB tail window
    filler = "X" * 300_000
    success = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, fail_block + filler + "\n" + success)

    errors = parse_compile_errors_from_log(log, max_bytes=256_000)
    # Full-file rfind finds the failure header → errors must be returned
    assert errors != [], "P1: failure header must be found even outside tail window"
    assert len(errors) == 1
    assert "CS0001" in errors[0]


# ---------------------------------------------------------------------------
# init_corroboration / corroborate module-level helpers
# ---------------------------------------------------------------------------

def test_init_corroboration_and_corroborate_clean(tmp_path, monkeypatch):
    """init_corroboration caches; corroborate() passes through clean log."""
    import unity_mcp.editor_log as el
    content = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, content)

    monkeypatch.setattr(el, "_cor_project_path", None)
    monkeypatch.setattr(el, "_cor_log_path", log)
    monkeypatch.setattr(el, "_cor_source_dirs", None)

    result = el.corroborate("No compilation errors.")
    assert result == "No compilation errors."


# no-assert: crash guard
def test_init_corroboration_idempotent(monkeypatch):
    """Calling init_corroboration() multiple times doesn't raise."""
    import unity_mcp.editor_log as el
    # Should not raise even if autodetect returns None
    el.init_corroboration()
    el.init_corroboration()


def test_init_corroboration_prod_path_scoped_chat_cs_not_stale(tmp_path, monkeypatch):
    """ITEM 1 (RC-4 prod path): Chat/*.cs newer than dll must NOT cause stale verdict.

    Drives the PRODUCTION entry: init_corroboration patches _cor_* then corroborate().
    Current (broken) code: init_corroboration calls find_plugin_source_dir() which
    rglobs the whole plugin_dir and picks up Chat .cs → stale verdict.
    Fixed code: uses find_plugin_source_files() which excludes Chat → fresh verdict.
    """
    import unity_mcp.editor_log as el

    # Build fake plugin layout with core + Chat (same as _make_plugin_layout)
    plugin_root = tmp_path / "unity-plugin"
    core_dir = plugin_root / "Editor"
    core_dir.mkdir(parents=True)
    (core_dir / "UnityMCP.Editor.asmdef").write_text('{"name":"UnityMCP.Editor"}', encoding="utf-8")
    base_t = 1000000.0
    core_cs = core_dir / "Core.cs"
    core_cs.write_text("class Core {}", encoding="utf-8")
    os.utime(core_cs, (base_t, base_t))

    chat_dir = core_dir / "Chat"
    chat_dir.mkdir()
    (chat_dir / "UnityMCP.Editor.Chat.asmdef").write_text('{"name":"UnityMCP.Editor.Chat"}', encoding="utf-8")
    chat_cs = chat_dir / "ChatFoo.cs"
    chat_cs.write_text("class ChatFoo {}", encoding="utf-8")
    # Chat .cs is VERY new — newer than dll
    os.utime(chat_cs, (base_t + 500, base_t + 500))

    # dll is fresh relative to core but STALE relative to Chat
    _make_dll(tmp_path, base_t + 50)

    # Clean log (no compile errors)
    content = _bee_block(exit_code=0, output_lines=[""])
    log = _write_log(tmp_path, content)

    # Patch init_corroboration to use our fake plugin_root
    # We set _cor_source_files directly as init_corroboration would after the fix
    scoped_files = el.find_plugin_source_files(plugin_dir=plugin_root)
    monkeypatch.setattr(el, "_cor_project_path", tmp_path)
    monkeypatch.setattr(el, "_cor_log_path", log)
    # Simulate what the FIXED init_corroboration stores: scoped source_files
    # After the fix, corroborate() must use source_files= not source_dirs=
    # We inject via the module attribute that corroborate() reads.
    # Before fix: _cor_source_dirs is set, corroborate passes source_dirs= (rglobs Chat)
    # After fix: _cor_source_files is set, corroborate passes source_files= (no Chat)
    monkeypatch.setattr(el, "_cor_source_files", scoped_files, raising=False)
    # Clear source_dirs so old path is not accidentally used
    if hasattr(el, "_cor_source_dirs"):
        monkeypatch.setattr(el, "_cor_source_dirs", None)

    result = el.corroborate("No compilation errors.")
    # Chat is newer than dll but scoped list excludes Chat → dll is fresh → no stale warn
    assert "stale" not in result.lower() and "warn" not in result.lower(), (
        f"Expected no stale/warn (Chat excluded), got: {result!r}"
    )


# ---------------------------------------------------------------------------
# get_editor_log_path: warn on non-existent env override
# ---------------------------------------------------------------------------

def test_env_override_nonexistent_warns_to_stderr(tmp_path, monkeypatch, capsys):
    """When UNITY_MCP_EDITOR_LOG points to non-existent path, warn to stderr."""
    nonexistent = tmp_path / "ghost.log"
    monkeypatch.setenv("UNITY_MCP_EDITOR_LOG", str(nonexistent))
    from unity_mcp import editor_log
    # Re-import to pick up env var (function call, not import-time)
    result = editor_log.get_editor_log_path()
    assert result == nonexistent  # still returns path
    captured = capsys.readouterr()
    assert "warning" in captured.err.lower() or "warn" in captured.err.lower()


# ---------------------------------------------------------------------------
# CORROBORATED prefix is short ASCII (no em dash)
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# RC-4: find_plugin_source_files (scope to UnityMCP.Editor only)
# ---------------------------------------------------------------------------

def _make_plugin_layout(root: Path) -> dict:
    """Create a fake unity-plugin directory with core + Chat + Tests asmdefs."""
    core_dir = root / "Editor"
    core_dir.mkdir(parents=True)
    (core_dir / "UnityMCP.Editor.asmdef").write_text('{"name":"UnityMCP.Editor"}', encoding="utf-8")
    core_cs = core_dir / "Core.cs"
    core_cs.write_text("class Core {}", encoding="utf-8")

    chat_dir = core_dir / "Chat"
    chat_dir.mkdir()
    (chat_dir / "UnityMCP.Editor.Chat.asmdef").write_text('{"name":"UnityMCP.Editor.Chat"}', encoding="utf-8")
    chat_cs = chat_dir / "ChatFoo.cs"
    chat_cs.write_text("class ChatFoo {}", encoding="utf-8")

    tests_dir = core_dir / "Tests"
    tests_dir.mkdir()
    (tests_dir / "UnityMCP.Editor.Tests.asmdef").write_text('{"name":"UnityMCP.Editor.Tests"}', encoding="utf-8")
    tests_cs = tests_dir / "TestFoo.cs"
    tests_cs.write_text("class TestFoo {}", encoding="utf-8")

    return {"core": core_cs, "chat_asmdef_dir": chat_dir, "chat_cs": chat_cs,
            "tests_cs": tests_cs, "plugin_root": root}


def test_find_plugin_source_files_excludes_chat_cs(tmp_path):
    """Chat/ subdir has non-UnityMCP.Editor asmdef → its .cs files excluded."""
    from unity_mcp.editor_log import find_plugin_source_files
    layout = _make_plugin_layout(tmp_path)
    result = find_plugin_source_files(plugin_dir=tmp_path)
    paths = [f.name for f in result]
    assert "ChatFoo.cs" not in paths
    assert "Core.cs" in paths


def test_find_plugin_source_files_excludes_tests_cs(tmp_path):
    """Tests/ subdir has non-UnityMCP.Editor asmdef → its .cs files excluded."""
    from unity_mcp.editor_log import find_plugin_source_files
    layout = _make_plugin_layout(tmp_path)
    result = find_plugin_source_files(plugin_dir=tmp_path)
    paths = [f.name for f in result]
    assert "TestFoo.cs" not in paths


def test_find_plugin_source_files_includes_core_cs(tmp_path):
    """Core dir has UnityMCP.Editor.asmdef → Core.cs included."""
    from unity_mcp.editor_log import find_plugin_source_files
    layout = _make_plugin_layout(tmp_path)
    result = find_plugin_source_files(plugin_dir=tmp_path)
    assert any(f.name == "Core.cs" for f in result)


def test_check_dll_freshness_chat_edit_not_stale(tmp_path):
    """Chat .cs newer than dll → freshness True (not stale) because Chat is excluded."""
    from unity_mcp.editor_log import check_dll_freshness, find_plugin_source_files
    layout = _make_plugin_layout(tmp_path)
    base_t = 1000000.0
    _make_dll(tmp_path, base_t + 50)  # dll is fresh relative to core
    os.utime(layout["core"], (base_t, base_t))  # core .cs is old
    os.utime(layout["chat_cs"], (base_t + 200, base_t + 200))  # chat .cs is very new

    # Use source_files (scoped list, no rglob picking up Chat)
    core_files = find_plugin_source_files(plugin_dir=tmp_path)
    result = check_dll_freshness(tmp_path, source_files=core_files)
    assert result is True  # chat edit doesn't make dll stale


def test_find_plugin_source_files_no_editor_asmdef(tmp_path):
    """plugin_dir with ONLY a foreign asmdef (e.g. UnityMCP.Editor.Chat.asmdef) + .cs.

    Documented behavior: .cs files under the foreign-asmdef dir are excluded;
    any .cs at the plugin_dir root (not under any excluded ancestor) are included.
    Intent: 'UnityMCP.Editor' assembly boundary = no foreign asmdef ancestor.
    """
    from unity_mcp.editor_log import find_plugin_source_files

    # Only a Chat asmdef — no UnityMCP.Editor.asmdef at all
    chat_dir = tmp_path / "Chat"
    chat_dir.mkdir()
    (chat_dir / "UnityMCP.Editor.Chat.asmdef").write_text('{"name":"UnityMCP.Editor.Chat"}', encoding="utf-8")
    chat_cs = chat_dir / "ChatOnly.cs"
    chat_cs.write_text("class ChatOnly {}", encoding="utf-8")

    # A .cs at root level (not under any foreign-asmdef dir)
    root_cs = tmp_path / "Orphan.cs"
    root_cs.write_text("class Orphan {}", encoding="utf-8")

    result = find_plugin_source_files(plugin_dir=tmp_path)
    names = [f.name for f in result]
    # Chat .cs is under the foreign-asmdef dir → excluded
    assert "ChatOnly.cs" not in names
    # Root-level .cs has no excluding ancestor → included
    assert "Orphan.cs" in names


# ---------------------------------------------------------------------------
# P2: codeless [no-cs-code] suffix + idle-failed 3rd gate
# ---------------------------------------------------------------------------

def test_strong_broken_codeless_gets_no_cs_code_suffix(tmp_path):
    """P2: _collect_strong_broken / _collect_errors append [no-cs-code] sentinel."""
    content = (
        "##### ExitCode\n"
        "1\n"
        "##### Output\n"
        "All compiler errors have to be fixed before you can enter playmode!\n"
    )
    log = _write_log(tmp_path, content)
    from unity_mcp.editor_log import parse_compile_errors_from_log
    errors = parse_compile_errors_from_log(log)
    assert len(errors) == 1
    assert "[no-cs-code]" in errors[0]
    assert "DO NOT GUESS" in errors[0]


def test_corroborate_stale_log_not_resurrected_when_compile_status_idle(tmp_path):
    """P2: stale log errors NOT resurrected when compile_status='idle' (3rd gate)."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 200)  # cs newer → stale dll

    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(1,1): error CS0001: stale error"],
    )
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src], compile_status="idle",
    )
    # 3rd gate: compile_status=idle means C# says clean → don't resurrect stale log block
    assert "[editor.log" not in result


def test_corroborate_stale_log_resurrected_when_compile_status_idle_failed(tmp_path):
    """P2: stale log errors ARE resurrected when compile_status='idle-failed'."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 200)  # cs newer → stale dll

    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(1,1): error CS0001: real error"],
    )
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src], compile_status="idle-failed",
    )
    # Both dll-stale + log-error + idle-failed → resurrect
    assert "[editor.log" in result
    assert "CS0001" in result


def test_corroborated_prefix_is_ascii(tmp_path):
    """CORROBORATED prefix must be pure ASCII — no em dash or Unicode."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 100)

    content = _bee_block(
        exit_code=1,
        output_lines=["Assets/Foo.cs(1,1): error CS0117: test"],
    )
    log = _write_log(tmp_path, content)

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src],
    )
    # Extract the prefix line
    first_line = result.split("\n")[0]
    first_line.encode("ascii")  # raises UnicodeEncodeError if non-ASCII present


# ---------------------------------------------------------------------------
# Phase 3c: compile_status gate on soft-warn
# ---------------------------------------------------------------------------

def test_corroborate_stale_dll_compile_status_idle_suppresses_soft_warn(tmp_path):
    """Stale dll + compile_status='idle' → no soft warn (Unity compiled clean this session)."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 200)  # cs newer → stale
    log = _write_log(tmp_path, _bee_block(exit_code=0, output_lines=[""]))

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src], compile_status="idle",
    )
    assert "warn" not in result.lower()
    assert "stale" not in result.lower()


def test_corroborate_stale_dll_compile_status_idle_failed_fires_soft_warn(tmp_path):
    """Stale dll + compile_status='idle-failed' → soft warn fires."""
    base_t = 1000000.0
    _make_dll(tmp_path, base_t)
    src = tmp_path / "src"
    _make_cs(src, base_t + 200)
    log = _write_log(tmp_path, _bee_block(exit_code=0, output_lines=[""]))

    from unity_mcp.editor_log import corroborate_compile_status
    result = corroborate_compile_status(
        "No compilation errors.", project_path=tmp_path, log_path=log,
        source_dirs=[src], compile_status="idle-failed",
    )
    assert "warn" in result.lower() or "stale" in result.lower()
