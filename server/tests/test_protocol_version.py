"""TDD: PROTOCOL_VERSION constant and version string parsing in bridge.py."""
import logging
import struct
import json
from unittest.mock import AsyncMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import PROTOCOL_VERSION, parse_version_string, VersionInfo, UnityBridge
from helpers import make_writer, reconnect_preamble


def test_protocol_version_is_3():
    assert PROTOCOL_VERSION == 3


def test_parse_new_version_format():
    info = parse_version_string("proto:3|plugin:0.37.0|stamp:abc123")
    assert info.proto == 3
    assert info.plugin == "0.37.0"
    assert info.stamp == "abc123"


def test_parse_new_version_format_no_stamp():
    info = parse_version_string("proto:3|plugin:0.37.0")
    assert info.proto == 3
    assert info.plugin == "0.37.0"
    assert info.stamp == ""


def test_parse_old_version_format_with_stamp():
    info = parse_version_string("1.0|stamp:deadbeef")
    assert info.proto == 1
    assert info.stamp == "deadbeef"
    assert info.plugin == ""


def test_parse_old_version_format_no_stamp():
    info = parse_version_string("1.0")
    assert info.proto == 1
    assert info.stamp == ""
    assert info.plugin == ""


def test_proto_mismatch_warning_python_ahead(caplog):
    """Python proto > Unity proto → log warning (old plugin, still works)."""
    from unity_mcp.bridge import check_protocol_version
    with caplog.at_level(logging.WARNING, logger="unity_mcp.bridge"):
        check_protocol_version(python_proto=3, unity_proto=1)
    assert any("upgrade" in r.message.lower() or "outdated" in r.message.lower()
               for r in caplog.records)


def test_proto_mismatch_error_unity_ahead():
    """Python proto < Unity proto → raise error (Python must be upgraded)."""
    from unity_mcp.bridge import check_protocol_version
    with pytest.raises(ConnectionError, match="upgrade"):
        check_protocol_version(python_proto=1, unity_proto=3)


def test_proto_match_no_warning(caplog):
    """Matching proto → no warning/error."""
    from unity_mcp.bridge import check_protocol_version
    with caplog.at_level(logging.WARNING, logger="unity_mcp.bridge"):
        check_protocol_version(python_proto=3, unity_proto=3)
    assert not caplog.records


async def test_check_protocol_version_called_during_reconnect():
    """_reconnect() calls check_protocol_version after ping succeeds."""
    from unity_mcp.bridge import check_protocol_version

    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(proto=3)])

    called_with = []

    def spy_check(python_proto, unity_proto):
        called_with.append((python_proto, unity_proto))

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        with patch("unity_mcp.bridge.check_protocol_version", side_effect=spy_check):
            bridge = UnityBridge("127.0.0.1", 9999)
            await bridge._reconnect(fire_callbacks=False)

    assert called_with, "check_protocol_version was not called during reconnect"
    assert called_with[0] == (PROTOCOL_VERSION, 3)


async def test_reconnect_nonfatal_on_get_version_failure():
    """get_version TCP error during reconnect → warning logged, not raised."""
    from helpers import ping_response

    ph, pp = ping_response()
    reader = AsyncMock()
    writer = make_writer()
    # Only ping reads succeed; version reads raise (simulating old Unity)
    reader.readexactly = AsyncMock(side_effect=[ph, pp, OSError("timeout")])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge("127.0.0.1", 9999)
        # Should not raise
        await bridge._reconnect(fire_callbacks=False)

    assert bridge.connected


# ── DEV-60: connect() (first connect of a session) must also negotiate
# protocol version — previously only _reconnect() did this, so a mixed-version
# pair (old plugin + new server) went undetected until the first reconnect,
# or never for a long-lived single connection. ──────────────────────────────


async def test_check_protocol_version_called_during_initial_connect():
    """connect() calls check_protocol_version after the client_hello handshake."""
    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(proto=3)])

    called_with = []

    def spy_check(python_proto, unity_proto):
        called_with.append((python_proto, unity_proto))

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        with patch("unity_mcp.bridge.check_protocol_version", side_effect=spy_check):
            bridge = UnityBridge("127.0.0.1", 9999, expected_project_path="/some/project")
            with patch.object(bridge, "_verify_candidate_project", new=AsyncMock()):
                await bridge.connect()

    assert called_with, "check_protocol_version was not called during initial connect"
    assert called_with[0] == (PROTOCOL_VERSION, 3)


async def test_initial_connect_blocks_when_plugin_is_ahead():
    """Unity proto ahead of Python proto → connect() raises ConnectionError (block)."""
    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(proto=PROTOCOL_VERSION + 1)])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge("127.0.0.1", 9999, expected_project_path="/some/project")
        with patch.object(bridge, "_verify_candidate_project", new=AsyncMock()):
            with pytest.raises(ConnectionError, match="upgrade"):
                await bridge.connect()


async def test_initial_connect_warns_when_plugin_is_outdated(caplog):
    """Python proto ahead of Unity proto → warning logged, connect() still succeeds."""
    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=[*reconnect_preamble(proto=1)])

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge("127.0.0.1", 9999, expected_project_path="/some/project")
        with patch.object(bridge, "_verify_candidate_project", new=AsyncMock()):
            with caplog.at_level(logging.WARNING, logger="unity_mcp.bridge"):
                await bridge.connect()

    assert bridge.connected
    assert any("outdated" in r.message.lower() for r in caplog.records)


async def test_initial_connect_succeeds_when_hello_unanswered():
    """A pre-hello plugin (e.g. 1.46.1) that never answers client_hello must
    not block the first connect (ARC-17 wire-compat tolerance)."""
    reader = AsyncMock()
    writer = make_writer()
    reader.readexactly = AsyncMock(side_effect=OSError("connection reset"))

    with patch("asyncio.open_connection", return_value=(reader, writer)):
        bridge = UnityBridge("127.0.0.1", 9999, expected_project_path="/some/project")
        with patch.object(bridge, "_verify_candidate_project", new=AsyncMock()):
            await bridge.connect()  # must not raise

    assert bridge.connected
    # Prove the hello was actually attempted (not just skipped outright).
    sent = json.loads(writer.write.call_args_list[0][0][0][4:])
    assert sent["cmd"] == "client_hello"
