"""generate_schema_page.py — render MCP tool schema as a MkDocs page.

Merges exported tool definitions with toolsmith quality scores and generates
docs/tools-schema/index.md with parameter tables, scores, and JSON schemas.

Usage:
  cd server && uv run python ../scripts/generate_schema_page.py
  # With pre-built reports:
  python scripts/generate_schema_page.py --tools reports/tools.json --scores reports/toolsmith.json --out docs/tools-schema/index.md
"""

import argparse
import json
import pathlib
import sys


def _load_tools(path: pathlib.Path) -> list[dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    return data.get("tools", data) if isinstance(data, dict) else data


def _load_scores(path: pathlib.Path | None) -> dict[str, dict]:
    if not path or not path.exists():
        return {}
    text = path.read_text(encoding="utf-8").strip()
    if not text:
        return {}
    data = json.loads(text)
    result = {}
    for t in data.get("tools", []):
        if "tool_name" in t:
            result[t["tool_name"]] = {
                "score": t.get("score", 0),
                "risk_level": t.get("risk_level", "low"),
                "issues": t.get("issues", []),
            }
        elif "tool" in t:
            name = t["tool"].get("name", "")
            errs = t.get("error_count", 0)
            warns = t.get("warning_count", 0)
            result[name] = {
                "score": max(0, 100 - errs * 10 - warns * 2),
                "risk_level": "high" if errs >= 3 else "medium" if errs >= 1 or warns >= 5 else "low",
                "issues": t.get("findings", []),
            }
    return result


def _score_badge(score: int) -> str:
    if score >= 80:
        return f"🟢 {score}/100"
    if score >= 60:
        return f"🟡 {score}/100"
    return f"🔴 {score}/100"


def _risk_badge(risk: str) -> str:
    badges = {"low": "🟢 low", "medium": "🟡 medium", "high": "🔴 high", "critical": "⛔ critical"}
    return badges.get(risk, risk)


def render_schema_page(tools: list[dict], scores: dict[str, dict]) -> str:
    lines = [
        "---",
        "hide:",
        "  - navigation",
        "---",
        "",
        "# MCP Tool Schema",
        "",
        f"> **{len(tools)} registered tools** — auto-generated from server tool definitions.",
        "",
    ]

    if scores:
        avg_score = sum(s.get("score", 0) for s in scores.values()) / len(scores)
        lines.append(f"> Quality: **{avg_score:.1f}/100** avg score"
                     f" · [Glama](https://glama.ai/mcp/servers/german-krasnikov/unity-biome-mcp/schema)")
        lines.append("")

    # Summary table
    lines += [
        "## Overview",
        "",
        "| Tool | Score | Risk | Description |",
        "|------|-------|------|-------------|",
    ]
    for tool in sorted(tools, key=lambda t: t.get("name", "")):
        name = tool.get("name", "?")
        desc = tool.get("description", "").split("\n")[0].strip()
        if len(desc) > 80:
            desc = desc[:77] + "..."
        sc = scores.get(name, {})
        score_str = _score_badge(sc["score"]) if "score" in sc else "—"
        risk_str = _risk_badge(sc["risk_level"]) if "risk_level" in sc else "—"
        lines.append(f"| [`{name}`](#{name}) | {score_str} | {risk_str} | {desc} |")
    lines += ["", "---", ""]

    # Per-tool details
    lines.append("## Tool Details")
    lines.append("")

    for tool in sorted(tools, key=lambda t: t.get("name", "")):
        name = tool.get("name", "?")
        desc = tool.get("description", "").replace("\n", " ").strip()
        schema = tool.get("inputSchema", {})
        props = schema.get("properties", {})
        required = set(schema.get("required", []))
        sc = scores.get(name, {})

        lines.append(f"### `{name}`")
        lines.append("")

        if sc:
            parts = []
            if "score" in sc:
                parts.append(_score_badge(sc["score"]))
            if "risk_level" in sc:
                parts.append(f"Risk: {_risk_badge(sc['risk_level'])}")
            if parts:
                lines.append(" · ".join(parts))
                lines.append("")

        lines.append(desc)
        lines.append("")

        if props:
            lines.append("**Parameters:**")
            lines.append("")
            lines.append("| Parameter | Type | Required | Description |")
            lines.append("|-----------|------|----------|-------------|")
            for pname, pinfo in sorted(props.items()):
                ptype = pinfo.get("type", "any")
                if "enum" in pinfo:
                    ptype = " \\| ".join(f"`{v}`" for v in pinfo["enum"])
                preq = "✓" if pname in required else ""
                pdesc = pinfo.get("description", "").replace("\n", " ").replace("|", "\\|").strip()
                if pdesc and len(pdesc) > 120:
                    pdesc = pdesc[:117] + "..."
                if pinfo.get("default") is not None:
                    pdesc += f" (default: `{pinfo['default']}`)"
                lines.append(f"| `{pname}` | {ptype} | {preq} | {pdesc} |")
            lines.append("")

        # Issues from toolsmith
        issues = sc.get("issues", [])
        if issues:
            lines.append("<details>")
            lines.append(f"<summary>{len(issues)} quality issues</summary>")
            lines.append("")
            for iss in issues:
                sev = iss.get("severity", "info")
                msg = iss.get("message", "")
                fix = iss.get("suggestion", "")
                line = f"- **{sev}**: {msg}"
                if fix:
                    line += f" — *{fix}*"
                lines.append(line)
            lines.append("")
            lines.append("</details>")
            lines.append("")

        if props:
            lines.append("<details>")
            lines.append("<summary>JSON Schema</summary>")
            lines.append("")
            lines.append("```json")
            lines.append(json.dumps(schema, indent=2))
            lines.append("```")
            lines.append("")
            lines.append("</details>")
            lines.append("")

        lines.append("---")
        lines.append("")

    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tools", type=pathlib.Path)
    parser.add_argument("--scores", type=pathlib.Path)
    parser.add_argument("--out", type=pathlib.Path,
                        default=pathlib.Path("docs/tools-schema/index.md"))
    args = parser.parse_args()

    repo = pathlib.Path(__file__).parent.parent

    if args.tools:
        tools_path = args.tools
    else:
        tmp = repo / "reports" / "_schema_tools.json"
        tmp.parent.mkdir(parents=True, exist_ok=True)
        import subprocess
        subprocess.run(
            [sys.executable, str(repo / "scripts" / "export_tools.py"),
             "--format", "toolsmith", "--out", str(tmp)],
            check=True,
        )
        tools_path = tmp

    tools = _load_tools(tools_path)
    scores = _load_scores(args.scores)
    page = render_schema_page(tools, scores)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(page, encoding="utf-8")
    print(f"Generated {args.out} with {len(tools)} tools, {len(scores)} scores")


if __name__ == "__main__":
    main()
