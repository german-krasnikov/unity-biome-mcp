"""Shared strict semantic-version validation for release packages."""


import re

_CORE = r"(?:0|[1-9]\d*)"
_PRERELEASE_ID = r"(?:0|[1-9]\d*|[A-Za-z-][0-9A-Za-z-]*)"
_BUILD_ID = r"[0-9A-Za-z-]+"
_SEMVER = re.compile(
    rf"^{_CORE}\.{_CORE}\.{_CORE}"
    rf"(?:-{_PRERELEASE_ID}(?:\.{_PRERELEASE_ID})*)?"
    rf"(?:\+{_BUILD_ID}(?:\.{_BUILD_ID})*)?$"
)


def is_strict_semver(value: object) -> bool:
    return isinstance(value, str) and _SEMVER.fullmatch(value) is not None
