// TDD — ChipTextInterleaver tests (Group B).
// Pure headless: no Unity runtime dependency.
// Verifies Bug 1 fix: interleaved segment ordering.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipTextInterleaverTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        // B1: empty text + no chips → UserMessage with one empty-text segment
        [Test]
        public void EmptyTextNoChips_OneEmptyTextSegment()
        {
            var msg = ChipTextInterleaver.Build("", new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
            Assert.AreEqual("", msg.Segments[0].Text);
        }

        // B2: text only, no chips → single text segment
        [Test]
        public void TextOnlyNoChips_SingleTextSegment()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
            Assert.AreEqual("hello world", msg.Segments[0].Text);
        }

        // B3: chip at offset 0, text "hello" → chip segment, then "hello" text segment
        [Test]
        public void ChipAtOffset0_ChipThenText()
        {
            var chip = H("/A", "A", 1);
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(2, msg.Segments.Count);
            Assert.IsTrue(msg.Segments[0].IsChip);
            Assert.AreEqual("/A", msg.Segments[0].Chip.Path);
            Assert.IsFalse(msg.Segments[1].IsChip);
            Assert.AreEqual("hello", msg.Segments[1].Text);
        }

        // B4: chip at offset 5, text "hello world" → "hello" text, chip, " world" text
        [Test]
        public void ChipAtOffset5_TextChipText()
        {
            var chip = H("/A", "A", 1);
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip> { PC(chip, 5) });
            Assert.AreEqual(3, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
            Assert.AreEqual("hello", msg.Segments[0].Text);
            Assert.IsTrue(msg.Segments[1].IsChip);
            Assert.IsFalse(msg.Segments[2].IsChip);
            Assert.AreEqual(" world", msg.Segments[2].Text);
        }

        // B5: two chips at different offsets — segments in correct order
        [Test]
        public void TwoChipsAtDifferentOffsets_CorrectOrder()
        {
            var chipA = H("/A", "A", 1);
            var chipB = H("/B", "B", 2);
            var positioned = new List<PositionedChip> { PC(chipA, 3), PC(chipB, 8) };
            var msg = ChipTextInterleaver.Build("abcdefghij", positioned);
            // Expect: "abc" text, chip A, "efghi" text... wait: offsets 3 and 8 in "abcdefghij"
            // pos0→3: "abc", chip A, pos3→8: "defgh", chip B, pos8→10: "ij"
            Assert.AreEqual(5, msg.Segments.Count);
            Assert.AreEqual("abc",   msg.Segments[0].Text);
            Assert.AreEqual("/A",    msg.Segments[1].Chip.Path);
            Assert.AreEqual("defgh", msg.Segments[2].Text);
            Assert.AreEqual("/B",    msg.Segments[3].Chip.Path);
            Assert.AreEqual("ij",    msg.Segments[4].Text);
        }

        // B6: two chips same DisplayName different offsets — both appear in output
        [Test]
        public void TwoChipsSameDisplayName_BothInSegments()
        {
            var chip1 = new ChipData(ChipKindKeys.Hierarchy, "/A", "Camera", 1);
            var chip2 = new ChipData(ChipKindKeys.Hierarchy, "/B", "Camera", 2);
            var positioned = new List<PositionedChip> { PC(chip1, 0), PC(chip2, 5) };
            var msg = ChipTextInterleaver.Build("hello world", positioned);
            var chipSegs = new List<MessageSegment>();
            foreach (var s in msg.Segments) if (s.IsChip) chipSegs.Add(s);
            Assert.AreEqual(2, chipSegs.Count);
            Assert.AreEqual("/A", chipSegs[0].Chip.Path);
            Assert.AreEqual("/B", chipSegs[1].Chip.Path);
        }

        // B7: ToLlmPayload — text with @mention + chip context appended
        [Test]
        public void ToLlmPayload_TextWithMentionPlusChipContext()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            var positioned = new List<PositionedChip> { PC(chip, 5) };
            var msg = ChipTextInterleaver.Build("fix it please", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            // "fix i" (ends in 'i') → space added before @Assets/Foo.cs → "fix i @Assets/Foo.cs t please"
            StringAssert.Contains("fix i @Assets/Foo.cs t please", payload);
            StringAssert.Contains("[script:Assets/Foo.cs]", payload);
        }

        // B8: ToLlmPayload — empty chips → just plain text, no trailing newline
        [Test]
        public void ToLlmPayload_NoChips_PlainTextOnly()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            Assert.AreEqual("hello world", payload);
            Assert.IsFalse(payload.EndsWith("\n"));
        }

        // B9: ToLlmPayload — chip with depth=none → context block excluded, @mention remains
        [Test]
        public void ToLlmPayload_NoneDepthChip_ContextExcludedMentionRemains()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            var positioned = new List<PositionedChip> { PC(chip, 0) };
            var msg = ChipTextInterleaver.Build("fix", positioned);
            var cfg = new ChipConfig { ScriptDepth = "none" };
            var payload = ChipTextInterleaver.ToLlmPayload(msg, cfg);
            Assert.AreEqual("@Assets/Foo.cs fix", payload);
            Assert.IsFalse(payload.Contains("[script:"));
        }

        // Extra: null text treated as empty
        [Test]
        public void Extra_NullText_TreatedAsEmpty()
        {
            var msg = ChipTextInterleaver.Build(null, new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.IsFalse(msg.Segments[0].IsChip);
        }

        // Extra: chip list preserves only chips (no duplicates in Chips property)
        [Test]
        public void Extra_TwoChips_ChipsPropertyHasBoth()
        {
            var chipA = H("/A", "A", 1);
            var chipB = H("/B", "B", 2);
            var msg = ChipTextInterleaver.Build("hi", new List<PositionedChip>
                { PC(chipA, 0), PC(chipB, 1) });
            Assert.AreEqual(2, msg.Chips.Count);
        }

        // ── Group U: User scenario regression tests ───────────────────────────

        // U1: chip at 0, text "jhkjhkj", chip at 7 → [chip, text, chip] segments
        [Test]
        public void ChipTextChip_InterleavedSegments()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -99);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);

            Assert.AreEqual(3, msg.Segments.Count);
            Assert.IsTrue(msg.Segments[0].IsChip);
            Assert.AreEqual("/Main Camera", msg.Segments[0].Chip.Path);
            Assert.IsFalse(msg.Segments[1].IsChip);
            Assert.AreEqual("jhkjhkj", msg.Segments[1].Text);
            Assert.IsTrue(msg.Segments[2].IsChip);
            Assert.AreEqual("/Light", msg.Segments[2].Chip.Path);
            Assert.AreEqual(2, msg.Chips.Count);
        }

        // U2: same as U1 but verify LLM payload contains chip context
        [Test]
        public void ChipTextChip_LlmPayloadContainsChipContext()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -99);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());

            StringAssert.Contains("jhkjhkj", payload);
            StringAssert.Contains("[hierarchy:", payload);
            Assert.Greater(payload.Length, "jhkjhkj".Length);
        }

        // U3: both chips at offset 7 (end of text) — text first, then both chips.
        // Regression: chips must NOT be lost even when offset equals text length.
        [Test]
        public void BothChipsAtEnd_TextThenBothChips()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -99);
            var positioned = new List<PositionedChip> { PC(chipA, 7), PC(chipB, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);

            Assert.AreEqual(2, msg.Chips.Count);
            var chipSegs = new List<MessageSegment>();
            foreach (var s in msg.Segments) if (s.IsChip) chipSegs.Add(s);
            Assert.AreEqual(2, chipSegs.Count);
        }

        // U4: chips at end — LLM payload still includes chip context
        [Test]
        public void BothChipsAtEnd_PayloadIncludesChipContext()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var positioned = new List<PositionedChip> { PC(chipA, 7) };
            var msg = ChipTextInterleaver.Build("jhkjhkj", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());

            StringAssert.Contains("[hierarchy:", payload);
            StringAssert.Contains("jhkjhkj", payload);
        }

        // U5: display text includes @mentions but no chip context blocks
        [Test]
        public void DisplayText_IncludesAtMentions()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip> { PC(chip, 5) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("hello @Main Camera", display);
            Assert.IsFalse(display.Contains("[hierarchy:"));
        }

        // D9 (moved from UserMessageBubbleTests): no @ in any text segment — Build internals
        [Test]
        public void TextSegments_NoAtMentions()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.Build("fix this", new List<PositionedChip> { PC(chip, 4) });
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text);
        }

        // ── Group M: @mention tests ──────────────────────────────────────────

        // M1: chip at start → display text starts with @DisplayName
        [Test]
        public void ChipAtStart_DisplayStartsWithAtMention()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.Build("do stuff", new List<PositionedChip> { PC(chip, 0) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Player do stuff", display);
        }

        // M2: chip at end → display text ends with @DisplayName
        [Test]
        public void ChipAtEnd_DisplayEndsWithAtMention()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.Build("fix", new List<PositionedChip> { PC(chip, 3) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("fix @Player", display);
        }

        // M3: chip in middle → @DisplayName between text parts
        [Test]
        public void ChipInMiddle_AtMentionBetweenText()
        {
            var chip = H("/Light", "Light", 2);
            var msg = ChipTextInterleaver.Build("move to pos", new List<PositionedChip> { PC(chip, 5) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("move @Light to pos", display);
        }

        // M4: two chips at different positions → both @mentions present
        [Test]
        public void TwoChips_BothAtMentionsInDisplayText()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -100);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", -200);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 4) };
            var msg = ChipTextInterleaver.Build("test", positioned);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Main Camera test @Light", display);
        }

        // M5: consecutive chips (no text between) → @A @B adjacent
        [Test]
        public void ConsecutiveChips_AdjacentAtMentions()
        {
            var chipA = H("/A", "Alpha", 1);
            var chipB = H("/B", "Beta", 2);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 0) };
            var msg = ChipTextInterleaver.Build("go", positioned);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Alpha @Beta go", display);
        }

        // M6: no chips → display text unchanged (no spurious @)
        [Test]
        public void NoChips_DisplayTextUnchanged()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("hello world", display);
        }

        // M7: empty text + chip → just @DisplayName
        [Test]
        public void EmptyTextWithChip_JustAtMention()
        {
            var chip = H("/Cube", "Cube", 5);
            var msg = ChipTextInterleaver.Build("", new List<PositionedChip> { PC(chip, 0) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Cube", display);
        }

        // M8: LLM payload includes @mentions in text part
        [Test]
        public void LlmPayload_IncludesAtMentions()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -100);
            var positioned = new List<PositionedChip> { PC(chip, 0) };
            var msg = ChipTextInterleaver.Build("test", positioned);
            var payload = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            StringAssert.StartsWith("@/Main Camera test", payload);
        }

        // M9: user scenario — "test" with two Main Camera chips at 0 and 5
        [Test]
        public void UserScenario_TwoChipsAroundText()
        {
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -100);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -100);
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 4) };
            var msg = ChipTextInterleaver.Build("test", positioned);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Main Camera test @Main Camera", display);
        }

        // M10: script chip → @DisplayName uses DisplayName not Path
        [Test]
        public void ScriptChip_UsesDisplayName()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Scripts/Foo.cs", "Foo.cs", 0);
            var msg = ChipTextInterleaver.Build("fix", new List<PositionedChip> { PC(chip, 3) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("fix @Foo.cs", display);
        }

        // ── Group R: BuildFromRaw tests ──────────────────────────────────────

        // R1: @mention stripped, display text round-trips
        [Test]
        public void BuildFromRaw_StripsAtMention()
        {
            var chip = H("/Player", "Player", 1);
            var positioned = new List<PositionedChip> { PC(chip, 0) };
            var msg = ChipTextInterleaver.BuildFromRaw("@Player fix", positioned);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Player fix", display);
        }

        // R2: chip at end of text, no trailing space
        [Test]
        public void BuildFromRaw_ChipAtEndNoTrailingSpace()
        {
            var chip = H("/Player", "Player", 1);
            var positioned = new List<PositionedChip> { PC(chip, 4) };
            var msg = ChipTextInterleaver.BuildFromRaw("fix @Player", positioned);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("fix @Player", display);
        }

        // R3: two chips in raw text
        [Test]
        public void BuildFromRaw_TwoChips()
        {
            var chipA = H("/A", "Alpha", 1);
            var chipB = H("/B", "Beta", 2);
            // "@Alpha @Beta go" — chipA at 0, chipB at 7
            var positioned = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 7) };
            var msg = ChipTextInterleaver.BuildFromRaw("@Alpha @Beta go", positioned);
            Assert.AreEqual(2, msg.Chips.Count);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            StringAssert.Contains("@Alpha", display);
            StringAssert.Contains("@Beta", display);
        }

        // R4: no chips — passthrough to Build
        [Test]
        public void BuildFromRaw_NoChips_Passthrough()
        {
            var msg = ChipTextInterleaver.BuildFromRaw("hello world", new List<PositionedChip>());
            Assert.AreEqual(1, msg.Segments.Count);
            Assert.AreEqual("hello world", msg.Segments[0].Text);
        }

        // R5: chip offset beyond text length — guard path, chip still present
        [Test]
        public void BuildFromRaw_OffsetBeyondText_GuardPath()
        {
            var chip = H("/Player", "Player", 1);
            var positioned = new List<PositionedChip> { PC(chip, 100) };
            var msg = ChipTextInterleaver.BuildFromRaw("hi", positioned);
            Assert.AreEqual(1, msg.Chips.Count);
        }

        // ── Group A: agent chip tests ──────────────────────────────────────────

        // A1: agent chip in payload → context block contains [agent:name]
        [Test]
        public void AgentChip_InContextBlock_FormatsAsAgentTag()
        {
            var chip       = new ChipData(ChipKindKeys.Agent, "senior-developer", "senior-developer", 0);
            var positioned = new List<PositionedChip> { PC(chip, 0) };
            var msg        = ChipTextInterleaver.Build("fix bug", positioned);
            var payload    = ChipTextInterleaver.ToLlmPayload(msg, new ChipConfig());
            StringAssert.Contains("[agent:senior-developer]", payload);
        }
    }
}
