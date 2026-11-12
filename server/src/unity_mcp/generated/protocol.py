# Generated from dsl/data/protocol.data-contract, dsl/data/protocol.core-data
# source-sha256=178a86c73cacf90bfe937a878726cc1d450801b4c38d661fe76e55840ceaffcd
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class Protocol:
    versionRefPattern: str


PROTOCOL_ROWS: tuple[Protocol, ...] = (
    Protocol(versionRefPattern="[\\d.]+(?:-[0-9A-Za-z.]+)?"),
)
