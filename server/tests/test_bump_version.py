"""Tests for scripts/bump_version.py — atomic patch bump of package.json."""
import json
import os
import tempfile
from pathlib import Path

import pytest

from unity_mcp.scripts.bump_version import bump_patch


# #31: bump_patch increments patch version
def test_bump_patch_increments(tmp_path):
    pkg = tmp_path / "package.json"
    pkg.write_text(json.dumps({"version": "0.20.3", "name": "test"}), encoding="utf-8")

    new_ver = bump_patch(pkg)
    assert new_ver == "0.20.4"
    data = json.loads(pkg.read_text(encoding="utf-8"))
    assert data["version"] == "0.20.4"


# #33: second bump increments again (idempotency on second call = further increment)
def test_bump_idempotent_on_second_call(tmp_path):
    pkg = tmp_path / "package.json"
    pkg.write_text(json.dumps({"version": "0.20.3"}), encoding="utf-8")

    bump_patch(pkg)
    bump_patch(pkg)

    data = json.loads(pkg.read_text(encoding="utf-8"))
    assert data["version"] == "0.20.5"
