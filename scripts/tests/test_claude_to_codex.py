"""Regression tests for ClientSkills Claude -> Codex conversion."""

from __future__ import annotations

import importlib.util
import sys
from textwrap import dedent
from typing import TYPE_CHECKING

import pytest

if TYPE_CHECKING:
    import pathlib


def load_converter(repo_root: pathlib.Path):
    script = repo_root / "unity-plugin" / "ClientSkills" / "scripts" / "claude_to_codex.py"
    spec = importlib.util.spec_from_file_location("claude_to_codex", script)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def snapshot_tree(root: pathlib.Path) -> tuple[tuple[str, str, bytes], ...]:
    """Capture directory presence and exact file bytes below a test root."""
    if not root.exists():
        return ()
    entries: list[tuple[str, str, bytes]] = []
    for path in sorted(root.rglob("*")):
        relative = path.relative_to(root).as_posix()
        if path.is_dir():
            entries.append((relative, "directory", b""))
        else:
            entries.append((relative, "file", path.read_bytes()))
    return tuple(entries)


def test_normalize_codex_paths_uses_skill_md(repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)

    body = "\n".join(
        [
            "Read `.claude/skills/csharp-unity.md`.",
            "Read `.claude/skills/scene-assembly/SKILL.md`.",
            "Template: `.claude/skills/[skill].md`.",
            "Already active: `.agents/skills/unity-testing.md`.",
            "Resource: `.claude/skills/scene-assembly/concepts/wiring-patterns.md`.",
        ]
    )

    assert converter.normalize_codex_paths(body) == "\n".join(
        [
            "Read `.agents/skills/csharp-unity/SKILL.md`.",
            "Read `.agents/skills/scene-assembly/SKILL.md`.",
            "Template: `.agents/skills/[skill]/SKILL.md`.",
            "Already active: `.agents/skills/unity-testing/SKILL.md`.",
            "Resource: `.agents/skills/scene-assembly/concepts/wiring-patterns.md`.",
        ]
    )


def test_parse_frontmatter_supports_block_and_inline_lists(repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)

    parsed = converter.parse_simple_frontmatter(
        dedent(
            """\
            name: tester
            description: "Runs focused tests."
            skills:
              - unity-biome-workflow
              - "unity-testing"
            tools: [Read, Bash]
            """
        )
    )

    assert parsed == {
        "name": "tester",
        "description": "Runs focused tests.",
        "skills": ["unity-biome-workflow", "unity-testing"],
        "tools": ["Read", "Bash"],
    }


def test_agent_skill_preloads_become_codex_instructions(tmp_path: pathlib.Path, repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)
    agent_dir = tmp_path / ".claude" / "agents"
    agent_dir.mkdir(parents=True)
    (agent_dir / "tester.md").write_text(
        dedent(
            """\
            ---
            name: tester
            description: Tests Unity behavior.
            skills:
              - unity-biome-workflow
              - unity-testing
            ---

            Follow the evidence.
            """
        )
    )

    generated = converter.build_agent_files(tmp_path)

    assert len(generated) == 1
    assert 'sandbox_mode = "read-only"' not in generated[0].content
    assert ".agents/skills/unity-biome-workflow/SKILL.md" in generated[0].content
    assert ".agents/skills/unity-testing/SKILL.md" in generated[0].content
    assert "Follow the evidence." in generated[0].content


