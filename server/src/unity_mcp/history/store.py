"""T23: HistoryStore — append events to JSONL, write meta atomically."""

import contextlib
import json
import os
from pathlib import Path  # noqa: TC003

from ..agent_event import AgentEvent  # noqa: TC001
from .models import ConversationHeader  # noqa: TC001


class HistoryStore:
    def __init__(self, conv_dir: Path, conv_id: str) -> None:
        self._conv_dir = conv_dir
        self._conv_id = conv_id

    @property
    def jsonl_path(self) -> Path:
        return self._conv_dir / f"{self._conv_id}.jsonl"

    @property
    def meta_path(self) -> Path:
        return self._conv_dir / f"{self._conv_id}.meta.json"

    def append_event(self, event: AgentEvent) -> None:
        """Append one JSON line. Silent on OSError."""
        with contextlib.suppress(OSError):
            line = event.model_dump_json(exclude_none=True) + "\n"
            with open(self.jsonl_path, "a", encoding="utf-8") as f:
                f.write(line)

    def flush_header(self, header: ConversationHeader) -> None:
        """Atomic write of .meta.json (tmp + os.replace)."""
        with contextlib.suppress(OSError):
            tmp = self._conv_dir / f".tmp-{self._conv_id}.meta.json"
            tmp.write_text(
                json.dumps(header.to_dict(), separators=(",", ":")),
                encoding="utf-8",
            )
            os.replace(tmp, self.meta_path)
