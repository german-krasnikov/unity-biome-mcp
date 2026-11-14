"""Tests for utils.py — parse_pipe_fields and existing helpers."""
import pytest
from unity_mcp.utils import parse_pipe_fields


def test_parse_pipe_fields_basic():
    assert parse_pipe_fields("epoch=5|state=idle") == {"epoch": "5", "state": "idle"}


def test_parse_pipe_fields_value_with_equals():
    """Values may contain '=' — split(1) must preserve them."""
    assert parse_pipe_fields("err=msg=extra") == {"err": "msg=extra"}


def test_parse_pipe_fields_empty():
    assert parse_pipe_fields("") == {}


def test_parse_pipe_fields_no_equals():
    assert parse_pipe_fields("noequals|alsonone") == {}


def test_parse_pipe_fields_mixed():
    assert parse_pipe_fields("ok|key=val|bad") == {"key": "val"}
