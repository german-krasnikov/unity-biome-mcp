"""Focused reload regressions with real corroboration, clocks and transport wrapping."""
import asyncio
import json
import time
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp import editor_log, server
from unity_mcp.errors import SessionIdentityMismatch, UncertainDeliveryError, UnityUnavailableError, recovery_barrier
from unity_mcp.tools import diagnose, reload_ladder, sync


@pytest.fixture(autouse=True)
def isolated(monkeypatch):
    monkeypatch.setattr(sync, 'read_reload_port', lambda: None)
    monkeypatch.setattr(editor_log, '_cor_log_path', None)
    monkeypatch.setattr(sync, '_RECOVERY_TIMEOUT', 0)


@pytest.mark.real_clock
@pytest.mark.parametrize('phase', ['sync_status', 'sync', 'compile_status', 'get_compile_errors', 'diagnose', 'warm_type_cache'])
async def test_whole_deadline_covers_every_await(monkeypatch, phase):
    cancelled = asyncio.Event()
    calls = []
    async def send(cmd, args):
        calls.append(cmd)
        if cmd == phase:
            try:
                await asyncio.Future()
            finally:
                cancelled.set()
        return {'sync_status': 'epoch=7|state=ready|stamp=old:1',
                'sync': 'sync_ack|epoch=7|will_compile=false', 'compile_status': 'idle|1',
                'get_compile_errors': 'No compilation errors', 'diagnose': 'dlls=A:1:fresh', 'warm_type_cache': 'ok'}[cmd]
    monkeypatch.setattr(sync, '_send', send)
    started = time.monotonic()
    result = await asyncio.wait_for(sync.sync_unity(timeout=.025), .2)
    assert time.monotonic() - started < .15
    assert result.startswith('STOP:')
    assert cancelled.is_set()
    assert calls.count('sync') <= 1


@pytest.mark.parametrize('state', ['idle', 'idle-failed', 'idle-never', 'unknown'])
async def test_fresh_compile_state_controls_corroboration(monkeypatch, tmp_path, state):
    log = tmp_path / 'Editor.log'
    log.write_text('Mono: successfully reloaded assembly\n', encoding='utf-8')
    monkeypatch.setattr(editor_log, '_cor_log_path', log)
    monkeypatch.setattr(editor_log, '_cor_project_path', tmp_path)
    monkeypatch.setattr(editor_log, 'check_dll_freshness', lambda *args, **kwargs: False)
    calls = []
    async def send(cmd, args):
        calls.append(cmd)
        return f'{state}|1' if cmd == 'compile_status' else 'No compilation errors'
    monkeypatch.setattr(sync, '_send', send)
    result = await sync._get_errors()
    assert (result == '') == (state == 'idle')
    assert result.startswith('compile failed') == (state == 'idle-failed')
    assert calls == ['compile_status', 'get_compile_errors'] + (['diagnose'] if state == 'idle' else [])


@pytest.mark.parametrize('status', ['epoch=7|state=failed|stamp=new:2',
                                  'epoch=7|state=unknown|stamp=new:2',
                                  'epoch=7|stamp=new:2', 'epoch=999|state=ready|stamp=new:2'])
async def test_mvid_cannot_overrule_failed_unknown_or_other_epoch(status):
    async def send(cmd, args):
        if cmd == 'sync_status':
            return status
        return 'idle|1' if cmd == 'compile_status' else 'No compilation errors'
    result = await sync._attempt_recovery(send, 'old', deadline=time.monotonic()+1, expected_epoch=7)
    assert result is not None
    assert 'clean' not in result


async def test_expired_recovery_never_dispatches():
    send = AsyncMock()
    assert await sync._attempt_recovery(send, 'old', deadline=time.monotonic()-1)
    send.assert_not_awaited()


@pytest.mark.parametrize('route', ['fallback', 'sync', 'ladder'])
@pytest.mark.parametrize('original', [UncertainDeliveryError(cmd='force_refresh', op_id='sent-once', delivery='SENT'),
                                      SessionIdentityMismatch('wrong editor')])
