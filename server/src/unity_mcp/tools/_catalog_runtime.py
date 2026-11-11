"""Mechanical runtime of the generated tool module (generated/catalog_tools.py): the wire encoders and the ToolSet.

It holds no fact about any tool; the names, types, defaults, flags and descriptions are in dsl/tools.biome.
"""
import importlib
import inspect
from typing import Any

from ._annotations import derive

ANY = Any  # a parameter the catalog types Any (Context, Literal, dict, ...) is checked by name and default, not by annotation
_NOTHING = object()


def _encoder(encode):
    """The wire form of a value: None (left out) when there is no value, or when it equals `omit`, the default Unity applies itself.
    `raw` sends the value as it is (a number stays a number) for a wrapper that never turned it into text."""
    def wire(value, omit=_NOTHING, raw=False):
        return None if value is None or value == omit else value if raw else encode(value)
    return wire


w_str = _encoder(lambda value: value)
w_int = w_float = _encoder(str)
w_bool = _encoder(lambda value: "true" if value else "false")
w_ostr, w_oint, w_ofloat, w_obool = w_str, w_int, w_float, w_bool


def _conform(name, fn, summary, params):
    """A function must have the parameters the catalog states, in order, and a description that starts with its summary."""
    want = [(p[0], p[1], p[2] if len(p) > 2 else inspect.Parameter.empty) for p in params]
    anys = {i for i, w in enumerate(want) if w[1] is ANY}
    got = [(p.name, ANY if i in anys else p.annotation, p.default) for i, p in enumerate(inspect.signature(fn).parameters.values())]
    if got != want:
        raise TypeError(f"catalog drift: {name}{got} must be {name}{want}")
    if not (fn.__doc__ or "").startswith(summary):
        raise TypeError(f"catalog drift: the description of {name} must start with {summary!r}")


def _origin(fn):
    return fn.__module__, fn.__qualname__


class ToolSet:
    """What the catalog expects of the Python side and the functions that fulfil it. A generated wrapper is provided and registered last
    by install(); a hand-written tool is registered by its own module, where it was registered before the catalog."""

    def __init__(self):
        self._expected = {}
        self._provided = {}    # name -> a generated wrapper, registered by install()
        self._registered = {}  # name -> (module, qualname) of a hand-written function that its module registered
        self._session = {}     # name -> SESSION flag, read by derive() when registering
        self._body_modules = {}  # name -> module name (unity_mcp.tools.<module>) of a BODY tool's hand-written _<name>_body

    def expect(self, name, effect, summary, params, session=False):
        """The catalog states a tool: its effect, the first line of its description and its parameters, (name, type[, default]) in order."""
        if name in self._expected:
            raise ValueError(f"{name} is stated twice in the catalog")
        self._expected[name] = (effect, summary, params)
        self._session[name] = session

    def provide(self, fn):
        """The function (a decorator) that fulfils the expectation of its name: a generated wrapper, which install() registers."""
        if self._provided.setdefault(fn.__name__, fn) is not fn:
            raise ValueError(f"{fn.__name__} is provided by two functions")
        return fn

    def expect_body(self, name: str, module: str) -> None:
        """Record that <module> (unity_mcp.tools.<module>) owns the hand-written `_<name>_body` that a BODY-tagged
        generated wrapper calls. Stores the module name only -- importing it now would recreate the circular-import
        problem the generated wrapper's own lazy import already avoids; install() resolves it once every
        tools/*.py module has finished loading."""
        if name not in self._expected:
            raise LookupError(f"{name} is not a tool of the catalog")
        self._body_modules[name] = module

    def signature(self, name: str) -> dict[str, frozenset[str]]:
        """Required/optional ARG names of a catalog tool (no DEFAULT/DEFAULT_TEXT -> required)."""
        _, _, params = self._expected[name]
        return {
            "required": frozenset(p[0] for p in params if len(p) < 3),
            "optional": frozenset(p[0] for p in params if len(p) >= 3),
        }

    def register(self, mcp, *fns):
        """A module registers its hand-written tools: each function is checked against the catalog, then all are registered with the
        MCP annotation of their effect (a refusal registers none). Registering the same function again, for another mcp, is fine."""
        for fn in fns:
            if fn.__name__ not in self._expected:
                raise LookupError(f"{fn.__name__} is not a tool of the catalog")
            if fn.__name__ in self._provided or self._registered.get(fn.__name__, _origin(fn)) != _origin(fn):
                raise ValueError(f"{fn.__name__} has two owners")
            _, summary, params = self._expected[fn.__name__]
            _conform(fn.__name__, fn, summary, params)
        for fn in fns:
            mcp.tool(annotations=derive(self._expected[fn.__name__][0], self._session[fn.__name__]))(fn)
            self._registered[fn.__name__] = _origin(fn)

    def install(self, mcp):
        """Check that every tool of the catalog has a function, then register the generated wrappers: a refusal registers none."""
        done = self._provided.keys() | self._registered.keys()
        missing, unknown = self._expected.keys() - done, self._provided.keys() - self._expected.keys()
        if missing or unknown:
            raise LookupError(f"catalog tools without a function: {sorted(missing)}; functions without a catalog tool: {sorted(unknown)}")
        for name, module_name in self._body_modules.items():
            mod = importlib.import_module(f".{module_name}", "unity_mcp.tools")
            fn = getattr(mod, f"_{name}_body", None)
            if fn is None:
                raise LookupError(f"{module_name}._{name}_body does not exist for BODY tool '{name}'")
            want = self.signature(name)["required"] | self.signature(name)["optional"]
            got = frozenset(p.name for p in inspect.signature(fn).parameters.values()
                            if p.kind not in (inspect.Parameter.VAR_POSITIONAL, inspect.Parameter.VAR_KEYWORD))
            if got != want:
                raise TypeError(f"catalog drift: {module_name}._{name}_body{sorted(got)} must accept keyword(s) {sorted(want)}")
        wrappers = [(name, self._provided[name]) for name in self._expected if name in self._provided]
        for name, fn in wrappers:
            _conform(name, fn, *self._expected[name][1:])
        for name, fn in wrappers:
            mcp.tool(annotations=derive(self._expected[name][0], self._session[name]))(fn)
