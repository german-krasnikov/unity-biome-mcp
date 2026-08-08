"""Tests for readme_facts.py — collect/meta tests (split from test_single_source.py)."""
import json
import pathlib
import sys

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import readme_facts as rf

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
_META = REPO_ROOT / "docs" / "assets" / "_meta.json"

# Cache so the 5 TestCollectFacts tests share a single 3-subprocess call
# instead of spawning 15 pytest --collect-only processes in CI.
_FACTS = rf.collect_facts(REPO_ROOT)


class TestCollectFacts:
    def test_returns_all_keys(self) -> None:
        f = _FACTS
        for k in (
            "tools",
            "tests_total",
            "tests_python",
            "tests_stress",
            "tests_unity",
            "tests_unity_source",
            "tests_live",
            "server_version",
            "plugin_version",
        ):
            assert k in f, f"missing key: {k}"

    def test_tools_is_int_and_plausible(self) -> None:
        f = _FACTS
        assert isinstance(f["tools"], int) and f["tools"] >= 90

    def test_versions_are_strings(self) -> None:
        f = _FACTS
        assert isinstance(f["server_version"], str) and "." in f["server_version"]
        assert isinstance(f["plugin_version"], str) and "." in f["plugin_version"]

    def test_test_counts_are_ints(self) -> None:
        f = _FACTS
        for k in (
            "tests_total",
            "tests_python",
            "tests_stress",
            "tests_unity",
            "tests_live",
        ):
            assert isinstance(f[k], int) and f[k] >= 0

    def test_totals_add_up(self) -> None:
        f = _FACTS
        assert f["tests_total"] == (
            f["tests_python"]
            + f["tests_stress"]
            + f["tests_unity"]
            + f["tests_live"]
        )

    def test_deterministic(self) -> None:
        tool_root = REPO_ROOT / "server" / "src" / "unity_mcp"
        unity_root = REPO_ROOT / "unity-plugin"
        assert rf.count_mcp_tools(tool_root) == rf.count_mcp_tools(tool_root)
        assert rf.count_unity_tests(unity_root) == rf.count_unity_tests(unity_root)


class TestFactCollectors:
    def test_counts_registration_forms_from_source(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "tools.py").write_text(
            "async def register(mcp):\n"
            "    mcp.tool(annotations=None)(foo)\n"
            "    mcp.tool(annotations=None)(bar)\n"
            "@mcp.tool()\n"
            "async def baz(): pass\n",
            encoding="utf-8",
        )
        assert rf.count_mcp_tools(tmp_path) == 3

    def test_counts_public_tool_specs_and_excludes_internal(
        self, tmp_path: pathlib.Path
    ) -> None:
        specs = tmp_path / "tools" / "tool_specs.py"
        specs.parent.mkdir()
        specs.write_text(
            "class ToolSpec:\n"
            "    def __init__(self, category): pass\n"
            "_SPECS: dict[str, ToolSpec] = {\n"
            "    'public': ToolSpec(category='CORE'),\n"
            "    'protocol': ToolSpec(category='_INTERNAL'),\n"
            "}\n",
            encoding="utf-8",
        )
        assert rf.count_mcp_tools(tmp_path) == 1

    def test_tool_parse_errors_fail_closed(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "broken.py").write_text("def broken(:\n", encoding="utf-8")
        with pytest.raises(SyntaxError):
            rf.count_mcp_tools(tmp_path)

    def test_missing_test_directories_return_zero(self, tmp_path: pathlib.Path) -> None:
        missing = tmp_path / "missing"
        assert rf.count_pytest_python(missing) == 0
        assert rf.count_pytest_stress(missing) == 0
        assert rf.count_pytest_live(missing) == 0

    def test_pytest_collection_errors_fail_closed(
        self, tmp_path: pathlib.Path, monkeypatch
    ) -> None:
        monkeypatch.setattr(
            rf.subprocess,
            "run",
            lambda *args, **kwargs: rf.subprocess.CompletedProcess(
                args=args[0],
                returncode=2,
                stdout="",
                stderr="collection error",
            ),
        )
        with pytest.raises(RuntimeError, match="collection exited 2"):
            rf.count_pytest_python(tmp_path)

    def test_reads_versions_from_isolated_metadata(self, tmp_path: pathlib.Path) -> None:
        pyproject = tmp_path / "pyproject.toml"
        package = tmp_path / "package.json"
        pyproject.write_text('[project]\nversion = "1.2.3"\n', encoding="utf-8")
        package.write_text('{"version": "2.3.4"}', encoding="utf-8")
        assert rf.read_server_version(pyproject) == "1.2.3"
        assert rf.read_plugin_version(package) == "2.3.4"

    def test_missing_metadata_returns_none(self, tmp_path: pathlib.Path) -> None:
        assert rf.read_server_version(tmp_path / "pyproject.toml") is None
        assert rf.read_plugin_version(tmp_path / "package.json") is None


class TestUnityTestProvenance:
    """Unity README inventory is deterministic and never queries a live Editor."""

    def test_count_unity_tests_returns_provenance(self) -> None:
        result = rf.count_unity_tests(REPO_ROOT / "unity-plugin")
        assert result.source == "static_grep"
        assert isinstance(result.count, int) and result.count >= 0

    def test_collect_facts_includes_tests_unity_source(self) -> None:
        f = _FACTS
        assert "tests_unity_source" in f
        assert f["tests_unity_source"] == "static_grep"

    def test_static_fallback_counts_attributes_only(
        self, tmp_path: pathlib.Path
    ) -> None:
        (tmp_path / "Tests.cs").write_text(
            "// [Test]\n"
            "/* [UnityTest] */\n"
            'const string label = \"[Test]\";\n'
            "[Test]\nvoid A() {}\n"
            "[UnityTest]\nIEnumerator B() { yield break; }\n"
            "[TestCase(1)]\nvoid C(int value) {}\n",
            encoding="utf-8",
        )
        (tmp_path / "notes.txt").write_text("[Test]\n", encoding="utf-8")
        result = rf.count_unity_tests(tmp_path)
        assert result == rf.TestCount(3, "static_grep")

    def test_missing_plugin_directory_is_unavailable(
        self, tmp_path: pathlib.Path
    ) -> None:
        assert rf.count_unity_tests(tmp_path / "missing") == rf.TestCount(
            0, "unavailable"
        )


class TestMetaJson:
    def test_meta_json_exists(self) -> None:
        assert _META.exists(), "_meta.json not written yet — run --collect first"

    def test_meta_json_is_valid(self) -> None:
        data = json.loads(_META.read_text())
        assert isinstance(data, dict) and "tools" in data

    def test_meta_json_tools_matches_real_count(self) -> None:
        data = json.loads(_META.read_text())
        live_count = rf.count_mcp_tools(REPO_ROOT / "server" / "src" / "unity_mcp")
        assert data["tools"] == live_count