async def test_actual_send_raw_uncertainty_never_cross_channel_replays(monkeypatch, original, route):
    async def wire(cmd, args, **kwargs):
        if cmd == 'diagnose':
            data = 'main_mvid=old\ncompile=idle-stale\nstamp=old:1\nstamp_frozen=true'
        elif cmd == 'sync_status':
            data = 'epoch=7|state=ready|stamp=old:1'
        else:
            raise original
        return {'ok': True, 'data': data}
    bridge = SimpleNamespace(send=AsyncMock(side_effect=wire), _probe=None)
    monkeypatch.setattr(server, 'slot', SimpleNamespace(bridge=bridge))
    monkeypatch.setattr(server, '_stdio_alive', lambda: True)
    monkeypatch.setattr(server, '_check_read_only', lambda cmd, args: None)
    monkeypatch.setattr(server, '_classify_connection_error', lambda exc, probe: None)
    fallback = AsyncMock()
    with pytest.raises((ConnectionError, ToolError)) as error:
        if route == 'fallback':
            await reload_ladder._send_with_fallback(server._send_raw, fallback, 'force_refresh', {})
        elif route == 'sync':
            monkeypatch.setattr(sync, '_send', server._send_raw)
            await sync.sync_unity(timeout=1)
        else:
            await reload_ladder.run_ladder(server._send_raw, send_reload=fallback, deadline=time.monotonic()+.025)
    assert recovery_barrier(error.value) is original
    assert bridge.send.await_count == (1 if route == 'fallback' else 2)
    fallback.assert_not_awaited()


@pytest.mark.parametrize('state', ['idle-failed', 'idle-never', 'compiling', ''])
def test_diagnostic_failed_or_unknown_cannot_be_overruled_by_cached_mvid(state):
    fields = diagnose._DiagnoseFields(mvid='old', stamp='old:1', compile=state)
    assert not diagnose._verdict(fields, prev_mvid='old', expected_compile=False).startswith(('CLEAN', 'NO-OP'))


async def test_soft_warning_does_not_trigger_ladder(monkeypatch):
    ladder = AsyncMock(return_value='MANUAL-REQUIRED')
    monkeypatch.setattr(sync, '_run_ladder', ladder)
    async def send(cmd, args):
        if cmd == 'sync': return 'sync_ack|epoch=7|will_compile=true'
        if cmd == 'sync_status': return 'epoch=7|state=ready|stamp=old:1'
        if cmd == 'compile_status': return 'idle|1'
        return '[warn: unverified freshness]'
    monkeypatch.setattr(sync, '_send', send)
    assert (await sync.sync_unity(timeout=1)).startswith('UNKNOWN:')
    ladder.assert_not_awaited()


async def test_blocked_sync_is_terminal_and_keeps_reason(monkeypatch):
    reason = "source_patch_on_ready_explicit_disable_required"
    send = AsyncMock(side_effect=["epoch=7|state=ready|stamp=old:1", f"blocked|reason={reason}"])
    monkeypatch.setattr(sync, "_send", send)
    result = await sync.sync_unity(timeout=1)
    assert result == f"BLOCKED: {reason}"
    assert [call.args[0] for call in send.await_args_list] == ["sync_status", "sync"]


@pytest.mark.parametrize('token,expected', [('stale', 'FAIL:stale-dll'),
    ('unknown(missing-pdb)', 'sync clean (no compile needed)'),
    ('fresh', 'sync clean (no compile needed)')])
async def test_public_sync_checks_known_source_mismatch_after_action(monkeypatch, token, expected):
    calls = []
    async def send(cmd, args):
        calls.append(cmd)
        return {'sync_status': 'epoch=7|state=ready|stamp=old:1',
                'sync': 'sync_ack|epoch=7|will_compile=false', 'compile_status': 'idle|1',
                'get_compile_errors': 'No compilation errors', 'diagnose': f'dlls=A:1:{token}',
                'warm_type_cache': 'ok'}[cmd]
    monkeypatch.setattr(sync, '_send', send)
    assert await sync.sync_unity(timeout=1) == expected
    assert calls[:5] == ['sync_status', 'sync', 'compile_status', 'get_compile_errors', 'diagnose']
    assert calls.count('sync') == 1
    assert ('warm_type_cache' in calls) == (token != 'stale')


async def test_terminal_source_observation_disconnect_cannot_return_clean(monkeypatch):
    async def send(cmd, args):
        if cmd == 'diagnose':
            raise ConnectionError('lost final observation')
        return {'sync_status': 'epoch=7|state=ready', 'sync': 'sync_ack|epoch=7|will_compile=false',
                'compile_status': 'idle|1', 'get_compile_errors': 'No compilation errors', 'warm_type_cache': 'ok'}[cmd]
    monkeypatch.setattr(sync, '_send', send)
    ladder = AsyncMock()
    monkeypatch.setattr(sync, '_run_ladder', ladder)
    assert await sync.sync_unity(timeout=1) == editor_log.UNITY_UNREACHABLE
    ladder.assert_not_awaited()


