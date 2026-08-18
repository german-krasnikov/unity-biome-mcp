"""Fail-closed source-to-wheel metadata contract regressions."""


import struct
import sys
from email.parser import BytesParser
from pathlib import Path

import pytest

TESTS = Path(__file__).resolve().parent
sys.path.insert(0, str(TESTS.parent))
sys.path.insert(0, str(TESTS))

from gauntlet import package_archives
from gauntlet.package_archives import inspect_package_archive
from gauntlet.package_contracts import (
    PACKAGE_SOURCE_PATHS,
    PackageArchiveError,
)
from gauntlet.python_package_contract import (
    source_python_contract,
    wheel_python_contract,
)
from gauntlet.source_packages import (
    SourcePackageError,
    parse_source_package_identities,
)
from gauntlet.wheel_metadata import validate_wheel_metadata
from gauntlet_test_fixtures import write_wheel


def _project(**overrides: object) -> dict[str, object]:
    project: dict[str, object] = {
        "name": "unity-biome-mcp",
        "version": "1.27.0",
        "description": "MCP server",
        "license": {"text": "MIT"},
        "authors": [{"name": "Release Bot", "email": "release@example.invalid"}],
        "classifiers": ["Programming Language :: Python :: 3"],
        "requires-python": ">=3.14",
        "dependencies": ["demo>=1"],
        "optional-dependencies": {"dev": ["pytest>=8"]},
        "urls": {"Homepage": "https://example.invalid/project"},
        "scripts": {"unity-biome-mcp": "unity_mcp.cli:main"},
    }
    project.update(overrides)
    return project


def _metadata(*extra_headers: str, body: str = "") -> object:
    lines = [
        "Metadata-Version: 2.4",
        "Name: unity-biome-mcp",
        "Version: 1.27.0",
        "Summary: MCP server",
        "Project-URL: Homepage, https://example.invalid/project",
        "Author-email: Release Bot <release@example.invalid>",
        "License: MIT",
        "Classifier: Programming Language :: Python :: 3",
        "Requires-Python: >=3.14",
        "Requires-Dist: demo>=1",
        "Provides-Extra: dev",
        "Requires-Dist: pytest>=8; extra == 'dev'",
        *extra_headers,
    ]
    return BytesParser().parsebytes(("\n".join(lines) + f"\n\n{body}").encode())


def _entry_points() -> bytes:
    return b"[console_scripts]\nunity-biome-mcp = unity_mcp.cli:main\n"


def test_contract_full_source_and_wheel_metadata_match() -> None:
    assert source_python_contract(_project()) == wheel_python_contract(
        _metadata(),
        _entry_points(),
    )


def test_contract_unsourced_summary_changes_identity() -> None:
    source = source_python_contract(_project(description="Source summary"))

    assert source != wheel_python_contract(_metadata(), _entry_points())


@pytest.mark.parametrize("header", ["X-Injected: true", "Summary: duplicate"])
def test_wheel_contract_unbound_or_duplicate_header_rejected(header: str) -> None:
    with pytest.raises(PackageArchiveError, match="metadata|header|duplicate|unsupported"):
        wheel_python_contract(_metadata(header), _entry_points())


def test_wheel_contract_unsourced_description_body_rejected() -> None:
    with pytest.raises(PackageArchiveError, match="body|description"):
        wheel_python_contract(_metadata(body="injected long description\n"), _entry_points())


@pytest.mark.parametrize(
    "payload",
    [
        b"Wheel-Version: 1.0\nGenerator: attacker 9.9\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
        b"Wheel-Version: 1.0\nGenerator: hatchling 1.31.0\nX-Injected: yes\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
        b"Wheel-Version: 1.0\nGenerator: hatchling 1.31.0\nGenerator: hatchling 1.31.0\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
    ],
)
def test_wheel_metadata_arbitrary_duplicate_or_unknown_fields_rejected(payload: bytes) -> None:
    with pytest.raises(PackageArchiveError, match="WHEEL|Generator|field|duplicate"):
        validate_wheel_metadata(payload, "unity_biome_mcp-1.27.0-py3-none-any.whl")


