"""Structural and source-of-truth checks for shipped client skills and agents."""

from __future__ import annotations

import ast
import importlib.util
import pathlib
import re
import shlex
import subprocess
import sys

CYRILLIC_RE = re.compile(r"[А-Яа-яЁё]")
MARKDOWN_LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
TOOL_CALL_RE = re.compile(r"(?<![.`/])\b([a-z][a-z0-9_]+)\(")
SAFE_ID_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
FENCED_TEXT_RE = re.compile(r"```text\n(.*?)```", re.DOTALL)
TOOL_CATEGORY_OWNERS = {
    "CORE": {"unity-mcp-operations", "unity-scene-authoring"},
    "SCENE": {"unity-scene-authoring", "unity-ui-authoring", "unity-physics-spatial"},
    "COMPONENTS": {"unity-scene-authoring"},
    "ASSETS": {"unity-assets-prefabs", "unity-materials-shaders"},
    "MEDIA": {
        "unity-animation",
        "unity-ui-authoring",
        "unity-materials-shaders",
        "unity-particles-vfx",
    },
    "VERIFY": {
        "unity-csharp-editing",
        "unity-testing-verification",
        "unity-diagnostics-performance",
    },
    "RUNTIME": {
        "unity-testing-verification",
        "unity-diagnostics-performance",
        "unity-physics-spatial",
    },
    "TESTS": {"unity-testing-verification"},
    "SYSTEM": {
        "unity-mcp-operations",
        "unity-csharp-editing",
        "unity-diagnostics-performance",
    },
}
KNOWN_BAD_FORMS = (
    "resolve_tool_schema(tool=",
    "get_console_since(mark=",
    "configure_objects(targets=",
    "configure_objects(objects_and_config=",
    "setup_objects(spec=",
    "compile_preflight()",
    'manage_component(path="/Lamp", type="Light", action="disable")',
)
KNOWN_BAD_PATTERNS = (
    re.compile(r"\bTELEPORT\s+\S+\s+TO\s+"),
    re.compile(r"primitive=Sphere[^\n]*components=SphereCollider"),
)
INTERNAL_AI_SKILL_REFERENCES = {
    "AI/animation.md": ".claude/skills/csharp-unity.md",
    "AI/assets.md": ".claude/skills/token-optimization.md",
    "AI/batch.md": ".claude/skills/token-optimization.md",
    "AI/hierarchy-serializer.md": ".claude/skills/token-optimization.md",
    "AI/intent-tools.md": ".claude/skills/token-optimization.md",
    "AI/playtest-composer.md": ".claude/skills/playmode-verification.md",
    "AI/playtest-dsl.md": ".claude/skills/playmode-verification.md",
    "AI/references.md": ".claude/skills/csharp-unity.md",
    "AI/region-tool.md": ".claude/skills/token-optimization.md",
    "AI/runtime-playtest.md": ".claude/skills/playmode-verification.md",
    "AI/search.md": ".claude/skills/csharp-unity.md",
    "AI/session-skills.md": ".claude/skills/playmode-verification.md",
    "AI/shaders.md": ".claude/skills/csharp-unity.md",
    "AI/spatial.md": ".claude/skills/csharp-unity.md",
    "AI/timeline.md": ".claude/skills/csharp-unity.md",
    "AI/tools-reference.md": ".claude/skills/token-optimization.md",
}


def load_converter(repo_root: pathlib.Path):
    script = repo_root / "unity-plugin" / "ClientSkills" / "scripts" / "claude_to_codex.py"
    spec = importlib.util.spec_from_file_location("client_skills_converter", script)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def client_root(repo_root: pathlib.Path) -> pathlib.Path:
    return repo_root / "unity-plugin" / "ClientSkills"


def shipped_markdown(repo_root: pathlib.Path) -> list[pathlib.Path]:
    root = client_root(repo_root)
    return sorted([*root.glob("skills/**/*.md"), *root.glob("agents/*.md")])


def tool_specs(repo_root: pathlib.Path) -> dict[str, str]:
    source = repo_root / "server" / "src" / "unity_mcp" / "tools" / "tool_specs.py"
    tree = ast.parse(source.read_text())
    for node in tree.body:
        if isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name):
            if node.target.id == "_SPECS" and isinstance(node.value, ast.Dict):
                specs: dict[str, str] = {}
                for key, value in zip(node.value.keys, node.value.values, strict=False):
                    if not isinstance(key, ast.Constant) or not isinstance(key.value, str):
                        continue
                    category = None
                    if isinstance(value, ast.Call):
                        for keyword in value.keywords:
                            if (
                                keyword.arg == "category"
                                and isinstance(keyword.value, ast.Constant)
                                and isinstance(keyword.value.value, str)
                            ):
                                category = keyword.value.value
                    assert category is not None, key.value
                    specs[key.value] = category
                return specs
    raise AssertionError("_SPECS dictionary not found")


