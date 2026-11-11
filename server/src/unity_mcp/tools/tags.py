"""Per-tag tool-name sets, folded from the catalog's (tag, name) pairs
(dsl/tools.biome ITEM TAG). K-2 (EACH ... BY) is not in biome-dsl yet
(P6/P25: native group-by is simpler than landing Core for one consumer);
this is that fold, done once, read everywhere below."""
from collections import defaultdict

from .generated.catalog_tags import TAG_PAIRS

TAGS: frozenset[str] = frozenset(tag for tag, _ in TAG_PAIRS)

_by_tag: dict[str, set[str]] = defaultdict(set)
for _tag, _name in TAG_PAIRS:
    _by_tag[_tag].add(_name)
_BY_TAG: dict[str, frozenset[str]] = {k: frozenset(v) for k, v in _by_tag.items()}


def tagged(tag: str) -> frozenset[str]:
    """Tool names carrying TAG <tag> in the catalog (dsl/tools.biome)."""
    return _BY_TAG.get(tag, frozenset())
