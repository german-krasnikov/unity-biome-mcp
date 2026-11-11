"""Tier 0 content lock of what dsl/ generates, without Node: dsl/content-lock.json states the sha256 of every DSL source and of every file printed from it.

    python scripts/dsl_content_lock.py generate   write the lock from the files on disk (scripts/dsl.sh generate runs it after biome generate)
    python scripts/dsl_content_lock.py check      exit 1 when a locked file is not as the lock states (server/tests/test_dsl_content_lock.py runs the same check)

The banner of a generated file states the digest of its source and nothing of its body (server/tests/test_dsl_fresh.py), so a hand edit that keeps the banner, a
changed contract or examples file and the machine rows dsl/decisions/*.json (no banner) were found only by `scripts/dsl.sh check`, which needs Node. The lock finds them.
It cannot find a lock that was refreshed by hand around a stale file: that the files are what the compiler prints is still `scripts/dsl.sh check`.
"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOCK = "dsl/content-lock.json"
# A region of a hand-written file, `path#name`: the text between <!-- biome:begin name --> and <!-- biome:end name -->
REGIONS = ("AI/playtest-dsl.md#verbs", "docs/tools/index.md#catalog")
# The banner of a file that the kit printed: two comment lines, the second one states the digest of the source
BANNER = re.compile(rb"\A(?:#|//) Generated from [^\n]*\n(?:#|//) source-sha256=[0-9a-f]{64}\r?\n")


def digest(root: Path, key: str) -> str | None:
    """The sha256 of the file `key`, or of its region for `path#name`, as a banner digests its source (no BOM, LF line ends); None when the file or the region is gone."""
    name, _, region = key.partition("#")
    path = root / name
    if not path.is_file():
        return None
    data = re.sub(rb"\r\n?", b"\n", path.read_bytes().removeprefix(b"\xef\xbb\xbf"))
    if region:
        _, begin, rest = data.partition(f"<!-- biome:begin {region} -->\n".encode())
        data, end, _ = rest.partition(f"<!-- biome:end {region} -->".encode())
        if not (begin and end):
            return None
    return hashlib.sha256(data).hexdigest()


def is_locked(root: Path, name: str) -> bool:
    """Whether the file `name` is locked: a DSL source or row (not prose), the manifest that owns a generated directory, or a file that starts with the banner."""
    path = root / name
    if name == LOCK or not path.is_file():
        return False
    if (name.startswith("dsl/") and path.suffix != ".md") or path.name == ".biome-files":
        return True
    with path.open("rb") as file:
        return BANNER.match(file.read(400)) is not None


def discover(root: Path, names: list[str]) -> list[str]:
    """The keys to lock among the file names: the regions, and each name that is_locked."""
    return sorted({*REGIONS, *(name for name in names if is_locked(root, name))})


def locked(root: Path = ROOT) -> dict[str, str]:
    """{key: sha256} as dsl/content-lock.json states it."""
    return json.loads((root / LOCK).read_text(encoding="utf-8"))["files"]


def stale(root: Path = ROOT) -> list[str]:
    """The keys of the lock whose file (or region) is gone or has another digest than the lock states."""
    return [key for key, want in locked(root).items() if (got := digest(root, key)) is None or got != want]


def tracked(root: Path) -> list[str]:
    """The names of the files that git lists in `root`: tracked, or untracked and not ignored (a file that `biome generate` has just printed)."""
    listed = subprocess.run(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"], cwd=root, check=True, capture_output=True)
    return listed.stdout.decode("utf-8").split("\0")


def generate(root: Path, names: list[str]) -> int:
    """Write the lock for the keys that `discover` takes from `names` and return how many it locks; a region that is gone is an error, never a null."""
    files = {key: digest(root, key) for key in discover(root, names)}
    if gone := [key for key, got in files.items() if got is None]:
        raise SystemExit(f"cannot lock {gone}: no such file or region")
    header = "by scripts/dsl_content_lock.py generate (scripts/dsl.sh generate runs it); do not edit"
    (root / LOCK).write_text(json.dumps({"generated": header, "files": files}, indent=2) + "\n", encoding="utf-8", newline="\n")
    return len(files)


def main(argv: list[str]) -> int:
    if argv[1:] == ["generate"]:
        print(f"{LOCK}: {generate(ROOT, tracked(ROOT))} files")
        return 0
    if argv[1:] == ["check"]:
        pinned, bad = locked(), stale()
        for key in bad:
            print(f"stale: {key} is not as scripts/dsl.sh generate left it; run scripts/dsl.sh generate", file=sys.stderr)
        print(f"{LOCK}: {len(pinned) - len(bad)} of {len(pinned)} files as locked")
        return 1 if bad else 0
    print("usage: dsl_content_lock.py generate | check", file=sys.stderr)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv))
