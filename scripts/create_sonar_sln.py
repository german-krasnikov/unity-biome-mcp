#!/usr/bin/env python3
"""Create sonar.sln at repo root by copying UnityMCP .csproj files from unity-test-project/.

Root cause fix: Unity-generated .csproj files live in unity-test-project/, but their
<Compile Include> entries point to unity-plugin/Editor/ via absolute paths. SonarScanner
for .NET sets each module's base dir to the .csproj directory (unity-test-project/), so
unity-plugin/Editor/ is outside every module's scope → Roslyn analysis discarded → text
scan only.

Fix: copy .csproj files to the repo root. Module base dir becomes repo root, which
encompasses unity-plugin/Editor/ → Roslyn data attaches → full C# analysis.

Usage: python3 scripts/create_sonar_sln.py DEDUP.sln [OUTPUT.sln]
"""
import re
import shutil
import sys
from pathlib import Path

_PLUGIN_RE = re.compile(r"^UnityMCP\.")
_PROJECT_LINE = re.compile(
    r'Project\("[^"]+"\)\s*=\s*"([^"]+)",\s*"([^"]+)",\s*"([^"]+)"'
)
_SLN_HEADER = """\
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
"""


def main(dedup_sln: str, output_sln: str = "sonar.sln") -> int:
    sln_path = Path(dedup_sln)
    sln_dir = sln_path.parent
    root = Path(".")

    projects: list[tuple[str, str, str]] = []
    for line in sln_path.read_text(encoding="utf-8-sig").splitlines():
        m = _PROJECT_LINE.match(line.strip())
        if not m:
            continue
        name, rel_path, guid = m.group(1), m.group(2), m.group(3)
        if not _PLUGIN_RE.match(name):
            continue
        src = sln_dir / rel_path
        if not src.exists():
            print(f"  SKIP {name}: {src} not found")
            continue
        dest = root / f"sonar-{name}.csproj"
        shutil.copy2(src, dest)
        projects.append((name, dest.name, guid))
        print(f"  {src.name} -> {dest.name}")

    if not projects:
        print("No UnityMCP projects found in sln")
        return 1

    lines = [_SLN_HEADER]
    for name, path, guid in projects:
        lines.append(
            f'Project("{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}")'
            f' = "{name}", "{path}", "{guid}"'
        )
        lines.append("EndProject")
    lines += [
        "Global",
        "\tGlobalSection(SolutionConfigurationPlatforms) = preSolution",
        "\t\tDebug|Any CPU = Debug|Any CPU",
        "\tEndGlobalSection",
        "\tGlobalSection(ProjectConfigurationPlatforms) = postSolution",
    ]
    for _, _, guid in projects:
        lines.append(f"\t\t{guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU")
        lines.append(f"\t\t{guid}.Debug|Any CPU.Build.0 = Debug|Any CPU")
    lines += ["\tEndGlobalSection", "EndGlobal"]

    Path(output_sln).write_text("\n".join(lines), encoding="utf-8")
    print(f"Created {output_sln} with {len(projects)} projects")
    return 0


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(f"Usage: {sys.argv[0]} DEDUP.sln [OUTPUT.sln]")
        sys.exit(1)
    sys.exit(main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "sonar.sln"))
