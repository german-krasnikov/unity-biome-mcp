"""Auto-restart MCP server when UPM plugin is newer than __version__."""
import asyncio
import logging
import os
import shutil
import sys
from dataclasses import dataclass

from unity_mcp import __version__
from unity_mcp._update_check import _is_newer
from unity_mcp.config.resolver import GIT_INSTALL_URL

logger = logging.getLogger("unity_mcp")


@dataclass
class _UpdateResult:
    triggered: bool
    reason: str  # "not_needed" | "no_uvx" | "already_running" | "not_uvx_install" | "started"


def _default_is_uvx_install() -> bool:
    """True when running as a uvx-managed tool (not a local venv)."""
    if os.environ.get("UV_TOOL_DIR"):
        return True
    argv0 = sys.argv[0] if sys.argv else ""
    if "uvx" in argv0:
        return True
    exe = sys.executable or ""
    return ".venv" not in exe and "venv" not in exe


class ServerUpdater:
    """Detects plugin/server version mismatch and triggers uvx --reinstall + exit."""

    def __init__(
        self,
        install_url: str,
        *,
        version_fn=lambda: __version__,
        which_fn=shutil.which,
        subprocess_fn=asyncio.create_subprocess_exec,
        exit_fn=os._exit,
        is_uvx_install_fn=_default_is_uvx_install,
    ):
        self._install_url = install_url
        self._version_fn = version_fn
        self._which_fn = which_fn
        self._subprocess_fn = subprocess_fn
        self._exit_fn = exit_fn
        self._is_uvx_install_fn = is_uvx_install_fn
        self._updating = False

    async def maybe_update(self, plugin_version: str) -> _UpdateResult:
        """Check if plugin is newer; if so reinstall and exit. Non-blocking guard."""
        if self._updating:
            return _UpdateResult(triggered=False, reason="already_running")

        if self._which_fn("uvx") is None:
            logger.warning(
                "Server update skipped: uvx not found in PATH. "
                "Run: uvx --reinstall --from %s unity-biome-mcp",
                self._install_url,
            )
            return _UpdateResult(triggered=False, reason="no_uvx")

        if not self._is_uvx_install_fn():
            logger.info(
                "Server update skipped: not a uvx install (local venv detected). "
                "Update manually if needed."
            )
            return _UpdateResult(triggered=False, reason="not_uvx_install")

        if not self._is_update_needed(plugin_version):
            return _UpdateResult(triggered=False, reason="not_needed")

        self._updating = True
        current = self._version_fn()
        logger.info(
            "Server update: %s → %s, running uvx --reinstall...", current, plugin_version
        )

        success = await self._run_uvx_reinstall()
        if success:
            await self._exit_for_restart()
            return _UpdateResult(triggered=True, reason="started")

        self._updating = False
        logger.error(
            "Server update failed. Run manually: "
            "uvx --reinstall --from %s unity-biome-mcp",
            self._install_url,
        )
        return _UpdateResult(triggered=False, reason="reinstall_failed")

    def _is_update_needed(self, plugin_version: str) -> bool:
        """True if plugin_version > __version__ (semver)."""
        if not plugin_version:
            return False
        return _is_newer(plugin_version, self._version_fn())

    async def _run_uvx_reinstall(self) -> bool:
        """Run: uvx --reinstall --from <url> unity-biome-mcp. Returns True on exit 0."""
        try:
            proc = await self._subprocess_fn(
                "uvx", "--reinstall", "--from", self._install_url, "unity-biome-mcp"
            )
            code = await proc.wait()
            return code == 0
        except Exception as exc:
            logger.error("uvx reinstall error: %s", exc)
            return False

    async def _exit_for_restart(self) -> None:
        """Hard exit — Claude Code auto-restarts the MCP server process."""
        logger.info(
            "Server update complete. Restarting... "
            "(if Claude Code does not reconnect automatically, run /mcp)"
        )
        self._exit_fn(0)


_updater = ServerUpdater(install_url=GIT_INSTALL_URL)
