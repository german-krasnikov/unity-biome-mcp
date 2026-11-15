"""Unified task runner for unity-biome-mcp.

Usage:
    nox -s preflight       # full local check (lint + tests + docs checks)
    nox -s lint            # ruff (version pinned from server/uv.lock)
    nox -s test            # all Python test lanes
    nox -s dotnet          # all pure .NET test projects
    nox -s docs_build      # mkdocs build --strict
    nox -l                 # list all sessions
"""
import functools
import subprocess
from pathlib import Path

import nox

nox.options.reuse_existing_virtualenvs = True

ROOT = Path(__file__).parent
SERVER = ROOT / "server"
DOTNET_TESTS = ROOT / "unity-plugin" / "Tests~"

# ── helpers ──────────────────────────────────────────────────────────────────

@functools.lru_cache
def _ruff_pin() -> str:
    """Extract the pinned ruff version from server/uv.lock — same source as CI."""
    result = subprocess.run(
        ["uv", "export", "--locked", "--extra", "dev", "--no-hashes",
         "--no-header", "--no-emit-project"],
        capture_output=True, text=True, cwd=str(SERVER), check=True,
    )
    pins = [line for line in result.stdout.splitlines() if line.startswith("ruff==")]
    if len(pins) != 1:
        raise RuntimeError(f"expected 1 ruff pin in server/uv.lock, found {len(pins)}")
    return pins[0]


def _server_venv(session: nox.Session) -> None:
    """Install server[dev] into session venv using uv."""
    session.install("-e", str(SERVER) + "[dev]")


# ── Group 1: Python ─────────────────────────────────────────────────────────

@nox.session(python="3.14")
def lint(session: nox.Session) -> None:
    """Ruff lint (version pinned from server/uv.lock)."""
    session.install(_ruff_pin())
    session.run(
        "ruff", "check",
        "--config", str(SERVER / "pyproject.toml"),
        str(SERVER / "src/"), str(ROOT / "install/"), str(ROOT / "scripts/"),
    )


@nox.session(python="3.14")
def test_server(session: nox.Session) -> None:
    """Server unit tests (no Unity)."""
    _server_venv(session)
    session.chdir(str(SERVER))
    session.run(
        "python", "-m", "pytest", "tests", "-m", "not live", "-q",
        env={"PYTHONWARNDEFAULTENCODING": "1"},
    )


@nox.session(python="3.14")
def test_install(session: nox.Session) -> None:
    """Installer unit tests."""
    _server_venv(session)
    session.run(
        "python", "-m", "pytest", str(ROOT / "install" / "tests"), "-q",
        env={"PYTHONWARNDEFAULTENCODING": "1"},
    )


@nox.session(python="3.14")
def test_scripts(session: nox.Session) -> None:
    """Repository scripts tests."""
    _server_venv(session)
    session.run(
        "python", "-m", "pytest", str(ROOT / "scripts" / "tests"), "-q",
        env={"PYTHONWARNDEFAULTENCODING": "1"},
    )


@nox.session(python="3.14")
def readme_check(session: nox.Session) -> None:
    """Verify README counts and generated assets are current."""
    _server_venv(session)
    session.run("python", str(ROOT / "scripts" / "update_readme.py"), "--check-facts")
    session.run("python", str(ROOT / "scripts" / "update_readme.py"), "--check")


@nox.session(python="3.14")
def version_check(session: nox.Session) -> None:
    """Verify version sync across package.json / pyproject.toml / CHANGELOG."""
    _server_venv(session)
    session.run("python", str(ROOT / "scripts" / "sync_versions.py"), "--check")
    session.run("python", str(ROOT / "scripts" / "sync_changelog.py"), "--check")


@nox.session(python="3.14")
def skills_check(session: nox.Session) -> None:
    """Validate client skills freshness."""
    _server_venv(session)
    session.run("python", str(ROOT / "scripts" / "check_skills_freshness.py"))


@nox.session(python="3.14")
def quality(session: nox.Session) -> None:
    """Export tools + compute quality delta."""
    _server_venv(session)
    session.run("python", str(ROOT / "scripts" / "export_tools.py"))
    session.run("python", str(ROOT / "scripts" / "quality_delta.py"))


# ── Group 1b: Docs ──────────────────────────────────────────────────────────

