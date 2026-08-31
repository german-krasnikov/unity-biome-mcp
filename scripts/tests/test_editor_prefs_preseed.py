"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: offline
EditorPrefs preseed (defense-in-depth).

Root cause of the min-* full-scenario hang (Run 4, 33383130341): FSR's
first-run modal dialog EnsureUserAwareOfAutoRefresh -> DisplayDialogComplex
(FastScriptReloadWelcomeScreen.cs:989) blocks ProcessInitializeOnLoadAttributes
on a headed Editor — the MCP TCP listener never starts because the whole
domain-load sequence is stuck behind a dialog with no user to click it. The
fork owner is landing a point fix (guard) in the adapter; this preseed is a
second, independent layer: writing the two EditorPrefs that suppress the
dialog's vulnerable branch *before Unity ever launches*, offline, per OS,
so this class of hang cannot recur even if some other path re-triggers the
same dialog.

kAutoRefreshMode=2 (EnabledOutsidePlaymode) skips the code path (L978) that
leads to the dialog; "<ProductName>_StopShowingAutoReloadEnabledDialogBox"=1
is Unity's own do-not-ask-again flag for this exact dialog, project-scoped
by embedding ProductName in the literal key name (Unity's own per-project
EditorPrefs convention — not a separate storage mechanism).

