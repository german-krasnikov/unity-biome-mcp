"""Parametrized tests for reload_risk.classify() and classify_batch()."""
import pytest
from unity_mcp import reload_risk


@pytest.mark.parametrize("cmd, args, expected", [
    ("asset", {"path": "Assets/Foo.cs"},     "script"),
    ("asset", {"path": "Assets/Foo.asmdef"}, "script"),
    ("asset", {"path": "Assets/Foo.dll"},    "script"),
    ("asset", {"path": "Assets/Foo.asmref"}, "script"),
    ("asset", {"path": "Assets/Foo.rsp"},    "script"),
    ("asset", {"path": "Assets/Foo.mdb"},    "script"),
    ("asset", {"path": "Assets/Foo.pdb"},    "script"),
    ("asset", {"path": "Assets/Foo.aar"},    "script"),
    ("asset", {"path": "Assets/Foo.jar"},    "script"),
    ("asset", {"path": "Assets/Foo.prefab"}, "none"),
    ("asset", {"path": "Assets/Foo.mat"},    "none"),
    ("asset", {"path": "Assets/Foo.uxml"},   "none"),
    ("recompile",     {},                    "script"),
    ("force_refresh", {},                    "script"),
    ("sync_unity",    {},                    "script"),
    ("get_hierarchy", {},                    "none"),
    ("set_property",  {"path": "..."},       "none"),
    ("asset", None,                          "none"),
    ("write_text", {"path": "Assets/Foo.cs"},     "script"),
    ("write_text", {"path": "Assets/Foo.prefab"}, "none"),
])
def test_classify(cmd, args, expected):
    assert reload_risk.classify(cmd, args) == expected


@pytest.mark.parametrize("commands, expected", [
    ("create_object name=Cube\nset_property path=/Cube comp=Transform prop=x val=1", "none"),
    ("asset action=write_text path=Assets/Foo.cs content=x", "script"),
    ("# comment\nasset action=write_text path=a.prefab", "none"),
    ("recompile\n", "script"),
    ("", "none"),
    ('asset action=write_text path="Assets/X.asmdef"', "script"),
    ("sync_unity\ncreate_object name=X", "script"),
    ("set_property path=/X comp=T prop=y val=0", "none"),
])
def test_classify_batch(commands, expected):
    assert reload_risk.classify_batch(commands) == expected
