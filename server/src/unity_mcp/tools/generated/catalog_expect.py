# Generated from dsl/tools.biome
# source-sha256=c232024f9b8a86f2efa3a4cd269813172482f66b9e96b82e3bac0e6bca44a5d0
# Edit the source, then run biome generate.

# A literal success token (or an OR-list, pipe-separated) that confirms a
# mutation (EXPECT rows of dsl/tools.biome): see reflect/rules_catalog.py.
# mode: 'token' (tokens is the OR-list), 'bool_arg' (tokens is 'true_token|false_token', argname picks the branch), 'arg_echo' (tokens is the arg names whose presence each requires its own '<name>=' echo).
EXPECT_RULES: dict[str, tuple] = {
    'auto_wire': ('token', 'Wired:', False, None),
    'autofit_collider': ('token', 'fitted', False, None),
    'create_ui': ('token', 'Created', False, None),
    'delete_object': ('token', 'deleted', True, None),
    'recompile': ('token', 'ok', False, None),
    'region_clear': ('bool_arg', 'DRY|DELETED', False, 'dry_run'),
    'set_material': ('arg_echo', 'shader|color', False, None),
    'transfer_object': ('token', 'Moved|→', False, None),
    'undo_last': ('token', 'reverted|nothing', False, None),
    'unwire_event': ('token', 'Cleared|Removed', False, None),
    'wire_event': ('token', 'wired|connected', True, None),
}