Runs in the standard `scripts/tests` lane: no Unity, no real registry/
defaults/file writes for the pure command/patch builders — only
preseed_editor_prefs (the I/O orchestrator) touches tmp_path/monkeypatched
subprocess calls.
"""
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import gauntlet.editor_prefs_preseed as preseed  # noqa: E402

# ---------------------------------------------------------------------------
# resolve_product_name — reads the disposable worker's own ProjectSettings,
# never a hardcoded literal (the "Unity Biome MCP Demo" value is empirical
# for today's tracked project, not a contract).
# ---------------------------------------------------------------------------

def _project_settings(tmp_path: Path, product_name: str) -> Path:
    project = tmp_path / "worker"
    settings_dir = project / "ProjectSettings"
    settings_dir.mkdir(parents=True)
    (settings_dir / "ProjectSettings.asset").write_text(
        "%YAML 1.1\n"
        "%TAG !u! tag:unity3d.com,2011:\n"
        "--- !u!129 &1\n"
        "PlayerSettings:\n"
        "  m_ObjectHideFlags: 0\n"
        "  companyName: Some Company\n"
        f"  productName: {product_name}\n"
        "  defaultCursor: {fileID: 0}\n",
        encoding="utf-8",
    )
    return project


def test_resolve_product_name_reads_tracked_worker_value(tmp_path: Path):
    project = _project_settings(tmp_path, "Unity Biome MCP Demo")
    assert preseed.resolve_product_name(project) == "Unity Biome MCP Demo"


def test_resolve_product_name_handles_arbitrary_names(tmp_path: Path):
    project = _project_settings(tmp_path, "Some Other Product")
    assert preseed.resolve_product_name(project) == "Some Other Product"


def test_resolve_product_name_missing_file_raises(tmp_path: Path):
    project = tmp_path / "worker"
    (project / "ProjectSettings").mkdir(parents=True)
    with pytest.raises(preseed.EditorPrefsPreseedError):
        preseed.resolve_product_name(project)


def test_resolve_product_name_missing_field_raises(tmp_path: Path):
    project = tmp_path / "worker"
    settings_dir = project / "ProjectSettings"
    settings_dir.mkdir(parents=True)
    (settings_dir / "ProjectSettings.asset").write_text(
        "PlayerSettings:\n  companyName: X\n", encoding="utf-8"
    )
    with pytest.raises(preseed.EditorPrefsPreseedError):
        preseed.resolve_product_name(project)


# ---------------------------------------------------------------------------
# macos_defaults_write_commands — pure
# ---------------------------------------------------------------------------

def test_macos_defaults_write_commands_covers_both_keys():
    commands = preseed.macos_defaults_write_commands("Unity Biome MCP Demo")
    joined = [" ".join(c) for c in commands]
    assert any("kAutoRefreshMode" in c and "-int" in c and "2" in c for c in joined)
    assert any(
        "Unity Biome MCP Demo_StopShowingAutoReloadEnabledDialogBox" in c
        and "-bool" in c
        and "TRUE" in c
        for c in joined
    )
    assert all(c[0] == "defaults" and c[1] == "write" for c in commands)
    assert all(c[2] == preseed.MACOS_DEFAULTS_DOMAIN for c in commands)


# ---------------------------------------------------------------------------
# linux_prefs_xml_patch — pure
# ---------------------------------------------------------------------------

def test_linux_prefs_xml_patch_creates_fresh_file_with_both_keys():
    xml = preseed.linux_prefs_xml_patch(None, "Unity Biome MCP Demo")
    assert '<pref name="kAutoRefreshMode" type="int">2</pref>' in xml
    assert (
        '<pref name="Unity Biome MCP Demo_StopShowingAutoReloadEnabledDialogBox" '
        'type="bool">1</pref>' in xml
    )
    assert xml.strip().startswith("<unity_prefs")
    assert xml.strip().endswith("</unity_prefs>")


def test_linux_prefs_xml_patch_preserves_unrelated_existing_prefs():
    existing = (
        '<unity_prefs version_major="1" version_minor="1">\n'
        '  <pref name="SomeOtherKey" type="int">7</pref>\n'
        "</unity_prefs>\n"
    )
    xml = preseed.linux_prefs_xml_patch(existing, "Unity Biome MCP Demo")
    assert '<pref name="SomeOtherKey" type="int">7</pref>' in xml
    assert '<pref name="kAutoRefreshMode" type="int">2</pref>' in xml


def test_linux_prefs_xml_patch_replaces_stale_value_not_duplicate():
    existing = (
        '<unity_prefs version_major="1" version_minor="1">\n'
        '  <pref name="kAutoRefreshMode" type="int">0</pref>\n'
        "</unity_prefs>\n"
    )
    xml = preseed.linux_prefs_xml_patch(existing, "Unity Biome MCP Demo")
    assert xml.count("kAutoRefreshMode") == 1
    assert '<pref name="kAutoRefreshMode" type="int">2</pref>' in xml


# ---------------------------------------------------------------------------
# windows_reg_add_commands — pure. Documented low-confidence: Unity's exact
# registry value-name hashing behavior for dynamically-named (per-project)
# EditorPrefs keys could not be verified without a live Windows Unity
# install; this writes the literal, unhashed key name, matching the
# widely-documented community pattern for standard EditorPrefs registry
# entries. Defense-in-depth only — the primary fix is the fork owner's
# adapter patch, not this preseed.
# ---------------------------------------------------------------------------

def test_windows_reg_add_commands_covers_both_keys():
    commands = preseed.windows_reg_add_commands("Unity Biome MCP Demo")
    joined = [" ".join(c) for c in commands]
    assert any(
        "kAutoRefreshMode" in c and "REG_DWORD" in c and "2" in c for c in joined
    )
    assert any(
        "Unity Biome MCP Demo_StopShowingAutoReloadEnabledDialogBox" in c
        and "REG_DWORD" in c
        and "1" in c
        for c in joined
    )
    assert all(c[0] == "reg" and c[1] == "add" for c in commands)
    assert all(preseed.WINDOWS_REGISTRY_KEY in c for c in commands)


# ---------------------------------------------------------------------------
# preseed_editor_prefs — I/O orchestrator, dispatches per os_name
# ---------------------------------------------------------------------------

def test_preseed_editor_prefs_linux_writes_prefs_file(tmp_path: Path):
    project = _project_settings(tmp_path, "Unity Biome MCP Demo")
    home = tmp_path / "home"
    home.mkdir()

    receipt = preseed.preseed_editor_prefs(project, os_name="Linux", home=home)

    prefs_path = home / ".config" / "unity3d" / "prefs"
    assert prefs_path.is_file()
    assert "kAutoRefreshMode" in prefs_path.read_text(encoding="utf-8")
    assert receipt["mechanism"] == "linux_prefs_xml"
    assert receipt["product_name"] == "Unity Biome MCP Demo"
    assert receipt["keys"] == [
        preseed.AUTO_REFRESH_MODE_KEY,
        "Unity Biome MCP Demo" + preseed.STOP_DIALOG_KEY_SUFFIX,
    ]


def test_preseed_editor_prefs_macos_invokes_defaults_write(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    project = _project_settings(tmp_path, "Unity Biome MCP Demo")
    calls: list[list[str]] = []
    monkeypatch.setattr(
        preseed.subprocess, "run", lambda cmd, **k: calls.append(cmd)
    )

    receipt = preseed.preseed_editor_prefs(project, os_name="macOS")

    assert len(calls) == 2
    assert receipt["mechanism"] == "macos_defaults"


def test_preseed_editor_prefs_windows_invokes_reg_add(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    project = _project_settings(tmp_path, "Unity Biome MCP Demo")
    calls: list[list[str]] = []
    monkeypatch.setattr(
        preseed.subprocess, "run", lambda cmd, **k: calls.append(cmd)
    )

    receipt = preseed.preseed_editor_prefs(project, os_name="Windows")

    assert len(calls) == 2
    assert receipt["mechanism"] == "windows_registry"


def test_preseed_editor_prefs_unknown_os_raises(tmp_path: Path):
    project = _project_settings(tmp_path, "Unity Biome MCP Demo")
    with pytest.raises(preseed.EditorPrefsPreseedError):
        preseed.preseed_editor_prefs(project, os_name="Plan9")


def test_preseed_editor_prefs_macos_command_failure_is_reported_not_raised(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Preseed is defense-in-depth, not the primary fix — a failed write
    must not abort the cell; it must be recorded honestly in the receipt."""
    project = _project_settings(tmp_path, "Unity Biome MCP Demo")

    def _fail(cmd, **k):
        raise subprocess.CalledProcessError(1, cmd, stderr="defaults: nope")

    monkeypatch.setattr(preseed.subprocess, "run", _fail)

    receipt = preseed.preseed_editor_prefs(project, os_name="macOS")

    assert receipt["applied"] is False
    assert "nope" in receipt["error"]


