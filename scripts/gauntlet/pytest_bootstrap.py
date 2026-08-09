"""Isolated pytest bootstrap with pre-imported trusted plugins."""

from __future__ import annotations

import sys
from pathlib import Path
from typing import TYPE_CHECKING

import pytest
import pytest_asyncio.plugin as asyncio_plugin
import pytest_timeout as timeout_plugin

if TYPE_CHECKING:
    from collections.abc import Sequence


def main(argv: Sequence[str] | None = None) -> int:
    arguments = list(sys.argv[1:] if argv is None else argv)
    if len(arguments) < 4:
        print("trusted pytest bootstrap arguments are incomplete", file=sys.stderr)
        return 2
    harness_root, source_package, source_tests, *pytest_arguments = arguments
    sys.path[:0] = [
        str(Path(harness_root).resolve(strict=True)),
        str(Path(source_package).resolve(strict=True)),
        str(Path(source_tests).resolve(strict=True)),
    ]
    from gauntlet import pytest_attestation

    return pytest.main(
        pytest_arguments,
        plugins=(asyncio_plugin, timeout_plugin, pytest_attestation),
    )


if __name__ == "__main__":
    raise SystemExit(main())
