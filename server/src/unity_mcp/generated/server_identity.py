# Generated from dsl/data/servername.data-contract, dsl/data/servername.core-data
# source-sha256=25f7016f3657ad2eab6e41486c3cb42e62b6e0b5dd46ec3a693ffacec8f6b76f
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ServerIdentity:
    serverName: str


SERVER_IDENTITY_ROWS: tuple[ServerIdentity, ...] = (
    ServerIdentity(serverName="unity-biome-mcp"),
)
