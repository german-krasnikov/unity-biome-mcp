"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: offline
EditorPrefs preseed (defense-in-depth).

Root cause of the min-* full-scenario hang (Run 4, 33383130341): FSR's
first-run modal dialog EnsureUserAwareOfAutoRefresh -> DisplayDialogComplex
(FastScriptReloadWelcomeScreen.cs:989) blocks
ProcessInitializeOnLoadAttributes on a headed Editor — the MCP TCP listener
never starts because the whole domain-load sequence is stuck behind a
dialog with no user to click it. The fork owner is landing a point fix
(guard) in the adapter, tracked by a new FINAL_FSR_ADAPTER_SHA; this
preseed is a second, independent layer, written offline before Unity's
first launch in each cell, so this class of hang cannot recur even if some
other path re-triggers the same dialog.

Two EditorPrefs, both project-agnostic in name except for one that Unity
itself scopes per-project by embedding ProductName as a literal prefix (its
own convention — not a separate storage mechanism):

- kAutoRefreshMode = 2 (EnabledOutsidePlaymode) — skips the vulnerable
  branch (FastScriptReloadWelcomeScreen.cs L978) that leads to the dialog.
- "<ProductName>_StopShowingAutoReloadEnabledDialogBox" = true — Unity's
  own do-not-ask-again flag for this exact dialog.

Storage format per OS (all offline, before Unity ever launches):

- macOS: `defaults write com.unity3d.UnityEditor5.x` — Unity's EditorPrefs
  on macOS are NSUserDefaults under this exact domain; widely documented
  (community "reset Unity Editor prefs" guidance uses `defaults delete`
  against the same domain).
- Linux: `~/.config/unity3d/prefs`, a small `<unity_prefs>` XML file —
  Unity's own documented Linux EditorPrefs location and format.
- Windows: HKCU / Software / Unity Technologies / Unity Editor 5.x (the
  registry subkey name is stable across Unity major versions). Honesty
  note: Unity's exact registry value-NAME encoding for dynamically-named
  (per-project) EditorPrefs keys could not be verified without a live
  Windows Unity install to inspect — some Unity internals reportedly
  hash-suffix certain value names. This writes the literal, unhashed key
  name, matching the widely-documented community pattern for standard
  EditorPrefs registry entries. This is the lowest-confidence leg of the
  three; it is defense-in-depth only, never the primary fix.
