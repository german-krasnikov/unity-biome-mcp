import pathlib

import pytest

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent


@pytest.fixture
def repo_root() -> pathlib.Path:
    return REPO_ROOT


@pytest.fixture
def fixtures_dir() -> pathlib.Path:
    return pathlib.Path(__file__).parent / "fixtures"


@pytest.fixture
def sample_changelog(fixtures_dir: pathlib.Path) -> str:
    return (fixtures_dir / "sample_changelog.md").read_text(encoding="utf-8")


@pytest.fixture
def real_changelog(repo_root: pathlib.Path) -> str:
    return (repo_root / "CHANGELOG.md").read_text(encoding="utf-8")
