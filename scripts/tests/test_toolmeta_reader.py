"""TDD tests for scripts/toolmeta_reader.py (A2 commit 0, Plans/DSL/A2-DESIGN.md Sec 1/Sec 6)."""
import pathlib
import sys

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import toolmeta_reader  # noqa: E402  (path set up above)

_FAKE = '''
                case "x":
                    return new CommandOptions
                    {
                        Mutating = true,
                        Required = string.Join(",", new string[] {
                            "a",
                        }),
                        Optional = string.Join(",", new string[] {
                            "b",
                            "c",
                        }),
                        PlayOk = true,
                        MaxResponseChars = 30000,
                    };
'''


def test_read_extracts_all_fields_of_one_case():
    found = toolmeta_reader.read(_FAKE)
    assert found["x"] == {
        "mutating": True, "runtime": False, "special": False, "always": False,
        "compile": False, "notbatch": False, "playok": True, "noundo": False,
        "max": 30000, "required": {"a"}, "optional": {"b", "c"},
    }


def test_read_defaults_missing_flags_to_false_and_zero():
    found = toolmeta_reader.read('case "y": return new CommandOptions {\n};')
    assert found["y"] == {
        "mutating": False, "runtime": False, "special": False, "always": False,
        "compile": False, "notbatch": False, "playok": False, "noundo": False,
        "max": 0, "required": set(), "optional": set(),
    }


def test_delegates_true_for_toolmeta_get_call():
    assert toolmeta_reader.delegates('ToolMeta.Get("x")') is True


def test_delegates_false_for_named_parameters():
    assert toolmeta_reader.delegates('mutating: true, required: "a"') is False


def test_delegates_true_on_the_paren_stripped_shape_top_level_actually_produces():
    # _top_level() (test_catalog_vs_csharp.py) drops a nested call's own "(" as a bracket-depth marker,
    # so a real delegating call's extracted `args` never contains "ToolMeta.Get(" -- only "ToolMeta.Get".
    assert toolmeta_reader.delegates(", ExecD, ToolMeta.Get") is True


def test_read_generated_covers_a_real_name():
    # Sanity check against the real file -- "ping" is basket 1 clean and stable.
    found = toolmeta_reader.read_generated()
    assert "ping" in found
