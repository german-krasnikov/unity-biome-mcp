#!/usr/bin/env python3
"""Convert JetBrains InspectCode XML to SonarQube Generic Issue JSON.

Replaces dotnet-reqube which fails on Linux due to Windows-style path handling.

Usage: python scripts/inspectcode_to_sonar.py inspectcode-report.xml -o sonarqube.json [--base-dir unity-test-project]
"""
import contextlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath, PureWindowsPath

_BUG_IDS = re.compile(
    r"PossibleNullReferenceException|NullableWarningSuppressionIsUsed"
    r"|CSharpErrors|CSharpWarnings"
    r"|AccessToDisposedClosure|PossibleMultipleEnumeration"
)

_NOISE_IDS = re.compile(
    r"^(Arrange|Typo)|Spaces|Indent|LineBreaks|Style|BadSymbol"
    r"|ArgumentsStyle|BuiltInTypeReference"
)

_SEVERITY_MAP = {
    "ERROR": "CRITICAL",
    "WARNING": "MAJOR",
    "SUGGESTION": "MINOR",
    "HINT": "INFO",
}


def convert(xml_path: str, base_dir: str = "") -> dict:
    tree = ET.parse(xml_path)
    root = tree.getroot()

    type_map: dict[str, dict] = {}
    for t in root.findall(".//IssueType"):
        tid = t.get("Id", "")
        raw_sev = t.get("Severity", "WARNING")
        if _NOISE_IDS.search(tid):
            continue
        is_bug = bool(_BUG_IDS.search(tid))
        type_map[tid] = {
            "description": t.get("Description") or tid,
            "severity": _SEVERITY_MAP.get(raw_sev, "MAJOR"),
            "type": "BUG" if is_bug else "CODE_SMELL",
        }

    issues = []
    for project in root.findall(".//Issues/Project"):
        for issue in project.findall("Issue"):
            type_id = issue.get("TypeId", "unknown")
            if type_id not in type_map:
                continue

            raw_path = issue.get("File", "")
            path = str(PurePosixPath(PureWindowsPath(raw_path)))
            if base_dir:
                if path.startswith(base_dir + "/"):
                    path = path[len(base_dir) + 1:]
                elif path.startswith("../"):
                    path = str((Path(base_dir) / path).resolve().relative_to(Path.cwd()))
                elif Path(path).is_absolute():
                    with contextlib.suppress(ValueError):
                        path = str(Path(path).relative_to(Path(base_dir).resolve().parent))

            info = type_map[type_id]
            issues.append({
                "engineId": "inspectcode",
                "ruleId": type_id,
                "severity": info["severity"],
                "type": info["type"],
                "primaryLocation": {
                    "message": issue.get("Message") or info["description"],
                    "filePath": path,
                    "textRange": {"startLine": max(1, int(issue.get("Line", "1") or "1"))},
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
    print(f"Converted {len(result['issues'])} issues → {args.output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
