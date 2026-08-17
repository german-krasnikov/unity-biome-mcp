"""Reflection rules for asset/media mutation commands.

All use make_action_guarded_no_error_rule: skip on read actions, Mismatch on
error tokens for write actions.
"""
from ..middleware_types import ACTION_READS
from .factory import make_action_guarded_no_error_rule

_cmds = (
    "animation", "animator", "particle", "timeline", "material",
    "asset", "prefab", "scriptable_object", "project_settings",
    "shader", "bake", "references", "navmesh_query",
)

for _cmd in _cmds:
    make_action_guarded_no_error_rule(_cmd, ACTION_READS.get(_cmd, frozenset()))
