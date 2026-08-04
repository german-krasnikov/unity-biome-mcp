"""Set-of-Mark (SoM) visual annotation package."""
from .extract import parse_rects
from .overlay import annotate

__all__ = ["annotate", "parse_rects"]
