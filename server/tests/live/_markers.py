"""Utility to strip middleware marker suffixes from execute_code responses."""
import re


def strip_markers(text: str) -> str:
    """Strip middleware suffixes appended to responses."""
    text = re.sub(r'\n?\[confidence: [\d.]+\].*$', '', text, flags=re.DOTALL).strip()
    text = re.sub(r'\n?⚠ CONSOLE ERRORS:\n.*$', '', text, flags=re.DOTALL).strip()
    text = re.sub(r'\n?\[next: [^\]]+\]', '', text).strip()
    return text
