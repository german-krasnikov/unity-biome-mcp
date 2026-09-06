"""Tests for scripts/opencover_parse.py — TDD Red phase first.

Fixture is a real, trimmed OpenCover XML captured from CI run 33976250848
(coverage-data-Linux artifact) -- see scripts/tests/fixtures/opencover_sample.xml
for provenance. Only the third method (AbstractNoOp) has its numeric fields
edited to represent a zero-sequence-point method; real Unity CI output never
naturally contains one (verified against 7718 real methods across both
captured files during PR-08 grounding).
"""
import importlib.util
import pathlib

_SCRIPT = pathlib.Path(__file__).parent.parent / "opencover_parse.py"
FIXTURES = pathlib.Path(__file__).parent / "fixtures"


def _load():
    spec = importlib.util.spec_from_file_location("opencover_parse", _SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


ocp = _load()

_ENV_HELPER_CS = (
    "/home/runner/work/unity-biome-mcp/unity-biome-mcp/unity-plugin/Editor/EnvironmentHelper.cs"
)
_AGENT_EVENT_PARSER_CS = (
    "/home/runner/work/unity-biome-mcp/unity-biome-mcp/unity-plugin/Editor/Chat/CLI/AgentEventParser.cs"
)


def _by_method_name(records, name):
    matches = [r for r in records if r.method_name == name]
    assert len(matches) == 1, f"expected exactly one {name!r} record, got {matches}"
    return matches[0]


def test_parse_opencover_extracts_method_fields():
    records = ocp.parse_opencover(FIXTURES / "opencover_sample.xml")
    assert len(records) == 3

    reset = _by_method_name(records, "Reset")
    assert reset.file == _AGENT_EVENT_PARSER_CS
    assert reset.class_name == "UnityMCP.Editor.Chat.AgentEventParser"
    assert reset.cyclomatic_complexity == 1
    assert reset.seq_covered == 4
    assert reset.seq_total == 4
    assert reset.branch_covered == 0
    assert reset.branch_total == 0
    assert reset.line_start == 16
    assert reset.line_end == 19

    parse_enum = _by_method_name(records, "ParseEnum[T]")
    assert parse_enum.file == _ENV_HELPER_CS
    assert parse_enum.class_name == "UnityMCP.Editor.EnvironmentHelper"
    assert parse_enum.cyclomatic_complexity == 2
    assert parse_enum.seq_covered == 0
    assert parse_enum.seq_total == 3
    assert parse_enum.branch_covered == 0
    assert parse_enum.branch_total == 0
    assert parse_enum.line_start == 157
    assert parse_enum.line_end == 159


def test_parse_opencover_zero_sequence_points_is_not_zero_coverage():
    records = ocp.parse_opencover(FIXTURES / "opencover_sample.xml")
    abstract_no_op = _by_method_name(records, "AbstractNoOp")

    # seq_total == 0 must be returned literally -- never silently coerced to
    # a divide-by-zero, and never treated as "0 covered of 0 == 100%".
    assert abstract_no_op.seq_total == 0
    assert abstract_no_op.seq_covered == 0
    # Downstream code must be able to detect "unknown" without raising:
    coverage_fraction = (
        None if abstract_no_op.seq_total == 0
        else abstract_no_op.seq_covered / abstract_no_op.seq_total
    )
    assert coverage_fraction is None


def test_parse_opencover_missing_file_raises():
    try:
        ocp.parse_opencover(pathlib.Path("does-not-exist.xml"))
    except FileNotFoundError:
        pass
    else:
        raise AssertionError("expected FileNotFoundError for a missing OpenCover XML")
