"""reload_ladder — T0-T5 reload-recovery ladder. MVID-delta = only heal proof (A1)."""
import asyncio  # noqa: E401
import contextlib
import json
import logging
import time
from collections.abc import Awaitable, Callable  # noqa: TC003
from pathlib import Path  # noqa: TC003

from unity_mcp.bridge_socket import frame_read, frame_write
from unity_mcp.tools.diagnose import _DiagnoseFields, _parse_diagnose, _verdict

log = logging.getLogger("unity_mcp.reload_ladder")

_T1_POLL_S: float = 40.0
_T4_POLL_S: float = 45.0
_POLL_INTERVAL_S: float = 1.0
_T2_SLEEP_S: float = 8.0
_T1_MAX_POLLS: int | None = None
_T4_MAX_POLLS: int | None = None
_BROKEN_DOMAIN = "_BROKEN_DOMAIN_"  # M2: delta but new domain is broken
_DEAD_MSG   = "MANUAL-REQUIRED: both ports dead — Unity unreachable, Reimport All manually"
_BROKEN_MSG = "REIMPORT-NEEDED: new domain loaded but compile failed — reimport package"


def _is_clean(f: _DiagnoseFields) -> bool:
    return not f.stamp_frozen and not f.iscompiling and f.cn_active

def _extract_main_mvid(f: _DiagnoseFields) -> str:
    return f.main_mvid or ""  # F3/F5: heal proof compares main_mvid, not reload mvid

def _tier_result(label: str, baseline: str, new_mvid: str | None) -> str | None:
    if new_mvid is _BROKEN_DOMAIN: return _BROKEN_MSG
    return f"HEALED: {label} mvid {baseline}→{new_mvid}" if new_mvid else None


async def _poll_mvid_delta(send, baseline: str, timeout_s: float,
                           max_polls: int | None = None,
                           cs_grace: int = 1) -> str | None:
    """Poll diagnose until main_mvid changes. Returns new MVID, _BROKEN_DOMAIN, or None.

    cs_grace: number of CS-error polls to tolerate before early-exit (default 1).
    """
    deadline = time.monotonic() + timeout_s
    polls = 0
    cs_seen = 0
    while True:
        try:
            raw = await send("diagnose", {})
            f = _parse_diagnose(raw)
            mvid = _extract_main_mvid(f)
            if mvid == baseline and "error CS" in (f.errors or ""):
                cs_seen += 1
                if cs_seen > cs_grace:
                    log.debug("early-exit: compile error with frozen MVID — domain will never reload")
                    return _BROKEN_DOMAIN
            else:
                cs_seen = 0
            if mvid and mvid != baseline:
                v = _verdict(f)
                if v.startswith("CLEAN-LIVE") or v.startswith("NO-OP"):
                    return mvid
                log.debug("M2: MVID delta but verdict %s — broken domain", v)
                return _BROKEN_DOMAIN
        except (ConnectionError, OSError):
            pass
        polls += 1
        if max_polls is not None and polls >= max_polls:
            return None
        if time.monotonic() >= deadline:
            return None
        await asyncio.sleep(_POLL_INTERVAL_S)


async def _t1(send, baseline: str) -> str | None:
    try:
        await send("force_refresh", {})
    except (ConnectionError, OSError):
        return None
    return await _poll_mvid_delta(send, baseline, _T1_POLL_S, _T1_MAX_POLLS)

async def _t2(send, baseline: str) -> str | None:
    with contextlib.suppress(ConnectionError, OSError):
        await send("recompile", {})
    await asyncio.sleep(_T2_SLEEP_S)
    return await _t1(send, baseline)

async def _t3(send, baseline: str, bump_file: Path | None) -> str | None:
    """Transient version bump. Always reverts bump_file.
    When bump_file is None (production install), triggers sync resolve instead."""
    if bump_file is None:
        try:
            await send("sync", {"resolve": "true"})
            return await _poll_mvid_delta(send, baseline, _T1_POLL_S, _T1_MAX_POLLS)
        except (ConnectionError, OSError):
            return None
    try:
        orig = bump_file.read_bytes()
    except OSError:
        return None
    new_mvid = None
    try:
        try:
            from unity_mcp.scripts.bump_version import _bump_str
            data = json.loads(orig)
            data["version"] = _bump_str(str(data.get("version", "0.0.0")))
        except (ValueError, OSError, json.JSONDecodeError):
            return None
        bump_file.write_text(json.dumps(data), encoding="utf-8")
        try:
            await send("sync", {"resolve": "true"})
            new_mvid = await _t1(send, baseline)
        except (ConnectionError, OSError):
            pass
    finally:
        bump_file.write_bytes(orig)
    return new_mvid

async def _t4(send, baseline: str,
              runner: Callable[[str], Awaitable[int]] | None) -> tuple[str | None, bool]:
    """Returns (mvid_or_sentinel, accessibility_ok)."""
    if runner is None: return None, True
    if await runner("activate") != 0: return None, False
    if await runner("cmd-r") == 1002: return None, False
    return await _poll_mvid_delta(send, baseline, _T4_POLL_S, _T4_MAX_POLLS), True

async def _t2_5_guard_check(send) -> bool | None:
    """Probe SessionState to detect ReloadGuard wedge. Returns True=wedged, False=clear, None=unknown."""
    try:
        code = 'UnityEditor.SessionState.GetBool("MCP_ReloadGuardLocked", false).ToString();'
        result = await send("execute_code", {"code": code})
        r = result.strip().lower()
        if r == "true": return True
        if r == "false": return False
        return None
    except (ConnectionError, OSError):
        return None


