"""Wire envelope contract (DSL gap analysis #6).

Every Unity -> Python TCP response is the same JSON envelope:
{"id": ..., "ok": ..., "data": ...} or {"id": ..., "ok": ..., "err": ...},
with optional extra fields (file, retry, receipt) layered on top. Before this
contract, JsonHelper.Response.cs hand-concatenated the "id"/"ok" header in
each of its 4 builder methods separately -- a 5th or 6th hand-rolled variant
could drift from the others with no test catching it.

This test scans the C# source directly (no Unity needed) and fails if a new
builder re-concatenates the header instead of going through the one shared
helper -- the single source of truth the DSL gap analysis asked for.
"""
import re
from pathlib import Path

_RESPONSE_CS = (
    Path(__file__).parents[2] / "unity-plugin" / "Editor" / "JsonHelper.Response.cs"
).read_text(encoding="utf-8")

# Builders that must assemble the envelope by calling the shared header
# helper, not by hand-concatenating "{\"id\":...,\"ok\":..." themselves.
# FormatResponseWithReceipt is exempt: it delegates to FormatResponse instead.
_DIRECT_HEADER_BUILDERS = (
    "FormatResponse",
    "FormatFileResponseWithData",
    "FormatBusyResponse",
)


def test_envelope_header_literal_appears_exactly_once():
    """The "id" and "ok" header literals must exist in exactly one place --
    the shared helper -- not once per builder method."""
    assert _RESPONSE_CS.count('\\"id\\":\\"') == 1, (
        "the \"id\" header literal appears more than once in "
        "JsonHelper.Response.cs -- a builder is hand-concatenating the "
        "envelope instead of calling the shared header helper"
    )
    assert _RESPONSE_CS.count('\\"ok\\":') == 1, (
        "the \"ok\" header literal appears more than once in "
        "JsonHelper.Response.cs -- a builder is hand-concatenating the "
        "envelope instead of calling the shared header helper"
    )


def test_direct_builders_delegate_to_shared_header_helper():
    """Every non-exempt Format* builder must call the shared helper."""
    for name in _DIRECT_HEADER_BUILDERS:
        match = re.search(
            rf"internal static string {name}\([^)]*\)(?:\s*=>\s*(?P<expr>.+?);"
            rf"|\s*\{{(?P<body>.*?)\n        \}})",
            _RESPONSE_CS,
            re.DOTALL,
        )
        assert match, f"could not isolate {name} in JsonHelper.Response.cs"
        snippet = match.group("expr") or match.group("body")
        assert "AppendEnvelopeHeader(" in snippet, (
            f"{name} does not call the shared AppendEnvelopeHeader helper"
        )


def test_canonical_envelope_field_names_present():
    """id/ok/data/err are the only required envelope fields -- the Python
    bridge reads exactly these keys (bridge.py: result.get('id'/'ok'/'data'/'err')).
    Source literals are escaped C# string content, e.g. \\"id\\"."""
    for field in ('\\"id\\"', '\\"ok\\"', '\\"data\\"', '\\"err\\"'):
        assert field in _RESPONSE_CS, f"canonical field {field} missing from JsonHelper.Response.cs"
