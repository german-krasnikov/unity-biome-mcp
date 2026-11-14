# Generated from dsl/data/protocol.data-contract, dsl/data/protocol.core-data
# source-sha256=dfc9f24da191cc273adc18a3f87835896189c4d470b6bdf0010c3360014d9d96
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class Protocol:
    versionRefPattern: str
    serverRefPattern: str
    gitRepoBase: str
    installSubdir: str


PROTOCOL_ROWS: tuple[Protocol, ...] = (
    Protocol(
        versionRefPattern="[\\d.]+(?:-[0-9A-Za-z.]+)?",
        serverRefPattern="[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.]+)?",
        gitRepoBase="git+https://github.com/german-krasnikov/unity-biome-mcp.git",
        installSubdir="server",
    ),
)
