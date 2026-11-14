# Generated from dsl/data/protocol.data-contract, dsl/data/protocol.core-data
# source-sha256=766abf9614b49ca7febc72e02947ba44da795f9265942a2ff9a2e47608bdefa2
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class Protocol:
    versionRefPattern: str
    serverRefPattern: str
    gitRepoBase: str
    installSubdir: str
    defaultPort: int
    maxFrameBytes: int
    maxToolResultLen: int
    minPythonMajor: int
    minPythonMinor: int


PROTOCOL_ROWS: tuple[Protocol, ...] = (
    Protocol(
        versionRefPattern="[\\d.]+(?:-[0-9A-Za-z.]+)?",
        serverRefPattern="[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.]+)?",
        gitRepoBase="git+https://github.com/german-krasnikov/unity-biome-mcp.git",
        installSubdir="server",
        defaultPort=9500,
        maxFrameBytes=10000000,
        maxToolResultLen=2000,
        minPythonMajor=3,
        minPythonMinor=14,
    ),
)
