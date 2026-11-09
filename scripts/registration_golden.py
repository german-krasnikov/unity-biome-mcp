"""Print the 14 CommandOptions fields (minus Description) of every hand-written
CommandRegistry.Register/RegisterAction/RegisterAsync call in unity-plugin/Editor as the text
lines of server/tests/golden/registration.txt.

This is the oracle A1/A2 compare their migration against (A0, Plans/DSL/A0-DESIGN.md): taken at
cc566bf4, before ToolMeta.g.cs existed, and never regenerated to make a difference disappear. An
intended change is an INTENDED entry, with its reason, in server/tests/test_registration_golden.py.

Reuses the tokenizer (_CALL, _top_level) already proven correct by
server/tests/test_catalog_vs_csharp.py -- not its registrations() dict shape, which that file
owns and keeps changing across A2/A3/A4.

Usage (from the repository root):
  python scripts/registration_golden.py > server/tests/golden/registration.txt
"""

import re
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
_SERVER_TESTS = _REPO_ROOT / "server" / "tests"
if str(_SERVER_TESTS) not in sys.path:
    sys.path.insert(0, str(_SERVER_TESTS))

import toolmeta_reader  # noqa: E402  (same directory as this module)
from test_catalog_vs_csharp import _CALL, _top_level  # noqa: E402  (path set up above)

_EDITOR = _REPO_ROOT / "unity-plugin" / "Editor"

HEADER = """\
# The 14 CommandOptions fields (minus Description) of every hand-written Register/RegisterAction/RegisterAsync
# call in unity-plugin/Editor, taken at cc566bf4 before ToolMeta.g.cs existed (the oracle of A1/A2's migration).
# <name> kind=<Register|RegisterAction|RegisterAsync> mutating=0|1 runtime=0|1 special=0|1 filehandler=0|1
#   always=0|1 compile=0|1 notbatch=0|1 playok=0|1 noundo=0|1 policy=0|1 max=<int> required=a,b optional=c,d
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_registration_golden.py.
"""

_FIELD_ORDER = ["kind", "mutating", "runtime", "special", "filehandler", "always", "compile",
                "notbatch", "playok", "noundo", "policy", "max", "required", "optional"]


def _bool(args: str, key: str) -> str:
    return str(int(bool(re.search(rf'\b{key}:\s*true\b', args))))


def _present(args: str, key: str) -> str:
    return str(int(bool(re.search(rf"\b{key}:", args))))


def _csv(args: str, key: str) -> set[str]:
    match = re.search(rf'\b{key}:\s*"([^"]*)"', args)
    return set(filter(None, match.group(1).split(","))) if match else set()


def _line(name: str, kind: str, args: str) -> str:
    """One "<name> kind=... field=value ..." line for a single Register/RegisterAction/RegisterAsync call."""
    required = sorted(_csv(args, "required") | ({"action"} if kind == "RegisterAction" else set()))
    optional = sorted(_csv(args, "optional"))
    chars = re.search(r"\bmaxResponseChars:\s*(\d+)", args)
    fields = {
        "kind": kind,
        "mutating": _bool(args, "mutating"),
        "runtime": _bool(args, "runtime"),
        "special": _bool(args, "specialDispatch"),
        "filehandler": _present(args, "fileHandler"),
        "always": _bool(args, "alwaysAllowed"),
        "compile": _bool(args, "allowedDuringCompile"),
        "notbatch": _bool(args, "notBatchable"),
        "playok": _bool(args, "playOk"),
        "noundo": _bool(args, "noUndo"),
        "policy": _present(args, "mutatingArgsPolicy"),
        "max": chars.group(1) if chars else "0",
        "required": ",".join(required),
        "optional": ",".join(optional),
    }
    if toolmeta_reader.delegates(args):
        # ToolMeta.Get("<name>") replaced the named flags -- read them from the generated switch instead
        # of the (now-absent) call-site text (A2-DESIGN.md Sec 1). kind/filehandler/policy are untouched:
        # ToolMeta never emits them, and they correctly read as absent from `args` either way.
        meta = toolmeta_reader.read_generated().get(name, {})
        for key in ("mutating", "runtime", "special", "always", "compile", "notbatch", "playok", "noundo"):
            fields[key] = str(int(meta.get(key, False)))
        fields["max"] = str(meta.get("max", 0))
        fields["required"] = ",".join(sorted(meta.get("required", set())))
        fields["optional"] = ",".join(sorted(meta.get("optional", set())))
    return name + " " + " ".join(f"{key}={fields[key]}" for key in _FIELD_ORDER)


def registration_lines(sources: list[str]) -> list[str]:
    """One "<name> kind=... field=value ..." line per Register/RegisterAction/RegisterAsync call, sorted by name."""
    found = {}
    for text in sources:
        for match in _CALL.finditer(text):
            kind, name = match.groups()
            args = _top_level(text, match.end())
            found[name] = _line(name, kind, args)
    return [found[name] for name in sorted(found)]


def main(argv: list[str]) -> None:
    sources = [path.read_text(encoding="utf-8") for path in _EDITOR.rglob("*.cs") if "Tests" not in path.parts]
    print(HEADER, end="")
    print("\n".join(registration_lines(sources)))


if __name__ == "__main__":
    main(sys.argv[1:])
