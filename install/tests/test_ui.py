"""Tests for install/ui.py — stdlib-only terminal UI library."""
import io
import os
import sys
import time
import unittest
from pathlib import Path
from unittest.mock import patch

# Add project root to path
sys.path.insert(0, str(Path(__file__).parents[2]))

from install.ui import (
    Spinner,
    _color_ok,
    _unicode_ok,
    box,
    err_box,
    fail,
    info,
    ok,
    prompt_yn,
    skip,
)


class TestColorDetection(unittest.TestCase):
    def test_color_ok_respects_no_color_env(self):
        with patch.dict(os.environ, {"NO_COLOR": "1"}):
            self.assertFalse(_color_ok())

    def test_color_ok_non_tty(self):
        with patch("sys.stderr") as mock_stderr:
            mock_stderr.isatty.return_value = False
            self.assertFalse(_color_ok())

    def test_color_ok_term_dumb(self):
        with patch.dict(os.environ, {"TERM": "dumb"}, clear=False):
            # Remove NO_COLOR if present, set TERM=dumb
            env = {k: v for k, v in os.environ.items() if k != "NO_COLOR"}
            env["TERM"] = "dumb"
            with patch.dict(os.environ, env, clear=True):
                self.assertFalse(_color_ok())


class TestUnicodeDetection(unittest.TestCase):
    def test_unicode_ok_utf8(self):
        with patch("sys.stdout") as mock_stdout:
            mock_stdout.encoding = "UTF-8"
            self.assertTrue(_unicode_ok())

    def test_unicode_ok_ascii_encoding(self):
        with patch("sys.stdout") as mock_stdout:
            mock_stdout.encoding = "ascii"
            self.assertFalse(_unicode_ok())

    def test_unicode_ok_none_encoding(self):
        with patch("sys.stdout") as mock_stdout:
            mock_stdout.encoding = None
            self.assertFalse(_unicode_ok())


class TestStatusFunctions(unittest.TestCase):
    def _capture(self, fn, msg):
        buf = io.StringIO()
        with patch("sys.stdout", buf):
            fn(msg)
        return buf.getvalue()

    def test_ok_outputs_check(self):
        out = self._capture(ok, "all good")
        self.assertTrue("✓" in out or "[OK]" in out)
        self.assertIn("all good", out)

    def test_fail_outputs_x(self):
        out = self._capture(fail, "broken")
        self.assertTrue("✗" in out or "[FAIL]" in out)
        self.assertIn("broken", out)

    def test_info_outputs_circle(self):
        out = self._capture(info, "note")
        self.assertTrue("○" in out or "[-]" in out)
        self.assertIn("note", out)

    def test_skip_outputs_dash(self):
        out = self._capture(skip, "skipped step")
        self.assertTrue("–" in out or "[SKIP]" in out)
        self.assertIn("skipped step", out)


class TestBoxRendering(unittest.TestCase):
    def _capture_box(self, lines, **kwargs):
        buf = io.StringIO()
        with patch("sys.stdout", buf):
            box(lines, **kwargs)
        return buf.getvalue()

    def test_box_contains_content(self):
        out = self._capture_box(["Hello", "World"])
        self.assertIn("Hello", out)
        self.assertIn("World", out)

    def test_box_unicode_borders(self):
        with patch("install.ui._unicode_ok", return_value=True), \
             patch("install.ui._color_ok", return_value=False):
            out = self._capture_box(["line"])
        # Unicode box chars
        self.assertTrue(
            "╭" in out or "─" in out,
            f"Expected unicode border chars, got: {repr(out)}"
        )

    def test_box_ascii_fallback(self):
        with patch("install.ui._unicode_ok", return_value=False), \
             patch("install.ui._color_ok", return_value=False):
            out = self._capture_box(["line"])
        self.assertIn("+", out)
        self.assertIn("-", out)

    def test_box_multiline(self):
        out = self._capture_box(["line1", "line2", "line3"])
        self.assertIn("line1", out)
        self.assertIn("line2", out)
        self.assertIn("line3", out)


class TestSpinner(unittest.TestCase):
    def test_spinner_context_manager(self):
        # Should not raise; start and stop cleanly
        buf = io.StringIO()
        with patch("sys.stderr", buf):
            with Spinner("loading"):
                time.sleep(0.05)
        # After exit, no exception

    def test_spinner_shows_label(self):
        buf = io.StringIO()
        with patch("sys.stderr", buf):
            with Spinner("my task"):
                time.sleep(0.1)
        output = buf.getvalue()
        self.assertIn("my task", output)

    def test_spinner_shows_step(self):
        buf = io.StringIO()
        with patch("sys.stderr", buf):
            with Spinner("doing", step="[1/4]"):
                time.sleep(0.1)
        output = buf.getvalue()
        self.assertIn("[1/4]", output)

    def test_spinner_clears_on_exit(self):
        # On exit, spinner writes \r to clear the line
        buf = io.StringIO()
        with patch("sys.stderr", buf):
            with Spinner("work"):
                time.sleep(0.05)
        output = buf.getvalue()
        self.assertIn("\r", output)


class TestErrBox(unittest.TestCase):
    def _capture_err_box(self, *args, **kwargs):
        buf = io.StringIO()
        with patch("sys.stdout", buf):
            err_box(*args, **kwargs)
        return buf.getvalue()

    def test_err_box_truncates_long_stderr(self):
        lines = [f"line {i}" for i in range(20)]
        out = self._capture_err_box("\n".join(lines))
        # Only last 5 lines should appear
        self.assertNotIn("line 0", out)
        self.assertNotIn("line 14", out)
        self.assertIn("line 19", out)

    def test_err_box_shows_log_path(self):
        out = self._capture_err_box("some error", log_path="/tmp/install.log")
        self.assertIn("/tmp/install.log", out)

    def test_err_box_no_log_path(self):
        out = self._capture_err_box("error text")
        # Should not crash, should contain error
        self.assertIn("error text", out)

    def test_err_box_short_stderr_shows_all(self):
        out = self._capture_err_box("line1\nline2\nline3")
        self.assertIn("line1", out)
        self.assertIn("line3", out)


class TestPromptYn(unittest.TestCase):
    def test_prompt_yn_default_yes_empty_input(self):
        with patch("builtins.input", return_value=""):
            result = prompt_yn("Continue?", default=True)
        self.assertTrue(result)

    def test_prompt_yn_default_no_empty_input(self):
        with patch("builtins.input", return_value=""):
            result = prompt_yn("Continue?", default=False)
        self.assertFalse(result)

    def test_prompt_yn_explicit_no(self):
        with patch("builtins.input", return_value="n"):
            result = prompt_yn("Continue?", default=True)
        self.assertFalse(result)

    def test_prompt_yn_explicit_yes(self):
        with patch("builtins.input", return_value="y"):
            result = prompt_yn("Continue?", default=False)
        self.assertTrue(result)

    def test_prompt_yn_uppercase_Y(self):
        with patch("builtins.input", return_value="Y"):
            result = prompt_yn("Continue?", default=False)
        self.assertTrue(result)

    def test_prompt_yn_uppercase_N(self):
        with patch("builtins.input", return_value="N"):
            result = prompt_yn("Continue?", default=True)
        self.assertFalse(result)

    def test_prompt_yn_label_in_default_yes(self):
        buf = io.StringIO()
        with patch("builtins.input", return_value="y"), \
             patch("sys.stdout", buf):
            prompt_yn("Delete files?", default=True)
        out = buf.getvalue()
        self.assertIn("Delete files?", out)
        self.assertIn("Y", out)  # uppercase Y = default


if __name__ == "__main__":
    unittest.main()
