"""Scene hierarchy/health audit — F4."""
# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import scene_health  # noqa: F401
