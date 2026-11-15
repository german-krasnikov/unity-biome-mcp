#!/usr/bin/env python3
"""Remove duplicate project names from Unity-generated .sln files.

Unity sometimes generates .sln files with duplicate project names
(e.g. Unity.Timeline appears twice), which causes MSBuild to fail
with MSB5004. This script removes the second occurrence of any
duplicate project name.

Usage: python scripts/dedup_sln.py INPUT.sln OUTPUT.sln
"""
import re
import sys


def dedup_sln(input_path: str, output_path: str) -> int:
    with open(input_path, encoding="utf-8-sig") as f:
        text = f.read()
    lines = text.split("\n")
    seen: set[str] = set()
    out: list[str] = []
    skip = False
    removed = 0

    for line in lines:
        m = re.match(r'Project\(.+\)\s*=\s*"([^"]+)"', line)
        if m:
            name = m.group(1)
            if name in seen:
                skip = True
                removed += 1
                continue
            seen.add(name)
        if skip and line.strip() == "EndProject":
            skip = False
            continue
        if not skip:
            out.append(line)

    with open(output_path, "w", encoding="utf-8") as f:
        f.write("\n".join(out))
    print(f"Deduplicated: {len(seen)} unique projects, {removed} duplicates removed")
    return removed


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(f"Usage: {sys.argv[0]} INPUT.sln OUTPUT.sln")
        sys.exit(1)
    dedup_sln(sys.argv[1], sys.argv[2])
