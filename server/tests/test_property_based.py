"""Property-based tests using Hypothesis for critical parsing and protocol paths."""
import json
import struct

import pytest

hypothesis = pytest.importorskip("hypothesis")
from hypothesis import given, settings  # noqa: E402
from hypothesis import strategies as st  # noqa: E402

# ── 1. TCP frame round-trip ───────────────────────────────────────────────────

@given(
    data=st.dictionaries(
        st.text(min_size=1, max_size=20).filter(str.isidentifier),
        st.one_of(st.integers(), st.booleans(), st.text(max_size=50)),
        max_size=8,
    )
)
@settings(max_examples=200)
def test_frame_roundtrip(data):
    """4-byte BE length-prefix framing: encode then decode yields the original dict."""
    payload = json.dumps(data).encode("utf-8")
    frame = struct.pack("!I", len(payload)) + payload
    length = struct.unpack("!I", frame[:4])[0]
    decoded = json.loads(frame[4 : 4 + length])
    assert decoded == data


# ── 2. JSON command round-trip ────────────────────────────────────────────────

@given(
    cmd=st.from_regex(r"[a-z][a-z_]{2,20}", fullmatch=True),
    args=st.dictionaries(
        st.text(min_size=1, max_size=10).filter(str.isidentifier),
        st.one_of(
            st.integers(),
            st.floats(allow_nan=False, allow_infinity=False),
            st.text(max_size=30),
            st.booleans(),
        ),
        max_size=4,
    ),
)
@settings(max_examples=200)
def test_command_roundtrip(cmd, args):
    """Unity TCP command envelope: JSON serialize then parse preserves cmd and args."""
    msg = {"cmd": cmd, "args": args}
    parsed = json.loads(json.dumps(msg))
    assert parsed["cmd"] == cmd
    assert parsed["args"] == args


# ── 3. parse_version_string: arbitrary input never crashes ───────────────────

@given(s=st.text(max_size=120))
@settings(max_examples=200)
def test_parse_version_string_no_crash(s):
    """parse_version_string must never raise on any input; always returns VersionInfo."""
    from unity_mcp.bridge import VersionInfo, parse_version_string

    result = parse_version_string(s)
    assert isinstance(result, VersionInfo)


# ── 4. parse_version_string: valid new-format strings round-trip ──────────────

@given(
    proto=st.integers(min_value=0, max_value=99),
    plugin=st.from_regex(r"[a-zA-Z0-9._-]{0,20}", fullmatch=True),
)
@settings(max_examples=200)
def test_parse_version_string_valid_format(proto, plugin):
    """proto:N|plugin:P strings parse with correct proto and plugin values."""
    from unity_mcp.bridge import parse_version_string

    s = f"proto:{proto}|plugin:{plugin}"
    result = parse_version_string(s)
    assert result.proto == proto
    assert result.plugin == plugin


# ── 5. parse_kv: arbitrary text never crashes; keys are always strings ────────

@given(text=st.text(max_size=200))
@settings(max_examples=200)
def test_parse_kv_no_crash(text):
    """parse_kv must never raise on any input and always returns dict[str, str]."""
    from unity_mcp.utils import parse_kv

    result = parse_kv(text)
    assert isinstance(result, dict)
    for k, v in result.items():
        assert isinstance(k, str)
        assert isinstance(v, str)
