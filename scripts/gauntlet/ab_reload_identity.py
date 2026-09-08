"""N3 T3 slices 1+2 phase logic: reload + new-code proof, cross-project
identity rejection. Pure functions over injected call/sync/bridge-factory
seams -- no Unity lifecycle, no process management (that lives in
scripts/run_ab_reload_identity.py). See Plans/N3-T3-T4-live-reload-identity.md.

`call` mirrors run_unity_tests.call(port, command, args) -> str: one fresh
TCP connection per invocation, no cache, no persistent bridge. Every read in
this module goes through that seam, so a B sentinel read is guaranteed to hit
the actual wire each time it is called.
"""

import contextlib
from collections.abc import Awaitable, Callable
from dataclasses import dataclass
from typing import Protocol

CallFn = Callable[[int, str, dict], Awaitable[str]]

_FQN = "UnityMCP.Worker.ABReloadHarness.AbReloadNonce"
NONCE_READ_CODE = f"return {_FQN}.Nonce;"
COUNTER_READ_CODE = f"return {_FQN}.Counter.ToString();"
COUNTER_INCREMENT_CODE = f"{_FQN}.Counter++; return {_FQN}.Counter.ToString();"


class ABReloadIdentityError(RuntimeError):
    """Oracle failure: an observed effect does not match the required
    identity/reload/no-resend contract."""


# --- fresh-wire reads (new-code execution proof, not stamp/source) ---------


async def read_nonce(call: CallFn, port: int) -> str:
    """Execute the fixture's changed C# method and return its fresh value --
    the only accepted new-code proof. Source text, a file stamp, the TCP
    socket, or a registry entry are never substitutes for this call."""
    raw = await call(port, "execute_code", {"code": NONCE_READ_CODE})
    return raw.strip()


async def read_counter(call: CallFn, port: int) -> int:
    raw = await call(port, "execute_code", {"code": COUNTER_READ_CODE})
    return int(raw.strip())


async def increment_counter(call: CallFn, port: int) -> int:
    raw = await call(port, "execute_code", {"code": COUNTER_INCREMENT_CODE})
    return int(raw.strip())


def _parse_diagnose_field(text: str, key: str) -> str:
    prefix = f"{key}="
    for line in text.splitlines():
        if line.startswith(prefix):
            return line[len(prefix):].strip()
    raise ABReloadIdentityError(f"response has no {prefix!r} line: {text!r}")


def parse_mvid(diagnose_text: str) -> str:
    """The domain-reload identity field diagnose reports as 'mvid=' --
    changes whenever the project's assemblies are actually recompiled and
    reloaded (get_compile_errors alone is not proof of a fresh DLL)."""
    return _parse_diagnose_field(diagnose_text, "mvid")


def parse_main_mvid(diagnose_text: str) -> str:
    """F3/F5 main-asmdef MVID field ('main_mvid=') -- evidence, not proof."""
    return _parse_diagnose_field(diagnose_text, "main_mvid")


async def read_mvid(call: CallFn, port: int) -> str:
    return parse_mvid(await call(port, "diagnose", {}))


async def read_port(call: CallFn, port: int) -> str:
    """A's self-advertised port (get_status 'port=' field), fresh wire read
    -- used to prove a reload never silently became a restart (a new Unity
    process rebinding under the same launcher would still be caught if it
    advertised a different port)."""
    return _parse_diagnose_field(await call(port, "get_status", {}), "port")


async def read_identity(call: CallFn, port: int) -> dict[str, str]:
    """Evidence bundle (not proof of new-code execution): project path,
    advertised port, and both diagnose MVID fields, each a fresh wire read.
    The wire protocol has no pid= field (get_status does not report one);
    the harness's own launched-process PID is merged in by the caller."""
    project_path = (await call(port, "editor", {"action": "project_path"})).strip()
    status_text = await call(port, "get_status", {})
    diagnose_text = await call(port, "diagnose", {})
    return {
        "project_path": project_path,
        "port": _parse_diagnose_field(status_text, "port"),
        "mvid": parse_mvid(diagnose_text),
        "main_mvid": parse_main_mvid(diagnose_text),
    }


# --- oracle functions (fail-closed, precise messages) -----------------------


def check_sentinel_unchanged(before: str, after: str) -> None:
    """T3 sentinel oracle: B's nonce/MVID read over a fresh wire must equal
    its pre-fault value. A fault confined to A must never mutate B."""
    if after != before:
        raise ABReloadIdentityError(
            f"B sentinel changed: expected {before!r}, observed {after!r}"
        )


def check_new_code_executed(expected_nonce: str, observed_nonce: str, old_nonce: str) -> None:
    """T3 new-code-execution oracle. `observed_nonce` must be the freshly
    installed value, never the pre-reload one presented as new."""
    if observed_nonce == old_nonce and old_nonce != expected_nonce:
        raise ABReloadIdentityError(
            f"old code presented as new: observed {observed_nonce!r} "
            f"matches pre-reload nonce {old_nonce!r}"
        )
    if observed_nonce != expected_nonce:
        raise ABReloadIdentityError(
            f"new code not executed: expected {expected_nonce!r}, observed {observed_nonce!r}"
        )


def check_no_resend(counter_before: int, counter_after: int, sent_count: int) -> None:
    """T3 lost-ACK / probe-safety oracle: the counter must have advanced by
    exactly `sent_count` -- no auto-resend duplicate, no side effect from a
    probe that sent zero effective writes."""
    delta = counter_after - counter_before
    if delta != sent_count:
        raise ABReloadIdentityError(
            f"auto-resend detected: counter advanced by {delta}, expected exactly {sent_count}"
        )


# --- cross-project identity rejection (slice 2) -----------------------------


class BridgeLike(Protocol):
    async def connect(self) -> None: ...
    async def close(self) -> None: ...


