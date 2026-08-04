"""stdlib-only terminal UI for the unity-biome-mcp installer."""
import os
import sys
import threading

# ── Platform detection ──────────────────────────────────────────────────────

def _color_ok() -> bool:
    if os.environ.get("NO_COLOR"):
        return False
    if os.environ.get("TERM") == "dumb":
        return False
    if not hasattr(sys.stderr, "isatty") or not sys.stderr.isatty():
        return False
    if sys.platform == "win32":
        try:
            import ctypes
            kernel = ctypes.windll.kernel32
            kernel.SetConsoleMode(kernel.GetStdHandle(-12), 7)
        except Exception:
            return False
    return True


def _unicode_ok() -> bool:
    enc = getattr(sys.stdout, "encoding", None)
    if not enc:
        return False
    return enc.upper().replace("-", "") in ("UTF8", "UTF16", "UTF32")


# ── ANSI helpers ────────────────────────────────────────────────────────────

def _c(code: str, text: str) -> str:
    if not _color_ok():
        return text
    return f"\033[{code}m{text}\033[0m"


# ── Status functions ─────────────────────────────────────────────────────────

def ok(msg: str) -> None:
    if _unicode_ok():
        sym = _c("32", "✓")
    else:
        sym = "[OK]"
    print(f"  {sym}  {msg}")


def fail(msg: str) -> None:
    if _unicode_ok():
        sym = _c("31", "✗")
    else:
        sym = "[FAIL]"
    print(f"  {sym}  {msg}")


def info(msg: str) -> None:
    if _unicode_ok():
        sym = _c("33", "○")
    else:
        sym = "[-]"
    print(f"  {sym}  {msg}")


def skip(msg: str) -> None:
    if _unicode_ok():
        sym = _c("38;5;240", "–")
    else:
        sym = "[SKIP]"
    print(f"  {sym}  {msg}")


# ── Box rendering ────────────────────────────────────────────────────────────

_BOX_UNICODE = {"tl": "╭", "tr": "╮", "bl": "╰", "br": "╯", "h": "─", "v": "│"}
_BOX_ASCII   = {"tl": "+", "tr": "+", "bl": "+", "br": "+", "h": "-", "v": "|"}

_STYLE_COLORS = {"success": "32", "error": "31", "default": "38;5;240"}


def box(lines: list[str], style: str = "default") -> None:
    chars = _BOX_UNICODE if _unicode_ok() else _BOX_ASCII
    color = _STYLE_COLORS.get(style, _STYLE_COLORS["default"])
    width = max(len(l) for l in lines) if lines else 0

    def b(s: str) -> str:
        return _c(color, s)

    top    = b(chars["tl"] + chars["h"] * (width + 2) + chars["tr"])
    bottom = b(chars["bl"] + chars["h"] * (width + 2) + chars["br"])
    print(top)
    for line in lines:
        print(b(chars["v"]) + " " + line.ljust(width) + " " + b(chars["v"]))
    print(bottom)


# ── Spinner ──────────────────────────────────────────────────────────────────

BRAILLE_FRAMES = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"
ASCII_FRAMES   = "-\\|/"
_FRAME_MS      = 0.08


class Spinner:
    def __init__(self, label: str, step: str = ""):
        self._label = label
        self._step  = step
        self._stop  = threading.Event()
        self._thread: threading.Thread | None = None

    def _run(self) -> None:
        frames = BRAILLE_FRAMES if _unicode_ok() else ASCII_FRAMES
        i = 0
        while not self._stop.is_set():
            frame = _c("36", frames[i % len(frames)])
            suffix = f" {self._step}" if self._step else ""
            sys.stderr.write(f"\r  {frame}  {self._label}{suffix}  ")
            sys.stderr.flush()
            i += 1
            self._stop.wait(_FRAME_MS)

    def __enter__(self) -> "Spinner":
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()
        return self

    def __exit__(self, *exc) -> None:
        self._stop.set()
        if self._thread:
            self._thread.join(timeout=1)
        sys.stderr.write("\r" + " " * 60 + "\r")
        sys.stderr.flush()


# ── Error box ────────────────────────────────────────────────────────────────

def err_box(stderr_text: str, log_path: str = "") -> None:
    tail = stderr_text.strip().splitlines()[-5:]
    extra = [f"Full log: {log_path}"] if log_path else []
    box(tail + extra, style="error")


# ── Prompt ───────────────────────────────────────────────────────────────────

def prompt_yn(label: str, default: bool = True) -> bool:
    hint = "[Y/n]" if default else "[y/N]"
    print(f"  {label} {hint} ", end="", flush=True)
    ans = input("").strip().lower()
    if not ans:
        return default
    return ans == "y"
