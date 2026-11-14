"""ILlmGateway ABC -- the single interface for server-side LLM calls (Task 3.1C).

SamplingService (sampling.py) is the sole implementation. These tests pin the interface
contract so call sites (visual_diff.py, screenshot_describe/, tools/intent_common.py) can
type-hint against ILlmGateway instead of the concrete claude-CLI-backed class.
"""
import pytest

from unity_mcp.llm_gateway import ILlmGateway
from unity_mcp.sampling import SamplingService


def test_sampling_service_is_llm_gateway():
    assert isinstance(SamplingService(), ILlmGateway)


def test_llm_gateway_cannot_be_instantiated_directly():
    with pytest.raises(TypeError):
        ILlmGateway()


def test_llm_gateway_declares_five_abstract_methods():
    assert ILlmGateway.__abstractmethods__ == {
        "verify_visual", "generate", "describe_image", "verify_visual_diff", "summarize",
    }
