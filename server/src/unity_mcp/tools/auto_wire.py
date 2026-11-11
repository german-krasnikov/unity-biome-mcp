"""auto_wire — fill null ObjectReference fields by name/type matching."""
# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import auto_wire  # noqa: F401