def tool_signatures(repo_root: pathlib.Path) -> dict[str, set[str]]:
    names = set(tool_specs(repo_root))
    signatures: dict[str, set[str]] = {}
    root = repo_root / "server" / "src" / "unity_mcp" / "tools"
    for path in root.glob("*.py"):
        tree = ast.parse(path.read_text())
        for node in tree.body:
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)) and node.name in names:
                signatures[node.name] = {
                    arg.arg for arg in [*node.args.posonlyargs, *node.args.args, *node.args.kwonlyargs]
                }
    return signatures


def test_skills_use_current_directory_shape_and_metadata(repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)
    skills_root = client_root(repo_root) / "skills"

    assert list(skills_root.glob("*.md")) == []
    skill_dirs = sorted(path for path in skills_root.iterdir() if path.is_dir())
    assert len(skill_dirs) == 11

    for directory in skill_dirs:
        assert SAFE_ID_RE.fullmatch(directory.name)
        skill_file = directory / "SKILL.md"
        assert skill_file.exists()
        frontmatter, _ = converter.split_frontmatter(skill_file.read_text())
        assert frontmatter.get("name") == directory.name
        description = frontmatter.get("description")
        assert isinstance(description, str) and description.startswith("Use ")
        assert len(skill_file.read_text().splitlines()) < 500


def test_agents_preload_existing_minimal_skills(repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)
    root = client_root(repo_root)
    skill_names = {directory.name for directory in (root / "skills").iterdir() if directory.is_dir()}
    agents = sorted((root / "agents").glob("*.md"))

    assert {path.stem for path in agents} == {
        "playmode-tester",
        "unity-csharp-developer",
        "unity-diagnostics",
        "unity-scene-editor",
        "unity-test-reviewer",
    }
    expected_colors = {
        "playmode-tester": "cyan",
        "unity-csharp-developer": "green",
        "unity-diagnostics": "yellow",
        "unity-scene-editor": "blue",
        "unity-test-reviewer": "magenta",
    }

    for agent in agents:
        frontmatter, _ = converter.split_frontmatter(agent.read_text())
        assert frontmatter.get("name") == agent.stem
        assert frontmatter.get("model") == "claude-sonnet-4-6"
        assert frontmatter.get("color") == expected_colors[agent.stem]
        preloads = converter.frontmatter_string_list(frontmatter, "skills")
        assert 1 <= len(preloads) <= 2
        assert set(preloads) <= skill_names


def test_operations_skill_routes_every_category_and_domain(repo_root: pathlib.Path) -> None:
    operations = (
        client_root(repo_root)
        / "skills"
        / "unity-mcp-operations"
        / "SKILL.md"
    ).read_text()
    skill_names = {
        directory.name
        for directory in (client_root(repo_root) / "skills").iterdir()
        if directory.is_dir()
    }

    for category in ("CORE", "SCENE", "COMPONENTS", "ASSETS", "MEDIA", "VERIFY", "RUNTIME", "TESTS", "SYSTEM"):
        assert f"`{category}`" in operations
    for skill_name in skill_names - {"unity-mcp-operations"}:
        assert f"`{skill_name}`" in operations


def test_every_public_tool_has_source_derived_skill_owners(repo_root: pathlib.Path) -> None:
    specs = tool_specs(repo_root)
    public_specs = {name: category for name, category in specs.items() if category != "_INTERNAL"}
    public_categories = set(public_specs.values())
    skill_names = {
        directory.name
        for directory in (client_root(repo_root) / "skills").iterdir()
        if directory.is_dir()
    }

    assert public_categories == set(TOOL_CATEGORY_OWNERS)
    ownership = {
        tool: TOOL_CATEGORY_OWNERS[category]
        for tool, category in public_specs.items()
    }
    assert set(ownership) == set(public_specs)
    for owners in ownership.values():
        assert owners
        assert owners <= skill_names


