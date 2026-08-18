
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet.json_io import (  # noqa: E402
    JsonFileError,
    atomic_write_json,
    load_json_object,
    parse_json_object,
)


def test_parse_json_object_valid_utf8_object_returns_value() -> None:
    value = parse_json_object(b'{"enabled":true,"nested":{"count":2}}', source="policy.json")

    assert value == {"enabled": True, "nested": {"count": 2}}


def test_parse_json_object_duplicate_root_key_rejects_deterministically() -> None:
    with pytest.raises(
        JsonFileError,
        match=r"^policy\.json: JSON object contains a duplicate key$",
    ):
        parse_json_object(b'{"id":1,"id":2}', source="policy.json")


def test_parse_json_object_duplicate_nested_key_rejects_deterministically() -> None:
    with pytest.raises(
        JsonFileError,
        match=r"^catalog\.json: JSON object contains a duplicate key$",
    ):
        parse_json_object(
            b'{"profile":{"scenario":"first","scenario":"second"}}',
            source="catalog.json",
        )


def test_parse_json_object_invalid_utf8_rejects_with_boundary_error() -> None:
    with pytest.raises(
        JsonFileError,
        match=r"^evidence\.json: JSON is not valid UTF-8$",
    ):
        parse_json_object(b'{"value":"\xff"}', source="evidence.json")


def test_parse_json_object_malformed_json_reports_stable_location() -> None:
    with pytest.raises(
        JsonFileError,
        match=r"^receipt\.json: JSON is invalid at line 2, column 1$",
    ):
        parse_json_object(b'{"value":\n}', source="receipt.json")


def test_parse_json_object_non_object_root_rejects_deterministically() -> None:
    with pytest.raises(
        JsonFileError,
        match=r"^manifest\.json: JSON root must be an object$",
    ):
        parse_json_object(b'[1,2,3]', source="manifest.json")


def test_load_json_object_delegates_strict_duplicate_detection(tmp_path: Path) -> None:
    path = tmp_path / "policy.json"
    path.write_bytes(b'{"profile":{"id":"a","id":"b"}}')

    with pytest.raises(
        JsonFileError,
        match=r"^policy\.json: JSON object contains a duplicate key$",
    ):
        load_json_object(path)


@pytest.mark.parametrize(
    "constant",
    [b"NaN", b"Infinity", b"-Infinity", b"1e1000000"],
)
def test_parse_json_object_rejects_non_finite_numbers(constant: bytes) -> None:
    with pytest.raises(JsonFileError, match="non-finite"):
        parse_json_object(b'{"value":' + constant + b"}", source="evidence.json")


def test_load_json_object_enforces_limit_on_opened_snapshot(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    path = tmp_path / "evidence.json"
    path.write_bytes(b'{"payload":"' + b"x" * 100 + b'"}')
    original_stat = Path.stat

    def stale_small_stat(candidate: Path, *args: object, **kwargs: object) -> object:
        if candidate == path:
            metadata = original_stat(candidate, *args, **kwargs)
            return SimpleNamespace(
                st_size=1,
                st_mode=metadata.st_mode,
                st_dev=metadata.st_dev,
                st_ino=metadata.st_ino,
            )
        return original_stat(candidate, *args, **kwargs)

    monkeypatch.setattr(Path, "stat", stale_small_stat)
    with pytest.raises(JsonFileError, match="size limit"):
        load_json_object(path, max_bytes=10)


def test_atomic_json_writer_rejects_non_finite_numbers(tmp_path: Path) -> None:
    path = tmp_path / "evidence.json"

    with pytest.raises(JsonFileError, match="serializable"):
        atomic_write_json(path, {"value": float("nan")})

    assert not path.exists()
