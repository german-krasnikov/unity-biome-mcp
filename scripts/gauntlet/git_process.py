"""Fail-closed Git process boundary for source evidence."""

from __future__ import annotations

import os
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


def git_command(root: Path, *arguments: str) -> list[str]:
    """Build a repository command that never honors replace refs."""
    return ["git", "--no-replace-objects", "-C", str(root), *arguments]


def git_environment() -> dict[str, str]:
    """Return the minimal ambient environment needed by deterministic Git reads."""
    environment = {
        key: value
        for key, value in os.environ.items()
        if key in {"PATH", "PATHEXT", "SYSTEMROOT", "SystemRoot", "WINDIR", "TMP", "TEMP", "TMPDIR"}
    }
    environment.update(
        {
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_NO_REPLACE_OBJECTS": "1",
            "GIT_OPTIONAL_LOCKS": "0",
            "LC_ALL": "C",
        }
    )
    return environment
