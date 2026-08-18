"""Materialize an overlay-free source tree from one exact Git commit."""


import shutil
import stat
import subprocess
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import BinaryIO

from gauntlet.git_process import git_command, git_environment

_MAX_TREE_FILES = 200_000
_MAX_TREE_BYTES = 2 * 1024 * 1024 * 1024
_COPY_CHUNK_BYTES = 1024 * 1024


class SourceSnapshotError(ValueError):
    """Raised when an exact disposable source tree cannot be proven."""


@dataclass(frozen=True, slots=True)
class _TreeFile:
    mode: str
    object_id: str
    relative_path: str


def materialize_source_snapshot(
    source_root: Path,
    *,
    expected_head_sha: str,
    destination: Path,
) -> None:
    """Extract only tracked bytes from ``expected_head_sha`` into destination."""
    root = _source_root(source_root)
    expected = _object_id(expected_head_sha)
    _require_exact_head(root, expected)
    output = _empty_destination(destination)
    try:
        files = _tree_files(root, expected)
        _materialize_blobs(root, files, output)
        _require_exact_head(root, expected)
    except Exception:
        shutil.rmtree(output, ignore_errors=True)
        raise


def _source_root(path: Path) -> Path:
    try:
        metadata = path.lstat()
        resolved = path.resolve(strict=True)
    except OSError as exc:
        raise SourceSnapshotError("source repository is not accessible") from exc
    if not stat.S_ISDIR(metadata.st_mode):
        raise SourceSnapshotError("source repository must be a real directory")
    if Path(_git(resolved, "rev-parse", "--show-toplevel")).resolve() != resolved:
        raise SourceSnapshotError("source repository must be the Git worktree root")
    return resolved


def _object_id(value: str) -> str:
    normalized = value.lower()
    invalid = any(character not in "0123456789abcdef" for character in normalized)
    if len(normalized) not in {40, 64} or invalid:
        raise SourceSnapshotError("expected source commit is not a Git object ID")
    return normalized


def _require_exact_head(root: Path, expected: str) -> None:
    try:
        commit = _git(root, "rev-parse", "--verify", f"{expected}^{{commit}}")
        head = _git(root, "rev-parse", "HEAD")
    except SourceSnapshotError as exc:
        raise SourceSnapshotError("expected source commit is unavailable") from exc
    if commit != expected or head != expected:
        raise SourceSnapshotError("source HEAD differs from the expected commit")


def _empty_destination(path: Path) -> Path:
    try:
        if path.exists():
            metadata = path.lstat()
            if not stat.S_ISDIR(metadata.st_mode) or any(path.iterdir()):
                raise SourceSnapshotError("source snapshot destination must be an empty directory")
        else:
            path.mkdir(parents=True)
        return path.resolve(strict=True)
    except OSError as exc:
        raise SourceSnapshotError("source snapshot destination is not writable") from exc


def _tree_files(root: Path, commit: str) -> tuple[_TreeFile, ...]:
    try:
        listing = _git_bytes(root, "ls-tree", "-rz", "--full-tree", commit)
        files = tuple(_parse_tree_record(record) for record in listing.split(b"\0") if record)
    except (UnicodeError, ValueError) as exc:
        raise SourceSnapshotError("exact Git tree cannot be decoded") from exc
    if not files:
        raise SourceSnapshotError("exact Git tree contains no files")
    if len(files) > _MAX_TREE_FILES:
        raise SourceSnapshotError("exact Git tree contains too many files")
    return files


def _parse_tree_record(record: bytes) -> _TreeFile:
    metadata, raw_path = record.split(b"\t", 1)
    mode, object_type, object_id = metadata.decode("ascii").split(" ", 2)
    relative_path = raw_path.decode("utf-8")
    relative = PurePosixPath(relative_path)
    if (
        not relative_path
        or relative.is_absolute()
        or "\\" in relative_path
        or ":" in relative_path
        or any(part in {"", ".", ".."} for part in relative.parts)
    ):
        raise SourceSnapshotError("exact Git tree contains an unsafe path")
    if mode not in {"100644", "100755"} or object_type != "blob":
        raise SourceSnapshotError("exact Git tree contains a non-regular entry")
    return _TreeFile(mode, _object_id(object_id), relative.as_posix())


def _materialize_blobs(root: Path, files: tuple[_TreeFile, ...], destination: Path) -> None:
    process = _cat_file_process(root)
    total = 0
    try:
        assert process.stdin is not None
        assert process.stdout is not None
        for item in files:
            process.stdin.write(f"{item.object_id}\n".encode("ascii"))
            process.stdin.flush()
            observed_id, size = _blob_header(process.stdout.readline(), item.object_id)
            if observed_id != item.object_id:
                raise SourceSnapshotError("Git returned an unexpected blob object")
            total += size
            if total > _MAX_TREE_BYTES:
                raise SourceSnapshotError("exact Git tree exceeds the extraction limit")
            _write_blob(process.stdout, destination, item, size)
        process.stdin.close()
        if process.wait(timeout=30) != 0:
            raise SourceSnapshotError("Git blob materialization failed")
    except Exception:
        process.kill()
        process.wait(timeout=30)
        raise


def _cat_file_process(root: Path) -> subprocess.Popen[bytes]:
    try:
        return subprocess.Popen(
            git_command(root, "cat-file", "--batch"),
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            env=git_environment(),
        )
    except OSError as exc:
        raise SourceSnapshotError("Git blob reader could not start") from exc


def _blob_header(header: bytes, expected_id: str) -> tuple[str, int]:
    try:
        object_id, object_type, raw_size = header.rstrip(b"\n").decode("ascii").split(" ", 2)
        size = int(raw_size)
    except (UnicodeError, ValueError) as exc:
        raise SourceSnapshotError("Git blob header is invalid") from exc
    if object_id != expected_id or object_type != "blob" or size < 0:
        raise SourceSnapshotError("Git blob header does not match the exact tree")
    return object_id, size


def _write_blob(stream: BinaryIO, destination: Path, item: _TreeFile, size: int) -> None:
    target = destination.joinpath(*PurePosixPath(item.relative_path).parts)
    target.parent.mkdir(parents=True, exist_ok=True)
    remaining = size
    with target.open("xb") as output:
        while remaining:
            chunk = stream.read(min(remaining, _COPY_CHUNK_BYTES))
            if not chunk:
                raise SourceSnapshotError("Git blob payload is truncated")
            output.write(chunk)
            remaining -= len(chunk)
    if stream.read(1) != b"\n":
        raise SourceSnapshotError("Git blob payload terminator is invalid")
    target.chmod(0o755 if item.mode == "100755" else 0o644)


def _git(root: Path, *arguments: str) -> str:
    try:
        result = subprocess.run(
            git_command(root, *arguments),
            check=True,
            capture_output=True,
            text=True,
            timeout=30,
            env=git_environment(),
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise SourceSnapshotError("required Git source operation failed") from exc
    return result.stdout.strip()


def _git_bytes(root: Path, *arguments: str) -> bytes:
    try:
        result = subprocess.run(
            git_command(root, *arguments),
            check=True,
            capture_output=True,
            timeout=120,
            env=git_environment(),
        )
    except (OSError, subprocess.SubprocessError) as exc:
        raise SourceSnapshotError("required Git tree operation failed") from exc
    return result.stdout
