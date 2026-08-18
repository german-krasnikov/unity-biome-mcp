"""Upgrade path tests: check_python() boundary conditions for Python 3.14 requirement."""
import sys
from unittest.mock import patch

from install.commands import check_python


def test_check_python_too_old_returns_false():
    with patch.object(sys, "version_info", (3, 13, 0)):
        assert check_python() is False


def test_check_python_minimum_returns_true():
    with patch.object(sys, "version_info", (3, 14, 0)):
        assert check_python() is True


def test_check_python_newer_returns_true():
    with patch.object(sys, "version_info", (3, 15, 0)):
        assert check_python() is True