BridgeFactory = Callable[[int, str], BridgeLike]

# The only two exception type names that mean "Unity rejected a foreign
# project identity" (bridge.py: _CandidateIdentityError on initial connect,
# SessionIdentityMismatch on a reconnect against an already-captured
# identity). Any other ConnectionError (e.g. ConnectionRefusedError -- the
# port is simply down) must not be misread as a passing identity check.
_IDENTITY_ERROR_TYPES = frozenset({"_CandidateIdentityError", "SessionIdentityMismatch"})


async def probe_identity(bridge_factory: BridgeFactory, port: int, expected_project_path: str) -> dict[str, object]:
    """Attempt a bridge connect pinned to `expected_project_path` against
    `port`. A mismatch must be rejected during connect() -- before any
    mutating command the caller issues reaches the peer (MCP-SESS-024)."""
    bridge = bridge_factory(port, expected_project_path)
    try:
        await bridge.connect()
    except ConnectionError as exc:
        return {"rejected": True, "error_type": type(exc).__name__, "error": str(exc)}
    finally:
        with contextlib.suppress(Exception):
            await bridge.close()
    return {"rejected": False, "error_type": None, "error": None}


# --- slice 1+2 phase runner --------------------------------------------------


@dataclass
class T3Config:
    port_a: int
    port_b: int
    project_a: str
    project_b: str
    new_nonce_a: str  # written to disk before run_t3(); the recompile's changed C# value
    nonce_b: str       # B's fixed sentinel, never touched by A's fault


@dataclass
class T3Seams:
    call: CallFn
    sync_unity: Callable[[int, str], Awaitable[str]]  # (port, project_path) -> verdict string
    bridge_factory: BridgeFactory


async def run_t3(config: T3Config, seams: T3Seams) -> dict[str, object]:
    """Setup -> action -> inspection, in that order.

    Setup: baseline nonce/MVID reads on A and B.
    Action: recompile A via the injected sync_unity seam (production
    sync_unity in the live wiring).
    Inspection: A's advertised port is unchanged (a reload must never
    become a restart), A executes the new code (never the old code
    presented as new), B's sentinel is unchanged over a fresh wire, and a
    foreign project identity on A's port is rejected by an actual identity
    error -- not merely any ConnectionError, e.g. a dead port -- before any
    counter mutation, while the correct identity is accepted (positive
    control).
    """
    old_nonce_a = await read_nonce(seams.call, config.port_a)
    mvid_a_before = await read_mvid(seams.call, config.port_a)
    nonce_b_before = await read_nonce(seams.call, config.port_b)
    mvid_b_before = await read_mvid(seams.call, config.port_b)
    port_a_before = await read_port(seams.call, config.port_a)

    sync_verdict = await seams.sync_unity(config.port_a, config.project_a)

    port_a_after = await read_port(seams.call, config.port_a)
    if port_a_after != port_a_before:
        raise ABReloadIdentityError(
            f"A's advertised port changed from {port_a_before!r} to {port_a_after!r} "
            "-- a reload must never become a restart"
        )

    new_nonce_observed = await read_nonce(seams.call, config.port_a)
    check_new_code_executed(config.new_nonce_a, new_nonce_observed, old_nonce_a)
    mvid_a_after = await read_mvid(seams.call, config.port_a)
    if mvid_a_after == mvid_a_before:
        raise ABReloadIdentityError("MVID unchanged after recompile -- old assembly still loaded, not new code")

    nonce_b_after = await read_nonce(seams.call, config.port_b)
    check_sentinel_unchanged(config.nonce_b, nonce_b_before)
    check_sentinel_unchanged(config.nonce_b, nonce_b_after)
    mvid_b_after = await read_mvid(seams.call, config.port_b)
    check_sentinel_unchanged(mvid_b_before, mvid_b_after)

    identity_reload = {
        "old_nonce": old_nonce_a,
        "new_nonce": new_nonce_observed,
        "mvid_before": mvid_a_before,
        "mvid_after": mvid_a_after,
        "port_before": port_a_before,
        "port_after": port_a_after,
        "sync_verdict": sync_verdict,
        "sentinel_nonce": config.nonce_b,
        "sentinel_nonce_before": nonce_b_before,
        "sentinel_nonce_after": nonce_b_after,
        "sentinel_mvid_before": mvid_b_before,
        "sentinel_mvid_after": mvid_b_after,
    }

    reject_foreign = await probe_identity(seams.bridge_factory, config.port_a, config.project_b)
    if not reject_foreign["rejected"]:
        raise ABReloadIdentityError("Foreign project identity on A's port was not rejected")
    if reject_foreign["error_type"] not in _IDENTITY_ERROR_TYPES:
        raise ABReloadIdentityError(
            f"Rejection was {reject_foreign['error_type']}, not an identity error "
            "-- port may be down rather than correctly rejecting a foreign project"
        )

    counter_before = await read_counter(seams.call, config.port_a)
    accept_correct = await probe_identity(seams.bridge_factory, config.port_a, config.project_a)
    if accept_correct["rejected"]:
        raise ABReloadIdentityError(f"Correct project identity was wrongly rejected: {accept_correct['error']}")
    counter_after = await read_counter(seams.call, config.port_a)
    check_no_resend(counter_before, counter_after, 0)

    cross_identity = {
        "reject_foreign": reject_foreign,
        "accept_correct": accept_correct,
        "counter_before": counter_before,
        "counter_after": counter_after,
    }

    return {"t3_identity_reload": identity_reload, "t3_cross_identity": cross_identity}


# Production seam factories (live_sync_unity, live_bridge_factory, and T3
# slice 3/T4's counterparts) live in gauntlet.ab_reload_live_seams -- wiring
# only, not exercised by this module's offline tests.
