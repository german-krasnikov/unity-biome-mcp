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
]