@pytest.mark.parametrize('route', ['sync_recovery', 'ladder_main', 'ladder_reload'])
async def test_blocked_recovery_ack_never_becomes_clean_or_next_action(monkeypatch, route):
    calls = []
    reason = 'source_patch_OnReady_explicit_disable_required'
    async def channel(cmd, args):
        calls.append(cmd)
        if cmd == 'diagnose':
            return 'main_mvid=old\ncompile=idle-stale\nstamp=old:1\nstamp_frozen=true'
        if cmd == 'force_refresh':
            return f'blocked|reason={reason}'
        if cmd == 'sync':
            return 'sync_ack|epoch=7|will_compile=true'
        if cmd == 'sync_status':
            return 'epoch=7|state=compiling|dur=0.0|stamp=old:1' if route == 'sync_recovery' else 'epoch=7|state=ready|stamp=old:1'
        if cmd == 'compile_status':
            return 'idle|1'
        return 'No compilation errors'
    with pytest.raises(ToolError, match=reason):
        if route == 'sync_recovery':
            monkeypatch.setattr(sync, '_send', channel)
            monkeypatch.setattr(sync, '_FOCUS_HINT_AFTER', -1)
            await sync.sync_unity(timeout=1)
        elif route == 'ladder_reload':
            main = AsyncMock(side_effect=ConnectionError('main offline'))
            await reload_ladder.run_ladder(main, send_reload=channel, deadline=time.monotonic()+.025)
        else:
            await reload_ladder.run_ladder(channel, deadline=time.monotonic()+.025)
    assert calls[-1] == 'force_refresh'
    assert calls.count('force_refresh') == 1


@pytest.mark.parametrize('stage,command', [('drain', 'force_refresh'), ('ack', 'force_refresh'), ('ack', 'diagnose')])
async def test_real_reload_frame_preserves_uncertain_write_boundary(monkeypatch, stage, command):
    writer = MagicMock()
    writer.drain = AsyncMock(side_effect=ConnectionError('drain failed') if stage == 'drain' else None)
    reader = SimpleNamespace(readexactly=AsyncMock(side_effect=asyncio.IncompleteReadError(b'', 4)))
    monkeypatch.setattr(asyncio, 'open_connection', AsyncMock(return_value=(reader, writer)))
    with pytest.raises(ConnectionError) as error:
        await reload_ladder.make_reload_send(9600)(command, {})
    frame = writer.write.call_args.args[0]
    message = json.loads(frame[4:])
    assert message['cmd'] == command
    assert len(frame)-4 == int.from_bytes(frame[:4], 'big')
    writer.write.assert_called_once()
    writer.close.assert_called_once()
    if command == 'force_refresh':
        assert isinstance(error.value, UncertainDeliveryError)
        assert error.value.op_id == message['id']
    else:
        assert recovery_barrier(error.value) is None


async def test_corroboration_retains_wrapped_session_mismatch():
    original = SessionIdentityMismatch('other project')
    wrapped = UnityUnavailableError('wrapped')
    wrapped.__cause__ = original
    with pytest.raises(ConnectionError) as error:
        await editor_log.get_corroborated_errors(AsyncMock(side_effect=wrapped), compile_status='idle')
    assert recovery_barrier(error.value) is original


@pytest.mark.parametrize('later', ['compile=idle-failed\nerrors=error CS1234: broken',
    'compile=compiling\niscompiling=true',
    'compile=idle\nreload_failed=true'])
async def test_later_diagnose_failure_or_busy_cannot_be_ignored(monkeypatch, later):
    async def send(cmd, args):
        return {'sync_status': 'epoch=7|state=ready', 'sync': 'sync_ack|epoch=7|will_compile=false',
                'compile_status': 'idle|1', 'get_compile_errors': 'No compilation errors',
                'diagnose': later+'\ndlls=A:1:fresh', 'warm_type_cache': 'ok'}[cmd]
    monkeypatch.setattr(sync, '_send', send)
    assert 'sync clean' not in await sync.sync_unity(timeout=1)


@pytest.mark.asyncio
@pytest.mark.parametrize("dll,expected", [("fresh", "sync clean"),
    ("unknown(partial-source-coverage)", "sync clean"), ("stale", "FAIL:stale-dll")])
async def test_matching_ready_epoch_does_not_require_plugin_mvid_change(monkeypatch, dll, expected):
    calls=[]
    async def send(command,args):
        calls.append(command)
        assert command != "force_refresh", "matching ready epoch does not authorize redundant mutation"
        return {"sync_status":"epoch=7|state=ready|stamp=unchanged-plugin-mvid:1",
            "sync":"sync_ack|epoch=7|will_compile=true", "compile_status":"idle|0.1",
            "get_compile_errors":"No compilation errors",
            "diagnose":f"compile=idle|0.1\nsync_state=ready\ndlls=Assembly-CSharp:1:{dll}\ncn_active=false\niscompiling=false\nerrors=No compilation errors",
            "warm_type_cache":"ok"}[command]
    monkeypatch.setattr(sync,"_send",send)
    monkeypatch.setattr(sync,"read_reload_port",lambda:None)
    monkeypatch.setattr(editor_log,"_cor_log_path",None)
    assert await sync.sync_unity(timeout=0.5) == expected
    assert "diagnose" in calls
    assert calls.count("sync") == 1
