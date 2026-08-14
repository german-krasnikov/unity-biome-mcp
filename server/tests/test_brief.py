"""T21: AttachmentSlot, ContextBrief value object unit tests."""
from __future__ import annotations

from unity_mcp.brief import AttachmentSlot, ContextBrief, _estimate_tokens


def test_attachment_slot_of_no_truncation():
    slot = AttachmentSlot.of("console", "short text", 100)
    assert not slot.truncated
    assert slot.content == "short text"
    assert slot.used_tokens == _estimate_tokens("short text")


def test_attachment_slot_of_truncates_at_token_budget():
    content = "a" * 200  # 200 chars → needs budget < 50 tokens to truncate
    slot = AttachmentSlot.of("console", content, 10)  # 10 tokens = 40 chars
    assert slot.truncated is True
    assert "…(truncated)" in slot.content


def test_attachment_slot_truncates_at_newline():
    # 4 tokens = 16 chars max; "line1\nline2\n" = 12 chars fits; "line3..." does not
    content = "line1\nline2\nline3" + "x" * 100
    slot = AttachmentSlot.of("console", content, 4)
    assert slot.truncated is True
    assert "line1" in slot.content
    assert "\n…(truncated)" in slot.content


def test_context_brief_of_empty_slots():
    brief = ContextBrief.of([], 2000)
    assert brief.total_tokens == 0
    assert brief.budget == 2000
    assert len(brief.content_hash) == 12


def test_context_brief_to_text_ordering():
    """Critical before medium; within same priority, alphabetical by kind."""
    slots = [
        AttachmentSlot.of("hierarchy", "h content", 100),        # medium
        AttachmentSlot.of("compile_errors", "err content", 100),  # critical
        AttachmentSlot.of("console", "con content", 100),         # critical
    ]
    brief = ContextBrief.of(slots, 2000)
    text = brief.to_text()
    compile_pos = text.index("[Compile]")
    console_pos = text.index("[Console]")
    hierarchy_pos = text.index("[Hierarchy]")
    assert compile_pos < console_pos   # compile_errors < console alphabetically
    assert console_pos < hierarchy_pos  # critical before medium


def test_context_brief_to_text_empty_slot_omitted():
    slots = [AttachmentSlot.of("console", "", 100)]
    brief = ContextBrief.of(slots, 2000)
    text = brief.to_text()
    assert "[Console]" not in text


def test_context_brief_content_hash_deterministic():
    slots = [AttachmentSlot.of("console", "some content", 100)]
    brief1 = ContextBrief.of(slots, 2000)
    brief2 = ContextBrief.of(slots, 2000)
    assert brief1.content_hash == brief2.content_hash


def test_context_brief_content_hash_changes_with_content():
    slots_a = [AttachmentSlot.of("console", "content A", 100)]
    slots_b = [AttachmentSlot.of("console", "content B", 100)]
    brief1 = ContextBrief.of(slots_a, 2000)
    brief2 = ContextBrief.of(slots_b, 2000)
    assert brief1.content_hash != brief2.content_hash
