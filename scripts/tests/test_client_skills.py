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
    "SCENE": {"unity-scene-authoring", "unity-ugui-authoring", "unity-physics-spatial"},
    "COMPONENTS": {"unity-scene-authoring"},
    "ASSETS": {"unity-assets-prefabs", "unity-materials-shaders"},
    "UGUI": {"unity-ugui-authoring"},
    "UITOOLKIT": {"unity-uitoolkit-authoring"},
    "MEDIA": {
        "unity-animation",
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
AI_DOC_CLIENT_SKILL_REFERENCES = {
    "AI/animation.md": "unity-plugin/ClientSkills/skills/unity-animation/SKILL.md",
    "AI/assets.md": "unity-plugin/ClientSkills/skills/unity-assets-prefabs/SKILL.md",
    "AI/batch.md": "unity-plugin/ClientSkills/skills/unity-mcp-operations/references/batching.md",
    "AI/hierarchy-serializer.md": "unity-plugin/ClientSkills/skills/unity-mcp-operations/SKILL.md",
    "AI/playtest-composer.md": "unity-plugin/ClientSkills/skills/unity-testing-verification/SKILL.md",
    "AI/playtest-dsl.md": "unity-plugin/ClientSkills/skills/unity-testing-verification/references/playtest-dsl.md",
    "AI/references.md": "unity-plugin/ClientSkills/skills/unity-csharp-editing/SKILL.md",
    "AI/region-tool.md": "unity-plugin/ClientSkills/skills/unity-physics-spatial/SKILL.md",
    "AI/runtime-playtest.md": "unity-plugin/ClientSkills/skills/unity-testing-verification/SKILL.md",
    "AI/search.md": "unity-plugin/ClientSkills/skills/unity-scene-authoring/SKILL.md",
    "AI/session-skills.md": "unity-plugin/ClientSkills/skills/unity-mcp-operations/references/session-and-reuse.md",
    "AI/shaders.md": "unity-plugin/ClientSkills/skills/unity-materials-shaders/SKILL.md",
    "AI/spatial.md": "unity-plugin/ClientSkills/skills/unity-physics-spatial/SKILL.md",
    "AI/timeline.md": "unity-plugin/ClientSkills/skills/unity-animation/references/timeline.md",
    "AI/tools-reference.md": "unity-plugin/ClientSkills/skills/unity-mcp-operations/SKILL.md",
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
    tree = ast.parse(source.read_text(encoding="utf-8"))
    for node in tree.body:
        if (
            isinstance(node, ast.AnnAssign)
            and isinstance(node.target, ast.Name)
            and node.target.id == "_SPECS"
            and isinstance(node.value, ast.Dict)
        ):
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
        tree = ast.parse(path.read_text(encoding="utf-8"))
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
    assert len(skill_dirs) == 12

    for directory in skill_dirs:
        assert SAFE_ID_RE.fullmatch(directory.name)
        skill_file = directory / "SKILL.md"
        assert skill_file.exists()
        frontmatter, _ = converter.split_frontmatter(skill_file.read_text(encoding="utf-8"))
        assert frontmatter.get("name") == directory.name
        description = frontmatter.get("description")
        assert isinstance(description, str) and description.startswith("Use ")
        assert len(skill_file.read_text(encoding="utf-8").splitlines()) < 500


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
    }
    expected_colors = {
        "playmode-tester": "cyan",
        "unity-csharp-developer": "green",
        "unity-diagnostics": "yellow",
        "unity-scene-editor": "blue",
    }

    for agent in agents:
        frontmatter, _ = converter.split_frontmatter(agent.read_text(encoding="utf-8"))
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
    ).read_text(encoding="utf-8")
    skill_names = {
        directory.name
        for directory in (client_root(repo_root) / "skills").iterdir()
        if directory.is_dir()
    }

    for category in (
        "CORE", "SCENE", "COMPONENTS", "ASSETS", "UGUI", "UITOOLKIT",
        "MEDIA", "VERIFY", "RUNTIME", "TESTS", "SYSTEM",
    ):
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


def test_ui_skills_cover_current_split_and_agent_routing(repo_root: pathlib.Path) -> None:
    root = client_root(repo_root)
    ugui = (root / "skills" / "unity-ugui-authoring" / "SKILL.md").read_text(
        encoding="utf-8"
    )
    uitk = (root / "skills" / "unity-uitoolkit-authoring" / "SKILL.md").read_text(
        encoding="utf-8"
    )
    scene_agent = (root / "agents" / "unity-scene-editor.md").read_text(
        encoding="utf-8"
    )

    for tool in ("create_ui", "set_rect", "lint_ugui", "ui_intent", "list_events"):
        assert f"`{tool}`" in ugui
    assert "validate_triggers" not in ugui
    for tool in (
        "inspect_uitk",
        "lint_uitk",
        "uitk_element",
        "attach_uitk",
        "uitk_file",
        "uitk_intent",
    ):
        assert f"`{tool}`" in uitk
    for ui_skill in ("unity-ugui-authoring", "unity-uitoolkit-authoring"):
        assert f".claude/skills/{ui_skill}/SKILL.md" in scene_agent


