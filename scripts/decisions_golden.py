"""Print the lines of the golden files of the decisions (server/tests/golden/decisions/), the oracle of dsl/decisions (step 7.2, P15).

Each golden holds what the hand-written code answered before its rule moved into a generated decision: one Python literal per line
(a tuple of the call and the answer), so no expectation is read from the code under test and no JSON is authored. The golden is taken
once, at the commit before the seam changes, and is never regenerated to make a difference disappear: an intended change is an
INTENDED entry, with its reason, in the test of the family. This script reproduces the lines (run it at the commit named in the
header of the golden, to check the golden) and nothing else.

Usage (from the repository root; <commit> is the commit the file is taken at, written into its header):
  python scripts/decisions_golden.py is_write <commit>
  python scripts/decisions_golden.py verdict <commit>
  python scripts/decisions_golden.py guards <commit>
  python scripts/decisions_golden.py retry <commit>
"""
import asyncio
import contextlib
import itertools
import json
import os
import pathlib
import random
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

VERDICT_HEADER = """\
# What the hand-written verdict of the playtest answered at {commit}, before its rules moved into the generated decisions passes, outcome,
# fileState, suiteInner and suiteOuter (dsl/decisions/verdict.logic). A cell that says "raises X" is a call that raised X.
# ("report", raw, format, _is_playtest_pass(raw, format), _classify_outcome(raw, format), _parse_counts(raw), _classify_state(*counts, raw)):
#   the reports of the tests of the project (test_runtime, test_outcome_adapter, test_consecutive_write_guard), the headers
#   "PLAYTEST: p/t" in six spellings against thirteen neighbours and five counts, the JSON ledgers of every failed/steps/teardown
#   combination, receipts that are no ledger, counts beyond a million.
# ("suite", [(path, raw, ok), ...], format_layered_verdict(compute_suite_verdict(...))): one to three files and the suite rows
#   (<suite startup>, <suite cleanup>) that mark the outer verdict.
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_verdict_golden.py.
"""

GUARDS_HEADER = """\
# What the guards of the Python side answered at {commit}, before their rules moved into the generated decision guardVerdict (site py,
# dsl/decisions/guards.logic). One line per call: (site of the guard, the facts it was given, the answer: the text it returned or "raises
# <Name>: <text>"). The sites: Middleware.check_read_only and server._check_read_only (an endpoint in read-only mode, a call that is
# or is not a write), Middleware.check_play_mode_required (the play state known or not, playing or not, a runtime-only command or not),
# server._send_raw (the direct_only gate: every ToolSpec, reaching "No Unity connection" when the gate lets it through),
# tools/_common._guard_read_only (the file-writing tools), tools/batch._preprocess_stop_mode and _preprocess_continue_mode (a direct-only
# line in a batch).
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_guards_golden.py.
"""

