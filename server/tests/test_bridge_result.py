"""TDD tests for unwrap_bridge_result — the shared collapse of a raw bridge
dict {"ok":bool,"data":...,"err":...,"file":...} used by both
server.py::_send_raw and middleware_pipeline.py's wrapped() closure."""
from unity_mcp.bridge_result import unwrap_bridge_result


def test_unwrap_ok_data_only():
    text, ok = unwrap_bridge_result({"ok": True, "data": "hello"})
    assert (text, ok) == ("hello", True)


def test_unwrap_ok_file_only_no_data():
    text, ok = unwrap_bridge_result({"ok": True, "file": "/tmp/mv.png"})
    assert (text, ok) == ("Data saved to: /tmp/mv.png", True)


def test_unwrap_ok_data_and_file_combined():
    text, ok = unwrap_bridge_result(
        {"ok": True, "data": "FRONT:Player(vis)", "file": "/tmp/mv.png"}
    )
    assert ok is True
    assert "FRONT:Player(vis)" in text
    assert "Data saved to: /tmp/mv.png" in text


def test_unwrap_not_ok_returns_err():
    text, ok = unwrap_bridge_result({"ok": False, "err": "boom"})
    assert (text, ok) == ("boom", False)


def test_unwrap_not_ok_missing_err_defaults():
    text, ok = unwrap_bridge_result({"ok": False})
    assert ok is False
    assert text == "Unknown error"