async def _t5(send, baseline: str) -> str | None:
    try:
        await send("force_play_stop", {})  # server-side play+stop via delayCall (allowedDuringCompile)
    except (ConnectionError, OSError):
        return None
    return await _poll_mvid_delta(send, baseline, _T4_POLL_S, max_polls=None)


def make_reload_send(port: int, host: str = "127.0.0.1"):
    """One-shot async send for reload mini-server. New TCP conn per call."""
    async def _send(cmd: str, args: dict) -> str:
        reader, writer = await asyncio.open_connection(host, port)
        try:
            msg = json.dumps({"cmd": cmd, "args": args, "id": "r"}).encode()
            frame_write(writer, msg)
            await writer.drain()
            try:
                resp = json.loads(await frame_read(reader))
                return resp.get("data", "") or resp.get("err", "")
            except (asyncio.IncompleteReadError, json.JSONDecodeError, OSError) as e:
                raise ConnectionError(f"reload transport error: {e}") from e
        finally:
            writer.close()
    return _send


async def _send_with_fallback(send_main, send_reload, cmd: str, args: dict) -> str:
    """Try send_main; fall back to send_reload on ConnectionError/OSError."""
    try:
        return await send_main(cmd, args)
    except (ConnectionError, OSError) as exc:
        if send_reload is None:
            raise
        log.debug("main failed (%s), using reload channel", exc)
        return await send_reload(cmd, args)


async def _probe_diagnose(send, send_reload) -> tuple[str, bool] | str:
    """T0 probe — try main then reload channel.

    Returns (raw, main_dead) on success, or _DEAD_MSG string when both ports fail.
    """
    try:
        return await send("diagnose", {}), False
    except (ConnectionError, OSError):
        if send_reload is None:
            return _DEAD_MSG
        try:
            return await send_reload("diagnose", {}), True
        except (ConnectionError, OSError):
            return _DEAD_MSG


async def _handle_guard_wedge(eff, baseline: str, play_stop_consent: bool) -> str | None:
    """T2.5 guard check — if wedged, escalate to T5, skipping T3/T4.

    Returns a result string if the guard path handles escalation, None to continue.
    """
    guard_wedged = await _t2_5_guard_check(eff)
    if not guard_wedged:  # False or None → continue to T3/T4
        return None
    log.warning("T2.5: ReloadGuard wedged — skipping T3/T4, escalating to T5")
    if not play_stop_consent:
        return (
            "GUARD-WEDGED: ReloadGuard is locked. "
            "T3/T4 cannot help. Set play_stop_consent=True or wait 120s for watchdog."
        )
    return (
        _tier_result("T5-guard", baseline, await _t5(eff, baseline))
        or "MANUAL-REQUIRED: guard wedged and T5 failed"
    )


async def _run_tiers_T1_T2(eff, baseline: str, start_tier: int) -> str | None:
    """Run T1 (if start_tier ≤ 1) then T2. Return result string or None to continue."""
    if start_tier <= 1:
        v = _tier_result("T1", baseline, await _t1(eff, baseline))
        if v: return v
    v = _tier_result("T2", baseline, await _t2(eff, baseline))
    if v: return v
    return None


async def _run_tiers_T3_T4_T5(
    eff, baseline: str, bump_file: Path | None,
    osascript_runner: Callable[[str], Awaitable[int]] | None,
    play_stop_consent: bool, main_dead: bool,
) -> str:
    """Run T3→T5 escalation. Returns a terminal result string."""
    if not main_dead:
        v = _tier_result("T3", baseline, await _t3(eff, baseline, bump_file))
        if v: return v
    if osascript_runner is not None:
        new_mvid, ok = await _t4(eff, baseline, osascript_runner)
        if not ok:
            return "REIMPORT-NEEDED: Accessibility denied (error 1002) — grant Terminal access in System Settings"
        v = _tier_result("T4", baseline, new_mvid)
        if v: return v
    if not play_stop_consent:
        return "MANUAL-REQUIRED: all automatic tiers exhausted — focus Unity or Reimport All"
    v = _tier_result("T5", baseline, await _t5(eff, baseline))
    return v or "MANUAL-REQUIRED: Unity state unrecoverable via TCP — Reimport All manually"


async def run_ladder(send, *, send_reload=None, bump_file: Path | None = None,
                     osascript_runner: Callable[[str], Awaitable[int]] | None = None,
                     play_stop_consent: bool = False, start_tier: int = 1) -> str:
    """Escalation ladder T0→T5. start_tier=2 skips T1 (caller did force_refresh)."""
    probe = await _probe_diagnose(send, send_reload)
    if isinstance(probe, str):
        return probe
    raw, main_dead = probe

    fields = _parse_diagnose(raw)
    baseline = _extract_main_mvid(fields)
    if baseline in ("absent", ""):
        return "REIMPORT-NEEDED: main_mvid absent — main asmdef not loaded"
    if _is_clean(fields):
        return f"CLEAN: already live mvid={baseline}"
    eff = send_reload if main_dead else send

    v = await _run_tiers_T1_T2(eff, baseline, start_tier)
    if v: return v

    guard = await _handle_guard_wedge(eff, baseline, play_stop_consent)
    if guard is not None:
        return guard

    return await _run_tiers_T3_T4_T5(
        eff, baseline, bump_file, osascript_runner, play_stop_consent, main_dead
    )