# ---------------------------------------------------------------------------
# read_prefs_snapshot — Run 6: min-linux-x64's receipt shows preseed
# applied=true, yet the stack still shows FSR's L977 auto-refresh check not
# returning early — meaning either Unity never read the written XML, or the
# format doesn't match what Unity expects. Capture the actual on-disk
# content (before AND after the cell) so a future run can compare it
# against what Unity itself writes, instead of guessing the format again.
# ---------------------------------------------------------------------------

def test_read_prefs_snapshot_linux_reads_the_real_file(tmp_path: Path):
    home = tmp_path / "home"
    prefs_path = home / ".config" / "unity3d" / "prefs"
    prefs_path.parent.mkdir(parents=True)
    prefs_path.write_text('<unity_prefs version_major="1" version_minor="1">\n</unity_prefs>\n', encoding="utf-8")

    snapshot = preseed.read_prefs_snapshot("Linux", home=home)

    assert "unity_prefs" in snapshot


def test_read_prefs_snapshot_linux_returns_none_when_absent(tmp_path: Path):
    home = tmp_path / "home"
    home.mkdir()
    assert preseed.read_prefs_snapshot("Linux", home=home) is None


def test_read_prefs_snapshot_macos_invokes_defaults_read(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls = []

    def _run(cmd, **kwargs):
        calls.append(cmd)

        class _Result:
            stdout = "kAutoRefreshMode = 2;\n"
            stderr = ""

        return _Result()

    monkeypatch.setattr(preseed.subprocess, "run", _run)

    snapshot = preseed.read_prefs_snapshot("macOS", home=tmp_path)

    assert calls[0][:3] == ["defaults", "read", preseed.MACOS_DEFAULTS_DOMAIN]
    assert "kAutoRefreshMode" in snapshot


def test_read_prefs_snapshot_windows_invokes_reg_query(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls = []

    def _run(cmd, **kwargs):
        calls.append(cmd)

        class _Result:
            stdout = "kAutoRefreshMode    REG_DWORD    0x2\n"
            stderr = ""

        return _Result()

    monkeypatch.setattr(preseed.subprocess, "run", _run)

    snapshot = preseed.read_prefs_snapshot("Windows", home=tmp_path)

    assert calls[0][:3] == ["reg", "query", preseed.WINDOWS_REGISTRY_KEY]
    assert "kAutoRefreshMode" in snapshot


def test_read_prefs_snapshot_never_raises_on_command_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    def _run(cmd, **kwargs):
        raise OSError("no such command")

    monkeypatch.setattr(preseed.subprocess, "run", _run)

    snapshot = preseed.read_prefs_snapshot("macOS", home=tmp_path)

    assert "unreadable" in snapshot.lower()
