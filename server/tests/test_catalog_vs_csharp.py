"""The catalog is the source (A4, Plans/DSL/PLAN.md A4): a tool of the catalog states its own MAXCHARS, and the
subset of Register calls still hand-written (not yet `ToolMeta.Get(cmd)`-delegated) must say the same number in
its `maxResponseChars:` argument.

`registrations()`/`_top_level`/`_CALL` are the shared literal-call-site reader: for a delegated call it reads
the generated ToolMeta.g.cs switch instead (via toolmeta_reader), since a delegating call site states none of
these flags itself.
"""
import re
from pathlib import Path

import pytest
import toolmeta_reader

from unity_mcp.tools.generated import catalog_specs

pytestmark = pytest.mark.csharp_parity

_EDITOR = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor"
_CALL = re.compile(r'CommandRegistry\.(Register|RegisterAsync|RegisterAction)\(\s*"([^"]+)"')


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


def _hand_written() -> dict[str, dict]:
    return registrations([path.read_text(encoding="utf-8") for path in _EDITOR.rglob("*.cs") if "Tests" not in path.parts])


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
