# Generated from dsl/data/chipkinds.data-contract, dsl/data/chipkinds.core-data
# source-sha256=2257360977b5717a93a3e6b50713ba35812f2e067be634a780148b3f76626328
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class ChipKind:
    key: str
    hexColor: str
    defaultDepth: str
    priority: int


CHIP_KIND_ROWS: tuple[ChipKind, ...] = (
    ChipKind(key="agent", hexColor="#9b59b6", defaultDepth="path", priority=5),
    ChipKind(
        key="annotated_screenshot",
        hexColor="#e74c3c",
        defaultDepth="summary",
        priority=40,
    ),
    ChipKind(key="image", hexColor="#f472b6", defaultDepth="path", priority=50),
    ChipKind(key="profile", hexColor="#f0a030", defaultDepth="full", priority=75),
    ChipKind(key="hierarchy", hexColor="#4a9eff", defaultDepth="path", priority=100),
    ChipKind(key="region", hexColor="#22d3ee", defaultDepth="summary", priority=120),
    ChipKind(key="component", hexColor="#38bdf8", defaultDepth="summary", priority=125),
    ChipKind(key="field", hexColor="#f59e0b", defaultDepth="summary", priority=130),
    ChipKind(key="folder", hexColor="#a78bfa", defaultDepth="path", priority=150),
    ChipKind(key="scene", hexColor="#c084fc", defaultDepth="path", priority=200),
    ChipKind(key="script", hexColor="#4ade80", defaultDepth="path", priority=300),
    ChipKind(key="prefab", hexColor="#60a5fa", defaultDepth="path", priority=400),
    ChipKind(key="model", hexColor="#34d399", defaultDepth="path", priority=450),
    ChipKind(key="material", hexColor="#f97316", defaultDepth="path", priority=500),
    ChipKind(key="audio", hexColor="#a78bfa", defaultDepth="path", priority=550),
    ChipKind(key="texture", hexColor="#facc15", defaultDepth="path", priority=600),
    ChipKind(key="so", hexColor="#fb7185", defaultDepth="path", priority=700),
    ChipKind(key="asset", hexColor="#94a3b8", defaultDepth="path", priority=2147483647),
)
