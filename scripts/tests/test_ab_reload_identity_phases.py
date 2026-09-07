"""Offline tests for gauntlet.ab_reload_identity.run_t3 (N3 T3 slices 1+2:
reload + new-code proof, cross-project identity rejection). No Unity, no
network -- fake call/sync_unity/bridge_factory seams only.

Covers: slice-1 happy path; old-nonce presented as new -> RED; a mutated B
sentinel -> RED; every B nonce read goes through a fresh seam invocation (no
cached value); slice-2 rejects a foreign project identity on A's port before
any counter mutation and accepts the correct one (positive control); the
whole run visits setup -> action -> inspection in that order.
See Plans/N3-T3-T4-live-reload-identity.md Task 2 Part 1.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

import gauntlet.ab_reload_identity as t3  # noqa: E402

from unity_mcp.errors import SessionIdentityMismatch  # noqa: E402

PORT_A, PORT_B = 9620, 9630
PROJECT_A, PROJECT_B = "/tmp/worker-a", "/tmp/worker-b"


def diagnose_text(mvid: str) -> str:
    return f"mvid={mvid}\ncompile=idle\n"


class ScriptedCall:
    """Fake call(port, cmd, args) seam (mirrors run_unity_tests.call): pops
    one scripted response per (port, cmd, code) call, in call order. Records
    every invocation so a test can prove no cached/reused value was used."""

    def __init__(self, script: dict[tuple, list[str]], log: list[tuple] | None = None) -> None:
        self._script = {key: list(values) for key, values in script.items()}
        self.log = log if log is not None else []

    async def __call__(self, port: int, cmd: str, args: dict) -> str:
        key = (port, cmd, args.get("code", ""))
        self.log.append(("call", *key))
        queue = self._script.get(key)
        if not queue:
            raise AssertionError(f"no scripted response left for {key}")
        return queue.pop(0)


class ScriptedSync:
    def __init__(self, verdict: str = "sync clean", log: list[tuple] | None = None) -> None:
        self.verdict = verdict
        self.log = log if log is not None else []

    async def __call__(self, port: int, project_path: str) -> str:
        self.log.append(("sync", port, project_path))
        return self.verdict


class FakeBridge:
    def __init__(self, port: int, expected_project_path: str, real_project_path: str) -> None:
        self._expected = expected_project_path
        self._real = real_project_path
        self.connected = False

    async def connect(self) -> None:
        if self._expected != self._real:
            raise SessionIdentityMismatch(
                f"Reconnect rejected: expected project {self._real!r}, received {self._expected!r}"
            )
        self.connected = True

    async def close(self) -> None:
        self.connected = False


def make_bridge_factory(real_project_by_port: dict[int, str], log: list[tuple] | None = None):
    log = log if log is not None else []

    def factory(port: int, expected_project_path: str) -> FakeBridge:
        log.append(("bridge", port, expected_project_path))
        return FakeBridge(port, expected_project_path, real_project_by_port[port])

    factory.log = log
    return factory


def always_accept_bridge_factory():
    def factory(port: int, expected_project_path: str) -> FakeBridge:
        return FakeBridge(port, expected_project_path, expected_project_path)

    return factory


def always_reject_bridge_factory():
    def factory(port: int, expected_project_path: str) -> FakeBridge:
        return FakeBridge(port, expected_project_path, expected_project_path + "-DIFFERENT")

    return factory


class _RefusedBridge:
    """A port with nothing listening (or a non-identity failure) -- not the
    same thing as Unity correctly rejecting a foreign project identity."""

    async def connect(self) -> None:
        raise ConnectionRefusedError("Connection refused")

    async def close(self) -> None:  # pragma: no cover - never reached
        pass


def connection_refused_bridge_factory():
    def factory(port: int, expected_project_path: str) -> _RefusedBridge:
        return _RefusedBridge()

    return factory


def make_config(new_nonce_a: str = "A2-new", nonce_b: str = "B-fixed") -> t3.T3Config:
    return t3.T3Config(
        port_a=PORT_A, port_b=PORT_B, project_a=PROJECT_A, project_b=PROJECT_B,
        new_nonce_a=new_nonce_a, nonce_b=nonce_b,
    )


def make_happy_script(
    old_nonce_a: str = "A1-old", new_nonce_a: str = "A2-new", nonce_b: str = "B-fixed",
    mvid_a_before: str = "MVID-A1", mvid_a_after: str = "MVID-A2", mvid_b: str = "MVID-B1",
    counter: str = "0", port_a_before: int = PORT_A, port_a_after: int = PORT_A,
) -> dict[tuple, list[str]]:
    return {
        (PORT_A, "execute_code", t3.NONCE_READ_CODE): [old_nonce_a, new_nonce_a],
        (PORT_A, "diagnose", ""): [diagnose_text(mvid_a_before), diagnose_text(mvid_a_after)],
        (PORT_B, "execute_code", t3.NONCE_READ_CODE): [nonce_b, nonce_b],
        (PORT_B, "diagnose", ""): [diagnose_text(mvid_b), diagnose_text(mvid_b)],
        (PORT_A, "execute_code", t3.COUNTER_READ_CODE): [counter, counter],
        (PORT_A, "get_status", ""): [f"port={port_a_before}\n", f"port={port_a_after}\n"],
    }


def make_happy_seams(script: dict[tuple, list[str]] | None = None, log: list[tuple] | None = None) -> t3.T3Seams:
    log = log if log is not None else []
    call = ScriptedCall(script or make_happy_script(), log=log)
    sync = ScriptedSync(log=log)
    bridge_factory = make_bridge_factory({PORT_A: PROJECT_A, PORT_B: PROJECT_B}, log=log)
    return t3.T3Seams(call=call, sync_unity=sync, bridge_factory=bridge_factory)


# --- slice 1: reload + new-code proof ---------------------------------------


@pytest.mark.asyncio
async def test_run_t3_slice1_happy_path() -> None:
    result = await t3.run_t3(make_config(), make_happy_seams())
    reload = result["t3_identity_reload"]
    assert reload["old_nonce"] == "A1-old"
    assert reload["new_nonce"] == "A2-new"
    assert reload["mvid_before"] == "MVID-A1"
    assert reload["mvid_after"] == "MVID-A2"
    assert reload["sync_verdict"] == "sync clean"
    assert reload["port_before"] == str(PORT_A)
    assert reload["port_after"] == str(PORT_A)
    assert reload["sentinel_nonce_before"] == "B-fixed"
    assert reload["sentinel_nonce_after"] == "B-fixed"


@pytest.mark.asyncio
async def test_run_t3_raises_when_a_port_changes_after_sync() -> None:
    script = make_happy_script(port_a_before=PORT_A, port_a_after=PORT_A + 1)
    with pytest.raises(t3.ABReloadIdentityError, match="port changed"):
        await t3.run_t3(make_config(), make_happy_seams(script))


@pytest.mark.asyncio
async def test_run_t3_raises_when_a_presents_old_nonce_as_new() -> None:
    script = make_happy_script(old_nonce_a="A1-old", new_nonce_a="A1-old")
    with pytest.raises(t3.ABReloadIdentityError, match="old code presented as new"):
        await t3.run_t3(make_config(), make_happy_seams(script))


@pytest.mark.asyncio
async def test_run_t3_raises_when_mvid_does_not_change() -> None:
    script = make_happy_script(mvid_a_before="MVID-A1", mvid_a_after="MVID-A1")
    with pytest.raises(t3.ABReloadIdentityError, match="MVID unchanged"):
        await t3.run_t3(make_config(), make_happy_seams(script))


@pytest.mark.asyncio
async def test_run_t3_raises_when_b_sentinel_nonce_mutated() -> None:
    script = make_happy_script()
    script[(PORT_B, "execute_code", t3.NONCE_READ_CODE)] = ["B-fixed", "B-MUTATED"]
    with pytest.raises(t3.ABReloadIdentityError, match="B sentinel changed"):
        await t3.run_t3(make_config(), make_happy_seams(script))


@pytest.mark.asyncio
async def test_run_t3_raises_when_b_sentinel_mvid_mutated() -> None:
    script = make_happy_script()
    script[(PORT_B, "diagnose", "")] = [diagnose_text("MVID-B1"), diagnose_text("MVID-B2")]
    with pytest.raises(t3.ABReloadIdentityError, match="B sentinel changed"):
        await t3.run_t3(make_config(), make_happy_seams(script))


@pytest.mark.asyncio
async def test_run_t3_reads_b_nonce_via_two_separate_fresh_calls() -> None:
    log: list[tuple] = []
    await t3.run_t3(make_config(), make_happy_seams(log=log))
    b_nonce_reads = [entry for entry in log if entry == ("call", PORT_B, "execute_code", t3.NONCE_READ_CODE)]
    assert len(b_nonce_reads) == 2, "B's sentinel must be read fresh before AND after, never cached"


# --- slice 2: cross-project identity rejection -------------------------------


@pytest.mark.asyncio
async def test_run_t3_identity_rejection_recorded_and_a_untouched() -> None:
    result = await t3.run_t3(make_config(), make_happy_seams())
    cross = result["t3_cross_identity"]
    assert cross["reject_foreign"]["rejected"] is True
    assert cross["reject_foreign"]["error_type"] == "SessionIdentityMismatch"
    assert cross["accept_correct"]["rejected"] is False
    assert cross["counter_before"] == cross["counter_after"], "A's counter must be unchanged by identity probing"


@pytest.mark.asyncio
async def test_run_t3_raises_when_foreign_identity_not_rejected() -> None:
    seams = make_happy_seams()
    seams.bridge_factory = always_accept_bridge_factory()
    with pytest.raises(t3.ABReloadIdentityError, match="was not rejected"):
        await t3.run_t3(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t3_raises_when_correct_identity_wrongly_rejected() -> None:
    seams = make_happy_seams()
    seams.bridge_factory = always_reject_bridge_factory()
    with pytest.raises(t3.ABReloadIdentityError, match="wrongly rejected"):
        await t3.run_t3(make_config(), seams)


@pytest.mark.asyncio
async def test_run_t3_raises_when_rejection_is_connection_refused() -> None:
    """A dead port raising ConnectionRefusedError is not the same evidence
    as Unity actually rejecting a foreign project identity -- it must not
    be misread as a passing positive control (Part 0 Task 2 review #1)."""
    seams = make_happy_seams()
    seams.bridge_factory = connection_refused_bridge_factory()
    with pytest.raises(t3.ABReloadIdentityError, match="not an identity error"):
        await t3.run_t3(make_config(), seams)


@pytest.mark.asyncio
async def test_probe_identity_positive_control_no_raise() -> None:
    factory = always_accept_bridge_factory()
    outcome = await t3.probe_identity(factory, PORT_A, PROJECT_A)
    assert outcome == {"rejected": False, "error_type": None, "error": None}


# --- phase order: setup -> action -> inspection ------------------------------


@pytest.mark.asyncio
async def test_run_t3_visits_setup_action_inspection_in_order() -> None:
    """Part 0 Task 2 review #4: assert the specific setup call tuples (not a
    bare count) so an unexpected extra/missing/reordered read is visible."""
    log: list[tuple] = []
    await t3.run_t3(make_config(), make_happy_seams(log=log))
    sync_indices = [i for i, entry in enumerate(log) if entry[0] == "sync"]
    assert len(sync_indices) == 1, "sync_unity (the action) must run exactly once"
    sync_index = sync_indices[0]
    setup = log[:sync_index]
    inspection = log[sync_index + 1:]
    assert setup == [
        ("call", PORT_A, "execute_code", t3.NONCE_READ_CODE),
        ("call", PORT_A, "diagnose", ""),
        ("call", PORT_B, "execute_code", t3.NONCE_READ_CODE),
        ("call", PORT_B, "diagnose", ""),
        ("call", PORT_A, "get_status", ""),
    ], "setup reads exactly: A nonce, A mvid, B nonce, B mvid, A's pre-action port"
    assert inspection[0] == ("call", PORT_A, "get_status", ""), \
        "A's port is re-read immediately after the action, before anything else"
    assert any(entry[0] == "bridge" for entry in inspection), "inspection reads and probes follow the action"


# --- standalone read/parse helpers -------------------------------------------


@pytest.mark.asyncio
async def test_read_nonce_strips_whitespace() -> None:
    async def call(port: int, cmd: str, args: dict) -> str:
        return "  NONCE-A  \n"

    assert await t3.read_nonce(call, PORT_A) == "NONCE-A"


@pytest.mark.asyncio
async def test_read_counter_parses_int() -> None:
    async def call(port: int, cmd: str, args: dict) -> str:
        return "3\n"

    assert await t3.read_counter(call, PORT_A) == 3


@pytest.mark.asyncio
async def test_increment_counter_uses_increment_code() -> None:
    seen = {}

    async def call(port: int, cmd: str, args: dict) -> str:
        seen["code"] = args["code"]
        return "1"

    assert await t3.increment_counter(call, PORT_A) == 1
    assert seen["code"] == t3.COUNTER_INCREMENT_CODE


def test_parse_mvid_extracts_field() -> None:
    assert t3.parse_mvid("mvid=ABCD\ncompile=idle\n") == "ABCD"


def test_parse_mvid_raises_when_absent() -> None:
    with pytest.raises(t3.ABReloadIdentityError, match="mvid="):
        t3.parse_mvid("compile=idle\n")


def test_parse_main_mvid_does_not_match_plain_mvid_line() -> None:
    assert t3.parse_main_mvid("mvid=ABCD\nmain_mvid=EFGH\n") == "EFGH"


@pytest.mark.asyncio
async def test_read_identity_bundles_project_path_port_and_mvids() -> None:
    async def call(port: int, cmd: str, args: dict) -> str:
        if cmd == "editor":
            return PROJECT_A
        if cmd == "get_status":
            return f"port={port}\nscene=Foo\n"
        if cmd == "diagnose":
            return "mvid=M1\nmain_mvid=MM1\n"
        raise AssertionError(cmd)

    identity = await t3.read_identity(call, PORT_A)
    assert identity == {
        "project_path": PROJECT_A, "port": str(PORT_A), "mvid": "M1", "main_mvid": "MM1",
    }
