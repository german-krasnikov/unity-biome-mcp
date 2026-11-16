"""Verify bridge modules re-export every symbol declared in biome-mcp surfaces (P7)."""
from __future__ import annotations

import importlib
from pathlib import Path

import pytest

from biome_mcp.surfaces import FRAMING_SURFACE, LOCKFILE_SURFACE


@pytest.mark.parametrize("name", sorted(LOCKFILE_SURFACE))
def test_lockfile_bridge_exports(name: str) -> None:
    bridge = importlib.import_module("unity_mcp.bridge.lockfile")
    assert hasattr(bridge, name), f"unity_mcp.bridge.lockfile missing re-export '{name}'"


@pytest.mark.parametrize("name", sorted(FRAMING_SURFACE))
def test_framing_bridge_exports(name: str) -> None:
    bridge = importlib.import_module("unity_mcp.bridge.socket_io")
    assert hasattr(bridge, name), f"unity_mcp.bridge.socket_io missing re-export '{name}'"


def test_no_private_socket_imports() -> None:
    """No unity-biome-mcp source references the removed _apply_socket_options."""
    server_src = Path(__file__).resolve().parents[2] / "src"
    bad = [
        f.relative_to(server_src)
        for f in server_src.rglob("*.py")
        if "_apply_socket_options" in f.read_text(encoding="utf-8")
    ]
    assert not bad, f"References to removed private name _apply_socket_options: {bad}"
