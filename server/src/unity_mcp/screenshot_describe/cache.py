import hashlib
import time
from collections import OrderedDict

from ..metrics import METRICS


class FingerprintCache:
    def __init__(self, ttl: float = 60.0, max_entries: int = 64):
        self._ttl = ttl
        self._max = max_entries
        self._store: OrderedDict[str, tuple[str, float]] = OrderedDict()

    def _key(self, fp: str, prompt: str) -> str:
        h = hashlib.md5(prompt.encode()).hexdigest()[:8]
        return f"{fp}:{h}"

    def get(self, fp: str, prompt: str) -> str | None:
        k = self._key(fp, prompt)
        entry = self._store.get(k)
        if not entry:
            METRICS.inc("fpcache.miss")
            return None
        value, ts = entry
        if time.time() - ts > self._ttl:
            del self._store[k]
            METRICS.inc("fpcache.miss")
            return None
        self._store.move_to_end(k)
        METRICS.inc("fpcache.hit")
        return value

    def put(self, fp: str, prompt: str, value: str) -> None:
        k = self._key(fp, prompt)
        self._store[k] = (value, time.time())
        self._store.move_to_end(k)
        while len(self._store) > self._max:
            self._store.popitem(last=False)
