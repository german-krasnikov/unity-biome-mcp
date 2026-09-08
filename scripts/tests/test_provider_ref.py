"""TDD coverage for gauntlet.provider_ref.resolve_provider_ref: SHA
passthrough, branch/tag resolution via a mocked `git ls-remote`, and
fail-closed behavior on any ambiguous, missing, timed-out, or malformed
result. No network; scripts/tests lane only (see AI/testing.md).
"""
import json
import subprocess
import sys
from datetime import datetime
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import gauntlet.provider_ref as pr

PIN = {
    "schema_version": 2,
    "package_name": "com.handzlikchris.fastscriptreload",
    "git_url": "https://example.invalid/fork.git?path=/Assets",
    "ref": "master",
    "ref_kind": "branch",
}
SHA = "51140b71d9e5df1de231b33ec20ee089b18bebec"


def _write_pin(tmp_path, **overrides):
    payload = {**PIN, **overrides}
    path = tmp_path / "pin.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


class _FakeCompleted:
    def __init__(self, stdout="", returncode=0, stderr=""):
        self.stdout = stdout
        self.returncode = returncode
        self.stderr = stderr


def test_resolve_provider_ref_sha_passthrough_no_subprocess_call(tmp_path, monkeypatch):
    def _forbidden(*args, **kwargs):
        raise AssertionError("subprocess.run must not be called for an already-resolved SHA pin")

    monkeypatch.setattr(pr.subprocess, "run", _forbidden)
    pin_path = _write_pin(tmp_path, ref=SHA, ref_kind="sha")
    out_path = tmp_path / "resolved.json"

    result = pr.resolve_provider_ref(pin_path, out_path)

    assert result["ref"] == SHA
    assert result["requested_ref"] == SHA
    assert result["resolved_by"] == "passthrough"
    assert json.loads(out_path.read_text(encoding="utf-8"))["ref"] == SHA


def test_resolve_provider_ref_branch_resolves_via_ls_remote(tmp_path, monkeypatch):
    captured = {}

    def _fake_run(command, **kwargs):
        captured["command"] = command
        return _FakeCompleted(stdout=f"{SHA}\trefs/heads/master\n")

    monkeypatch.setattr(pr.subprocess, "run", _fake_run)
    pin_path = _write_pin(tmp_path)
    out_path = tmp_path / "resolved.json"

    result = pr.resolve_provider_ref(pin_path, out_path)

    assert result["ref"] == SHA
    assert result["requested_ref"] == "master"
    assert result["resolved_by"] == "git ls-remote"
    command = captured["command"]
    url_index = command.index("https://example.invalid/fork.git")
    assert command[url_index - 1] == "--"
    assert "https://example.invalid/fork.git?path=/Assets" not in command
    assert "refs/heads/master" in command
    assert "refs/tags/master^{}" in command
    assert json.loads(out_path.read_text(encoding="utf-8"))["ref"] == SHA
    assert datetime.fromisoformat(result["resolved_at_utc"]).tzinfo is not None


def test_resolve_provider_ref_tag_resolves_via_ls_remote(tmp_path, monkeypatch):
    monkeypatch.setattr(
        pr.subprocess,
        "run",
        lambda *args, **kwargs: _FakeCompleted(stdout=f"{SHA}\trefs/tags/v1^{{}}\n"),
    )
    pin_path = _write_pin(tmp_path, ref="v1")

    result = pr.resolve_provider_ref(pin_path, tmp_path / "resolved.json")

    assert result["ref"] == SHA
    assert result["requested_ref"] == "v1"
    assert result["resolved_by"] == "git ls-remote"


def test_resolve_provider_ref_ambiguous_multiple_lines_raises(tmp_path, monkeypatch):
    other_sha = "0" * 40
    monkeypatch.setattr(
        pr.subprocess,
        "run",
        lambda *args, **kwargs: _FakeCompleted(
            stdout=f"{SHA}\trefs/heads/master\n{other_sha}\trefs/tags/master^{{}}\n"
        ),
    )
    pin_path = _write_pin(tmp_path)

    with pytest.raises(pr.ProviderRefError):
        pr.resolve_provider_ref(pin_path, tmp_path / "resolved.json")


def test_resolve_provider_ref_no_match_nonzero_exit_raises(tmp_path, monkeypatch):
    monkeypatch.setattr(
        pr.subprocess,
        "run",
        lambda *args, **kwargs: _FakeCompleted(stdout="", returncode=2, stderr="no matching ref"),
    )
    pin_path = _write_pin(tmp_path, ref="does-not-exist")

    with pytest.raises(pr.ProviderRefError):
        pr.resolve_provider_ref(pin_path, tmp_path / "resolved.json")


def test_resolve_provider_ref_timeout_raises(tmp_path, monkeypatch):
    def _raise_timeout(*args, **kwargs):
        raise subprocess.TimeoutExpired(cmd="git", timeout=pr.GIT_LS_REMOTE_TIMEOUT_SECONDS)

    monkeypatch.setattr(pr.subprocess, "run", _raise_timeout)
    pin_path = _write_pin(tmp_path)

    with pytest.raises(pr.ProviderRefError):
        pr.resolve_provider_ref(pin_path, tmp_path / "resolved.json")


@pytest.mark.parametrize("bad_hash", ["abc123", SHA + "ff"], ids=["short", "long"])
def test_resolve_provider_ref_malformed_hash_length_raises(tmp_path, monkeypatch, bad_hash):
    monkeypatch.setattr(
        pr.subprocess,
        "run",
        lambda *args, **kwargs: _FakeCompleted(stdout=f"{bad_hash}\trefs/heads/master\n"),
    )
    pin_path = _write_pin(tmp_path)

    with pytest.raises(pr.ProviderRefError):
        pr.resolve_provider_ref(pin_path, tmp_path / "resolved.json")


@pytest.mark.parametrize("missing", ["package_name", "git_url", "ref"])
def test_resolve_provider_ref_missing_pin_field_raises(tmp_path, missing):
    payload = {key: value for key, value in PIN.items() if key != missing}
    pin_path = tmp_path / "pin.json"
    pin_path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(pr.ProviderRefError):
        pr.resolve_provider_ref(pin_path, tmp_path / "resolved.json")


# ---------------------------------------------------------------------------
# SHA_RE — public so create_unity_test_worker.py can reuse it (A3) rather
# than duplicating the 40-hex regex.
# ---------------------------------------------------------------------------

def test_sha_re_is_exported_for_reuse_by_other_modules():
    assert pr.SHA_RE.fullmatch(SHA)
    assert not pr.SHA_RE.fullmatch("not-a-sha")
