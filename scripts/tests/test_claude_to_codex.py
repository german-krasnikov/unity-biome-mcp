"""Regression tests for ClientSkills Claude -> Codex conversion."""

from __future__ import annotations

import importlib.util
import pathlib
import sys


def load_converter(repo_root: pathlib.Path):
    script = repo_root / "unity-plugin" / "ClientSkills" / "scripts" / "claude_to_codex.py"
    spec = importlib.util.spec_from_file_location("claude_to_codex", script)
    assert spec is not None
    assert spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


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
