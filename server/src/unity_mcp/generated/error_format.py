# Generated from dsl/data/errorformat.data-contract, dsl/data/errorformat.core-data
# source-sha256=67b3223e43cf3c2a5719ed4eca3d7f7efb6700cf401c983a0e495a331fefce63
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ErrorFormat:
    errPrefix: str


ERROR_FORMAT_ROWS: tuple[ErrorFormat, ...] = (
    ErrorFormat(errPrefix="err:"),
)

# Convenience constant: the prefix all C# command handlers use for error responses.
ERR_PREFIX: str = ERROR_FORMAT_ROWS[0].errPrefix