RETRY_HEADER = """\
# What RetryPolicy.decide(error, attempt, session_deadline, cmd) answered at {commit}, before its order of rules moved into the generated
# decisions retryGate and retryReason (dsl/decisions/retry.logic). One line per call: (error, attempt, max_retries, deadline passed,
# reload window active, the probe says busy, the process is dead, then the answer (should_retry, delay in seconds, reason) and how
# many times the probe was asked (has_strong_busy_signal, is_process_dead)).
# The errors: one of each class the policy tells apart (SessionIdentityMismatch, CapacityBusyError with the default wait and with 7.5 s,
# DomainReloadError, ConnectionRefusedError) and seven that it does not (OSError, TimeoutError, ConnectionResetError, BrokenPipeError,
# IncompleteReadError, RuntimeError, UnicodeDecodeError); the attempts 0..4 of 3 retries, and 0 of 0.
# The delays are those of production (2 / 4 / 8 s backoff, 1 s for the first grace retry): the golden is taken outside tests/_fast_clock.py.
# Do not regenerate this file to remove a difference: an intended change is an INTENDED entry of test_retry_golden.py.
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


def _ledger(step_ok: bool, teardown_ok: bool = True, name: str = "Foo OK.playtest") -> str:
    """The canonical JSON receipt of one step, as the tests of the project write it."""
    return json.dumps({"schema_version": 1, "run_id": "r1", "passed": 1 if step_ok else 0, "failed": 0 if step_ok else 1, "duration_seconds": 0.1,
                       "steps": [{"index": 0, "type": "Assert", "ok": step_ok, "ms": 1.0, "source_file": name, "source_line": 1,
                                  "raw_passed": step_ok, "expected_fail": False}],
                       "outer": {"teardown_ok": teardown_ok, "scene_clean": True}, "text_report": "whatever"})


_STEP = {"index": 0, "type": "Assert", "ok": True, "ms": 1.0, "source_file": "f.playtest", "source_line": 1, "raw_passed": True, "expected_fail": False}
_HEADS = ["PLAYTEST: {p}/{t} (1.0s)", "PLAYTEST:{p}/{t}", "PLAYTEST: {p}/{t}", "PLAYTEST: {p} / {t} (1.0s)", "PLAYTEST:  {p}/{t} OK", "playtest: {p}/{t} (1.0s)"]
_AROUND = [("", ""), ("", "\n[1] LOG smoke"), ("", "\n[2] FAIL x"), ("", "\n[2] ERROR x"), ("", "\n[2] CONSOLE_ERR x"), ("", "\n[2] BLOCKED x"),
           ("", "\n[2] TIMEOUT x"), ("", "\n[2] ABORTED x"), ("", "\nNOT_ABORTED"), ("\u26a1 2 consecutive writes\n", ""), ('{"a": 1}\n', ""), ("\n", ""), (" ", "")]
# (raw, format) of the tests of the project that state a verdict: test_runtime.py, test_outcome_adapter.py, test_consecutive_write_guard.py
_TESTED = [
    ("", None), ("PLAYTEST: 3/3 (1.0s) OK", None), ("PLAYTEST: 1/3 (1.0s)\n[2] FAIL", None), ("PLAYTEST: 2/2 (1.0s)\n[1] CONSOLE_ERR msg", None),
    ("PLAYTEST: 0/0 (0.0s) OK", None), (_ledger(True), "json"), (_ledger(False), "json"), (_ledger(True, False), "json"),
    (json.dumps({"schema_version": 1, "passed": 1, "failed": 1, "outer": {"teardown_ok": True, "scene_clean": True}, "steps": [_STEP]}), "json"),
    (json.dumps({"passed": 0, "failed": 0, "text_report": "", "outer": {"teardown_ok": True}, "steps": []}), "json"),
    (json.dumps({"passed": 0, "failed": 0, "outer": {"teardown_ok": True}}), "json"),
    ("PLAYTEST: 1/1 (1.0s)\n[2] ABORTED: global timeout 1s", "text"), ('{"source_file": "weird.playtest"}\nPLAYTEST: 2/2 (1.0s) OK', "text"),
    ("\u26a1 3 consecutive writes without reading. Consider verifying state.\nPLAYTEST: 3/3 passed", None), ("PLAYTEST: 2/2 passed", None),
    ("\u26a1 warning\nPLAYTEST: 2/2\nFAIL: assertion failed", None), ("some other output", None), ("\u26a1 warning\nPLAYTEST: 2/3 failed", None),
    ("PLAYTEST: 3/3 (1.0s) OK", "text"), ("PLAYTEST: 1/3 (1.0s)\n[2] FAIL: HP", "text"), ("PLAYTEST: 0/0 ERROR: timeout", "text"), ("", "text"),
    ("PLAYTEST: 2/2 (1.0s)\n[1] CONSOLE_ERR msg", "text"), (_ledger(True, name="f.playtest"), "json"), ("not json at all", "json"),
    ("PLAYTEST: 1/2 (1.0s) OK\n[2] FAIL", "text"),
]
# receipts that are no ledger, and counts beyond the million of the fact domain
_ODD = [("not json", "json"), ("[]", "json"), ("{}", "json"), ('{"outer": 1}', "json"), ("null", "json"), ("PLAYTEST: 1/1", "json"), (_ledger(True), "text"),
        (_ledger(True), None), ("PLAYTEST: 1/1 (1s)", None), (None, "text"), (None, None), ("   ", "text"), ("{", None), ('{"a": 1}', None), ("{}", None),
        ('{"outer": {"teardown_ok": true}, "failed": 0, "steps": [1]}', "json"), ('{"outer": {"teardown_ok": true}, "failed": 0, "steps": "abc"}', "json"),
        ('{"outer": {"teardown_ok": true}, "failed": 0, "steps": 5}', "json"), ('{"outer": {"teardown_ok": true}, "failed": 0.5, "steps": [{"ok": true}]}', "json"),
        ('{"outer": {"teardown_ok": true}, "failed": false, "steps": [{"ok": true}]}', "json"), ('{"outer": {"teardown_ok": true}, "failed": "0", "steps": [{"ok": true}]}', "json"),
        ('{"outer": {"teardown_ok": true}, "failed": 12345678901234567890, "steps": [{"ok": true}]}', "json"),
        ('{"outer": {"teardown_ok": true}, "failed": 0, "steps": [{"ok": 1}]}', "json"), ('{"outer": {"teardown_ok": true}, "failed": 1, "steps": [{"ok": 0}]}', "json"),
        ('{"outer": {"teardown_ok": true}, "failed": 0, "steps": [{"ok": true}, {}]}', "json"),
        ("PLAYTEST: 1000000/1000000 (1s)", "text"), ("PLAYTEST: 1000001/1000001 (1s)", "text"), ("PLAYTEST: 5/1000001 (1s)", "text"),
        ("PLAYTEST: 99999999999/99999999999 (1s)\n[1] ERROR x", "text"), ("PLAYTEST: 1000001/5 (1s)", "text")]


def verdict_reports() -> list[tuple[str | None, str | None]]:
    """The (raw, format) reports of the verdict golden, in a fixed order, without repeats."""
    reports = [*_TESTED, *_ODD]
    for head, (passed, total), (before, after) in itertools.product(_HEADS, [(0, 0), (1, 1), (3, 3), (1, 3), (4, 3)], _AROUND):
        reports.append((before + head.format(p=passed, t=total) + after, "text"))
        if (before, after) == ("", ""):
            reports.append((head.format(p=passed, t=total), None))
    for failed, steps, teardown, extra in itertools.product([None, 0, 1, -1], [None, [], [True], [True, False], [False]], [True, False, None], [{}, {"text_report": "ABORTED"}]):
        receipt = {"schema_version": 1, **({} if failed is None else {"failed": failed}), **({} if steps is None else {"steps": [{**_STEP, "ok": ok} for ok in steps]}),
                   **({} if teardown is None else {"outer": {"teardown_ok": teardown}}), **extra}
        reports.append((json.dumps(receipt), "json"))
        if not extra and teardown:
            reports.append((json.dumps(receipt), None))
    return list(dict.fromkeys(reports))


def suite_scenarios() -> list[list[tuple[str, str, bool]]]:
    """The (path, raw, ok) rows of the suites of the verdict golden: every one and two files, 60 seeded triples, each with no suite row,
    a startup row that passed and one that failed."""
    files = ["PLAYTEST: 2/2 (1.0s)", "PLAYTEST: 1/2 (1.0s)\n[2] FAIL x", "PLAYTEST: 1/1 (1.0s)\n[2] ABORTED x", "PLAYTEST: 0/0 ERROR: boom", "",
             "PLAYTEST: 0/1 ERROR: startup failed", "PLAYTEST: 0/0 TIMED_OUT", "garbage"]
    sets = [[raw] for raw in files] + [list(pair) for pair in itertools.product(files, repeat=2)]
    rng = random.Random("suite")
    sets += [[rng.choice(files) for _ in range(3)] for _ in range(60)]
    scenarios = []
    for raws in sets:
        rows = [(f"Assets/T/{index}.playtest", raw, raw == "PLAYTEST: 2/2 (1.0s)") for index, raw in enumerate(raws)]
        scenarios += [rows, [("<suite startup>", "", True), *rows], [("<suite cleanup>", "err", False), *rows]]
    return scenarios


def answer(function, *args):
    """The answer of a hand-written function, or "raises <name>": a crash is part of what it did."""
    try:
        return function(*args)
    except Exception as error:
        return f"raises {type(error).__name__}"


def verdict_lines() -> list[str]:
    """The literal lines of the live verdict functions: one per report and one per suite."""
    from unity_mcp.suite_verdict import _classify_state, _parse_counts, compute_suite_verdict, format_layered_verdict
    from unity_mcp.tools.runtime import _classify_outcome, _is_playtest_pass

    lines = []
    for raw, fmt in verdict_reports():
        counts = answer(_parse_counts, raw)
        state = answer(_classify_state, *counts, raw) if isinstance(counts, tuple) else counts
        lines.append(repr(("report", raw, fmt, answer(_is_playtest_pass, raw, fmt), answer(_classify_outcome, raw, fmt), counts, state)))
    lines += [repr(("suite", rows, format_layered_verdict(compute_suite_verdict([(path, raw, 0.0, ok) for path, raw, ok in rows]))))
              for rows in suite_scenarios()]
    return lines


# the calls that tell a write from a read to the read-only guards: reads, writes, a command nobody knows, the switches, an action either way
_GUARDED_CALLS = [("get_hierarchy", {}), ("inspect", {"paths": "/a"}), ("get_status", {}), ("set_property", {"path": "/a", "value": "1"}), ("delete_object", {"path": "/a"}),
                  ("create_object", {}), ("batch", {"commands": "get_hierarchy"}), ("execute_code", {"code": "1"}), ("run_playtest", {"script": "LOG x"}),
                  ("editor", {"action": "state"}), ("editor", {"action": "play"}), ("editor", {"action": "mutation_mode"}),
                  ("editor", {"action": "mutation_mode", "enable": "true"}), ("scene", {"action": "list"}), ("scene", {"action": "open", "path": "a.unity"}),
                  ("navmesh", {"action": "status"}), ("navmesh", {"action": "bake"}), ("wait_until", {}), ("wait_until", {"abort_on_fail": "true"}),
                  ("get_changes", {}), ("get_changes", {"clear": "false"}), ("doctor", {}), ("doctor", {"fix": True}), ("get_metrics", {}),
                  ("get_metrics", {"reset": "true"}), ("totally_unregistered_xyz", {}), ("source_patch_write", {}), ("screenshot", {})]
_BATCHES = ["", "get_hierarchy\nset_property path=/a value=1", "discover_tools", "console_mark text=x", "watch name=a", "run_tests mode=edit",
            "# a comment\n\nget_hierarchy\nconsole_mark text=x\nget_hierarchy", "screenshot\nbuild", "get_hierarchy depth=1\nset_active path=/a active=false"]


@contextlib.contextmanager
def _env(name: str, value: str | None):
    """The environment variable `name` set to `value` (unset when None) for the block."""
    before = os.environ.get(name)
    if value is None:
        os.environ.pop(name, None)
    else:
        os.environ[name] = value
    try:
        yield
    finally:
        if before is None:
            os.environ.pop(name, None)
        else:
            os.environ[name] = before


def detailed(function, *args):
    """answer(), with the text of the exception: "raises ToolError: <text>"."""
    try:
        return function(*args)
    except Exception as error:
        return f"raises {type(error).__name__}: {error}"


def guard_keys() -> list[tuple]:
    """The calls of the guards golden, in a fixed order: (site, the facts it is given...); the answer comes from guard_answer."""
    from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS
    from unity_mcp.tools.tool_specs import _SPECS

    keys: list[tuple] = [("Middleware.check_read_only", read_only, cmd, args) for read_only in (False, True) for cmd, args in _GUARDED_CALLS]
    runtime = [*sorted(_RUNTIME_ONLY_CMDS), "get_component", "run_playtest", "watch_remove", "totally_unregistered_xyz"]
    keys += [("Middleware.check_play_mode_required", known, playing, cmd) for known, playing in itertools.product((False, True), repeat=2) for cmd in runtime]
    for value in (None, "0", "1", "true"):
        keys += [("server._check_read_only", value, cmd, args) for cmd, args in _GUARDED_CALLS]
        keys += [("tools._common._guard_read_only", value, tool) for tool in ("save_skill", "save_template", "save_session", "get_metrics", "doctor")]
    keys += [("server._send_raw", cmd) for cmd in sorted(_SPECS)]
    return keys + [(site, commands) for commands in _BATCHES for site in ("batch._preprocess_stop_mode", "batch._preprocess_continue_mode")]


def guard_answer(key: tuple):
    """What the live Python guard of `key` answers: the text it returns, or "raises <Name>: <text>"."""
    from unittest.mock import Mock, patch

    from unity_mcp import server
    from unity_mcp.middleware import Middleware
    from unity_mcp.tools import batch
    from unity_mcp.tools._common import _guard_read_only

    site, *facts = key
    if site == "Middleware.check_read_only":
        middleware = Middleware()
        middleware.is_read_only = facts[0]
        return detailed(middleware.check_read_only, *facts[1:])
    if site == "Middleware.check_play_mode_required":
        middleware = Middleware()
        middleware._play_state_known, middleware.is_playing = facts[0], facts[1]
        return detailed(middleware.check_play_mode_required, facts[2])
    if site == "server._check_read_only" or site == "tools._common._guard_read_only":
        with _env("UNITY_MCP_READ_ONLY", facts[0]):
            return detailed(server._check_read_only if site == "server._check_read_only" else _guard_read_only, *facts[1:])
    if site == "server._send_raw":
        with patch("unity_mcp.server.slot", Mock(bridge=None)), _env("UNITY_MCP_READ_ONLY", None):
            return detailed(lambda: asyncio.run(server._send_raw(facts[0], {})))
    return detailed(getattr(batch, "_preprocess_" + site.rsplit("_preprocess_", 1)[1]), facts[0])


def guards_lines() -> list[str]:
    """One literal line per call of the Python guards: the key, then the answer of the live guard."""
    return [repr((*key, guard_answer(key))) for key in guard_keys()]


_ATTEMPTS = [(0, 3), (1, 3), (2, 3), (3, 3), (4, 3), (0, 0)]  # (attempt, max_retries)
_ERRORS = ["identity", "capacity", "capacity-7.5", "reload", "refused", "OSError", "TimeoutError", "ConnectionResetError", "BrokenPipeError",
           "IncompleteReadError", "RuntimeError", "UnicodeDecodeError"]


def _error(label: str) -> Exception:
    from unity_mcp.bridge_socket import DomainReloadError
    from unity_mcp.errors import CapacityBusyError, SessionIdentityMismatch

    return {"identity": lambda: SessionIdentityMismatch("x"), "capacity": lambda: CapacityBusyError("x"),
            "capacity-7.5": lambda: CapacityBusyError("x", retry_after_seconds=7.5), "reload": lambda: DomainReloadError("x"),
            "refused": lambda: ConnectionRefusedError("x"), "OSError": lambda: OSError("x"), "TimeoutError": lambda: TimeoutError(),
            "ConnectionResetError": lambda: ConnectionResetError("x"), "BrokenPipeError": lambda: BrokenPipeError("x"),
            "IncompleteReadError": lambda: asyncio.IncompleteReadError(b"", 4), "RuntimeError": lambda: RuntimeError("x"),
            "UnicodeDecodeError": lambda: UnicodeDecodeError("utf-8", b"\xff", 0, 1, "bad")}[label]()


def retry_keys() -> list[tuple]:
    """(error label, attempt, max_retries, deadline passed, reload active, probe busy, process dead), in a fixed order."""
    return [(label, attempt, maximum, *flags) for label in _ERRORS for attempt, maximum in _ATTEMPTS for flags in itertools.product((False, True), repeat=4)]


def retry_answer(key: tuple) -> tuple:
    """(should_retry, delay, reason, busy reads, dead reads) of a RetryPolicy built for `key`, with a probe that counts its reads."""
    import time
    from types import SimpleNamespace

    from unity_mcp.bridge_reload_state import DomainReloadTracker
    from unity_mcp.bridge_retry import RetryPolicy

    label, attempt, maximum, deadline_passed, reload_active, busy, dead = key
    reads = SimpleNamespace(busy=0, dead=0)

    def has_strong_busy_signal() -> bool:
        reads.busy += 1
        return busy

    def is_process_dead() -> bool:
        reads.dead += 1
        return dead

    reload = DomainReloadTracker()
    if reload_active:
        reload.mark()
    policy = RetryPolicy(probe=SimpleNamespace(has_strong_busy_signal=has_strong_busy_signal, is_process_dead=is_process_dead), reload=reload,
                         is_retry_safe=lambda cmd: True, max_retries=maximum)
    deadline = time.monotonic() - 1 if deadline_passed else time.monotonic() + 1000
    return (*policy.decide(_error(label), attempt, deadline, cmd="x"), reads.busy, reads.dead)


def retry_lines() -> list[str]:
    """One literal line per call of the live RetryPolicy.decide: the key, then the answer."""
    return [repr((*key, *retry_answer(key))) for key in retry_keys()]


_FAMILIES = {"is_write": (IS_WRITE_HEADER, is_write_lines), "verdict": (VERDICT_HEADER, verdict_lines), "guards": (GUARDS_HEADER, guards_lines),
             "retry": (RETRY_HEADER, retry_lines)}


def main(argv: list[str]) -> None:
    if len(argv) != 2 or argv[0] not in _FAMILIES:
        sys.exit(__doc__)
    header, lines = _FAMILIES[argv[0]]
    print(header.format(commit=argv[1]) + "\n".join(lines()))


if __name__ == "__main__":
    main(sys.argv[1:])
