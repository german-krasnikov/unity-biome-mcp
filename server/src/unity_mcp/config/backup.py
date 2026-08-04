"""Timestamped backup for config files."""
import pathlib
import shutil
from datetime import datetime


def backup(path: pathlib.Path) -> pathlib.Path | None:
    """Create timestamped backup: foo.json → foo.json.2026-06-19T08-30-00.bak"""
    if not path.exists():
        return None
    ts = datetime.now().strftime("%Y-%m-%dT%H-%M-%S")
    bak = path.with_name(f"{path.name}.{ts}.bak")
    shutil.copy2(path, bak)
    return bak
