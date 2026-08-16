"""Tests for visual regression screenshot tools."""
import os
from unittest.mock import patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.tools.scene import screenshot_baseline, screenshot_compare

pytest.importorskip("PIL")
from PIL import Image  # noqa: E402


async def test_screenshot_baseline_creates_file(tmp_path, mock_bridge):
    src_png = tmp_path / "capture.png"
    src_png.write_bytes(b"\x89PNG\r\n\x1a\nFAKE")
    mock_bridge.send.return_value = {"ok": True, "data": f"Data saved to: {src_png}"}

    baseline_dir = tmp_path / ".claude" / "baselines"
    with patch("unity_mcp.tools.scene.os.getcwd", return_value=str(tmp_path)):
        result = await screenshot_baseline("test_scene")

    assert "Baseline saved:" in result
    assert os.path.exists(str(baseline_dir / "test_scene.png"))


async def test_screenshot_compare_identical(tmp_path, mock_bridge):
    src_png = tmp_path / "capture.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(src_png)

    baseline_dir = tmp_path / ".claude" / "baselines"
    baseline_dir.mkdir(parents=True)
    import shutil
    shutil.copy2(src_png, baseline_dir / "default.png")

    mock_bridge.send.return_value = {"ok": True, "data": f"Data saved to: {src_png}"}

    with patch("unity_mcp.tools.scene.os.getcwd", return_value=str(tmp_path)):
        result = await screenshot_compare("default")

    assert "IDENTICAL" in result


async def test_screenshot_compare_different(tmp_path, mock_bridge):
    baseline_dir = tmp_path / ".claude" / "baselines"
    baseline_dir.mkdir(parents=True)
    Image.new("RGB", (10, 10), (0, 0, 0)).save(baseline_dir / "default.png")

    current_png = tmp_path / "capture.png"
    Image.new("RGB", (10, 10), (255, 0, 0)).save(current_png)
    mock_bridge.send.return_value = {"ok": True, "data": f"Data saved to: {current_png}"}

    with patch("unity_mcp.tools.scene.os.getcwd", return_value=str(tmp_path)):
        result = await screenshot_compare("default")

    # New format: pixel diff result, cached semantic, or semantic disabled
    assert any(k in result for k in ("PIXEL", "SIZE_MISMATCH", "[cached]"))


async def test_screenshot_compare_no_baseline(tmp_path, mock_bridge):
    current_png = tmp_path / "capture.png"
    current_png.write_bytes(b"\x89PNG\r\n\x1a\nDATA")
    mock_bridge.send.return_value = {"ok": True, "data": f"Data saved to: {current_png}"}

    with patch("unity_mcp.tools.scene.os.getcwd", return_value=str(tmp_path)):
        result = await screenshot_compare("nonexistent")

    assert "No baseline" in result
    assert "screenshot_baseline" in result


async def test_screenshot_compare_blocks_capture_in_read_only(
    tmp_path, mock_bridge, monkeypatch
):
    baseline_dir = tmp_path / ".claude" / "baselines"
    baseline_dir.mkdir(parents=True)
    Image.new("RGB", (2, 2), (0, 0, 0)).save(baseline_dir / "default.png")
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")

    with patch("unity_mcp.tools.scene.os.getcwd", return_value=str(tmp_path)):
        with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
            await screenshot_compare("default")

    mock_bridge.send.assert_not_awaited()


@pytest.mark.parametrize("name", ["", "../outside", "folder/name", r"folder\name"])
async def test_screenshot_baseline_rejects_unsafe_name_before_capture(name, mock_bridge):
    with pytest.raises(ToolError, match="Invalid baseline name"):
        await screenshot_baseline(name)

    mock_bridge.send.assert_not_awaited()


@pytest.mark.parametrize("name", ["", "../outside", "folder/name", r"folder\name"])
async def test_screenshot_compare_rejects_unsafe_name_before_lookup(name, mock_bridge):
    with pytest.raises(ToolError, match="Invalid baseline name"):
        await screenshot_compare(name)

    mock_bridge.send.assert_not_awaited()
