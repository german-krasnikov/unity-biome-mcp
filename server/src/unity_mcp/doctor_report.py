"""Human-readable report formatting for doctor check results."""

from dataclasses import dataclass

from unity_mcp.config.resolver import GIT_INSTALL_URL


@dataclass
class CheckResult:
    name: str
    ok: bool
    detail: str
    fix_cmd: str = ""
    auto_fixable: bool = False


USER_MESSAGES = {
    "disconnected": f"MCP server isn't running. Start: uvx --from {GIT_INSTALL_URL} unity-biome-mcp",
    "compiling": "Unity is compiling. Wait and retry.",
    "dlls_stale": "Plugin code outdated. Run: Assets → Reimport All",
    "frozen": "Unity stopped responding. Check Editor.log.",
}


def format_report(results: list[CheckResult]) -> str:
    lines = []
    for r in results:
        marker = "✓" if r.ok else "✗"
        lines.append(f"  {marker} {r.name}: {r.detail}")
        if not r.ok and r.fix_cmd:
            lines.append(f"    → {r.fix_cmd}")

    passed = sum(1 for r in results if r.ok)
    total = len(results)
    summary = f"{passed}/{total} checks passed"
    lines.insert(0, f"Unity Biome MCP Doctor — {summary}")
    lines.insert(1, "")
    lines.append("")
    lines.append(summary)
    return "\n".join(lines)
