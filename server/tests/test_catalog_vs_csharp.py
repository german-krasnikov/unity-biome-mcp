"""While the Unity registrations of the catalog's tools are hand-written, they must say what the catalog says (step 7.1).

For every tool with a hand-written CommandRegistry.Register* call in unity-plugin/Editor this compares `mutating` with
ToolSpec.mutability, `runtime` with runtime_only, `required` and `optional` with the Python parameters, and, for a tool of the
catalog, `maxResponseChars` with its MAXCHARS. KNOWN_DIVERGENT is the census of the tools that differ today (measured at 86eca971,
no verdict on any of them): it must match exactly, so a new difference fails and a repaired one asks to be removed from the list.
"""
import re
from pathlib import Path
from types import SimpleNamespace

import pytest
import toolmeta_reader

from unity_mcp.server import mcp
from unity_mcp.tools.generated import catalog_specs
from unity_mcp.tools.tool_specs import _SPECS

pytestmark = pytest.mark.csharp_parity

_EDITOR = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor"
_CALL = re.compile(r'CommandRegistry\.(Register|RegisterAsync|RegisterAction)\(\s*"([^"]+)"')

KNOWN_DIVERGENT = {
    "mutating": {"batch"},
    "runtime": set(),
    # A3-DESIGN.md Sec 3 #30/#31: get_component.type's DEFAULT_TEXT "" fallback (component= alias) and
    # scene_environment's RegisterAction-forced action are structural, not bugs -- see both tools' optional
    # entries below for the same two facts viewed from the other field.
    "required": {"get_component", "scene_environment"},
    # A3-DESIGN.md Sec 3 #30/#31 (get_component, scene_environment, as above); #32-34 (asset/uitk_file/
    # inspect_uitk's class_name/cls/show_unity_private renames -- WIRE fixes test_toolmeta_vs_golden.py but
    # not this oracle, which compares Python's own live property name against the untouched C# literal);
    # #35 (inspect.type is a C#-only legacy fallback for components, never exposed in Python).
    "optional": {"asset", "get_component", "inspect", "inspect_uitk", "scene_environment", "uitk_file"},
}


def _top_level(text: str, start: int) -> str:
    """What stands directly in the parentheses of the call that opens before `start`: strings kept, nested brackets dropped."""
    out, depth, i = [], 0, start
    while i < len(text):
        c = text[i]
        if c in "\"'":
            end = i + 1
            while text[end] != c:
                end += 2 if text[end] == "\\" else 1
            if depth == 0 and c == '"':
                out.append(text[i:end + 1])
            i = end + 1
            continue
        if text.startswith("//", i):
            i = text.index("\n", i)
            continue
        if c in "([{":
            depth += 1
        elif c in ")]}":
            if depth == 0:
                break
            depth -= 1
        elif depth == 0:
            out.append(c)
        i += 1
    return "".join(out)


def _csv(args: str, key: str) -> set[str]:
    match = re.search(rf'\b{key}:\s*"([^"]*)"', args)
    return set(filter(None, match.group(1).split(","))) if match else set()


def registrations(sources: list[str]) -> dict[str, dict]:
    """name -> the flags of its Register call: mutating, runtime, play_ok, no_undo, required and optional (sets), max_chars."""
    found = {}
    for text in sources:
        for match in _CALL.finditer(text):
            kind, name = match.groups()
            args = _top_level(text, match.end())
            chars = re.search(r"\bmaxResponseChars:\s*(\d+)", args)
            found[name] = {
                "mutating": bool(re.search(r"\bmutating:\s*true\b", args)),
                "runtime": bool(re.search(r"\bruntime:\s*true\b", args)),
                "play_ok": bool(re.search(r"\bplayOk:\s*true\b", args)),
                "no_undo": bool(re.search(r"\bnoUndo:\s*true\b", args)),
                "required": _csv(args, "required") | ({"action"} if kind == "RegisterAction" else set()),
                "optional": _csv(args, "optional"),
                "max_chars": int(chars.group(1)) if chars else 0,
            }
            if toolmeta_reader.delegates(args):
                # ToolMeta.Get("<name>") replaced the named flags (A2-DESIGN.md Sec 1) -- read them from
                # the generated switch instead of the (now-absent) call-site text.
                meta = toolmeta_reader.read_generated().get(name, {})
                found[name] = {
                    "mutating": meta.get("mutating", False), "runtime": meta.get("runtime", False),
                    "play_ok": meta.get("playok", False), "no_undo": meta.get("noundo", False),
                    "required": meta.get("required", set()), "optional": meta.get("optional", set()),
                    "max_chars": meta.get("max", 0),
                }
    return found