def test_resources_copy_check_and_prune_are_recursive(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    source = tmp_path / ".claude" / "skills" / "unity-testing"
    source.mkdir(parents=True)
    (source / "SKILL.md").write_text("---\nname: unity-testing\ndescription: Tests.\n---\n")
    (source / "references").mkdir()
    (source / "references" / "dsl.md").write_text("reference")
    (source / "scripts").mkdir()
    (source / "scripts" / "lint.py").write_bytes(b"print('ok')\n")
    (source / "ignored.meta").write_text("unity")
    (source / "__pycache__").mkdir()
    (source / "__pycache__" / "lint.pyc").write_bytes(b"cache")

    files = converter.build_skill_files(tmp_path)
    resources = converter.discover_skill_resources(tmp_path)
    plan = converter.build_sync_plan(tmp_path, files, resources, prune=True)
    converter.apply_sync_plan(
        tmp_path,
        plan,
        converter.expected_hashes(tmp_path, files, resources),
    )

    target = tmp_path / ".agents" / "skills" / "unity-testing"
    assert {path.name for path in plan.changed} == {"SKILL.md", "dsl.md", "lint.py"}
    assert (target / "references" / "dsl.md").read_text() == "reference"
    assert (target / "scripts" / "lint.py").read_bytes() == b"print('ok')\n"
    assert not (target / "ignored.meta").exists()
    assert converter.check_generated_state(tmp_path, files, resources) == []

    (target / "references" / "dsl.md").write_text("drift")
    mismatches = converter.check_generated_state(tmp_path, files, resources)
    assert str(target / "references" / "dsl.md") in mismatches

    with pytest.raises(ValueError, match="not owned"):
        converter.build_sync_plan(tmp_path, files, resources, prune=True)
    assert (target / "references" / "dsl.md").read_text() == "drift"

    (target / "references" / "dsl.md").write_text("reference")
    (source / "references" / "dsl.md").unlink()
    resources = converter.discover_skill_resources(tmp_path)
    mismatches = converter.check_generated_state(tmp_path, files, resources)
    assert str(tmp_path / converter.MANIFEST_RELATIVE_PATH) in mismatches
    plan = converter.build_sync_plan(tmp_path, files, resources, prune=True)
    assert plan.remove == (target / "references" / "dsl.md",)
    converter.apply_sync_plan(
        tmp_path,
        plan,
        converter.expected_hashes(tmp_path, files, resources),
    )
    assert not (target / "references" / "dsl.md").exists()


def test_prune_rejects_modified_generated_and_preserves_unmanaged_files(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    generated = tmp_path / ".agents" / "skills" / "sample" / "old.md"
    generated.parent.mkdir(parents=True)
    generated.write_text("generated")
    unmanaged = tmp_path / ".agents" / "skills" / "personal" / "SKILL.md"
    unmanaged.parent.mkdir(parents=True)
    unmanaged.write_text("personal")
    converter.write_manifest(
        tmp_path,
        {generated.relative_to(tmp_path).as_posix(): converter.file_hash(generated)},
    )
    generated.write_text("user edit")

    with pytest.raises(ValueError, match="modified generated file"):
        converter.build_sync_plan(tmp_path, [], [], prune=True)
    assert generated.read_text() == "user edit"
    assert unmanaged.read_text() == "personal"


def test_invalid_manifest_path_traversal_stops_sync(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    outside = tmp_path.parent / "outside-client-skill.txt"
    outside.write_text("must survive")
    traversal = ".agents/skills/../../../outside-client-skill.txt"
    converter.write_manifest(
        tmp_path,
        {traversal: converter.file_hash(outside)},
    )

    with pytest.raises(ValueError, match="invalid managed entry"):
        converter.build_sync_plan(tmp_path, [], [], prune=True)
    assert outside.read_text() == "must survive"


def test_legacy_prune_removes_only_known_unchanged_generated_files(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    unchanged = tmp_path / ".agents" / "skills" / "legacy" / "SKILL.md"
    unchanged.parent.mkdir(parents=True)
    unchanged.write_text("generated")
    modified = tmp_path / ".codex" / "agents" / "legacy.toml"
    modified.parent.mkdir(parents=True)
    modified.write_text("user edit")
    legacy = {
        ".agents/skills/legacy/SKILL.md": converter.git_blob_hash(unchanged),
        ".codex/agents/legacy.toml": converter.git_blob_hash(modified)[:-1] + "0",
    }

    with pytest.raises(ValueError, match="modified generated file"):
        converter.build_sync_plan(
            tmp_path,
            [],
            [],
            prune=True,
            legacy_blobs=legacy,
        )

    assert unchanged.exists()
    assert modified.exists()
    assert modified.read_text() == "user edit"

    plan = converter.build_sync_plan(
        tmp_path,
        [],
        [],
        prune=True,
        legacy_blobs={
            ".agents/skills/legacy/SKILL.md": converter.git_blob_hash(unchanged),
        },
    )
    converter.apply_sync_plan(tmp_path, plan, {})
    assert not unchanged.exists()


def test_legacy_prune_skips_current_generated_target(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    target = tmp_path / ".agents" / "skills" / "current" / "SKILL.md"
    target.parent.mkdir(parents=True)
    target.write_text("new current content")
    relative = ".agents/skills/current/SKILL.md"

    generated = [converter.GeneratedFile(target, "new current content")]
    plan = converter.build_sync_plan(
        tmp_path,
        generated,
        [],
        prune=True,
        legacy_blobs={relative: "wrong"},
    )

    assert plan.remove == ()
    assert plan.unchanged == (target,)
    assert target.read_text() == "new current content"


def test_prune_removes_retired_agent_and_resource_owned_by_manifest(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    reviewer = tmp_path / ".codex" / "agents" / "unity-test-reviewer.toml"
    policy = (
        tmp_path
        / ".agents"
        / "skills"
        / "unity-testing-verification"
        / "references"
        / "test-authoring.md"
    )
    reviewer.parent.mkdir(parents=True)
    policy.parent.mkdir(parents=True)
    reviewer.write_text("managed reviewer")
    policy.write_text("managed repository policy")
    converter.write_manifest(
        tmp_path,
        {
            reviewer.relative_to(tmp_path).as_posix(): converter.file_hash(reviewer),
            policy.relative_to(tmp_path).as_posix(): converter.file_hash(policy),
        },
    )

    plan = converter.build_sync_plan(tmp_path, [], [], prune=True)
    converter.apply_sync_plan(tmp_path, plan, {})

    assert set(plan.remove) == {reviewer, policy}
    assert not reviewer.exists()
    assert not policy.exists()
    assert converter.load_manifest(tmp_path) == {}


def test_modified_retired_manifest_file_blocks_prune_without_partial_changes(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    reviewer = tmp_path / ".codex" / "agents" / "unity-test-reviewer.toml"
    policy = (
        tmp_path
        / ".agents"
        / "skills"
        / "unity-testing-verification"
        / "references"
        / "test-authoring.md"
    )
    replacement = tmp_path / ".agents" / "skills" / "current" / "SKILL.md"
    reviewer.parent.mkdir(parents=True)
    policy.parent.mkdir(parents=True)
    reviewer.write_text("managed reviewer")
    policy.write_text("managed policy")
    converter.write_manifest(
        tmp_path,
        {
            reviewer.relative_to(tmp_path).as_posix(): converter.file_hash(reviewer),
            policy.relative_to(tmp_path).as_posix(): converter.file_hash(policy),
        },
    )
    policy.write_text("user customization")
    generated = [converter.GeneratedFile(replacement, "current skill")]

    with pytest.raises(ValueError, match="modified generated file"):
        converter.build_sync_plan(tmp_path, generated, [], prune=True)

    assert reviewer.read_text() == "managed reviewer"
    assert policy.read_text() == "user customization"
    assert not replacement.exists()


def test_read_only_agent_maps_to_codex_sandbox(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    agent_dir = tmp_path / ".claude" / "agents"
    agent_dir.mkdir(parents=True)
    (agent_dir / "tester.md").write_text(
        "---\nname: tester\ndescription: Tests.\ndisallowedTools: Write, Edit\n---\nRead only.\n"
    )

    content = converter.build_agent_files(tmp_path)[0].content

    assert 'sandbox_mode = "read-only"' in content


def test_unsafe_and_duplicate_skill_ids_fail(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    skill_dir = tmp_path / ".claude" / "skills"
    skill_dir.mkdir(parents=True)
    (skill_dir / "same.md").write_text("flat")
    (skill_dir / "same").mkdir()
    (skill_dir / "same" / "SKILL.md").write_text("folder")

    try:
        converter.discover_skill_sources(skill_dir)
    except ValueError as exc:
        assert "Duplicate skill id" in str(exc)
    else:
        raise AssertionError("duplicate id must fail")

    agent_dir = tmp_path / ".claude" / "agents"
    agent_dir.mkdir(parents=True)
    (agent_dir / "bad.md").write_text(
        "---\nname: ../escape\ndescription: Unsafe.\n---\n"
    )
    try:
        converter.build_agent_files(tmp_path)
    except ValueError as exc:
        assert "invalid identifier" in str(exc)
    else:
        raise AssertionError("unsafe id must fail")


def test_duplicate_agent_ids_fail(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    agent_dir = tmp_path / ".claude" / "agents"
    agent_dir.mkdir(parents=True)
    (agent_dir / "first.md").write_text(
        "---\nname: duplicate\ndescription: First.\n---\n"
    )
    (agent_dir / "second.md").write_text(
        "---\nname: duplicate\ndescription: Second.\n---\n"
    )

    try:
        converter.build_agent_files(tmp_path)
    except ValueError as exc:
        assert "Duplicate agent id" in str(exc)
    else:
        raise AssertionError("duplicate agent id must fail")


def test_resource_sync_is_idempotent(tmp_path: pathlib.Path, repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)
    source = tmp_path / ".claude" / "skills" / "sample"
    source.mkdir(parents=True)
    (source / "SKILL.md").write_text("---\nname: sample\ndescription: Sample.\n---\n")
    (source / "template.txt").write_text("template")

    files = converter.build_skill_files(tmp_path)
    resources = converter.discover_skill_resources(tmp_path)
    plan = converter.build_sync_plan(tmp_path, files, resources, prune=True)
    converter.apply_sync_plan(
        tmp_path,
        plan,
        converter.expected_hashes(tmp_path, files, resources),
    )
    second = converter.build_sync_plan(tmp_path, files, resources, prune=True)

    assert len(plan.changed) == 2
    assert second.changed == ()
    assert len(second.unchanged) == 2
    assert second.remove == ()


def test_first_sync_preserves_unowned_codex_file(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    agent_source = tmp_path / ".claude" / "agents"
    agent_source.mkdir(parents=True)
    (agent_source / "tester.md").write_text(
        "---\nname: tester\ndescription: Tests.\n---\nGenerated.\n"
    )
    personal = tmp_path / ".codex" / "agents" / "tester.toml"
    personal.parent.mkdir(parents=True)
    personal.write_text("personal")
    files = converter.build_agent_files(tmp_path)

    with pytest.raises(ValueError, match="not owned"):
        converter.build_sync_plan(tmp_path, files, [], prune=True)

    assert personal.read_text() == "personal"
    assert not (tmp_path / converter.MANIFEST_RELATIVE_PATH).exists()


def test_corrupt_manifest_stops_sync(tmp_path: pathlib.Path, repo_root: pathlib.Path) -> None:
    converter = load_converter(repo_root)
    manifest = tmp_path / converter.MANIFEST_RELATIVE_PATH
    manifest.parent.mkdir(parents=True)
    manifest.write_text("{broken")

    with pytest.raises(ValueError, match="invalid ownership manifest"):
        converter.build_sync_plan(tmp_path, [], [], prune=True)


def test_symlinked_managed_ancestor_stops_sync(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
) -> None:
    converter = load_converter(repo_root)
    outside = tmp_path.parent / f"{tmp_path.name}-outside"
    outside.mkdir()
    agents = tmp_path / ".codex" / "agents"
    agents.parent.mkdir(parents=True)
    agents.symlink_to(outside, target_is_directory=True)
    generated = [converter.GeneratedFile(agents / "tester.toml", 'name = "tester"\n')]

    with pytest.raises(ValueError, match="Symlink"):
        converter.build_sync_plan(tmp_path, generated, [], prune=True)

    assert list(outside.iterdir()) == []


def test_apply_rolls_back_when_atomic_write_fails(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    converter = load_converter(repo_root)
    first = tmp_path / ".codex" / "agents" / "a.toml"
    second = tmp_path / ".codex" / "agents" / "b.toml"
    first.parent.mkdir(parents=True)
    first.write_text("old-a")
    second.write_text("old-b")
    converter.write_manifest(
        tmp_path,
        {
            first.relative_to(tmp_path).as_posix(): converter.file_hash(first),
            second.relative_to(tmp_path).as_posix(): converter.file_hash(second),
        },
    )
    files = [
        converter.GeneratedFile(first, "new-a"),
        converter.GeneratedFile(second, "new-b"),
    ]
    plan = converter.build_sync_plan(tmp_path, files, [], prune=True)
    original_write = converter._atomic_write
    calls = 0

    def fail_second(path: pathlib.Path, content: bytes) -> None:
        nonlocal calls
        calls += 1
        if calls == 2:
            raise OSError("disk full")
        original_write(path, content)

    monkeypatch.setattr(converter, "_atomic_write", fail_second)
    with pytest.raises(OSError, match="disk full"):
        converter.apply_sync_plan(
            tmp_path,
            plan,
            converter.expected_hashes(tmp_path, files, []),
        )

    assert first.read_text() == "old-a"
    assert second.read_text() == "old-b"


def test_apply_rolls_back_replacement_removal_and_manifest_after_post_write_failure(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    converter = load_converter(repo_root)
    current = tmp_path / ".codex" / "agents" / "current.toml"
    retired = tmp_path / ".codex" / "agents" / "retired.toml"
    current.parent.mkdir(parents=True)
    current.write_bytes(b"old current\n")
    retired.write_bytes(b"retired generated\n")
    original_manifest = {
        current.relative_to(tmp_path).as_posix(): converter.file_hash(current),
        retired.relative_to(tmp_path).as_posix(): converter.file_hash(retired),
    }
    converter.write_manifest(tmp_path, original_manifest)
    manifest = tmp_path / converter.MANIFEST_RELATIVE_PATH
    manifest_before = manifest.read_bytes()
    files = [converter.GeneratedFile(current, "new current\n")]
    plan = converter.build_sync_plan(tmp_path, files, [], prune=True)
    assert plan.remove == (retired,)
    original_write = converter._atomic_write

    def fail_after_manifest_write(path: pathlib.Path, content: bytes) -> None:
        original_write(path, content)
        if path == manifest:
            raise OSError("manifest acknowledgement lost")

    monkeypatch.setattr(converter, "_atomic_write", fail_after_manifest_write)
    with pytest.raises(OSError, match="manifest acknowledgement lost"):
        converter.apply_sync_plan(
            tmp_path,
            plan,
            converter.expected_hashes(tmp_path, files, []),
        )

    assert current.read_bytes() == b"old current\n"
    assert retired.read_bytes() == b"retired generated\n"
    assert manifest.read_bytes() == manifest_before
    assert converter.load_manifest(tmp_path) == original_manifest


@pytest.mark.parametrize("interrupt_type", [KeyboardInterrupt, SystemExit])
@pytest.mark.parametrize("stage", ["new", "replaced", "removed", "manifest"])
def test_apply_interrupt_rolls_back_exact_tree_and_reraises_original(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
    interrupt_type: type[BaseException],
    stage: str,
) -> None:
    converter = load_converter(repo_root)
    new_target = tmp_path / ".agents" / "skills" / "new" / "SKILL.md"
    replaced = tmp_path / ".codex" / "agents" / "current.toml"
    retired = tmp_path / ".codex" / "agents" / "retired.toml"
    preserved_empty = tmp_path / ".agents" / "skills" / "personal-empty"
    replaced.parent.mkdir(parents=True)
    preserved_empty.mkdir(parents=True)
    replaced.write_bytes(b"old current\n")
    retired.write_bytes(b"retired generated\n")
    converter.write_manifest(
        tmp_path,
        {
            replaced.relative_to(tmp_path).as_posix(): converter.file_hash(replaced),
            retired.relative_to(tmp_path).as_posix(): converter.file_hash(retired),
        },
    )
    manifest = tmp_path / converter.MANIFEST_RELATIVE_PATH
    files = [
        converter.GeneratedFile(new_target, "new skill\n"),
        converter.GeneratedFile(replaced, "new current\n"),
    ]
    plan = converter.build_sync_plan(tmp_path, files, [], prune=True)
    before = snapshot_tree(tmp_path)
    original_write = converter._atomic_write
    original_replace = converter.os.replace

    def interrupt_after_write(path: pathlib.Path, content: bytes) -> None:
        original_write(path, content)
        expected = {
            "new": new_target,
            "replaced": replaced,
            "manifest": manifest,
        }.get(stage)
        if path == expected:
            raise interrupt_type(f"interrupt after {stage}")

    def interrupt_after_remove(source: pathlib.Path, destination: pathlib.Path) -> None:
        original_replace(source, destination)
        if stage == "removed" and source == retired:
            raise interrupt_type("interrupt after removed")

    monkeypatch.setattr(converter, "_atomic_write", interrupt_after_write)
    monkeypatch.setattr(converter.os, "replace", interrupt_after_remove)

    with pytest.raises(interrupt_type, match=f"interrupt after {stage}"):
        converter.apply_sync_plan(
            tmp_path,
            plan,
            converter.expected_hashes(tmp_path, files, []),
        )

    assert snapshot_tree(tmp_path) == before
    assert preserved_empty.is_dir()
    assert list(tmp_path.glob(".claude-to-codex-*")) == []


def test_apply_interrupt_preserves_recovery_backup_and_chains_original(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    converter = load_converter(repo_root)
    target = tmp_path / ".codex" / "agents" / "current.toml"
    target.parent.mkdir(parents=True)
    target.write_bytes(b"old current\n")
    converter.write_manifest(
        tmp_path,
        {target.relative_to(tmp_path).as_posix(): converter.file_hash(target)},
    )
    files = [converter.GeneratedFile(target, "new current\n")]
    plan = converter.build_sync_plan(tmp_path, files, [], prune=True)
    original_replace = converter.os.replace

    def interrupt_after_write(path: pathlib.Path, content: bytes) -> None:
        path.write_bytes(content)
        raise KeyboardInterrupt("sync interrupted")

    def fail_backup_restore(source: pathlib.Path, destination: pathlib.Path) -> None:
        if "backup" in source.parts and destination == target:
            raise OSError("restore blocked")
        original_replace(source, destination)

    monkeypatch.setattr(converter, "_atomic_write", interrupt_after_write)
    monkeypatch.setattr(converter.os, "replace", fail_backup_restore)

    with pytest.raises(RuntimeError, match="rollback was incomplete") as caught:
        converter.apply_sync_plan(
            tmp_path,
            plan,
            converter.expected_hashes(tmp_path, files, []),
        )

    assert isinstance(caught.value.__cause__, KeyboardInterrupt)
    transactions = list(tmp_path.glob(".claude-to-codex-*"))
    assert len(transactions) == 1
    backup = transactions[0] / "backup" / target.relative_to(tmp_path)
    assert backup.read_bytes() == b"old current\n"
    assert target.read_bytes() == b"new current\n"


def test_rollback_keeps_user_file_added_to_transaction_created_directory(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    converter = load_converter(repo_root)
    target = tmp_path / ".agents" / "skills" / "new" / "SKILL.md"
    user_file = target.parent / "notes.txt"
    files = [converter.GeneratedFile(target, "generated\n")]
    plan = converter.build_sync_plan(tmp_path, files, [], prune=True)
    original_write = converter._atomic_write

    def interrupt_with_user_file(path: pathlib.Path, content: bytes) -> None:
        original_write(path, content)
        user_file.write_text("keep me\n", encoding="utf-8")
        raise KeyboardInterrupt("sync interrupted")

    monkeypatch.setattr(converter, "_atomic_write", interrupt_with_user_file)
    with pytest.raises(KeyboardInterrupt, match="sync interrupted"):
        converter.apply_sync_plan(
            tmp_path,
            plan,
            converter.expected_hashes(tmp_path, files, []),
        )

    assert not target.exists()
    assert user_file.read_text(encoding="utf-8") == "keep me\n"


def test_transaction_directory_cleanup_failure_warns_after_successful_commit(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    converter = load_converter(repo_root)
    target = tmp_path / ".codex" / "agents" / "current.toml"
    files = [converter.GeneratedFile(target, "current\n")]
    plan = converter.build_sync_plan(tmp_path, files, [], prune=True)

    def fail_cleanup(_path: pathlib.Path) -> None:
        raise OSError("cleanup busy")

    monkeypatch.setattr(converter.shutil, "rmtree", fail_cleanup)
    converter.apply_sync_plan(
        tmp_path,
        plan,
        converter.expected_hashes(tmp_path, files, []),
    )

    assert target.read_text(encoding="utf-8") == "current\n"
    assert converter.check_generated_state(tmp_path, files, []) == []
    assert "WARNING cleanup incomplete" in capsys.readouterr().err
    assert len(list(tmp_path.glob(".claude-to-codex-*"))) == 1


def test_empty_managed_directory_cleanup_failure_warns_after_successful_commit(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    converter = load_converter(repo_root)
    retired = tmp_path / ".agents" / "skills" / "retired" / "SKILL.md"
    retired.parent.mkdir(parents=True)
    personal_empty = tmp_path / ".agents" / "skills" / "personal-empty"
    personal_empty.mkdir(parents=True)
    retired.write_text("retired\n", encoding="utf-8")
    converter.write_manifest(
        tmp_path,
        {retired.relative_to(tmp_path).as_posix(): converter.file_hash(retired)},
    )
    plan = converter.build_sync_plan(tmp_path, [], [], prune=True)
    original_rmdir = converter.Path.rmdir

    def fail_retired_directory(path: pathlib.Path) -> None:
        if path == retired.parent:
            raise OSError("directory busy")
        original_rmdir(path)

    monkeypatch.setattr(converter.Path, "rmdir", fail_retired_directory)
    converter.apply_sync_plan(tmp_path, plan, {})

    assert not retired.exists()
    assert converter.load_manifest(tmp_path) == {}
    assert retired.parent.is_dir()
    assert personal_empty.is_dir()
    assert "WARNING cleanup incomplete" in capsys.readouterr().err


def test_python_310_fallback_validates_generated_toml(
    tmp_path: pathlib.Path,
    repo_root: pathlib.Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    converter = load_converter(repo_root)
    monkeypatch.setattr(converter, "tomllib", None)
    agent_dir = tmp_path / ".claude" / "agents"
    agent_dir.mkdir(parents=True)
    (agent_dir / "tester.md").write_text(
        "---\nname: tester\ndescription: Tests.\n---\nFollow evidence.\n"
    )
    generated = converter.build_agent_files(tmp_path)[0]

    assert converter.validate_agent_content(str(generated.path), generated.content) == []
    assert converter.validate_agent_content("broken", "name = ") != []
