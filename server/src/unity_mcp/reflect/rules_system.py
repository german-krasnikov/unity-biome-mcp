"""Reflection rules for system-level mutation commands.

watch/checkpoint/sync_playtest_aliases_from_defs/export_playtest_aliases_to_defs/
uitk_element/uitk_file/menu moved to reflect_noerror's single installer loop
(rules_assets.py) — SV3-DESIGN.md §3.7 #3.
"""
from .factory import make_ok_rule

# Simple ok-token rules
make_ok_rule("set_llm_config", ("ok",))