"""
import subprocess
from pathlib import Path

AUTO_REFRESH_MODE_KEY = "kAutoRefreshMode"
AUTO_REFRESH_MODE_VALUE = 2  # EnabledOutsidePlaymode
STOP_DIALOG_KEY_SUFFIX = "_StopShowingAutoReloadEnabledDialogBox"

MACOS_DEFAULTS_DOMAIN = "com.unity3d.UnityEditor5.x"
WINDOWS_REGISTRY_KEY = r"HKCU\Software\Unity Technologies\Unity Editor 5.x"


class EditorPrefsPreseedError(RuntimeError):
    pass


def resolve_product_name(project: Path) -> str:
    """Read productName from the disposable worker's OWN
    ProjectSettings.asset — never a hardcoded literal. Unity's .asset files
    are a custom YAML variant (!u! tags) that break standard YAML parsers,
    so this is deliberately a simple line scan, matching how other tools in
    this codebase already parse similar files (e.g. ProjectVersion.txt)."""
    settings_path = project / "ProjectSettings" / "ProjectSettings.asset"
    if not settings_path.is_file():
        raise EditorPrefsPreseedError(f"ProjectSettings.asset not found: {settings_path}")
    for line in settings_path.read_text(encoding="utf-8").splitlines():
        if line.startswith("  productName: "):
            return line[len("  productName: ") :].strip()
    raise EditorPrefsPreseedError(f"productName not found in {settings_path}")


def _stop_dialog_key(product_name: str) -> str:
    return product_name + STOP_DIALOG_KEY_SUFFIX


def macos_defaults_write_commands(product_name: str) -> list[list[str]]:
    return [
        [
            "defaults", "write", MACOS_DEFAULTS_DOMAIN,
            AUTO_REFRESH_MODE_KEY, "-int", str(AUTO_REFRESH_MODE_VALUE),
        ],
        [
            "defaults", "write", MACOS_DEFAULTS_DOMAIN,
            _stop_dialog_key(product_name), "-bool", "TRUE",
        ],
    ]


def linux_prefs_xml_patch(existing_xml: str | None, product_name: str) -> str:
    entries: dict[str, tuple[str, str]] = {}
    if existing_xml:
        import re

        for match in re.finditer(
            r'<pref name="([^"]+)" type="([^"]+)">([^<]*)</pref>', existing_xml
        ):
            name, ptype, value = match.groups()
            entries[name] = (ptype, value)
    entries[AUTO_REFRESH_MODE_KEY] = ("int", str(AUTO_REFRESH_MODE_VALUE))
    entries[_stop_dialog_key(product_name)] = ("bool", "1")

    lines = ['<unity_prefs version_major="1" version_minor="1">']
    for name, (ptype, value) in entries.items():
        lines.append(f'  <pref name="{name}" type="{ptype}">{value}</pref>')
    lines.append("</unity_prefs>")
    return "\n".join(lines) + "\n"


def windows_reg_add_commands(product_name: str) -> list[list[str]]:
    return [
        [
            "reg", "add", WINDOWS_REGISTRY_KEY,
            "/v", AUTO_REFRESH_MODE_KEY,
            "/t", "REG_DWORD", "/d", str(AUTO_REFRESH_MODE_VALUE), "/f",
        ],
        [
            "reg", "add", WINDOWS_REGISTRY_KEY,
            "/v", _stop_dialog_key(product_name),
            "/t", "REG_DWORD", "/d", "1", "/f",
        ],
    ]


def _apply_commands(commands: list[list[str]]) -> None:
    for command in commands:
        subprocess.run(command, check=True, capture_output=True, text=True)


def find_candidate_prefs_paths(discovery_report: str, *, home: Path) -> list[Path]:
    """Parse a discover_touched_config_files() report for file paths that
    look like additional Unity EditorPrefs storage: under home, mentioning
    "unity" (case-insensitive) anywhere in the path, and not the
    already-known ~/.config/unity3d/prefs (that one is already preseeded
    directly, no need to rediscover it)."""
    known = (home / ".config" / "unity3d" / "prefs").resolve()
    home_str = str(home)
    candidates: set[Path] = set()
    for raw_line in discovery_report.splitlines():
        line = raw_line.strip()
        if not line.startswith(home_str):
            continue
        if "unity" not in line.lower():
            continue
        candidate = Path(line).resolve()
        if candidate == known:
            continue
        candidates.add(candidate)
    return sorted(candidates)


def adaptive_preseed_from_discovery(
    discovery_report: str, *, product_name: str, home: Path | None = None
) -> dict[str, object]:
    """Best-effort: for every candidate path found in a discovery report
    (beyond the already-known ~/.config/unity3d/prefs), attempt to merge
    our two keys into it too — in addition to, never instead of, the
    original path (Run 7 (b): "продолжай писать в старый путь тоже —
    безвредно"). Only ever patches files that already look like the known
    XML shape; anything else is recorded, not touched, since blindly
    rewriting an unknown format risks corrupting real Unity state."""
    home = home or Path.home()
    candidates = find_candidate_prefs_paths(discovery_report, home=home)
    attempts: list[dict[str, object]] = []
    for candidate in candidates:
        entry: dict[str, object] = {"path": str(candidate)}
        try:
            existing = candidate.read_text(encoding="utf-8") if candidate.is_file() else None
        except (OSError, UnicodeDecodeError) as error:
            entry["applied"] = False
            entry["error"] = str(error)
            attempts.append(entry)
            continue
        if existing is None or not existing.strip().startswith("<"):
            entry["applied"] = False
            entry["reason"] = "not XML-shaped, not patched"
            attempts.append(entry)
            continue
        try:
            candidate.write_text(
                linux_prefs_xml_patch(existing, product_name), encoding="utf-8"
            )
            entry["applied"] = True
            entry["format"] = "xml"
        except OSError as error:
            entry["applied"] = False
            entry["error"] = str(error)
        attempts.append(entry)
    return {"candidates_found": [str(c) for c in candidates], "attempts": attempts}


def create_discovery_marker(path: Path) -> None:
    """A reference file for `find -newer` — Run 7's discovery step touches
    this before Unity launches, then diffs what changed against it after
    Unity exits."""
    path.parent.mkdir(parents=True, exist_ok=True)
    path.touch()


def discover_touched_config_files(*, marker: Path, home: Path | None = None) -> str:
    """Best-effort report of what Unity actually touched under
    ~/.config and ~/.local/share during its run (find -newer marker), plus
    a full listing+cat of ~/.config/unity3d/**. Run 7: the tracked
    ~/.config/unity3d/prefs file is proven stable/untouched across a whole
    cell run, yet FSR's auto-refresh check still returned a non-default
    value — meaning Unity reads EditorPrefs from some OTHER location this
    is meant to reveal. Never raises; a failed find degrades the report,
    it does not fail the caller."""
    home = home or Path.home()
    lines: list[str] = []
    try:
        result = subprocess.run(
            [
                "find", str(home / ".config"), str(home / ".local" / "share"),
                "-newer", str(marker), "-type", "f",
            ],
            capture_output=True, text=True, timeout=30,
        )
        lines.append("=== files modified since marker ===")
        lines.append(result.stdout or "<none>")
        if result.stderr:
            lines.append(result.stderr)
    except (OSError, subprocess.SubprocessError) as error:
        lines.append(f"<find failed: {error}>")

    unity3d_dir = home / ".config" / "unity3d"
    lines.append("=== ~/.config/unity3d/** listing ===")
    if unity3d_dir.is_dir():
        entries = sorted(unity3d_dir.rglob("*"))
        lines.extend(str(entry) for entry in entries)
        lines.append("=== candidate file contents (< 64KiB) ===")
        for entry in entries:
            if entry.is_file() and entry.stat().st_size < 65536:
                lines.append(f"--- {entry} ---")
                try:
                    lines.append(entry.read_text(encoding="utf-8", errors="replace"))
                except OSError as error:
                    lines.append(f"<unreadable: {error}>")
    else:
        lines.append(f"<{unity3d_dir} does not exist>")
    return "\n".join(lines)


def read_prefs_snapshot(os_name: str, *, home: Path | None = None) -> str | None:
    """Read back whatever the CURRENT state of the OS's own EditorPrefs
    store is — for Linux, the literal file content; for macOS/Windows, a
    best-effort textual dump (defaults read / reg query). Never raises;
    returns a diagnostic string noting failure instead. Run 6
    (33390881487): min-linux-x64's receipt showed preseed applied=true,
    yet FSR's L977 auto-refresh check still did not return early — either
    Unity never read the written XML, or the format does not match what
    Unity itself expects. Capturing this before AND after the cell lets a
    future run compare against what Unity itself writes, instead of
    guessing the format again."""
    home = home or Path.home()
    if os_name == "Linux":
        path = home / ".config" / "unity3d" / "prefs"
        if not path.is_file():
            return None
        try:
            return path.read_text(encoding="utf-8", errors="replace")
        except OSError as error:
            return f"<unreadable: {error}>"
    if os_name == "macOS":
        command = ["defaults", "read", MACOS_DEFAULTS_DOMAIN]
    elif os_name == "Windows":
        command = ["reg", "query", WINDOWS_REGISTRY_KEY]
    else:
        return None
    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=10)
        return result.stdout or result.stderr
    except (OSError, subprocess.SubprocessError) as error:
        return f"<unreadable: {error}>"


def preseed_editor_prefs(
    project: Path, *, os_name: str, home: Path | None = None
) -> dict[str, object]:
    """Write both EditorPrefs offline, before Unity's first launch in this
    cell. Never raises on a failed write — preseed is defense-in-depth, not
    the primary fix, so a failure is recorded honestly in the returned
    receipt (for embedding into the cell's evidence, "честность среды")
    rather than aborting the cell."""
    product_name = resolve_product_name(project)
    keys = [AUTO_REFRESH_MODE_KEY, _stop_dialog_key(product_name)]
    mechanisms = {
        "macOS": "macos_defaults",
        "Windows": "windows_registry",
        "Linux": "linux_prefs_xml",
    }
    if os_name not in mechanisms:
        raise EditorPrefsPreseedError(f"Unsupported os_name for preseed: {os_name!r}")
    mechanism = mechanisms[os_name]

    try:
        if os_name == "macOS":
            _apply_commands(macos_defaults_write_commands(product_name))
        elif os_name == "Windows":
            _apply_commands(windows_reg_add_commands(product_name))
        else:
            prefs_path = (home or Path.home()) / ".config" / "unity3d" / "prefs"
            prefs_path.parent.mkdir(parents=True, exist_ok=True)
            existing = prefs_path.read_text(encoding="utf-8") if prefs_path.is_file() else None
            prefs_path.write_text(
                linux_prefs_xml_patch(existing, product_name), encoding="utf-8"
            )
    except subprocess.CalledProcessError as error:
        return {
            "mechanism": mechanism,
            "product_name": product_name,
            "keys": keys,
            "applied": False,
            "error": error.stderr or str(error),
        }

    return {
        "mechanism": mechanism,
        "product_name": product_name,
        "keys": keys,
        "applied": True,
    }


__all__ = [
    "AUTO_REFRESH_MODE_KEY",
    "AUTO_REFRESH_MODE_VALUE",
    "STOP_DIALOG_KEY_SUFFIX",
    "MACOS_DEFAULTS_DOMAIN",
    "WINDOWS_REGISTRY_KEY",
    "EditorPrefsPreseedError",
    "resolve_product_name",
    "macos_defaults_write_commands",
    "linux_prefs_xml_patch",
    "windows_reg_add_commands",
    "preseed_editor_prefs",
    "read_prefs_snapshot",
    "create_discovery_marker",
    "discover_touched_config_files",
    "find_candidate_prefs_paths",
    "adaptive_preseed_from_discovery",
]