def test_wheel_metadata_actual_hatchling_generator_is_accepted() -> None:
    validate_wheel_metadata(
        b"Wheel-Version: 1.0\nGenerator: hatchling 1.31.0\nRoot-Is-Purelib: true\nTag: py3-none-any\n",
        "unity_biome_mcp-1.27.0-py3-none-any.whl",
    )


@pytest.mark.parametrize(
    "payload",
    [
        b"# no entry points\n",
        b"[DEFAULT]\ncommand = package:main\n",
    ],
)
def test_wheel_contract_nonempty_but_semantically_empty_entry_points_rejected(
    payload: bytes,
) -> None:
    with pytest.raises(PackageArchiveError, match="entry_points|entry point"):
        wheel_python_contract(_metadata(), payload)


@pytest.mark.parametrize(
    "payload",
    [
        b"# NDA: secret-unbound-byte-string\n[console_scripts]\nunity-biome-mcp = unity_mcp.cli:main\n",
        b"[console_scripts]\n\nunity-biome-mcp = unity_mcp.cli:main\n",
    ],
)
def test_wheel_contract_entry_points_unbound_raw_bytes_rejected(payload: bytes) -> None:
    with pytest.raises(PackageArchiveError, match="entry_points|canonical"):
        wheel_python_contract(_metadata(), payload)


def test_contract_pep685_equivalent_extra_names_match() -> None:
    source = source_python_contract(
        _project(**{"optional-dependencies": {"my_extra": ["pytest>=8"]}})
    )
    wheel = _metadata(
        "Provides-Extra: my-extra",
        "Requires-Dist: pytest>=8; extra == 'my-extra'",
    )
    del wheel["Provides-Extra"]
    del wheel["Requires-Dist"]
    wheel["Requires-Dist"] = "demo>=1"
    wheel["Provides-Extra"] = "my-extra"
    wheel["Requires-Dist"] = "pytest>=8; extra == 'my-extra'"

    assert source == wheel_python_contract(wheel, _entry_points())


def test_source_contract_pep685_colliding_extra_names_rejected() -> None:
    with pytest.raises(PackageArchiveError, match="duplicate|extra"):
        source_python_contract(
            _project(
                **{
                    "optional-dependencies": {
                        "my_extra": ["one>=1"],
                        "my-extra": ["two>=1"],
                    }
                }
            )
        )


def test_source_package_malformed_requirement_uses_source_error() -> None:
    payloads = {
        PACKAGE_SOURCE_PATHS["python_wheel"]: (
            b'[project]\nname = "unity-biome-mcp"\nversion = "1.27.0"\n'
            b'dependencies = [">=1"]\n'
        ),
        PACKAGE_SOURCE_PATHS["unity_editor_upm"]: (
            b'{"name":"com.unity-biome-mcp.editor","version":"1.27.0"}'
        ),
        PACKAGE_SOURCE_PATHS["unity_reload_upm"]: (
            b'{"name":"com.unity-biome-mcp.reload","version":"0.1.4"}'
        ),
    }

    with pytest.raises(SourcePackageError, match="requirement|pyproject"):
        parse_source_package_identities(payloads)


def test_wheel_unsupported_compression_is_normalized(tmp_path: Path) -> None:
    path = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    write_wheel(path)
    snapshot = bytearray(path.read_bytes())
    local = snapshot.index(b"PK\x03\x04")
    central = snapshot.index(b"PK\x01\x02")
    struct.pack_into("<H", snapshot, local + 8, 99)
    struct.pack_into("<H", snapshot, central + 10, 99)

    with pytest.raises(PackageArchiveError, match="compression|wheel archive"):
        inspect_package_archive("python_wheel", bytes(snapshot), path.name)


def test_wheel_raw_library_exception_is_normalized(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    path = tmp_path / "unity_biome_mcp-1.27.0-py3-none-any.whl"
    write_wheel(path)

    def fail_open(*_args: object, **_kwargs: object) -> None:
        raise RuntimeError("raw zip failure")

    monkeypatch.setattr(package_archives.zipfile, "ZipFile", fail_open)
    with pytest.raises(PackageArchiveError, match="wheel archive"):
        inspect_package_archive("python_wheel", path.read_bytes(), path.name)
