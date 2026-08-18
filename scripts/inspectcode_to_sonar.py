#!/usr/bin/env python3
"""Convert JetBrains InspectCode XML to SonarQube Generic Issue JSON.

Replaces dotnet-reqube which fails on Linux due to Windows-style path handling.

Usage: python scripts/inspectcode_to_sonar.py inspectcode-report.xml -o sonarqube.json [--base-dir unity-test-project]
"""
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath, PureWindowsPath

_SEVERITY_MAP = {
    "HINT": "INFO",
    "SUGGESTION": "MINOR",
    "WARNING": "MAJOR",
    "ERROR": "CRITICAL",
}


def convert(xml_path: str, base_dir: str = "") -> dict:
    tree = ET.parse(xml_path)
    root = tree.getroot()

    type_map = {}
    for t in root.findall(".//IssueType"):
        type_map[t.get("Id")] = {
            "severity": _SEVERITY_MAP.get(t.get("Severity", "WARNING"), "MAJOR"),
            "description": t.get("Description", ""),
        }

    issues = []
    for project in root.findall(".//Issues/Project"):
        for issue in project.findall("Issue"):
            raw_path = issue.get("File", "")
            path = str(PurePosixPath(PureWindowsPath(raw_path)))
            if base_dir and path.startswith(base_dir + "/"):
                path = path[len(base_dir) + 1 :]

            type_id = issue.get("TypeId", "unknown")
            info = type_map.get(type_id, {"severity": "MAJOR", "description": ""})
            line = int(issue.get("Line", "1") or "1")

            issues.append({
                "engineId": "inspectcode",
                "ruleId": type_id,
                "severity": info["severity"],
                "type": "CODE_SMELL",
                "primaryLocation": {
                    "message": issue.get("Message", info["description"]),
                    "filePath": path,
                    "textRange": {"startLine": max(1, line)},
                },
            })

    return {"issues": issues}


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
    print(f"Converted {len(result['issues'])} issues to {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
