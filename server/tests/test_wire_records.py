"""Generated wire records (dsl/protocol.biome, printed through dsl/constructs/record.construct, SV1/Q8).

These assert the generated parser/formatter reproduce the exact wire forms that
unity-plugin/Editor/SyncHelper.cs prints (and that the hand-written Python
parsers in sync_algorithm.py / code_intel.py now delegate to for well-formed
input), so the construct is pinned to the real protocol, not just its own example rows.
"""
from unity_mcp.tools.generated.wire_records import (
    format_sync_ack,
    format_sync_status,
    parse_sync_status,
)


def test_format_sync_status_matches_syncheper_failed_form():
    # SyncHelper.GetSyncStatusCore, state=="failed" branch
    assert format_sync_status(epoch=3, state="failed", err="boom", stamp="g:t") == "epoch=3|state=failed|err=boom|stamp=g:t"


def test_format_sync_status_matches_syncheper_ready_form():
    assert format_sync_status(epoch=3, state="ready", stamp="g:t") == "epoch=3|state=ready|stamp=g:t"


def test_format_sync_status_matches_syncheper_compiling_form():
    assert format_sync_status(
        epoch=3, state="compiling", dur=1.5, stamp="g:t",
        iscompiling=True, cn_active=False, started=True, stamp_frozen=False,
    ) == "epoch=3|state=compiling|dur=1.5|stamp=g:t|iscompiling=true|cn_active=false|started=true|stamp_frozen=false"


def test_format_sync_ack_matches_syncheper_trigger_form():
    # SyncHelper.TriggerSyncCore's return value
    assert format_sync_ack(epoch=4, will_compile=True) == "sync_ack|epoch=4|will_compile=true"


def test_parse_sync_status_roundtrips_formatted_line():
    line = format_sync_status(epoch=3, state="ready", stamp="g:t")
    record = parse_sync_status(line)
    assert record is not None
    assert (record.epoch, record.state, record.stamp) == (3, "ready", "g:t")


def test_parse_sync_status_ignores_unknown_future_key():
    # A newer plugin may print a key this server doesn't declare yet.
    record = parse_sync_status("epoch=7|state=ready|future_key=1")
    assert record is not None
    assert record.epoch == 7
    assert record.state == "ready"


def test_parse_sync_status_rejects_duplicate_field():
    assert parse_sync_status("epoch=1|epoch=2|state=ready") is None


def test_parse_sync_status_rejects_bad_type():
    assert parse_sync_status("epoch=not-a-number|state=ready") is None
