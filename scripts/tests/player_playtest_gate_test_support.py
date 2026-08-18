
from pathlib import Path  # noqa: TC003

from gauntlet.receipts import content_hash
from gauntlet_test_fixtures import write_json

PLAYER_MATRICES = ("Linux", "macOS", "Windows")
SUCCESS_STEPS = 14
GRAPHICS_STEPS = 4
EXPECTED_FAILURE_STEPS = (
    "ASSERT /GridPlayer|GridPlayer|PosZ == 999",
    "WAIT_UNTIL /GridPlayer|GridPlayer|IsMoving == True TIMEOUT 0.1",
)


def write_player_playtest_evidence_set(root: Path, head_sha: str) -> dict[str, Path]:
    player_root = root / "player-evidence"
    player_root.mkdir()
    paths: dict[str, Path] = {}
    for matrix in PLAYER_MATRICES:
        path = player_root / f"player-playtest-evidence-{matrix}.json"
        write_player_playtest_evidence(path, head_sha=head_sha, matrix=matrix)
        paths[f"player_evidence_{matrix.lower()}"] = path
    return paths


def write_player_playtest_evidence(path: Path, *, head_sha: str, matrix: str, run_id: str = "123") -> None:
    payload: dict[str, object] = {
        "schema_version": 1,
        "head_sha": head_sha,
        "github": {
            "run_id": run_id,
            "run_attempt": "1",
            "runner_os": matrix,
            "matrix_name": matrix,
        },
        "player": {
            "path": f"artifacts/player-smoke/{matrix}",
            "kind": "directory",
            "sha256": "a" * 64,
            "file_count": 2,
            "size_bytes": 42,
        },
        "receipts": {
            "build_log": {"path": "player-build.log", "sha256": "b" * 64, "size_bytes": 8},
            "success": _receipt("player-playtest", SUCCESS_STEPS, 0, ()),
            "expected_failure": _receipt(
                "player-playtest-failure",
                5,
                len(EXPECTED_FAILURE_STEPS),
                EXPECTED_FAILURE_STEPS,
            ),
            "graphics": None,
        },
    }
    payload["evidence_sha256"] = content_hash(payload)
    write_json(path, payload)


def player_evidence_paths(paths: dict[str, Path]) -> tuple[Path, ...]:
    return tuple(paths[f"player_evidence_{matrix.lower()}"] for matrix in PLAYER_MATRICES)


def _receipt(stem: str, steps: int, failed: int, failed_steps: tuple[str, ...]) -> dict[str, object]:
    return {
        "json_path": f"{stem}.json",
        "json_sha256": "c" * 64,
        "junit_path": f"{stem}.xml",
        "junit_sha256": "d" * 64,
        "passed": steps - failed,
        "failed": failed,
        "steps": steps,
        "failed_steps": list(failed_steps),
    }
