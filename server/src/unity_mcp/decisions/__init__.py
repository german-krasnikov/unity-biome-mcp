"""The decisions of the server: what is a write, did the playtest pass, which guard stops a call, why a send is retried.

Each is a Logic DECISION in dsl/decisions/*.logic, printed into generated/ (step 7.2). A decision is a pure function of typed
facts; the code that reads the facts from a call, a report or a probe lives next to it (verdict_facts.py, guard_facts.py) or in the
module that owns the state. This package is a leaf: it imports the standard library and itself, nothing else of unity_mcp
(the import-linter contract of server/pyproject.toml, with its negative test test_decisions_boundary.py).
"""
