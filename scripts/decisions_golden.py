"""Print the lines of the golden files of the decisions (server/tests/golden/decisions/), the oracle of dsl/decisions (step 7.2, P15).

Each golden holds what the hand-written code answered before its rule moved into a generated decision: one Python literal per line
(a tuple of the call and the answer), so no expectation is read from the code under test and no JSON is authored. The golden is taken
once, at the commit before the seam changes, and is never regenerated to make a difference disappear: an intended change is an
INTENDED entry, with its reason, in the test of the family. This script reproduces the lines (run it at the commit named in the
header of the golden, to check the golden) and nothing else.

Usage (from the repository root; <commit> is the commit the file is taken at, written into its header):
  python scripts/decisions_golden.py is_write <commit>
"""
import itertools
import pathlib
import re
import sys

_ROOT = pathlib.Path(__file__).resolve().parent.parent
if str(_ROOT / "server" / "src") not in sys.path:
    sys.path.insert(0, str(_ROOT / "server" / "src"))

IS_WRITE_HEADER = """\
# What middleware_types.is_write(cmd, args, unknown_is_write=...) answered at {commit}, before its rule moved into the generated decision
# isMutating (dsl/decisions/mutability.logic). One line per call: (cmd, args, unknown_is_write, answer).
# The calls: every command the server or the plugin registers, with no arguments; every action-parameterised command with each read
# action, a write action, a read action in capitals and no action; the four switches (doctor fix, get_metrics reset, wait_until
# abort_on_fail, get_changes clear) with every kind of value; editor mutation_mode with and without enable; a few ordinary calls.
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_is_write_golden.py.
"""

_CS_CALL = re.compile(r'CommandRegistry\.(?:Register|RegisterAsync|RegisterAction)\(\s*"([^"]+)"')
# absent (None here), the two false literals, the true literals of the wire, and values only a caller of the function can pass
_SWITCH_VALUES = [None, "false", "true", "future", "", "False", "0", "1", False, True, 0, 1]
_SWITCHES = [("wait_until", "abort_on_fail"), ("get_changes", "clear"), ("doctor", "fix"), ("get_metrics", "reset")]
_ORDINARY = [("inspect", {"paths": "/Foo"}), ("set_property", {"path": "/Foo", "value": "1"}), ("run_playtest", {"script": "ASSERT_CONSOLE_CLEAN"}),
             ("batch", {"commands": "get_hierarchy"}), ("execute_code", {}), ("totally_unregistered_xyz", {}), ("nonexistent_cmd", {})]


def _plugin_commands() -> set[str]:
    """The commands the Unity plugin registers (CommandRegistry.Register*("name", ...) outside the tests)."""
    found: set[str] = set()
    for path in (_ROOT / "unity-plugin" / "Editor").rglob("*.cs"):
        if "Tests" not in path.parts:
            found.update(_CS_CALL.findall(path.read_text(encoding="utf-8")))
    return found


def is_write_calls() -> list[tuple[str, dict]]:
    """The (cmd, args) calls of the is_write golden, in a fixed order."""
    from unity_mcp.middleware_types import _KNOWN_SAFE_READS, ACTION_READS, READ_CMDS, WRITE_CMDS
    from unity_mcp.tools.tool_specs import _SPECS

    known = sorted(set(_SPECS) | _plugin_commands() | set(_KNOWN_SAFE_READS) | WRITE_CMDS | READ_CMDS | set(ACTION_READS))
    calls: list[tuple[str, dict]] = [(cmd, {}) for cmd in known]
    for cmd, reads in sorted(ACTION_READS.items()):
        actions = [*sorted(reads), min(reads).upper(), "zz_write"]
        calls += [(cmd, {"action": action}) for action in actions]
    for cmd, key in _SWITCHES:
        calls += [(cmd, {} if value is None else {key: value}) for value in _SWITCH_VALUES]
    for extra in [{}, {"enable": "true"}, {"enable": "false"}, {"enable": ""}, {"enable": False}, {"enable": None}]:
        calls.append(("editor", {"action": "mutation_mode", **extra}))
    return [*calls, *_ORDINARY]


def is_write_lines() -> list[str]:
    """One line per call and per unknown_is_write: the literal (cmd, args, unknown_is_write, answer) of the live is_write."""
    from unity_mcp.middleware_types import is_write

    return [repr((cmd, args, unknown, is_write(cmd, args, unknown_is_write=unknown)))
            for (cmd, args), unknown in itertools.product(is_write_calls(), (False, True))]


def main(argv: list[str]) -> None:
    if len(argv) != 2 or argv[0] != "is_write":
        sys.exit(__doc__)
    print(IS_WRITE_HEADER.format(commit=argv[1]) + "\n".join(is_write_lines()))


if __name__ == "__main__":
    main(sys.argv[1:])
