"""Relay v2 wire frame contract (DSL gap analysis #7).

Every "events" response from chat_relay.py is a frame of repeated
"{seq}\\n{text}\\n" pairs, built by RelayBuffer.cmd_events(). The only
consumer, unity-plugin/Editor/Chat/CLI/RelayChatProcess.cs (ParseV2Events),
decodes it by splitting on '\\n' and consuming pairs — it does not unescape,
so the format is only safe because RelayBuffer.enqueue() guarantees text
never contains a raw newline/CR.

There is nothing to deduplicate inside Python (the frame is built in one
place) and nothing to deduplicate inside C# (it is parsed in one place), so
a DSL RECORD/data-contract does not fit — the risk is that the two sides
silently drift apart with no test noticing. This file closes that gap two
ways: it decodes a real RelayBuffer frame with a Python mirror of the exact
C# decode loop (so an encoding change that breaks the invariant fails here),
and it statically scans RelayChatProcess.cs for the decode loop's key shape
(so a parsing change that breaks the contract fails here too, no Unity/dotnet
needed).
"""
import asyncio
from pathlib import Path

from unity_mcp.relay_buffer import FRAME_SEP, RelayBuffer

_RELAY_CHAT_PROCESS_CS = (
    Path(__file__).parents[3] / "unity-plugin" / "Editor" / "Chat" / "CLI"
    / "RelayChatProcess.cs"
).read_text(encoding="utf-8")


def _mirror_parse_v2_events(data: str, last_seq: int) -> tuple[list[tuple[int, str]], int]:
    """Python port of RelayChatProcess.cs::ParseV2Events — keep in sync.

    Splits on FRAME_SEP, consumes (seq, text) pairs, keeps only seq > last_seq,
    never unescapes text. Returns (decoded pairs, new last_seq).
    """
    parts = data.split(FRAME_SEP)
    decoded: list[tuple[int, str]] = []
    i = 0
    while i + 1 < len(parts):
        try:
            seq = int(parts[i])
        except ValueError:
            break
        if seq > last_seq:
            decoded.append((seq, parts[i + 1]))
            last_seq = seq
        i += 2
    return decoded, last_seq


def test_frame_is_seq_sep_text_sep_repeated():
    buf = RelayBuffer()
    buf.enqueue("hello")
    buf.enqueue("world")
    data = asyncio.run(buf.cmd_events(after_seq=-1, timeout_ms=0))
    assert data == f"0{FRAME_SEP}hello{FRAME_SEP}1{FRAME_SEP}world{FRAME_SEP}"


def test_cs_mirror_recovers_pairs_through_embedded_newlines():
    """Adversarial text (raw \\n/\\r) must not desync the C# pair-decoder."""
    buf = RelayBuffer()
    buf.enqueue("line one\nline two\r\nline three")
    buf.enqueue("plain")
    data = asyncio.run(buf.cmd_events(after_seq=-1, timeout_ms=0))

    decoded, last_seq = _mirror_parse_v2_events(data, last_seq=-1)

    assert decoded == [
        (0, "line one\\nline two\\r\\nline three"),
        (1, "plain"),
    ]
    assert last_seq == 1


def test_cs_mirror_skips_entries_at_or_below_last_seq():
    """Re-polling with a stale last_seq must not re-deliver or duplicate."""
    buf = RelayBuffer()
    buf.enqueue("a")
    buf.enqueue("b")
    buf.enqueue("c")
    data = asyncio.run(buf.cmd_events(after_seq=-1, timeout_ms=0))

    decoded, last_seq = _mirror_parse_v2_events(data, last_seq=0)

    assert decoded == [(1, "b"), (2, "c")]
    assert last_seq == 2


def test_cs_parser_still_matches_mirror_assumptions():
    """Static scan of RelayChatProcess.cs — no Unity/dotnet needed.

    Guards the three assumptions the Python mirror above relies on: split on
    a single char, parse seq as int, never unescape the text half.
    """
    assert "data.Split('\\n')" in _RELAY_CHAT_PROCESS_CS, (
        "ParseV2Events no longer splits frames on '\\n' — update FRAME_SEP "
        "and the Python mirror in this file to match"
    )
    assert "int.TryParse(parts[i], out var seq)" in _RELAY_CHAT_PROCESS_CS, (
        "ParseV2Events no longer parses the seq half as int"
    )
    assert "seq > _lastSeq" in _RELAY_CHAT_PROCESS_CS, (
        "ParseV2Events no longer guards against stale/duplicate seq delivery"
    )
    assert "do NOT unescape" in _RELAY_CHAT_PROCESS_CS, (
        "ParseV2Events may have started unescaping text — if so, "
        "RelayBuffer.enqueue()'s escaping must be revisited too"
    )