def test_transaction_and_verification_guidance_matches_public_contract(
    repo_root: pathlib.Path,
) -> None:
    root = client_root(repo_root)
    scene = (root / "skills" / "unity-scene-authoring" / "SKILL.md").read_text(
        encoding="utf-8"
    )
    diagnostics = (
        root / "skills" / "unity-diagnostics-performance" / "SKILL.md"
    ).read_text(encoding="utf-8")

    assert "atomic" in scene and "stop-on-error" in scene
    assert "prevent saving" in scene
    assert "filesystem" in scene and "execute_code" in scene
    assert "does not validate object references" in diagnostics
    assert "does not" in diagnostics and "screenshot" in diagnostics


def test_high_value_workflows_use_current_coordination_tools(repo_root: pathlib.Path) -> None:
    root = client_root(repo_root) / "skills"
    csharp = (root / "unity-csharp-editing" / "SKILL.md").read_text(encoding="utf-8")
    testing = (root / "unity-testing-verification" / "SKILL.md").read_text(encoding="utf-8")
    operations = (root / "unity-mcp-operations" / "SKILL.md").read_text(encoding="utf-8")

    assert "sync_unity(timeout=60)" in csharp
    assert "await_compile` only when compilation was already started" in csharp
    assert "run_tests_wait" in csharp
    assert "One correlated `run_tests_wait" in operations
    assert "Direct `run_tests` is low-level" in operations
    assert "hand-roll this polling protocol" in testing
    for tool in ("resolve_scene_refs", "lint_playtest", "lint_scene_refs", "validate_playtest_aliases"):
        assert tool in testing


def test_consumer_testing_guidance_excludes_repository_policy(
    repo_root: pathlib.Path,
) -> None:
    root = client_root(repo_root)
    consumer_text = "\n".join(
        path.read_text(encoding="utf-8") for path in shipped_markdown(repo_root)
    )

    for internal_detail in (
        "UnityMcpTestBase",
        "BiomeWorkerOnly",
        "unity_state_owner",
        "run_unity_tests.py",
        "test-authoring.md",
    ):
        assert internal_detail not in consumer_text

    testing = (
        root / "skills" / "unity-testing-verification" / "SKILL.md"
    ).read_text(encoding="utf-8")
    for required in (
        "run_tests_wait",
        "request_id",
        "run_id",
        "utf_guid",
        "lint_playtest_suite(pattern=",
        "run_playtest_suite(",
        "CLICK /HUD|UIDocument|submit-button",
        "FILL /HUD|UIDocument|player-name",
        "FOCUS /HUD|UIDocument|player-name",
    ):
        assert required in testing, required
    assert "paths=" not in testing

    internal = (repo_root / "AI" / "testing.md").read_text(encoding="utf-8")
    for required in (
        "UnityMcpTestBase",
        "BiomeWorkerOnly",
        "unity_state_owner",
        "run_unity_tests.py",
        "Disposable Worker Boundary",
        "Acceptance Order",
    ):
        assert required in internal, required

    converter = load_converter(repo_root)
    for agent_name in ("playmode-tester", "unity-csharp-developer"):
        frontmatter, _ = converter.split_frontmatter(
            (root / "agents" / f"{agent_name}.md").read_text(encoding="utf-8")
        )
        assert "unity-testing-verification" in converter.frontmatter_string_list(
            frontmatter, "skills"
        )


def test_installer_accepts_all_supported_release_skill_hashes(repo_root: pathlib.Path) -> None:
    installer = (
        repo_root / "unity-plugin" / "Editor" / "Wizard" / "SkillsInstaller.cs"
    ).read_text(encoding="utf-8")

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
            timeout=60,
        )
        for line in result.stdout.splitlines():
            metadata, path = line.split("\t", 1)
            if not path.endswith(".md"):
                continue
            blob = metadata.split()[2]
            assert blob in installer, f"{tag}: installer does not accept {path}"


