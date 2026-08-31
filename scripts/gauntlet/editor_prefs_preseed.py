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


def _read_project_settings_field(project: Path, field: str) -> str:
    """Shared line-scan for resolve_product_name/resolve_company_name —
    Unity's .asset files are a custom YAML variant (!u! tags) that break
    standard YAML parsers, so this stays a simple line scan, matching how
    other tools in this codebase already parse similar files (e.g.
    ProjectVersion.txt)."""
    settings_path = project / "ProjectSettings" / "ProjectSettings.asset"
    if not settings_path.is_file():
        raise EditorPrefsPreseedError(f"ProjectSettings.asset not found: {settings_path}")
    prefix = f"  {field}: "
    for line in settings_path.read_text(encoding="utf-8").splitlines():
        if line.startswith(prefix):
            return line[len(prefix):].strip()
    raise EditorPrefsPreseedError(f"{field} not found in {settings_path}")


def resolve_product_name(project: Path) -> str:
    """Read productName from the disposable worker's OWN
    ProjectSettings.asset — never a hardcoded literal."""
    return _read_project_settings_field(project, "productName")


def resolve_company_name(project: Path) -> str:
    """Read companyName from the disposable worker's OWN
    ProjectSettings.asset — never a hardcoded literal. Run 8
    (33396935103) coordinator diagnosis: Unity 6 on Linux keys its real
    flat-file EditorPrefs store per companyName/productName
    (~/.config/unity3d/<companyName>/<productName>/prefs), not only the
    machine-global ~/.config/unity3d/prefs this preseed used to write
    exclusively."""
    return _read_project_settings_field(project, "companyName")


def linux_company_product_prefs_path(home: Path, company_name: str, product_name: str) -> Path:
    """Unity 6's real per-project Linux EditorPrefs store — see
    resolve_company_name's docstring for why this exists alongside the
    machine-global flat path."""
    return home / ".config" / "unity3d" / company_name / product_name / "prefs"


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
    """Parse a discover_touched_config_files() report for additional Unity
    EditorPrefs storage: under home, and not the already-known
    ~/.config/unity3d/prefs (that one is already preseeded directly, no
    need to rediscover it).

    Run 8 (33396935103) regression: an earlier "contains 'unity' anywhere
    in the path" filter matched ~/.local/share/unity3d/Unity/Unity_lic.ulf
    (an XML-shaped Unity LICENSE file) and linux_prefs_xml_patch
    unconditionally overwrote it as a fabricated <unity_prefs> document,
    destroying the license and crashing the run's next Unity launch. Every
    real Unity flat prefs file observed (macOS, Linux, and every
    known Linux path variant) is always literally named "prefs" — only
    that exact basename may be a candidate, never a broader substring
    match against arbitrary Unity-related files (licenses, logs, caches)."""
    known = (home / ".config" / "unity3d" / "prefs").resolve()
    home_str = str(home)
    candidates: set[Path] = set()
    for raw_line in discovery_report.splitlines():
        line = raw_line.strip()
        if not line.startswith(home_str):
            continue
        candidate = Path(line).resolve()
        if candidate.name != "prefs":
            continue
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


def read_prefs_snapshot(
    os_name: str, *, home: Path | None = None,
    company_name: str | None = None, product_name: str | None = None,
) -> str | None:
    """Read back whatever the CURRENT state of the OS's own EditorPrefs
    store is — for Linux, the literal file content(s); for macOS/Windows, a
    best-effort textual dump (defaults read / reg query). Never raises;
    returns a diagnostic string noting failure instead. Run 6
    (33390881487): min-linux-x64's receipt showed preseed applied=true,
    yet FSR's L977 auto-refresh check still did not return early — either
    Unity never read the written XML, or the format does not match what
    Unity itself expects. Capturing this before AND after the cell lets a
    future run compare against what Unity itself writes, instead of
    guessing the format again.

    When company_name/product_name are given (Linux only), also includes
    Unity's real per-project store (see resolve_company_name) alongside
    the machine-global flat file — Run 8 (33396935103): capturing both
    lets a future run see exactly which one Unity actually read from."""
    home = home or Path.home()
    if os_name == "Linux":
        candidates = [home / ".config" / "unity3d" / "prefs"]
        if company_name and product_name:
            candidates.append(linux_company_product_prefs_path(home, company_name, product_name))
        parts: list[str] = []
        for path in candidates:
            if not path.is_file():
                continue
            try:
                content = path.read_text(encoding="utf-8", errors="replace")
            except OSError as error:
                content = f"<unreadable: {error}>"
            parts.append(f"--- {path} ---\n{content}")
        return "\n".join(parts) if parts else None
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

    company_name: str | None = None
    try:
        if os_name == "macOS":
            _apply_commands(macos_defaults_write_commands(product_name))
        elif os_name == "Windows":
            _apply_commands(windows_reg_add_commands(product_name))
        else:
            home_dir = home or Path.home()
            flat_path = home_dir / ".config" / "unity3d" / "prefs"
            flat_path.parent.mkdir(parents=True, exist_ok=True)
            existing = flat_path.read_text(encoding="utf-8") if flat_path.is_file() else None
            flat_path.write_text(
                linux_prefs_xml_patch(existing, product_name), encoding="utf-8"
            )
            # Additive, never instead of the flat path above (Run 8:
            # 33396935103) — Unity's real per-project store, merging
            # whatever Unity itself already wrote there.
            company_name = resolve_company_name(project)
            cp_path = linux_company_product_prefs_path(home_dir, company_name, product_name)
            cp_path.parent.mkdir(parents=True, exist_ok=True)
            cp_existing = cp_path.read_text(encoding="utf-8") if cp_path.is_file() else None
            cp_path.write_text(
                linux_prefs_xml_patch(cp_existing, product_name), encoding="utf-8"
            )
    except subprocess.CalledProcessError as error:
        return {
            "mechanism": mechanism,
            "product_name": product_name,
            "keys": keys,
            "applied": False,
            "error": error.stderr or str(error),
        }

    receipt: dict[str, object] = {
        "mechanism": mechanism,
        "product_name": product_name,
        "keys": keys,
        "applied": True,
    }
    if company_name is not None:
        receipt["company_name"] = company_name
    return receipt


__all__ = [
    "AUTO_REFRESH_MODE_KEY",
    "AUTO_REFRESH_MODE_VALUE",
    "STOP_DIALOG_KEY_SUFFIX",
    "MACOS_DEFAULTS_DOMAIN",
    "WINDOWS_REGISTRY_KEY",
    "EditorPrefsPreseedError",
    "resolve_product_name",
    "resolve_company_name",
    "linux_company_product_prefs_path",
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
