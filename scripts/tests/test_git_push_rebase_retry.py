"""scripts/git_push_rebase_retry.sh: retry `git pull --rebase` + `git push`
against races between CI bots pushing generated data (badges, test results,
coverage) to the same branch (see .github/workflows/ci-python.yml and
unity-tests.yml "Commit ..." steps).

Uses only local bare repos under tmp_path -- no network, no dependency on the
developer machine's global git config (each repo/clone gets an explicit
user.name/user.email and gpgsign=false).
"""
import os
import subprocess
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "git_push_rebase_retry.sh"

pytestmark = pytest.mark.skipif(
    os.name != "posix",
    reason="git_push_rebase_retry.sh is a POSIX shell script run by ubuntu CI jobs; "
    "Windows `bash` is the WSL launcher",
)

# Shared pre-receive hook: rejects a push while a marker file exists. By
# default it deletes the marker after one rejection (simulates exactly one
# racing push); GIT_TEST_REJECT_PERSIST keeps rejecting forever.
_PRE_RECEIVE_HOOK = """#!/bin/sh
if [ -n "${GIT_TEST_REJECT_MARKER:-}" ] && [ -f "$GIT_TEST_REJECT_MARKER" ]; then
    if [ -z "${GIT_TEST_REJECT_PERSIST:-}" ]; then
        rm -f "$GIT_TEST_REJECT_MARKER"
    fi
    echo "pre-receive: simulated racing push rejection" >&2
    exit 1
fi
exit 0
"""


def _run(cmd: list[str], cwd: Path, env: dict[str, str] | None = None) -> subprocess.CompletedProcess:
    return subprocess.run(
        cmd, cwd=cwd, env=env, capture_output=True, text=True, encoding="utf-8", check=False
    )


def _git(cwd: Path, *args: str) -> subprocess.CompletedProcess:
    result = _run(["git", "-c", "commit.gpgsign=false", *args], cwd=cwd)
    assert result.returncode == 0, f"git {args} failed: {result.stderr}"
    return result


def _init_bare_origin(path: Path) -> Path:
    origin = path / "origin.git"
    _git(path, "init", "--bare", "--initial-branch=master", str(origin))
    hook = origin / "hooks" / "pre-receive"
    hook.write_text(_PRE_RECEIVE_HOOK, encoding="utf-8")
    hook.chmod(0o755)
    return origin


def _clone(origin: Path, dest: Path, name: str) -> Path:
    clone = dest / name
    _git(dest, "clone", str(origin), str(clone))
    _git(clone, "config", "user.name", f"{name} bot")
    _git(clone, "config", "user.email", f"{name}@example.invalid")
    return clone


def _commit_file(clone: Path, filename: str, message: str) -> None:
    (clone / filename).write_text(f"{filename}\n", encoding="utf-8")
    _git(clone, "add", filename)
    _git(clone, "commit", "-m", message)


def _base_env(extra: dict[str, str] | None = None) -> dict[str, str]:
    env = dict(os.environ)
    env["PUSH_BACKOFF_SECONDS"] = "0"
    if extra:
        env.update(extra)
    return env


def test_retry_survives_one_racing_push_and_merges_both_commits(tmp_path: Path) -> None:
    origin = _init_bare_origin(tmp_path)
    a = _clone(origin, tmp_path, "a")
    b = _clone(origin, tmp_path, "b")

    _commit_file(a, "a.txt", "a: add a.txt")

    _commit_file(b, "b.txt", "b: add b.txt")
    _git(b, "push", "origin", "HEAD:master")

    marker = tmp_path / "reject-once.flag"
    marker.touch()
    env = _base_env({"GIT_TEST_REJECT_MARKER": str(marker)})

    result = _run(["bash", str(SCRIPT), "master", "origin"], cwd=a, env=env)

    assert result.returncode == 0, f"stdout={result.stdout} stderr={result.stderr}"
    assert not marker.exists(), "hook should have consumed the one-shot rejection"

    verify = tmp_path / "verify"
    _git(tmp_path, "clone", str(origin), str(verify))
    log = _git(verify, "log", "--format=%s").stdout
    assert "a: add a.txt" in log
    assert "b: add b.txt" in log


def test_retry_gives_up_after_max_attempts_and_exits_nonzero(tmp_path: Path) -> None:
    origin = _init_bare_origin(tmp_path)
    a = _clone(origin, tmp_path, "a")
    _commit_file(a, "a.txt", "a: add a.txt")

    marker = tmp_path / "reject-forever.flag"
    marker.touch()
    env = _base_env(
        {
            "GIT_TEST_REJECT_MARKER": str(marker),
            "GIT_TEST_REJECT_PERSIST": "1",
            "PUSH_MAX_ATTEMPTS": "1",
        }
    )

    result = _run(["bash", str(SCRIPT), "master", "origin"], cwd=a, env=env)

    assert result.returncode == 1
    assert "giving up after 1 attempts" in result.stderr

    verify = tmp_path / "verify"
    _git(tmp_path, "clone", str(origin), str(verify))
    log = _run(["git", "log", "--format=%s"], cwd=verify).stdout
    assert "a: add a.txt" not in log


def test_retry_aborts_unfinished_rebase_left_by_a_crashed_prior_attempt(tmp_path: Path) -> None:
    """A prior run of the script can be killed mid `git pull --rebase`,
    leaving `.git/rebase-merge` behind. Without a guard, every subsequent
    `git pull --rebase` in the retry loop fails immediately with "It looks
    like 'git rebase' is in progress" -- the loop can never recover on its
    own, however many attempts it has left.
    """
    origin = _init_bare_origin(tmp_path)
    seed = _clone(origin, tmp_path, "seed")
    _commit_file(seed, "seed.txt", "seed: init")
    _git(seed, "push", "origin", "HEAD:master")
    seed_sha = _git(seed, "rev-parse", "HEAD").stdout.strip()

    a = _clone(origin, tmp_path, "a")
    _commit_file(a, "a.txt", "a: add a.txt")
    head_sha = _git(a, "rev-parse", "HEAD").stdout.strip()

    rebase_merge = a / ".git" / "rebase-merge"
    rebase_merge.mkdir()
    (rebase_merge / "head-name").write_text("refs/heads/master\n", encoding="utf-8")
    (rebase_merge / "onto").write_text(f"{seed_sha}\n", encoding="utf-8")
    (rebase_merge / "orig-head").write_text(f"{head_sha}\n", encoding="utf-8")
    (rebase_merge / "interactive").write_text("", encoding="utf-8")

    result = _run(["bash", str(SCRIPT), "master", "origin"], cwd=a, env=_base_env())

    assert result.returncode == 0, f"stdout={result.stdout} stderr={result.stderr}"
    assert not rebase_merge.exists(), "guard must abort and clear the stuck rebase-merge dir"

    verify = tmp_path / "verify"
    _git(tmp_path, "clone", str(origin), str(verify))
    log = _run(["git", "log", "--format=%s"], cwd=verify).stdout
    assert "a: add a.txt" in log
