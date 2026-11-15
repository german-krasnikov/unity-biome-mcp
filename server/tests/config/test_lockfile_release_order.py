"""Portable lockfile release-order regressions."""


from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


def test_release_lock_closes_fd_before_unlink_on_windows(monkeypatch, tmp_path: Path) -> None:
    """Windows cannot unlink an open lockfile; release must close before unlink."""
    import unity_mcp.lockfile as lockfile

    lock_path = tmp_path / "server-9500-123.lock"
    lock_path.write_text("123\n", encoding="utf-8")
    fake_fd = 123
    closed: list[int] = []
    unlinked: list[str] = []

    def fake_close(fd: int) -> None:
        closed.append(fd)

    def fake_unlink(path: str) -> None:
        assert closed == [fake_fd]
        unlinked.append(path)

    monkeypatch.setattr(lockfile, "_IS_WIN", True)
    monkeypatch.setattr(lockfile, "_unlock", lambda fd: None)
    monkeypatch.setattr(lockfile.os, "close", fake_close)
    monkeypatch.setattr(lockfile.os, "unlink", fake_unlink)
    monkeypatch.setitem(lockfile._lock_paths, fake_fd, str(lock_path))

    lockfile.release_lock(fake_fd)

    assert closed == [fake_fd]
    assert unlinked == [str(lock_path)]