def test_retired_consumer_artifacts_have_exact_migration_hashes(
    repo_root: pathlib.Path,
    tmp_path: pathlib.Path,
) -> None:
    installer = (
        repo_root / "unity-plugin" / "Editor" / "Wizard" / "SkillsInstaller.cs"
    ).read_text(encoding="utf-8")
    for blob in (
        "73848d830e8d979b333c5398cd25d57ea0b2bd1d",
        "0988c9a8ca580951c7fbe70695b15b59a15fc9af",
        "a06497388792403ae2e34cef65324140ebad446c",
        "f6ed9e999cba3f8aefa653fa2013fc40ce53474a",
    ):
        assert blob in installer

    converter = load_converter(repo_root)
    expected = {
        ".agents/skills/unity-ui-authoring/SKILL.md": {
            "ace0723100751e5eec95e58544dd48a560f6ced8"
        },
        ".codex/agents/unity-test-reviewer.toml": {
            "08c48d396f7848be5f8f57aaa1c493c6c6dfb3e7"
        },
        ".agents/skills/unity-testing-verification/references/test-authoring.md": {
            "a06497388792403ae2e34cef65324140ebad446c",
            "f6ed9e999cba3f8aefa653fa2013fc40ce53474a",
        },
    }
    for relative, blobs in expected.items():
        assert converter.LEGACY_GENERATED_BLOBS[relative] == frozenset(blobs)

    old_skill_source = subprocess.run(
        ["git", "cat-file", "blob", "73848d830e8d979b333c5398cd25d57ea0b2bd1d"],
        cwd=repo_root,
        check=True,
        capture_output=True,
    ).stdout
    old_agent_source = subprocess.run(
        ["git", "cat-file", "blob", "0988c9a8ca580951c7fbe70695b15b59a15fc9af"],
        cwd=repo_root,
        check=True,
        capture_output=True,
    ).stdout
    skill_source = tmp_path / ".claude" / "skills" / "unity-ui-authoring" / "SKILL.md"
    agent_source = tmp_path / ".claude" / "agents" / "unity-test-reviewer.md"
    skill_source.parent.mkdir(parents=True)
    agent_source.parent.mkdir(parents=True)
    skill_source.write_bytes(old_skill_source)
    agent_source.write_bytes(old_agent_source)

    old_skill = converter.build_skill_files(tmp_path)[0]
    old_agent = converter.build_agent_files(tmp_path)[0]
    for generated in (old_skill, old_agent):
        generated.path.parent.mkdir(parents=True, exist_ok=True)
        generated.path.write_text(generated.content, encoding="utf-8")

    assert converter.git_blob_hash(old_skill.path) in expected[
        ".agents/skills/unity-ui-authoring/SKILL.md"
    ]
    assert converter.git_blob_hash(old_agent.path) in expected[
        ".codex/agents/unity-test-reviewer.toml"
    ]


def test_public_client_guidance_is_english_portable_and_linked(repo_root: pathlib.Path) -> None:
    for path in shipped_markdown(repo_root):
        text = path.read_text(encoding="utf-8")
        assert not CYRILLIC_RE.search(text), path
        assert "/Users/" not in text, path
        assert not re.search(r"[A-Za-z]:\\Users\\", text), path

        for target_text in MARKDOWN_LINK_RE.findall(text):
            target = target_text.split("#", 1)[0]
            if not target or "://" in target or target.startswith(".claude/"):
                continue
            assert (path.parent / target).resolve().exists(), f"{path}: missing {target}"


def test_ai_docs_link_to_canonical_client_skills(repo_root: pathlib.Path) -> None:
    # Tracked AI docs must remain useful in a clean clone; ignored local skills
    # are not a documentation dependency.
    for relative_path, skill_path in AI_DOC_CLIENT_SKILL_REFERENCES.items():
        text = (repo_root / relative_path).read_text(encoding="utf-8")
        assert skill_path in text, f"{relative_path}: missing client skill {skill_path}"
        assert (repo_root / skill_path).is_file(), f"missing canonical skill {skill_path}"
        tracked = subprocess.run(
            ["git", "ls-files", "--error-unmatch", skill_path],
            cwd=repo_root,
            check=False,
            capture_output=True,
            text=True,
        )
        assert tracked.returncode == 0, f"canonical skill is not tracked: {skill_path}"


def test_shipped_tool_calls_exist_and_stale_forms_are_absent(repo_root: pathlib.Path) -> None:
    specs = set(tool_specs(repo_root))
    calls: set[str] = set()

    for path in shipped_markdown(repo_root):
        text = path.read_text(encoding="utf-8")
        for bad in KNOWN_BAD_FORMS:
            assert bad not in text, f"{path}: stale form {bad}"
        for pattern in KNOWN_BAD_PATTERNS:
            assert not pattern.search(text), f"{path}: stale form {pattern.pattern}"
        calls.update(TOOL_CALL_RE.findall(text))

    assert calls <= specs, f"Unknown documented tool calls: {sorted(calls - specs)}"


def test_python_style_examples_use_current_keyword_arguments(repo_root: pathlib.Path) -> None:
    signatures = tool_signatures(repo_root)

    for path in shipped_markdown(repo_root):
        for block in FENCED_TEXT_RE.findall(path.read_text(encoding="utf-8")):
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

    assert len(skill_files) == 12
    assert any(target.name == "batching.md" for _, target in resources)
    assert any(target.name == "evidence.md" for _, target in resources)
    assert any(target.name == "playtest-dsl.md" for _, target in resources)
    assert not any(target.name == "test-authoring.md" for _, target in resources)
    rendered = {generated.path.stem: generated.content for generated in agent_files}
    assert set(rendered) == {
        "playmode-tester",
        "unity-csharp-developer",
        "unity-diagnostics",
        "unity-scene-editor",
    }
    assert 'sandbox_mode = "read-only"' in rendered["unity-diagnostics"]
    assert 'sandbox_mode = "read-only"' not in rendered["playmode-tester"]
    assert ".agents/skills/unity-testing-verification/SKILL.md" in rendered["playmode-tester"]
    assert ".agents/skills/unity-csharp-editing/SKILL.md" in rendered["unity-csharp-developer"]
