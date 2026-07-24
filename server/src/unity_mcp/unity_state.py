"""Unity Editor state file reader.

Unity writes ~/.unity-biome-mcp/state/port-{port}.state with:
  line 0: state name (ready/compiling/reloading/compile_failed)
  line 1: unix timestamp when state changed
  line 2: Unity process PID
  line 3: epoch (NEW — sync_unity epoch; absent in pre-0.21 state files)
"""
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

from .constants import SESSION_TIMEOUT as _STALE_SECONDS
from .paths import unity_mcp_dir


@dataclass
class UnityState:
    state: str
    timestamp: float
    epoch: int = 0  # backward-compatible: 0 when line 3 absent

    @property
    def is_busy(self) -> bool:
        return self.state in ("compiling", "reloading", "restarting")

    @property
    def is_stale(self) -> bool:
        return (time.time() - self.timestamp) > _STALE_SECONDS


def read_state_for_port(port: int) -> Optional[UnityState]:
    """Read Unity state from ~/.unity-biome-mcp/state/port-{port}.state."""
    path = unity_mcp_dir() / "state" / f"port-{port}.state"
    try:
        lines = path.read_text(encoding="utf-8", errors="replace").strip().split("\n")
        if len(lines) < 2:
            return None
        epoch = int(lines[3]) if len(lines) >= 4 else 0  # backward-compatible
        return UnityState(state=lines[0], timestamp=float(lines[1]), epoch=epoch)
    except (OSError, ValueError, IndexError):
        return None
