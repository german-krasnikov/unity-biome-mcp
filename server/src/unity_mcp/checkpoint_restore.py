"""T19: restore_files() — write before-state blobs back to disk. Atomic writes."""

import contextlib
import os
from dataclasses import dataclass, field
from pathlib import Path

from .changeset_store import ContentStore  # noqa: TC001
from .checkpoint import ConflictInfo, FileManifest  # noqa: TC001


@dataclass
class RestoreResult:
    restored: list[str] = field(default_factory=list)
    skipped: list[str] = field(default_factory=list)
    conflicts: list[ConflictInfo] = field(default_factory=list)


def restore_files(
    manifest: FileManifest,
    after_refs: dict[str, str],
    store: ContentStore,
    force: bool = False,
) -> RestoreResult:
    """Restore before-state for all paths in manifest.

    1. Detect conflicts (paths where user edited after agent turn).
    2. If conflict and not force: add to result.conflicts, skip write.
    3. Else: write store blob to disk atomically (tmp + os.replace).
    """
    from .changeset import ContentRef
    from .checkpoint_manifest import detect_conflicts

    conflicts = detect_conflicts(manifest, after_refs, store)
    conflict_set = {c.path for c in conflicts}

    result = RestoreResult(conflicts=[] if force else list(conflicts))

    # C3: pre-check — all blobs must exist before any write; no partial restores
    missing = [path for path, h in manifest.entries if not store.has(ContentRef(h))]
    if missing:
        result.skipped.extend(missing)
        return result

    for path, before_hash in manifest.entries:
        if path in conflict_set and not force:
            continue  # skip — already in result.conflicts

        before_ref = ContentRef(before_hash)
        content = store.get(before_ref)
        if content is None:
            result.skipped.append(path)
            continue

        p = Path(path)
        p.parent.mkdir(parents=True, exist_ok=True)
        tmp = p.parent / f".tmp-{p.name}"
        try:
            tmp.write_text(content, encoding="utf-8")
            os.replace(tmp, p)
            result.restored.append(path)
        except OSError:
            result.skipped.append(path)
            with contextlib.suppress(OSError):
                tmp.unlink()

    return result
