"""Unit tests for scene.py atomic file write helpers."""
import os
import pytest
from unity_mcp.tools.scene import _copy_file_atomic, _write_file_atomic


def test_write_file_atomic_creates_file(tmp_path):
    target = str(tmp_path / "out.json")
    _write_file_atomic(target, '{"key": "value"}')
    assert os.path.exists(target)
    with open(target, encoding="utf-8") as f:
        assert f.read() == '{"key": "value"}'


def test_write_file_atomic_overwrites_existing(tmp_path):
    target = str(tmp_path / "out.txt")
    _write_file_atomic(target, "first")
    _write_file_atomic(target, "second")
    with open(target, encoding="utf-8") as f:
        assert f.read() == "second"


def test_copy_file_atomic_copies_content(tmp_path):
    src = tmp_path / "src.txt"
    src.write_bytes(b"hello bytes")
    dst = str(tmp_path / "dst.txt")
    _copy_file_atomic(str(src), dst)
    assert os.path.exists(dst)
    with open(dst, "rb") as f:
        assert f.read() == b"hello bytes"


def test_write_file_atomic_cleans_tmp_on_success(tmp_path):
    target = str(tmp_path / "out.json")
    _write_file_atomic(target, "data")
    # After success no .tmp files should remain
    tmp_files = [f for f in os.listdir(tmp_path) if f.endswith(".tmp")]
    assert tmp_files == []
