#!/usr/bin/env python3
"""Convert JetBrains InspectCode XML to SonarQube Generic Issue JSON (new format).

Replaces dotnet-reqube which fails on Linux due to Windows-style path handling.
Outputs the new Clean Code format (rules + impacts) to avoid SonarCloud deprecation warnings.

Usage: python scripts/inspectcode_to_sonar.py inspectcode-report.xml -o sonarqube.json [--base-dir unity-test-project]
"""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath, PureWindowsPath

# These TypeIds indicate reliability bugs, not code smells
_BUG_IDS = re.compile(
    r"PossibleNullReferenceException|NullableWarningSuppressionIsUsed"
    r"|CSharpErrors|CSharpWarnings"
    r"|AccessToDisposedClosure|PossibleMultipleEnumeration"
)

# Pure style/formatting noise — skip entirely
_NOISE_IDS = re.compile(
    r"^(Arrange|Typo)|Spaces|Indent|LineBreaks|Style|BadSymbol"
    r"|ArgumentsStyle|BuiltInTypeReference"
)

_RAW_SEV_TO_IMPACT = {
    "ERROR":      ("RELIABILITY",    "HIGH"),
    "WARNING":    ("MAINTAINABILITY","MEDIUM"),
    "SUGGESTION": ("MAINTAINABILITY","LOW"),
    "HINT":       ("MAINTAINABILITY","INFO"),
}


def _impacts(type_id: str, raw_severity: str) -> list[dict]:
    sq, sev = _RAW_SEV_TO_IMPACT.get(raw_severity, ("MAINTAINABILITY", "MEDIUM"))
    if _BUG_IDS.search(type_id):
        sq = "RELIABILITY"
        sev = "HIGH" if raw_severity in ("WARNING", "ERROR") else "MEDIUM"
    return [{"softwareQuality": sq, "severity": sev}]


def convert(xml_path: str, base_dir: str = "") -> dict:
    tree = ET.parse(xml_path)
    root = tree.getroot()

    type_map: dict[str, dict] = {}
    for t in root.findall(".//IssueType"):
        tid = t.get("Id", "")
        raw_sev = t.get("Severity", "WARNING")
        if _NOISE_IDS.search(tid):
            continue
        type_map[tid] = {
            "name": t.get("Description", tid),
            "impacts": _impacts(tid, raw_sev),
        }

    rules = [
        {"id": tid, "name": info["name"], "engineId": "inspectcode", "impacts": info["impacts"]}
        for tid, info in type_map.items()
    ]

    issues = []
    for project in root.findall(".//Issues/Project"):
        for issue in project.findall("Issue"):
            type_id = issue.get("TypeId", "unknown")
            if type_id not in type_map:
                continue  # filtered noise or unknown rule

            raw_path = issue.get("File", "")
            path = str(PurePosixPath(PureWindowsPath(raw_path)))
            if base_dir and path.startswith(base_dir + "/"):
                path = path[len(base_dir) + 1:]
            elif path.startswith("../"):
                path = str(PurePosixPath(base_dir, path).resolve().relative_to(Path.cwd()))
            elif Path(path).is_absolute() and base_dir:
                try:
                    path = str(PurePosixPath(path).relative_to(Path(base_dir).resolve().parent))
                except ValueError:
                    pass

            line = max(1, int(issue.get("Line", "1") or "1"))
            msg = issue.get("Message", type_map[type_id]["name"])

            issues.append({
                "ruleId": type_id,
                "primaryLocation": {
                    "message": msg,
                    "filePath": path,
                    "textRange": {"startLine": line},
                },
            })

    return {"rules": rules, "issues": issues}


def main() -> int:
    import argparse

    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("input", help="InspectCode XML report")
    p.add_argument("-o", "--output", default="sonarqube-csharp.json")
    p.add_argument("--base-dir", default="", help="Strip this prefix from file paths")
    args = p.parse_args()

    if not Path(args.input).exists():
        print(f"Input not found: {args.input}")
        return 1

    result = convert(args.input, args.base_dir)
    Path(args.output).write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"Converted {len(result['issues'])} issues → {args.output} ({len(result['rules'])} rules)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
