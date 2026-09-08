#!/usr/bin/env bash
# Retries `git pull --rebase` + `git push` to survive CI bots racing to push
# generated data (badges, test results, coverage) to the same branch.
#
# Usage: git_push_rebase_retry.sh [branch=master] [remote=origin]
# Env:   PUSH_MAX_ATTEMPTS (default 5), PUSH_BACKOFF_SECONDS (default 3, per
#        attempt multiplier; 0 allowed for tests).
set -eu

readonly DEFAULT_MAX_ATTEMPTS=5
readonly DEFAULT_BACKOFF_SECONDS=3

branch="${1:-master}"
remote="${2:-origin}"
max_attempts="${PUSH_MAX_ATTEMPTS:-$DEFAULT_MAX_ATTEMPTS}"
backoff_seconds="${PUSH_BACKOFF_SECONDS:-$DEFAULT_BACKOFF_SECONDS}"

attempt=1
while [ "$attempt" -le "$max_attempts" ]; do
    # Abort a rebase left by a killed/crashed prior attempt; use --git-path
    # (not literal .git/...) so this also resolves in a linked worktree,
    # where .git is a file pointing at .git/worktrees/<name>, not a dir.
    rebase_merge_dir="$(git rev-parse --git-path rebase-merge)"
    rebase_apply_dir="$(git rev-parse --git-path rebase-apply)"
    if [ -d "$rebase_merge_dir" ] || [ -d "$rebase_apply_dir" ]; then
        echo "git_push_rebase_retry: aborting unfinished rebase before attempt $attempt" >&2
        git rebase --abort
    fi
    if git pull --rebase -X theirs "$remote" "$branch" && git push "$remote" "HEAD:$branch"; then
        exit 0
    fi
    echo "git_push_rebase_retry: attempt $attempt/$max_attempts failed" >&2
    if [ "$attempt" -lt "$max_attempts" ]; then
        sleep "$((attempt * backoff_seconds))"
    fi
    attempt=$((attempt + 1))
done

echo "git_push_rebase_retry: giving up after $max_attempts attempts to push $branch to $remote" >&2
exit 1
