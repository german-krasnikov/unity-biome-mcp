"""Tests for lint_scene_refs: path/snippet forwarding and mutual exclusion."""
import pytest
import unity_mcp.tools.runtime as _rt


@pytest.fixture(autouse=True)
def _bind():
    """Bind _send/_args so module functions work without server init."""
    orig_send = _rt._send
    orig_args = _rt._args
    _rt._args = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    yield
    _rt._send = orig_send
    _rt._args = orig_args


async def test_lint_sends_path():
    captured = {}

    async def _send(cmd, args=None, **kwargs):
        captured["cmd"] = cmd
        captured["args"] = args
        return "OK: no issues"

    _rt._send = _send
    result = await _rt.lint_scene_refs(path="Playtests/test.playtest")
    assert captured["cmd"] == "lint_scene_refs"
    assert captured["args"]["path"] == "Playtests/test.playtest"
    assert "snippet" not in captured["args"]
    assert result == "OK: no issues"


async def test_lint_sends_snippet():
    captured = {}

    async def _send(cmd, args=None, **kwargs):
        captured["cmd"] = cmd
        captured["args"] = args
        return "OK: no issues"

    _rt._send = _send
    result = await _rt.lint_scene_refs(snippet="ASSERT /Player")
    assert captured["cmd"] == "lint_scene_refs"
    assert captured["args"]["snippet"] == "ASSERT /Player"
    assert "path" not in captured["args"]
    assert result == "OK: no issues"


async def test_lint_mutual_exclusion():
    with pytest.raises(ValueError, match="mutually exclusive"):
        await _rt.lint_scene_refs(path="foo.playtest", snippet="ASSERT /x")


async def test_lint_requires_path_or_snippet():
    with pytest.raises(ValueError, match="required"):
        await _rt.lint_scene_refs()
