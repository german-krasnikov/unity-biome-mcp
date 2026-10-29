"""The oracle of the tool catalog (P15): the live MCP surface and ToolSpec of every tool equal golden/tool_surface.txt.

The golden was taken at 86eca971, before any generator existed, so a tool the catalog generates is compared with what its
hand-written version was, not with the generator's own output. Lines come from scripts/dsl_golden.py.
"""
from pathlib import Path

from dsl_golden import env, surface_lines

GOLDEN = Path(__file__).parent / "golden" / "tool_surface.txt"

# tool -> {field: reason}: a difference the owner approved. The field is a field of the golden line (title, description, schema,
# annotations, or a ToolSpec field). Any other difference fails, and an entry whose field no longer differs is stale.
INTENDED: dict[str, dict[str, str]] = {}


def _parse(line: str) -> tuple[tuple[str, str], dict[str, str]]:
    kind, name, *pairs = line.split(" ")
    return (kind, name), dict(pair.split("=", 1) for pair in pairs)


def diff_surface(golden: list[str], live: list[str], intended: dict[str, dict[str, str]]) -> list[str]:
    """What differs between the golden lines and the live ones, minus what `intended` allows."""
    want, got = (dict(map(_parse, lines)) for lines in (golden, live))
    problems = [f"{kind} {name}: not in the golden" for kind, name in sorted(got.keys() - want.keys())]
    problems += [f"{kind} {name}: in the golden, not live" for kind, name in sorted(want.keys() - got.keys())]
    allowed = set()
    for key in sorted(want.keys() & got.keys()):
        for field in sorted(want[key].keys() | got[key].keys()):
            if want[key].get(field) == got[key].get(field):
                continue
            if field in intended.get(key[1], {}):
                allowed.add((key[1], field))
            else:
                problems.append(f"{key[0]} {key[1]}: {field} is {got[key].get(field)}, the golden has {want[key].get(field)}")
    problems += [f"INTENDED {name}.{field} does not differ any more: remove it"
                 for name, fields in sorted(intended.items()) for field in fields if (name, field) not in allowed]
    return problems


def test_live_surface_equals_the_golden():
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    golden_env = next(line for line in lines if line.startswith("# env"))
    problems = diff_surface([line for line in lines if not line.startswith("#")], surface_lines(), INTENDED)
    assert not problems, f"{golden_env} (golden)\n{env()} (live)\n" + "\n".join(problems)


_TOOL = "tool a title=1 description=2 schema=3 annotations=4"
_SPEC = "spec a category=SCENE tier1=False"


def test_a_changed_field_is_reported_with_both_values():
    live = [_TOOL.replace("schema=3", "schema=9"), _SPEC]
    assert diff_surface([_TOOL, _SPEC], live, {}) == ["tool a: schema is 9, the golden has 3"]


def test_a_missing_and_a_new_tool_are_reported():
    assert diff_surface([_TOOL], [_SPEC], {}) == ["spec a: not in the golden", "tool a: in the golden, not live"]


def test_an_intended_field_is_allowed_and_nothing_else():
    live = [_TOOL, _SPEC.replace("False", "True")]
    assert diff_surface([_TOOL, _SPEC], live, {"a": {"tier1": "approved"}}) == []
    assert diff_surface([_TOOL, _SPEC], live, {"a": {"category": "approved"}}) == [
        "spec a: tier1 is True, the golden has False",
        "INTENDED a.category does not differ any more: remove it",
    ]
