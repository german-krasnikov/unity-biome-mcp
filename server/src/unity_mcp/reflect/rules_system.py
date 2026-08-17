"""Reflection rules for system-level mutation commands."""
from ..middleware_types import ACTION_READS
from .factory import (
    make_action_guarded_no_error_rule,
    make_no_error_rule,
    make_ok_rule,
)

# Simple ok-token rules
make_ok_rule("set_llm_config", ("ok",))

# No-error rules (any error token triggers Mismatch)
make_no_error_rule("watch")
make_no_error_rule("checkpoint")
make_no_error_rule("sync_playtest_aliases_from_defs")
make_no_error_rule("export_playtest_aliases_to_defs")

# Action-aware no-error rules
# uitk_element: all actions are writes; empty read set → always calls inner
make_action_guarded_no_error_rule(
    "uitk_element", ACTION_READS.get("uitk_element", frozenset())
)
# uitk_file: read action = "read"
make_action_guarded_no_error_rule(
    "uitk_file", ACTION_READS.get("uitk_file", frozenset())
)
# menu: read action = "list"
make_action_guarded_no_error_rule(
    "menu", ACTION_READS.get("menu", frozenset())
)
