"""Global persistent configuration for unity-biome-mcp.

Loaded from ~/.unity-biome-mcp/global-config.json. Never raises — returns
defaults on missing or malformed file. Save is atomic (tmp + rename).
"""
import json
import os
import pathlib
from dataclasses import dataclass, field

CONFIG_PATH: pathlib.Path = pathlib.Path.home() / ".unity-biome-mcp" / "global-config.json"


@dataclass
class GlobalConfig:
    idle_timeout_min: int = 30           # minutes before idle suspension/exit
    idle_auto_suspend: bool = True       # suspend bridge instead of hard exit when idle
    bridge_terminate_orphan: bool = True # exit when parent process dies
    bridge_orphan_grace_min: int = 2     # minutes to wait after orphan detected
    _from_file: bool = field(default=False, repr=False)

    @classmethod
    def load(cls) -> "GlobalConfig":
        """Load config from disk. Never raises — returns defaults on any error."""
        try:
            data = json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
            return cls(
                idle_timeout_min=int(data.get("idle_timeout_min", 30)),
                idle_auto_suspend=bool(data.get("idle_auto_suspend", True)),
                bridge_terminate_orphan=bool(data.get("bridge_terminate_orphan", True)),
                bridge_orphan_grace_min=int(data.get("bridge_orphan_grace_min", 2)),
                _from_file=True,
            )
        except Exception:
            return cls()

    def save(self) -> None:
        """Atomic write to CONFIG_PATH. Creates parent dir if needed."""
        CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
        data = {
            "idle_timeout_min": self.idle_timeout_min,
            "idle_auto_suspend": self.idle_auto_suspend,
            "bridge_terminate_orphan": self.bridge_terminate_orphan,
            "bridge_orphan_grace_min": self.bridge_orphan_grace_min,
        }
        tmp = CONFIG_PATH.with_suffix(".tmp")
        tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
        os.replace(tmp, CONFIG_PATH)

    def effective_idle_timeout_s(self) -> tuple[int, str]:
        """Return (seconds, source) where source is 'env'/'config'/'default'."""
        env = os.environ.get("UNITY_MCP_IDLE_TIMEOUT")
        if env is not None:
            try:
                return int(env), "env"
            except ValueError:
                pass
        if self._from_file:
            return self.idle_timeout_min * 60, "config"
        return 30 * 60, "default"

    def effective_auto_suspend(self) -> tuple[bool, str]:
        env = os.environ.get("UNITY_MCP_AUTO_SUSPEND")
        if env is not None:
            return env not in ("0", "false", "False"), "env"
        if self._from_file:
            return self.idle_auto_suspend, "config"
        return True, "default"

    def effective_terminate_orphan(self) -> tuple[bool, str]:
        env = os.environ.get("UNITY_MCP_TERMINATE_ORPHAN")
        if env is not None:
            return env not in ("0", "false", "False"), "env"
        if self._from_file:
            return self.bridge_terminate_orphan, "config"
        return True, "default"

    def effective_orphan_grace_s(self) -> tuple[int, str]:
        env = os.environ.get("UNITY_MCP_ORPHAN_GRACE_MIN")
        if env is not None:
            try:
                return int(env) * 60, "env"
            except ValueError:
                pass
        if self._from_file:
            return self.bridge_orphan_grace_min * 60, "config"
        return 2 * 60, "default"