def divergences(cs: dict, spec, tool) -> set[str]:
    """The kinds in which the hand-written registration `cs` differs from the Python side: ToolSpec `spec`, registered `tool`."""
    found = set()
    if spec and (spec.mutability == "write") != cs["mutating"]:
        found.add("mutating")
    if spec and spec.runtime_only != cs["runtime"]:
        found.add("runtime")
    if tool:
        properties, required = set(tool.parameters.get("properties", {})), set(tool.parameters.get("required", []))
        if required != cs["required"]:
            found.add("required")
        if properties - required != cs["optional"]:
            found.add("optional")
    return found


def _hand_written() -> dict[str, dict]:
    return registrations([path.read_text(encoding="utf-8") for path in _EDITOR.rglob("*.cs") if "Tests" not in path.parts])


def test_the_hand_written_registrations_differ_from_the_python_side_only_as_known():
    live = {name: divergences(cs, _SPECS.get(name), mcp._tool_manager._tools.get(name)) for name, cs in _hand_written().items()}
    assert len(live) > 100, "no registration was read: this test would pass without checking"
    problems = []
    for kind, known in KNOWN_DIVERGENT.items():
        now = {name for name, kinds in live.items() if kind in kinds}
        problems += [f"{kind}: {name} differs and is not in KNOWN_DIVERGENT" for name in sorted(now - known)]
        problems += [f"{kind}: {name} no longer differs: remove it from KNOWN_DIVERGENT" for name in sorted(known - now)]
    assert not problems, "\n".join(problems)


def test_the_tools_of_the_catalog_state_their_response_limit():
    hand_written = _hand_written()
    unity_tools = {name for name in catalog_specs.SPEC_KWARGS if name in hand_written}
    assert unity_tools, "no tool of the catalog has a hand-written registration"
    for name in unity_tools:
        assert hand_written[name]["max_chars"] == catalog_specs.SPEC_KWARGS[name].get("max_chars", 0), name


_CS = '''
    CommandRegistry.Register("a", args => H.F(  // it's a comment with a quote "
        JsonHelper.ExtractString(args, "required: \\"x\\""), 'c'),
        mutating: true, playOk: true, required: "p,q", optional: "r", maxResponseChars: 30000);
    CommandRegistry.RegisterAction("b", ExecB, required: "", optional: "s,t", noUndo: true);
    CommandRegistry.RegisterAsync("c", AsyncC, runtime: true, required: "u", optional: "");
'''


def test_a_delegating_call_reads_its_fields_from_toolmeta_reader(monkeypatch):
    monkeypatch.setattr(toolmeta_reader, "read_generated", lambda: {
        "d": {"mutating": True, "runtime": False, "playok": True, "noundo": False, "max": 500,
              "required": {"p"}, "optional": {"q"}},
    })
    found = registrations(['CommandRegistry.Register("d", ExecD, ToolMeta.Get("d"));'])
    assert found["d"] == {"mutating": True, "runtime": False, "play_ok": True, "no_undo": False,
                          "required": {"p"}, "optional": {"q"}, "max_chars": 500}


def test_the_registration_reader_takes_the_flags_of_the_call_and_nothing_nested():
    found = registrations([_CS])
    assert found["a"] == {"mutating": True, "runtime": False, "play_ok": True, "no_undo": False, "required": {"p", "q"}, "optional": {"r"},
                          "max_chars": 30000}
    assert found["b"] == {"mutating": False, "runtime": False, "play_ok": False, "no_undo": True, "required": {"action"}, "optional": {"s", "t"},
                          "max_chars": 0}
    assert found["c"] == {"mutating": False, "runtime": True, "play_ok": False, "no_undo": False, "required": {"u"}, "optional": set(),
                          "max_chars": 0}


def test_each_kind_of_difference_is_found():
    tool = SimpleNamespace(parameters={"properties": {"p": {}, "q": {}}, "required": ["p"]})
    spec = SimpleNamespace(mutability="write", runtime_only=True)
    same = {"mutating": True, "runtime": True, "required": {"p"}, "optional": {"q"}}
    assert divergences(same, spec, tool) == set()
    assert divergences({**same, "mutating": False, "runtime": False, "required": {"p", "q"}, "optional": set()}, spec, tool) == {
        "mutating", "runtime", "required", "optional"}