def test_high_value_workflows_use_current_coordination_tools(repo_root: pathlib.Path) -> None:
    root = client_root(repo_root) / "skills"
    csharp = (root / "unity-csharp-editing" / "SKILL.md").read_text()
    testing = (root / "unity-testing-verification" / "SKILL.md").read_text()
    operations = (root / "unity-mcp-operations" / "SKILL.md").read_text()

    assert "sync_unity(timeout=60)" in csharp
    assert "await_compile` only when compilation was already started" in csharp
    assert "run_unity_tests.py" in csharp
    assert "run_tests_wait" in csharp
    assert "Do not hand-roll" in csharp
    assert "One correlated `run_tests_wait" in operations
    assert "Direct `run_tests` is low-level" in operations
    assert "run_unity_tests.py" in operations
    assert "run_unity_tests.py" in testing
    assert "Do not hand-roll" in testing
    for tool in ("resolve_scene_refs", "lint_playtest", "lint_scene_refs", "validate_playtest_aliases"):
        assert tool in testing


def test_canonical_test_authoring_policy_is_complete_and_role_owned(
    repo_root: pathlib.Path,
) -> None:
    root = client_root(repo_root)
    canonical_path = (
        root
        / "skills"
        / "unity-testing-verification"
        / "references"
        / "test-authoring.md"
    )
    canonical = canonical_path.read_text()

    required_contract = (
        "Unity 6000.0.65f1",
        "Unity `6000.0` minimum",
        "`1.6.0`",
        "Editor's built-in Unity Test Framework",
        "UTF 1.8 was evaluated and rejected",
        "UnityMcpTestBase",
        "RegisterCleanup(Action)",
        "TrackOwnedObject<T>",
        "CreateOwnedEditorWindow<T>()",
        "TrackOwnedScene(Scene)",
        "CreateOwnedPreviewScene()",
        "Pre-existing Unity or SceneView preview scenes",
        "TrackOwnedAsset(string)",
        "[BiomeWorkerOnly",
        "[BiomeTestFixture]",
        "[BiomeTest]",
        "[BiomeSetUp]",
        "[BiomeTearDown]",
        "native NUnit/UTF",
        "[Test] async Task",
        "[UnityTest]",
        "[UnitySetUp]",
        "[UnityTearDown]",
        "IEnumerator",
        "forbidden without exception",
        "async void",
        "fire-and-forget",
        "Thread.Sleep",
        ".Wait()",
        ".Result",
        "Task.Delay",
        "WaitForEditorUpdatesAsync",
        "Awaitable.NextFrameAsync",
        "Awaitable.FixedUpdateAsync",
        "AssetDatabase.Refresh()",
        "Undo.ClearAll()",
        "NewScene()",
        "disposable worker",
        "unity_state_owner",
        "request_id",
        "run_id",
        "utf_guid",
        "START-UNKNOWN",
        "resolve_test_request",
        "get_test_run",
        "run_unity_tests.py",
        "run_tests_wait",
        "Agents must not recreate",
        "state=prepared",
        "ReloadGuard",
        "CommandRegistry",
        "UpdateChecker",
        "RelaySpawner",
        "RelaySpawnState",
        "LogAssert",
        "Sequential Acceptance Lanes",
        "server/.venv/bin/python -m pytest tests scripts/tests -q",
        "server/.venv/bin/python -m pytest install/tests -q",
        "uv run pytest tests -m 'not live' -q",
        "twice back-to-back",
        "uv run pytest tests/live -q",
        "run_unity_fault_injection.py",
        "--confirm-disposable-worker",
        "run_unity_domain_reload_acceptance.py",
        "--prepare-only",
        "--scenario all",
    )
    for required in required_contract:
        assert required in canonical, required
    assert "Custom attributes are not the abstraction for executable test business logic" in canonical
    for forbidden in (
        "Unity 6000.6",
        "ITestCommandWrapper",
        "TestCommandWrapperRegistry",
        "versionDefines",
        "UTF body observer",
    ):
        assert forbidden not in canonical, forbidden

    skill = (
        root / "skills" / "unity-testing-verification" / "SKILL.md"
    ).read_text()
    assert "references/test-authoring.md" in skill
    assert "Unity 6000.0.65f1" in skill
    assert "built-in UTF 1.6.0" in skill
    assert "Unity 6000.6" not in skill
    assert "run_unity_tests.py" in skill
    assert "run_tests_wait" in skill
    assert "CreateOwnedPreviewScene()" in skill
    assert "Pre-existing Unity or SceneView preview scenes" in skill
    for required in (
        "[UnitySetUp]",
        "[UnityTearDown]",
        "IEnumerator",
        "AssetDatabase.Refresh()",
        "state=prepared",
        "Release Test Order",
        "twice back-to-back",
    ):
        assert required in skill, required

    for agent_name in ("unity-csharp-developer", "unity-test-reviewer"):
        agent = (root / "agents" / f"{agent_name}.md").read_text()
        assert (
            ".claude/skills/unity-testing-verification/references/test-authoring.md"
            in agent
        )
        assert "run_id" in agent
        for required in (
            "[UnitySetUp]",
            "[UnityTearDown]",
            "IEnumerator",
            "AssetDatabase.Refresh",
            "state=prepared",
            "twice back-to-back",
            "Python live",
        ):
            assert required in agent, f"{agent_name}: {required}"

    developer = (root / "agents" / "unity-csharp-developer.md").read_text()
    reviewer = (root / "agents" / "unity-test-reviewer.md").read_text()
    assert "[Test] async Task" in developer
    assert "[UnityTest]" in developer and "forbidden" in developer
    assert "CreateOwnedEditorWindow" in developer and "GetWindow" in developer
    assert "native NUnit/UTF" in developer
    assert "AssetDatabase.Refresh" in developer
    assert "run_unity_tests.py" in developer
    assert "run_tests_wait" in developer
    assert "Required Checklist" in reviewer
    assert "unity_state_owner" in reviewer
    assert "native NUnit/UTF" in reviewer
    assert "BiomeWorkerOnly" in reviewer
    assert "[UnityTest]" in reviewer and "IEnumerator" in reviewer
    assert "CreateOwnedEditorWindow" in reviewer and "GetWindow" in reviewer
    assert "run_unity_tests.py" in reviewer
    assert "run_tests_wait" in reviewer
    assert "UTF body observer" not in reviewer
    assert "the wrapper observes body failure" not in reviewer
    assert "UNITY_TEST_PLAYER_LOOP_EXCEPTION:" not in canonical
    assert "UNITY_TEST_PLAYER_LOOP_EXCEPTION:" not in developer
    assert "UNITY_TEST_PLAYER_LOOP_EXCEPTION:" not in reviewer

    reliability = (repo_root / "docs" / "testing-reliability.md").read_text()
    for required in (
        "run_unity_fault_injection.py",
        "--confirm-disposable-worker",
        "run_unity_domain_reload_acceptance.py",
        "--prepare-only",
        "--scenario all",
        "state=prepared",
        "twice back-to-back",
        "server/.venv/bin/python -m pytest tests scripts/tests -q",
        "server/.venv/bin/python -m pytest install/tests -q",
        "uv run pytest tests -m 'not live' -q",
        "uv run pytest tests/live -q",
    ):
        assert required in reliability, required

    converter = load_converter(repo_root)
    for agent_name in ("playmode-tester", "unity-csharp-developer", "unity-test-reviewer"):
        frontmatter, _ = converter.split_frontmatter(
            (root / "agents" / f"{agent_name}.md").read_text()
        )
        assert "unity-testing-verification" in converter.frontmatter_string_list(
            frontmatter, "skills"
        )


