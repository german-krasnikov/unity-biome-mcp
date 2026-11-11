"""Installs the 11 catalog-driven token-confirmation rules (EXPECT rows of
dsl/tools.biome): a literal success token (or an OR-list), optionally gated
by an arg's boolean value or by which of several args the caller supplied.
SV5 — replaces the 11 hand-written functions this data used to be.

mode: 'token' -> make_ok_rule(cmd, tokens, strict); 'bool_arg' -> make_bool_token_rule
(the catalog's strict slot is unused — this mode is always strict, no fail-open);
'arg_echo' -> make_arg_echo_rule(cmd, tokens, strict).
"""
from ..tools.generated.catalog_expect import EXPECT_RULES
from .factory import make_arg_echo_rule, make_bool_token_rule, make_ok_rule

for _cmd, (_mode, _tokens_raw, _strict, _argname) in EXPECT_RULES.items():
    _tokens = tuple(_tokens_raw.split("|"))
    if _mode == "token":
        make_ok_rule(_cmd, _tokens, strict=_strict)
    elif _mode == "bool_arg":
        make_bool_token_rule(_cmd, _argname, _tokens[0], _tokens[1])
    elif _mode == "arg_echo":
        make_arg_echo_rule(_cmd, _tokens, strict=_strict)
