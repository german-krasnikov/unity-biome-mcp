import json
import logging
import pathlib
import time
import urllib.request
from unity_mcp import __version__
from unity_mcp.config.resolver import GIT_INSTALL_URL
from unity_mcp.paths import unity_mcp_dir

CACHE_FILE = unity_mcp_dir() / "update_cache.json"
CACHE_TTL = 86400  # 24 hours
GITHUB_URL = "https://api.github.com/repos/german-krasnikov/unity-biome-mcp/releases/latest"
TIMEOUT = 3

log = logging.getLogger("unity_mcp")


def check_for_update() -> str | None:
    """Check PyPI for newer version. Returns new version string or None.
    Uses 24h file cache. Silent on any network/IO error."""
    cache = _read_cache()
    if cache and time.time() - cache.get("ts", 0) < CACHE_TTL:
        latest = cache.get("latest", "")
        return latest if _is_newer(latest, __version__) else None

    try:
        req = urllib.request.Request(GITHUB_URL, headers={"Accept": "application/vnd.github+json"})
        with urllib.request.urlopen(req, timeout=TIMEOUT) as resp:
            data = json.loads(resp.read())
            latest = data["tag_name"].lstrip("v")
    except Exception:
        return None

    _write_cache({"ts": time.time(), "latest": latest})
    return latest if _is_newer(latest, __version__) else None


def _is_newer(remote: str, local: str) -> bool:
    """Return True if remote semver > local. Strips pre-release suffixes (e.g. '0.0.0-dev')."""
    try:
        r = tuple(int(x) for x in remote.split("-")[0].split("."))
        l = tuple(int(x) for x in local.split("-")[0].split("."))
        return r > l
    except Exception:
        return False


def _read_cache() -> dict | None:
    """Read cache file. Returns None if missing or corrupt."""
    try:
        return json.loads(CACHE_FILE.read_text(encoding="utf-8"))
    except Exception:
        return None


def _write_cache(data: dict) -> None:
    """Write cache file. Creates parent dir. Silent on error."""
    try:
        CACHE_FILE.parent.mkdir(parents=True, exist_ok=True)
        CACHE_FILE.write_text(json.dumps(data), encoding="utf-8")
    except Exception:
        pass


def format_update_banner(new_version: str) -> str:
    msg = f"  Update available: {__version__} → {new_version}  (run: uvx --reinstall --from {GIT_INSTALL_URL} unity-biome-mcp)"
    return f"\n{msg}\n"
