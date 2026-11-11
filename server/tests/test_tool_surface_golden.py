"""The oracle of the tool catalog (P15): the live MCP surface, ToolSpec and wire of every tool equal golden/tool_surface.txt.

The golden was taken at 86eca971, before any generator existed, so a tool the catalog generates is compared with what its
hand-written version was, not with the generator's own output. Lines come from scripts/dsl_golden.py; a `wire` line is the digest
of everything a forwarding wrapper sends for a fixed set of calls.
"""
from pathlib import Path

from dsl_golden import env, only_forwards, sample_calls, surface_lines, wire_digest

from unity_mcp.server import _args as _args_of_server
from unity_mcp.tools.tool_specs import _SPECS

GOLDEN = Path(__file__).parent / "golden" / "tool_surface.txt"

# tool -> {field: reason}: a difference the owner approved. The field is a field of the golden line (title, description, schema,
# annotations, or a ToolSpec field). Any other difference fails, and an entry whose field no longer differs is stale.
# `session`: ToolSpec gained this field (D2, 2026-11-08, S0) -- every spec now reports it, default False, until a tool opts in.
INTENDED: dict[str, dict[str, str]] = {name: {"session": "S0 D2: new ToolSpec.session field, default False"} for name in _SPECS}
# S0 2/11: discover_tools moves off the bare mcp.tool() (no annotation) onto the catalog's derive("RO", session=True).
INTENDED["discover_tools"]["annotations"] = 'S0 2/11: None -> derive("RO", session=True)'
# S0 4/11: lint_playtest moves off the hand _RW_IDEM constant onto the catalog's derive("RO") -- pure read-only, and its
# ARG path/script descriptions move from _param_descriptions.py into the catalog (dsl/tools.biome), changing the schema line too.
INTENDED["lint_playtest"]["annotations"] = 'S0 4/11: RW_IDEM -> derive("RO") (pure read-only)'
INTENDED["lint_playtest"]["schema"] = "S0 4/11: path/script descriptions moved from _param_descriptions.py into the catalog"
# S0 5/11: ping_object moves off the hand _RW constant onto the catalog's derive("RO", session=True) --
# idempotentHint flips True (retry-safe), readOnlyHint stays False (SESSION overrides the RO effect).
INTENDED["ping_object"]["annotations"] = 'S0 5/11: RW -> derive("RO", session=True) (retry-safe)'
# S0 7/11: resolve_tool_schema moves off a bare mcp.tool() (no MCP annotation) onto the catalog's
# derive("RO") -- plain read-only, no SESSION (it only reads the schema registry, no side effect at all).
INTENDED["resolve_tool_schema"]["annotations"] = 'S0 7/11: None -> derive("RO") (readOnlyHint=True)'
# S0 8/11: set_llm_config moves off a bare mcp.tool() (no MCP annotation) onto the catalog's
# derive("RW_IDEM") -- idempotentHint flips True; mutability (guards) stays write (effect RW_IDEM).
INTENDED["set_llm_config"]["annotations"] = 'S0 8/11: None -> derive("RW_IDEM") (idempotentHint=True, guards stay write)'
# A3-DESIGN.md Sec 3 #9/#10: run_tests/get_test_run gain real parameters (assemblies/categories/group/tests;
# compact) that C# already read but the Python wrapper never exposed -- description and schema both change.
INTENDED["run_tests"]["description"] = "A3 #9: assemblies/categories/group/tests documented"
INTENDED["run_tests"]["schema"] = "A3 #9: assemblies/categories/group/tests params added (C# already read them)"
INTENDED["get_test_run"]["description"] = "A3 #10: compact documented"
# A3-DESIGN.md Sec 3 #11/#12: animation gains compact (C# already read it, undeclared in the catalog); prefab
# gains parent/child_path (C# already read both) -- new FORWARD params change the schema hash and the sampled
# wire calls/digest for both tools.
INTENDED["animation"]["schema"] = "A3 #11: compact bool param added (C# already read it)"
INTENDED["animation"]["calls"] = "A3 #11: compact param changes the sampled wire calls"
INTENDED["animation"]["digest"] = "A3 #11: compact param changes the sampled wire digest"
INTENDED["prefab"]["schema"] = "A3 #12: parent/child_path params added (C# already read both)"
INTENDED["prefab"]["calls"] = "A3 #12: parent/child_path params change the sampled wire calls"
INTENDED["prefab"]["digest"] = "A3 #12: parent/child_path params change the sampled wire digest"
INTENDED["get_test_run"]["schema"] = "A3 #10: compact bool param added (C# already read it)"


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
    """A generated wrapper the oracle cannot run (a type or a call it does not know) would send anything and pass."""
    from unity_mcp.tools.generated import catalog_tools
    uncovered = [name for name, fn in catalog_tools.tools._provided.items() if not wire_digest(fn, _args_of_server)]
    assert not uncovered, f"the wire oracle does not run {uncovered}"
