"""The 14 CommandOptions fields (minus Description) of every hand-written Register/RegisterAction/RegisterAsync
call in unity-plugin/Editor equal golden/registration.txt (A0, Plans/DSL/A0-DESIGN.md).

The golden was taken at cc566bf4, before ToolMeta.g.cs existed, so A1/A2's generator-driven migration is
compared with what the literal call site said, not with the generator's own output. Lines come from
scripts/registration_golden.py. An intended change (a real migration step) is a dated INTENDED entry below --
never a silent regeneration of the golden.
"""
from pathlib import Path

import pytest
from registration_golden import registration_lines

pytestmark = pytest.mark.csharp_parity

GOLDEN = Path(__file__).parent / "golden" / "registration.txt"
_EDITOR = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor"

# name -> {field: reason}: a difference the owner approved (a real A1/A2/A3 migration step). Empty at A0's own
# commit. Any other difference fails, and an entry whose field no longer differs is stale.
INTENDED: dict[str, dict[str, str]] = {}


def _parse(line: str) -> tuple[str, dict[str, str]]:
    name, *pairs = line.split(" ")
    return name, dict(pair.split("=", 1) for pair in pairs)


def diff_registrations(golden: list[str], live: list[str], intended: dict[str, dict[str, str]]) -> list[str]:
    """What differs between the golden lines and the live ones, minus what `intended` allows."""
    want, got = (dict(map(_parse, lines)) for lines in (golden, live))
    problems = [f"{name}: not in the golden" for name in sorted(got.keys() - want.keys())]
    problems += [f"{name}: in the golden, not live" for name in sorted(want.keys() - got.keys())]
    allowed = set()
    for name in sorted(want.keys() & got.keys()):
        for field in sorted(want[name].keys() | got[name].keys()):
            if want[name].get(field) == got[name].get(field):
                continue
            if field in intended.get(name, {}):
                allowed.add((name, field))
            else:
                problems.append(f"{name}: {field} is {got[name].get(field)}, the golden has {want[name].get(field)}")
    problems += [f"INTENDED {name}.{field} does not differ any more: remove it"
                 for name, fields in sorted(intended.items()) for field in fields if (name, field) not in allowed]
    return problems


def _live_lines() -> list[str]:
    sources = [path.read_text(encoding="utf-8") for path in _EDITOR.rglob("*.cs") if "Tests" not in path.parts]
    return registration_lines(sources)


def test_live_registrations_equal_the_golden():
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    live = _live_lines()
    assert len(live) > 130, "no registration was read: this test would pass without checking"
    problems = diff_registrations([line for line in lines if not line.startswith("#")], live, INTENDED)
    assert not problems, "\n".join(problems)


_CS = '''
    CommandRegistry.Register("a", args => H.F(  // it's a comment with a quote "
        JsonHelper.ExtractString(args, "required: \\"x\\""), 'c'),
        mutating: true, runtime: true, specialDispatch: true, fileHandler: BuildX,
        alwaysAllowed: true, allowedDuringCompile: true, notBatchable: true, playOk: true, noUndo: true,
        mutatingArgsPolicy: IsWriteArgs, required: "p,q", optional: "r", maxResponseChars: 30000);
    CommandRegistry.RegisterAction("b", ExecB, required: "", optional: "s,t");
'''


def test_the_extractor_reads_every_flag_and_nothing_nested():
    assert registration_lines([_CS]) == [
        "a kind=Register mutating=1 runtime=1 special=1 filehandler=1 always=1 compile=1 notbatch=1 playok=1 "
        "noundo=1 policy=1 max=30000 required=p,q optional=r",
        "b kind=RegisterAction mutating=0 runtime=0 special=0 filehandler=0 always=0 compile=0 notbatch=0 "
        "playok=0 noundo=0 policy=0 max=0 required=action optional=s,t",
    ]


_LINE_A = ("a kind=Register mutating=1 runtime=0 special=0 filehandler=0 always=0 compile=0 notbatch=0 "
           "playok=0 noundo=0 policy=0 max=0 required= optional=")


def test_a_changed_field_is_reported_and_an_intended_one_is_allowed():
    changed = [_LINE_A.replace("mutating=1", "mutating=0")]
    assert diff_registrations([_LINE_A], changed, {}) == ["a: mutating is 0, the golden has 1"]
    assert diff_registrations([_LINE_A], changed, {"a": {"mutating": "dated reason"}}) == []
    assert diff_registrations([_LINE_A], [_LINE_A], {"a": {"mutating": "stale"}}) == [
        "INTENDED a.mutating does not differ any more: remove it"]
    assert diff_registrations([_LINE_A], [], {}) == ["a: in the golden, not live"]
    assert diff_registrations([], [_LINE_A], {}) == ["a: not in the golden"]
