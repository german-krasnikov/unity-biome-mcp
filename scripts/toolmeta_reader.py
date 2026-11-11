"""Parses unity-plugin/Editor/Generated/ToolMeta.g.cs's generated switch into the CommandOptions fields it
states. Used by server/tests/test_catalog_vs_csharp.py -- needed starting A2 (Plans/DSL/A2-DESIGN.md Sec 1),
for when a call site stops stating its flags as named parameters and states `ToolMeta.Get(cmd)` instead; the
old per-call-site regexes find nothing in that shape.
"""
import re
from functools import lru_cache
from pathlib import Path

_TOOLMETA_PATH = Path(__file__).resolve().parent.parent / "unity-plugin" / "Editor" / "Generated" / "ToolMeta.g.cs"
_ENTRY = re.compile(r'case "([^"]+)":\s*return new CommandOptions\s*\{(.*?)\};', re.DOTALL)
_DELEGATES = re.compile(r"\bToolMeta\.Get\b")


def _flag(body: str, prop: str) -> bool:
    return bool(re.search(rf"\b{prop}\s*=\s*true\b", body))


def _array(body: str, prop: str) -> set[str]:
    match = re.search(prop + r' = string\.Join\(",", new string\[\] \{(.*?)\}\)', body, re.DOTALL)
    return set(re.findall(r'"([^"]*)"', match.group(1))) if match else set()


def read(toolmeta_source: str) -> dict[str, dict]:
    """name -> {mutating, runtime, special, always, compile, notbatch, playok, noundo, max, required, optional}."""
    out = {}
    for name, body in _ENTRY.findall(toolmeta_source):
        chars = re.search(r"\bMaxResponseChars\s*=\s*(\d+)", body)
        out[name] = {
            "mutating": _flag(body, "Mutating"), "runtime": _flag(body, "Runtime"),
            "special": _flag(body, "SpecialDispatch"), "always": _flag(body, "AlwaysAllowed"),
            "compile": _flag(body, "AllowedDuringCompile"), "notbatch": _flag(body, "NotBatchable"),
            "playok": _flag(body, "PlayOk"), "noundo": _flag(body, "NoUndo"),
            "max": int(chars.group(1)) if chars else 0,
            "required": _array(body, "Required"), "optional": _array(body, "Optional"),
        }
    return out


@lru_cache(maxsize=1)
def read_generated() -> dict[str, dict]:
    """read() of the live ToolMeta.g.cs, cached for the process (callers loop over ~137 call sites)."""
    return read(_TOOLMETA_PATH.read_text(encoding="utf-8"))


def delegates(args: str) -> bool:
    """True if a call's own argument text is `ToolMeta.Get(...)` rather than named parameters.

    Matches the bare `ToolMeta.Get` identifier, not `ToolMeta.Get(` -- the bracket-depth-aware extractor
    test_catalog_vs_csharp.py's `_top_level` uses strips a nested call's own opening paren, so the `args`
    text it hands to `delegates()` never contains the trailing "(".
    """
    return bool(_DELEGATES.search(args))
