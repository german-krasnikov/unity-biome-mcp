"""ToolMeta.g.cs (A1, Plans/DSL/A1-DESIGN.md) states 11 of the golden's 14 CommandOptions fields for every
TOOL/INTERNAL tools.biome block that overlaps the golden registration census (A0): mutating (from HEAD's effect),
runtime, required/optional (from ARG), max (from MAXCHARS), and the GUARD/WIRE flags (special, always, compile,
notbatch, playok, noundo). kind, filehandler and policy are never emitted by ToolMeta and so never compared here --
A1-DESIGN.md Sec 1 gives the reason for each: kind is a call-site choice (Register/RegisterAction/RegisterAsync, not
catalog data), filehandler is a delegate no catalog line can express, and policy has zero golden users today.

KNOWN_DIVERGENT and KNOWN_UNCOVERED are the census A1 measured at its own HEAD (A1-DESIGN.md Sec 4 and Sec 0): 31
names with a pre-existing ARG-name/shape drift between the catalog and the hand-written C# (A3's scope to fix, not
A1's), and 19 golden names with no tools.biome block at all yet (S0/S0b/A0's moving scope, not A1's).
"""
import re
from pathlib import Path

import pytest
from test_registration_golden import _parse, diff_registrations

pytestmark = pytest.mark.csharp_parity

_GOLDEN = Path(__file__).parent / "golden" / "registration.txt"
_TOOLMETA = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor" / "Generated" / "ToolMeta.g.cs"

# The 11 of golden's 14 fields ToolMeta can state, in golden's own relative order (kind/filehandler/policy dropped).
_FIELDS = ["mutating", "runtime", "special", "always", "compile", "notbatch", "playok", "noundo", "max", "required",
           "optional"]

_ENTRY = re.compile(r'case "([^"]+)":\s*return new CommandOptions\s*\{(.*?)\};', re.DOTALL)

KNOWN_DIVERGENT: dict[str, dict[str, str]] = {}
for _name in ["ask_user", "get_component", "get_schema", "references", "render_analyze",
              "run_tests", "scene_environment", "set_material", "source_patch_write", "timeline",
              "validate_playtest_aliases"]:
    KNOWN_DIVERGENT.setdefault(_name, {})["required"] = "A1-DESIGN.md Sec 4: pre-existing ARG-name drift, A3's scope"
for _name in ["animation", "ask_user", "asset", "batch", "get_component", "get_frame_stats",
              "get_schema", "inspect", "inspect_uitk",
              "list_playtest_files", "prefab", "profile", "references", "render_analyze",
              "scene_environment", "screenshot", "set_material", "set_property", "shader",
              "timeline", "uitk_file", "validate_playtest_aliases"]:
    KNOWN_DIVERGENT.setdefault(_name, {})["optional"] = "A1-DESIGN.md Sec 4: pre-existing ARG-name drift, A3's scope"
# A3-DESIGN.md Sec 3 #8/#9/#14/#29: catalog already declared these ARGs (DEFAULT-based, so always generated as
# Optional); golden is a frozen pre-A1 C# snapshot that never listed them at all -- stays divergent regardless
# of the C#-literal fix landing in this commit (run_tests.required's divergence is the same request_id, viewed
# from the other field: golden still calls it required, the generated/live side no longer does).
KNOWN_DIVERGENT.setdefault("run_playtest", {})["optional"] = "A3-DESIGN.md Sec 3 #14: golden predates before_hook/after_hook/format"
KNOWN_DIVERGENT.setdefault("start_playtest", {})["optional"] = "A3-DESIGN.md Sec 3 #29: golden predates before_hook/after_hook/format"
KNOWN_DIVERGENT.setdefault("run_tests", {})["optional"] = "A3-DESIGN.md Sec 3 #8/#9: golden predates request_id as optional"
KNOWN_DIVERGENT.setdefault("batch", {})["mutating"] = (
    "dispatcher, not itself a write (test_catalog_vs_csharp.py's own KNOWN_DIVERGENT)")

KNOWN_UNCOVERED = frozenset({
    "checkpoint_undo_restore", "clear_console", "console_clear_buffer", "force_play_stop", "force_refresh",
    "get_disabled_tools", "get_profile_context", "navmesh", "search_context", "set_client_label",
    "set_runtime_property", "set_tool_catalog", "sync", "sync_status", "warm_type_cache", "watch_add",
    "watch_clear", "watch_remove", "watch_reset",
})


def _flag(body: str, prop: str) -> str:
    return str(int(bool(re.search(rf"\b{prop}\s*=\s*true\b", body))))


def _array(body: str, prop: str) -> str:
    """The quoted items of `<prop> = string.Join(",", new string[] { "a", "b", })`, sorted like golden's own CSV."""
    match = re.search(prop + r' = string\.Join\(",", new string\[\] \{(.*?)\}\)', body, re.DOTALL)
    items = re.findall(r'"([^"]*)"', match.group(1)) if match else []
    return ",".join(sorted(items))


def _toolmeta_line(name: str, body: str) -> str:
    chars = re.search(r"\bMaxResponseChars\s*=\s*(\d+)", body)
    fields = {
        "mutating": _flag(body, "Mutating"), "runtime": _flag(body, "Runtime"),
        "special": _flag(body, "SpecialDispatch"), "always": _flag(body, "AlwaysAllowed"),
        "compile": _flag(body, "AllowedDuringCompile"), "notbatch": _flag(body, "NotBatchable"),
        "playok": _flag(body, "PlayOk"), "noundo": _flag(body, "NoUndo"),
        "max": chars.group(1) if chars else "0", "required": _array(body, "Required"),
        "optional": _array(body, "Optional"),
    }
    return name + " " + " ".join(f"{key}={fields[key]}" for key in _FIELDS)


def _toolmeta_lines() -> dict[str, str]:
    text = _TOOLMETA.read_text(encoding="utf-8")
    return {name: _toolmeta_line(name, body) for name, body in _ENTRY.findall(text)}


def _reduced_golden_lines() -> dict[str, str]:
    lines = [line for line in _GOLDEN.read_text(encoding="utf-8").splitlines() if line and not line.startswith("#")]
    reduced = {}
    for line in lines:
        name, fields = _parse(line)
        reduced[name] = name + " " + " ".join(f"{key}={fields[key]}" for key in _FIELDS)
    return reduced


def test_toolmeta_matches_golden_on_overlap():
    golden, toolmeta = _reduced_golden_lines(), _toolmeta_lines()
    common = golden.keys() & toolmeta.keys()
    problems = diff_registrations([golden[name] for name in common], [toolmeta[name] for name in common],
                                   KNOWN_DIVERGENT)
    assert not problems, "\n".join(problems)


def test_uncovered_names_are_exactly_fenced():
    golden, toolmeta = _reduced_golden_lines(), _toolmeta_lines()
    assert golden.keys() - toolmeta.keys() == KNOWN_UNCOVERED


def test_toolmeta_covers_at_least_110_names():
    assert len(_toolmeta_lines()) >= 110, "no entry was parsed: this test would pass without checking"
