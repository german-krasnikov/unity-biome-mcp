"""The oracle of the tool catalog (P15): the live MCP surface, ToolSpec and wire of every tool equal golden/tool_surface.txt.

The golden is a frozen baseline taken at the close of Track #1 (EVAL3 DSL migration), so a tool the catalog generates is
compared with that frozen snapshot, not with the generator's own output. Lines come from scripts/dsl_golden.py; a `wire` line
is the digest of everything a forwarding wrapper sends for a fixed set of calls.
"""
from pathlib import Path

from dsl_golden import env, only_forwards, sample_calls, surface_lines, wire_digest

from unity_mcp.server import _args as _args_of_server
from unity_mcp.tools.tool_specs import _SPECS

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


_send = _args = _helper = None  # the globals of a tool module; the oracle swaps them while it runs a wrapper


async def _plain(a: str, n: int = 5):
    return await _send("f", _args(a=a, n=str(n)))


async def _raw(a: str, n: int = 5):
    return await _send("f", _args(a=a, n=n))


async def _omit(a: str, n: int = 5):
    return await _send("f", _args(a=a, n=str(n) if n != 5 else None))


async def _other_call(a: str, n: int = 5):
    return await _send("f", {"a": _helper(a)})


async def _awaits_other(a: str, n: int = 5):
    return await _send("f", {"a": await _helper(a)})


async def _no_samples(a: dict):
    return await _send("f", {"a": a})


def test_only_a_wrapper_that_calls_nothing_but_send_and_args_is_run():
    assert only_forwards(_plain) and only_forwards(_omit)
    assert not only_forwards(_other_call) and not only_forwards(_awaits_other)


def test_the_sample_calls_vary_one_parameter_at_a_time_and_are_the_same_each_time():
    calls = sample_calls(_plain, "f")
    assert {"a": "a", "n": 5} in calls and {"a": "", "n": 5} in calls and {"a": "a", "n": -1} in calls
    assert len(calls) == len({tuple(call.items()) for call in calls}) and calls == sample_calls(_plain, "f")
    assert sample_calls(_no_samples, "f") is None


def test_the_digest_follows_what_a_wrapper_sends():
    digest = wire_digest(_plain, _args_of_server, "f")
    assert digest and wire_digest(_plain, _args_of_server, "f") == digest      # the same wrapper, the same digest
    assert wire_digest(_raw, _args_of_server, "f") != digest                    # a number kept a number
    assert wire_digest(_omit, _args_of_server, "f") != digest                   # the default left out
    assert wire_digest(_other_call, _args_of_server, "f") is None               # not run at all


def test_every_wrapper_the_catalog_generates_is_covered_by_the_wire_oracle():
    """A generated wrapper the oracle cannot run (a type or a call it does not know) would send anything and pass.

    A BODY wrapper is exempt by design: it never calls _send itself (ToolSet.install's own body-drift
    check guards those instead), it calls the hand module's _<name>_body -- see ToolSet._body_modules.
    """
    from unity_mcp.tools.generated import catalog_tools
    uncovered = [name for name, fn in catalog_tools.tools._provided.items()
                 if name not in catalog_tools.tools._body_modules and not wire_digest(fn, _args_of_server)]
    assert not uncovered, f"the wire oracle does not run {uncovered}"
