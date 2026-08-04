## What

<!-- One sentence: what changed? -->

## Why

<!-- Why is this change needed? Link issues: Fixes #123 -->

## Test Evidence

- [ ] Python unit tests (`cd server && PYTHONWARNDEFAULTENCODING=1 python -m pytest tests/ -m "not live and not monkey" -q`)
- [ ] C# EditMode tests (Unity Test Runner)
- [ ] Live integration tests (`pytest tests/ -m "live and not live_cli" -q`) — if TCP/protocol changed
- [ ] Manual verification — describe below if applicable

## Checklist

- [ ] No file exceeds 300 lines (utility/static-only classes exempt)
- [ ] No new dependencies without justification
- [ ] CHANGELOG.md updated (if user-facing change)
- [ ] No secrets, credentials, or .env files included
