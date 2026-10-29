"""Mechanical runtime of the generated tool module (generated/catalog_tools.py): the wire encoders and the ToolSet.

It holds no fact about any tool; the names, types, defaults, flags and descriptions are in dsl/tools.biome.
"""
import inspect

from ._annotations import DEL, RO, RW, RW_IDEM

_ANNOTATION = {"RO": RO, "RW": RW, "RW_IDEM": RW_IDEM, "DEL": DEL}
_NOTHING = object()


def _encoder(encode):
    """The wire form of a value: None (left out) when there is no value, or when it equals `omit`, the default Unity applies itself."""
    def wire(value, omit=_NOTHING):
        return None if value is None or value == omit else encode(value)
    return wire


w_str = _encoder(lambda value: value)
w_int = w_float = _encoder(str)
w_bool = _encoder(lambda value: "true" if value else "false")
w_ostr, w_oint, w_ofloat, w_obool = w_str, w_int, w_float, w_bool


def _conform(name, fn, summary, params):
    """A function must have the parameters the catalog states, in order, and a description that starts with its summary."""
    want = [(p[0], p[1], p[2] if len(p) > 2 else inspect.Parameter.empty) for p in params]
    got = [(p.name, p.annotation, p.default) for p in inspect.signature(fn).parameters.values()]
    if got != want:
        raise TypeError(f"catalog drift: {name}{got} must be {name}{want}")
    if not (fn.__doc__ or "").startswith(summary):
        raise TypeError(f"catalog drift: the description of {name} must start with {summary!r}")


class ToolSet:
    """What the catalog expects of the Python side, the functions that fulfil it, and their registration with FastMCP."""

    def __init__(self):
        self._expected = {}
        self._provided = {}

    def expect(self, name, effect, summary, params):
        """The catalog states a tool: its effect, the first line of its description and its parameters, (name, type[, default]) in order."""
        if name in self._expected:
            raise ValueError(f"{name} is stated twice in the catalog")
        self._expected[name] = (effect, summary, params)

    def provide(self, fn):
        """The function (a decorator) that fulfils the expectation of its name; a generated wrapper or a hand-written tool."""
        if self._provided.setdefault(fn.__name__, fn) is not fn:
            raise ValueError(f"{fn.__name__} is provided by two functions")
        return fn

    def install(self, mcp):
        """Check every function against the catalog and register it, with the MCP annotation of its effect."""
        missing, unknown = self._expected.keys() - self._provided.keys(), self._provided.keys() - self._expected.keys()
        if missing or unknown:
            raise LookupError(f"catalog tools without a function: {sorted(missing)}; functions without a catalog tool: {sorted(unknown)}")
        for name, (effect, summary, params) in self._expected.items():
            _conform(name, self._provided[name], summary, params)
            mcp.tool(annotations=_ANNOTATION[effect])(self._provided[name])
