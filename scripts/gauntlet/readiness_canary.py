"""Owned-file restoration and DSL fixture data; never contacts or launches Unity."""

import hashlib
import json
import os
import re
import tempfile
import uuid
from pathlib import Path

FIXTURES = Path(__file__).resolve().parents[1] / "fixtures" / "build_readiness"


class OwnershipConflict(RuntimeError):
    pass


def _digest(content: bytes | None) -> str | None:
    return hashlib.sha256(content).hexdigest() if content is not None else None


class OwnedFileEdit:
    """CAS each write/restore against bytes, retaining a durable original backup.

    This is not an OS filesystem lock: cooperating callers must serialize writes.
    External changes detected before an effect are preserved as a conflict.
    """

    def __init__(self, path: Path, evidence: Path):
        self.path = Path(os.path.abspath(path))
        self._validate_path()
        self.before = self.path.read_bytes() if self.path.exists() else None
        self.expected = self.before
        self.before_stat = self.path.stat() if self.before is not None else None
        self.evidence = evidence / uuid.uuid4().hex
        self.evidence.mkdir(parents=True, exist_ok=False)
        if self.before is not None:
            (self.evidence / "before.bin").write_bytes(self.before)
        self._record("prepared")

    def _validate_path(self):
        if self.path.is_symlink() or self.path.resolve() != self.path:
            raise OwnershipConflict(f"Symlink or retargeted path: {self.path}")
        if self.path.exists() and not self.path.is_file():
            raise OwnershipConflict(f"Not a regular file: {self.path}")
        if not self.path.parent.is_dir():
            raise OwnershipConflict(f"Parent must already be owned/prepared: {self.path.parent}")

    def _check(self):
        self._validate_path()
        actual = self.path.read_bytes() if self.path.exists() else None
        if actual != self.expected:
            raise OwnershipConflict(f"External bytes changed; preserving {self.path}")

    def _record(self, state: str):
        record = {
            "state": state, "path": str(self.path),
            "before_sha256": _digest(self.before), "expected_sha256": _digest(self.expected),
            "before_mtime_ns": self.before_stat.st_mtime_ns if self.before_stat else None,
            "before_mode": self.before_stat.st_mode if self.before_stat else None,
        }
        (self.evidence / "ownership.json").write_text(
            json.dumps(record, indent=2) + "\n", encoding="utf-8"
        )

    def _write(self, content: bytes):
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(dir=self.path.parent, delete=False, suffix=".tmp") as stream:
                temporary = Path(stream.name)
                stream.write(content)
                stream.flush()
                os.fsync(stream.fileno())
            self._check()
            if self.before_stat:
                temporary.chmod(self.before_stat.st_mode)
            os.replace(temporary, self.path)
        finally:
            if temporary is not None and temporary.exists():
                temporary.unlink()

    def replace(self, content: bytes):
        self._check()
        # Persist intended bytes before effect, including a crash between replace/readback.
        (self.evidence / "intended.bin").write_bytes(content)
        self._write(content)
        self.expected = content
        self._check()
        self._record("changed")

    def restore(self):
        self._check()
        if self.before is None:
            if self.path.exists():
                self.path.unlink()
        else:
            self._write(self.before)
            os.utime(self.path, ns=(self.before_stat.st_atime_ns, self.before_stat.st_mtime_ns))
        self.expected = self.before
        self._check()
        self._record("restored")


def render_dsl(object_path: str, expected: int, *, instance_id: int | None = None) -> str:
    if not re.fullmatch(r"/__BiomeReadiness_[0-9a-f]{32}", object_path):
        raise OwnershipConflict("DSL target must be the exact uniquely owned canary path")
    if type(expected) is not int:
        raise OwnershipConflict("Expected semantic value must be an integer")
    if instance_id is not None:
        if type(instance_id) is not int or instance_id == 0:
            raise OwnershipConflict("Preview target needs an exact nonzero integer instance ID")
        object_path = f"#{instance_id}"
    template = (FIXTURES / "sample.playtest").read_text(encoding="utf-8")
    return template.replace("@OWNED_PATH@", object_path).replace("@EXPECTED@", str(expected))


def target_body(value: int) -> bytes:
    if value not in (101, 202):
        raise OwnershipConflict("Canary source values are limited to 101 and 202")
    template = (FIXTURES / "BuildReadinessCanaryTarget.cs").read_bytes()
    return template.replace(b"return 101;", f"return {value};".encode("ascii"))
