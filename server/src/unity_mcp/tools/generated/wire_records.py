# Generated from dsl/protocol.biome
# source-sha256=352117f8394d358b392f15dab91f0159c771fe0ca2fba08a9e7a60f297dc6bd5
# Edit the source, then run biome generate.

from typing import NamedTuple

from .. import _record_runtime as rt

float1 = float
word = text = str


class SyncStatus(NamedTuple):
    epoch: int
    state: word
    err: text | None = None
    dur: float1 | None = None
    stamp: text | None = None
    iscompiling: bool | None = None
    cn_active: bool | None = None
    started: bool | None = None
    stamp_frozen: bool | None = None


parse_sync_status = rt.parser(SyncStatus, (
    ("epoch", "int"),
    ("state", "word"),
    ("err", "text", "optional"),
    ("dur", "float1", "optional"),
    ("stamp", "text", "optional"),
    ("iscompiling", "bool", "optional"),
    ("cn_active", "bool", "optional"),
    ("started", "bool", "optional"),
    ("stamp_frozen", "bool", "optional"),
))
format_sync_status = rt.formatter((
    ("epoch", "int"),
    ("state", "word"),
    ("err", "text", "optional"),
    ("dur", "float1", "optional"),
    ("stamp", "text", "optional"),
    ("iscompiling", "bool", "optional"),
    ("cn_active", "bool", "optional"),
    ("started", "bool", "optional"),
    ("stamp_frozen", "bool", "optional"),
))
class SyncAck(NamedTuple):
    epoch: int
    will_compile: bool


parse_sync_ack = rt.parser(SyncAck, (
    ("epoch", "int"),
    ("will_compile", "bool"),
), head="sync_ack")
format_sync_ack = rt.formatter((
    ("epoch", "int"),
    ("will_compile", "bool"),
), head="sync_ack")
class TestsStarted(NamedTuple):
    request_id: word
    run_id: word
    utf_guid: word
    state: word


parse_tests_started = rt.parser(TestsStarted, (
    ("request_id", "word"),
    ("run_id", "word"),
    ("utf_guid", "word"),
    ("state", "word"),
), head="tests-started")
format_tests_started = rt.formatter((
    ("request_id", "word"),
    ("run_id", "word"),
    ("utf_guid", "word"),
    ("state", "word"),
), head="tests-started")