def test_installer_accepts_all_supported_release_skill_hashes(repo_root: pathlib.Path) -> None:
    installer = (
        repo_root / "unity-plugin" / "Editor" / "Wizard" / "SkillsInstaller.cs"
    ).read_text()

    for tag in ("v0.94.0", "v0.95.0", "v0.96.0"):
        result = subprocess.run(
            [
                "git",
                "ls-tree",
                "-r",
                tag,
                "unity-plugin/ClientSkills/agents",
                "unity-plugin/ClientSkills/skills",
            ],
            cwd=repo_root,
            check=True,
            capture_output=True,
            text=True,
        )
        for line in result.stdout.splitlines():
            metadata, path = line.split("\t", 1)
            if not path.endswith(".md"):
                continue
            blob = metadata.split()[2]
            assert blob in installer, f"{tag}: installer does not accept {path}"


def test_public_client_guidance_is_english_portable_and_linked(repo_root: pathlib.Path) -> None:
    for path in shipped_markdown(repo_root):
        text = path.read_text()
        assert not CYRILLIC_RE.search(text), path
        assert "/Users/" not in text, path
        assert not re.search(r"[A-Za-z]:\\Users\\", text), path

        for target_text in MARKDOWN_LINK_RE.findall(text):
            target = target_text.split("#", 1)[0]
            if not target or "://" in target or target.startswith(".claude/"):
                continue
            assert (path.parent / target).resolve().exists(), f"{path}: missing {target}"


def test_ai_docs_preserve_internal_skill_ownership(repo_root: pathlib.Path) -> None:
    # Internal contributor skills and packaged consumer skills are separate surfaces.
    for relative_path, skill_path in INTERNAL_AI_SKILL_REFERENCES.items():
        text = (repo_root / relative_path).read_text()
        assert skill_path in text, f"{relative_path}: missing internal skill {skill_path}"


