"""Installs the no-error rule of every reflect_noerror-tagged command.

make_action_guarded_no_error_rule is make_no_error_rule with a (possibly
empty) read-action guard (factory.py): skip on read actions, Mismatch on
error tokens for write actions. One loop over the full reflect_noerror
family (SV3-DESIGN.md §3.7 #2/#3/#4) replaces the asset-only loop plus the
7 equivalent calls that used to live in rules_system.py/rules_scene.py.
"""
from ..middleware_types import ACTION_READS
from ..tools.tags import tagged
from .factory import make_action_guarded_no_error_rule

for _cmd in sorted(tagged("reflect_noerror")):
    make_action_guarded_no_error_rule(_cmd, ACTION_READS.get(_cmd, frozenset()))