@nox.session(python="3.14")
def docs_build(session: nox.Session) -> None:
    """Build docs with mkdocs (strict mode)."""
    session.install("mkdocs", "mkdocs-material", "mkdocs-minify-plugin")
    session.chdir(str(ROOT))
    session.run("mkdocs", "build", "--strict")


@nox.session(python="3.14")
def readme_update(session: nox.Session) -> None:
    """Regenerate README stats, badges, SVGs."""
    _server_venv(session)
    session.run("python", str(ROOT / "scripts" / "update_readme.py"), "--all")


@nox.session(python="3.14")
def changelog_sync(session: nox.Session) -> None:
    """Mirror CHANGELOG.md → unity-plugin/CHANGELOG.md."""
    _server_venv(session)
    session.run("python", str(ROOT / "scripts" / "sync_changelog.py"))


# ── Group 2: Pure .NET ──────────────────────────────────────────────────────

_DOTNET_PROJECTS = {
    "playtest_core":        DOTNET_TESTS / "Pure" / "UnityMCP.Playtest.Core.Tests.csproj",
    "assembly_freshness":   DOTNET_TESTS / "AssemblyFreshness" / "UnityMCP.AssemblyFreshness.Tests.csproj",
    "source_patch":         DOTNET_TESTS / "SourcePatchReadiness" / "UnityMCP.SourcePatchReadiness.Tests.csproj",
    "decisions":            DOTNET_TESTS / "Decisions" / "UnityMCP.Decisions.Tests.csproj",
    "chat_turn":            DOTNET_TESTS / "ChatTurn" / "ChatTurn.Tests.csproj",
    "playtest_grammar":     DOTNET_TESTS / "PlaytestGrammar" / "UnityMCP.Playtest.Grammar.Tests.csproj",
}


def _dotnet_test(session: nox.Session, csproj: Path) -> None:
    session.run("dotnet", "test", str(csproj), "-c", "Release", "--nologo", external=True)


@nox.session(python=False, name="dotnet_playtest_core")
def dotnet_playtest_core(session: nox.Session) -> None:
    """Pure .NET: PlaytestCore."""
    _dotnet_test(session, _DOTNET_PROJECTS["playtest_core"])


@nox.session(python=False, name="dotnet_assembly_freshness")
def dotnet_assembly_freshness(session: nox.Session) -> None:
    """Pure .NET: AssemblyFreshness."""
    _dotnet_test(session, _DOTNET_PROJECTS["assembly_freshness"])


@nox.session(python=False, name="dotnet_source_patch")
def dotnet_source_patch(session: nox.Session) -> None:
    """Pure .NET: SourcePatchReadiness."""
    _dotnet_test(session, _DOTNET_PROJECTS["source_patch"])


@nox.session(python=False, name="dotnet_decisions")
def dotnet_decisions(session: nox.Session) -> None:
    """Pure .NET: Decisions."""
    _dotnet_test(session, _DOTNET_PROJECTS["decisions"])


@nox.session(python=False, name="dotnet_chat_turn")
def dotnet_chat_turn(session: nox.Session) -> None:
    """Pure .NET: ChatTurn."""
    _dotnet_test(session, _DOTNET_PROJECTS["chat_turn"])


@nox.session(python=False, name="dotnet_playtest_grammar")
def dotnet_playtest_grammar(session: nox.Session) -> None:
    """Pure .NET: PlaytestGrammar."""
    _dotnet_test(session, _DOTNET_PROJECTS["playtest_grammar"])


# ── Composite sessions ──────────────────────────────────────────────────────

@nox.session(python=False)
def test(session: nox.Session) -> None:
    """All Python test lanes."""
    for s in ("test_server", "test_install", "test_scripts"):
        session.notify(s)


@nox.session(python=False)
def dotnet(session: nox.Session) -> None:
    """All pure .NET test projects."""
    for name in _DOTNET_PROJECTS:
        session.notify(f"dotnet_{name}")


@nox.session(python=False)
def preflight(session: nox.Session) -> None:
    """Full local preflight: lint + tests + version/readme checks (excludes docs_build for speed)."""
    for s in ("lint", "test_server", "test_install", "test_scripts",
              "readme_check", "version_check", "skills_check"):
        session.notify(s)
