"""Lazy tool-schema registry — re-exports biome-mcp's generic SchemaRegistry.

Module-level singleton `_registry` is used by server.py to capture + resolve
(app-specific wiring, kept here rather than in the generic biome-mcp package).
"""
from biome_mcp.schema_registry import STUB_SCHEMA, SchemaRegistry  # noqa: F401 (re-export)

# Module-level singleton
_registry = SchemaRegistry()