def test_shipped_tool_calls_exist_and_stale_forms_are_absent(repo_root: pathlib.Path) -> None:
    specs = set(tool_specs(repo_root))
    calls: set[str] = set()

    for path in shipped_markdown(repo_root):
        text = path.read_text()
        for bad in KNOWN_BAD_FORMS:
            assert bad not in text, f"{path}: stale form {bad}"
        for pattern in KNOWN_BAD_PATTERNS:
            assert not pattern.search(text), f"{path}: stale form {pattern.pattern}"
        calls.update(TOOL_CALL_RE.findall(text))

    assert calls <= specs, f"Unknown documented tool calls: {sorted(calls - specs)}"


def test_python_style_examples_use_current_keyword_arguments(repo_root: pathlib.Path) -> None:
    signatures = tool_signatures(repo_root)

    for path in shipped_markdown(repo_root):
        for block in FENCED_TEXT_RE.findall(path.read_text()):
            if "# INVALID:" in block:
                continue
            try:
                tree = ast.parse(block)
            except SyntaxError:
                continue
            for node in ast.walk(tree):
                if not isinstance(node, ast.Call) or not isinstance(node.func, ast.Name):
                    continue
                name = node.func.id
                if name not in signatures:
                    continue
                unknown = {
                    keyword.arg
                    for keyword in node.keywords
                    if keyword.arg is not None and keyword.arg not in signatures[name]
                }
                assert not unknown, f"{path}: {name} has unknown arguments {sorted(unknown)}"

                if name != "batch":
                    continue
                commands = next(
                    (
                        keyword.value.value
                        for keyword in node.keywords
                        if keyword.arg == "commands"
                        and isinstance(keyword.value, ast.Constant)
                        and isinstance(keyword.value.value, str)
                    ),
                    None,
                )
                if not commands:
                    continue
                for line in commands.splitlines():
                    tokens = shlex.split(line.strip(), comments=False)
                    if not tokens or tokens[0].startswith("#") or tokens[0] not in signatures:
                        continue
                    command_name = tokens[0]
                    keys = {token.split("=", 1)[0] for token in tokens[1:] if "=" in token}
                    unknown = keys - signatures[command_name]
                    assert not unknown, (
                        f"{path}: batch {command_name} has unknown arguments {sorted(unknown)}"
                    )


def test_unity_metadata_covers_shipped_skill_tree(repo_root: pathlib.Path) -> None:
    root = client_root(repo_root)
    candidates = [
        path
        for path in [*(root / "skills").rglob("*"), *(root / "agents").glob("*.md")]
        if not path.name.endswith(".meta")
    ]

    missing = [path for path in candidates if not pathlib.Path(str(path) + ".meta").exists()]
    assert missing == []


def test_codex_render_preserves_preloads_resources_and_read_only_agents(repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)
    root = client_root(repo_root)
    skill_files = converter.discover_skill_sources(root / "skills")
    resources = converter.discover_skill_resources_from(root / "skills", pathlib.Path("/tmp/codex-skills"))
    agent_files = converter.build_agent_files_from(
        root / "agents",
        pathlib.Path("/tmp/codex-agents"),
    )

    assert len(skill_files) == 11
    assert any(target.name == "batching.md" for _, target in resources)
    assert any(target.name == "test-authoring.md" for _, target in resources)
    test_authoring_resources = [
        (source, target)
        for source, target in resources
        if target.name == "test-authoring.md"
    ]
    assert len(test_authoring_resources) == 1
    source, target = test_authoring_resources[0]
    canonical_resource = (
        root
        / "skills"
        / "unity-testing-verification"
        / "references"
        / "test-authoring.md"
    )
    assert source.resolve() == canonical_resource.resolve()
    assert converter.desired_files([], test_authoring_resources)[target] == (
        canonical_resource.read_bytes()
    )
    assert target.as_posix().endswith(
        "/unity-testing-verification/references/test-authoring.md"
    )
    rendered = {generated.path.stem: generated.content for generated in agent_files}
    assert 'sandbox_mode = "read-only"' in rendered["unity-diagnostics"]
    assert 'sandbox_mode = "read-only"' in rendered["unity-test-reviewer"]
    assert 'sandbox_mode = "read-only"' not in rendered["playmode-tester"]
    assert ".agents/skills/unity-testing-verification/SKILL.md" in rendered["playmode-tester"]
    assert (
        ".agents/skills/unity-testing-verification/references/test-authoring.md"
        in rendered["unity-test-reviewer"]
    )
