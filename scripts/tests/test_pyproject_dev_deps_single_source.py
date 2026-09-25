"""Regression test: server/pyproject.toml has exactly one dev-dependency
source of truth.

Plain `uv sync`/`uv run` (no `--extra` flag) installs `[dependency-groups].dev`
by default; CI's `pip install ".[dev]"` (Test/README/tool-quality/badge/docs.yml
jobs) and `uv export --locked --extra dev` (Lint's ruff pin) install/read
`[project.optional-dependencies].dev`. If these two lists diverge, a clean
`uv run pytest ...` (the command this project's own CLAUDE.md documents)
silently drops packages -- see
Plans/Reviews/ci-hygiene-r1/04-uv-dependabot.md, Finding #1.
"""

import tomllib
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PYPROJECT = REPO_ROOT / "server" / "pyproject.toml"


def _load_pyproject() -> dict:
    return tomllib.loads(PYPROJECT.read_text(encoding="utf-8"))


def test_dependency_groups_dev_is_a_self_reference_not_an_independent_list():
    data = _load_pyproject()
    project_name = data["project"]["name"]
    groups = data.get("dependency-groups")
    if groups is None:
        # Absent entirely is also a valid "single source of truth" shape
        # (uv then has no default group to drift from the `dev` extra).
        return
    dev_group = groups.get("dev")
    assert dev_group is not None, "[dependency-groups] present but has no `dev` entry"
    expected = [f"{project_name}[dev]"]
    assert dev_group == expected, (
        "[dependency-groups].dev must be a self-reference onto "
        f"[project.optional-dependencies].dev (expected {expected}), got "
        f"{dev_group!r} -- an independent package list here drifts from "
        '`pip install ".[dev]"` (what CI actually installs) the moment either '
        "list is edited without the other, and plain `uv run`/`uv sync` "
        "silently installs whatever is listed here instead."
    )
