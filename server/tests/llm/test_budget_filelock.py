"""TDD tests for budget/_filelock.py — cross-platform context manager (B6)."""
import threading
from pathlib import Path

from unity_mcp.budget._filelock import locked


def test_locked_acquires_and_releases(tmp_path):
    """Context manager acquires lock on enter and releases on exit."""
    data_file = tmp_path / "budget.json"
    sentinel = tmp_path / "budget.json.lock"

    with locked(data_file):
        # During the lock, sentinel file must exist
        assert sentinel.exists()
    # After the lock, sentinel still exists (file persists, lock released)
    assert sentinel.exists()


# no-assert: crash guard
def test_locked_releases_on_exception(tmp_path):
    """Lock is released even when body raises an exception."""
    data_file = tmp_path / "budget.json"

    try:
        with locked(data_file):
            raise ValueError("intentional error")
    except ValueError:
        pass

    # Should be able to acquire again — lock was released
    with locked(data_file):
        pass  # no exception = lock acquired successfully


def test_locked_creates_parent_dirs(tmp_path):
    """locked() creates parent directories if they don't exist."""
    data_file = tmp_path / "nested" / "dir" / "budget.json"

    with locked(data_file):
        assert (tmp_path / "nested" / "dir").exists()
