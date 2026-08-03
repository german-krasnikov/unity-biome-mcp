// TDD — InlineChipModel tests (Wave 0, replaces InlineChipTrackerTests.cs).
// Pure C# data model: no Unity rendering dependency. All headless.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipModelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // ── Count ─────────────────────────────────────────────────────────────

        [Test]
        public void AddChip_IncreasesCount()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Player", "Player", 1));
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void AddMultiple_PreservesOrder()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));
            m.Add(new ChipData(ChipKindKeys.Script,    "/C", "C", 3));

            Assert.AreEqual(3, m.Count);
            Assert.AreEqual("/A", m.Chips[0].Path);
            Assert.AreEqual("/B", m.Chips[1].Path);
            Assert.AreEqual("/C", m.Chips[2].Path);
        }

        [Test]
        public void RemoveAt_DecrementsCount()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));

            m.RemoveAt(0);

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual("/B", m.Chips[0].Path);
        }

        [Test]
        public void RemoveAt_OutOfRange_NoThrow()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1));

            Assert.DoesNotThrow(() => m.RemoveAt(-1));
            Assert.DoesNotThrow(() => m.RemoveAt(99));
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void Clear_EmptiesAll()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/C", "C", 3));

            m.Clear();

            Assert.AreEqual(0, m.Count);
        }

        // ── SerializePayload ──────────────────────────────────────────────────

        [Test]
        public void SerializePayload_UsesRegistryFormatPayload()
        {
            // Script chip — FormatPayload returns [script:path]
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0));

            var payload = m.SerializePayload(new ChipConfig());

            StringAssert.Contains("[script:Assets/Foo.cs]", payload);
        }

        [Test]
        public void SerializePayload_SkipsNoneDepth()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0));

            // Force all depths to "none"
            var cfg = new ChipConfig
            {
                ScriptDepth    = "none",
                HierarchyDepth = "none",
                SceneDepth     = "none",
                PrefabDepth    = "none",
                AssetDepth     = "none"
            };

            var payload = m.SerializePayload(cfg);

            Assert.IsEmpty(payload);
        }

        [Test]
        public void SerializePayload_MultipleChips_NewlineSeparated()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/A.cs", "A.cs", 0));
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/B.cs", "B.cs", 0));

            var payload = m.SerializePayload(new ChipConfig());

            StringAssert.Contains("[script:Assets/A.cs]", payload);
            StringAssert.Contains("[script:Assets/B.cs]", payload);
            // The two payloads must be separated by a newline
            StringAssert.Contains("\n", payload);
        }

        // ── Serialize / Restore ───────────────────────────────────────────────

        [Test]
        public void Serialize_Roundtrip()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Player", "Player", 1));
            m.Add(new ChipData(ChipKindKeys.Script,    "Assets/Foo.cs", "Foo.cs", 0));

            var (paths, kindKeys) = m.SerializeForReload();

            var m2 = new InlineChipModel();
            m2.RestoreFromReload(paths, kindKeys);

            Assert.AreEqual(2,                        m2.Count);
            Assert.AreEqual("/World/Player",           m2.Chips[0].Path);
            Assert.AreEqual(ChipKindKeys.Hierarchy,    m2.Chips[0].KindKey);
            Assert.AreEqual("Assets/Foo.cs",           m2.Chips[1].Path);
            Assert.AreEqual(ChipKindKeys.Script,       m2.Chips[1].KindKey);
        }

        [Test]
        public void Restore_EmptyArrays_NoChips()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new string[0], new string[0]);
            Assert.AreEqual(0, m.Count);
        }

        [Test]
        public void Restore_V3BackCompat_EmptyKindKey()
        {
            // v3 format: kindKey is "" — model must preserve empty string (not null, not remapped)
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "Assets/foo.fbx" }, new[] { "" });

            Assert.AreEqual(1, m.Count);
            // Path must survive
            Assert.AreEqual("Assets/foo.fbx", m.Chips[0].Path);
            // Empty kindKey must be preserved exactly (v3 back-compat contract)
            Assert.IsNotNull(m.Chips[0].KindKey);
            Assert.AreEqual("", m.Chips[0].KindKey);
        }

        // ── Group A: PositionedChip + InsertAt + AdjustOffsets ────────────────

        // A1: InsertAt stores chip with correct offset
        [Test]
        public void InsertAt_StoresChipWithCorrectOffset()
        {
            var m = new InlineChipModel();
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1);
            m.InsertAt(7, chip);
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(7, m.PositionedChips[0].TextOffset);
            Assert.AreEqual("/Player", m.PositionedChips[0].Chip.Path);
        }

        // A2: InsertAt multiple chips maintains sorted order by TextOffset
        [Test]
        public void InsertAt_MultipleChips_SortedByOffset()
        {
            var m = new InlineChipModel();
            m.InsertAt(10, new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));
            m.InsertAt(3,  new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.InsertAt(15, new ChipData(ChipKindKeys.Hierarchy, "/C", "C", 3));
            Assert.AreEqual(3,  m.Count);
            Assert.AreEqual(3,  m.PositionedChips[0].TextOffset);
            Assert.AreEqual(10, m.PositionedChips[1].TextOffset);
            Assert.AreEqual(15, m.PositionedChips[2].TextOffset);
            Assert.AreEqual("/A", m.PositionedChips[0].Chip.Path);
            Assert.AreEqual("/B", m.PositionedChips[1].Chip.Path);
        }

        // A3: RemoveAt(1) removes exactly chip at index 1 (not by name matching)
        [Test]
        public void RemoveAt_ByIndex_NotByName()
        {
            var m = new InlineChipModel();
            m.InsertAt(0,  new ChipData(ChipKindKeys.Hierarchy, "/A", "Same", 1));
            m.InsertAt(5,  new ChipData(ChipKindKeys.Hierarchy, "/B", "Same", 2));
            m.InsertAt(10, new ChipData(ChipKindKeys.Hierarchy, "/C", "Same", 3));
            m.RemoveAt(1);
            Assert.AreEqual(2, m.Count);
            Assert.AreEqual("/A", m.PositionedChips[0].Chip.Path);
            Assert.AreEqual("/C", m.PositionedChips[1].Chip.Path);
        }

        // A4: Two chips with identical DisplayName — RemoveAt(0) removes chip 0, chip 1 remains
        [Test]
        public void RemoveAt_DuplicateDisplayName_RemovesCorrectOne()
        {
            var m = new InlineChipModel();
            m.InsertAt(0, new ChipData(ChipKindKeys.Hierarchy, "/A", "Camera", 1));
            m.InsertAt(5, new ChipData(ChipKindKeys.Hierarchy, "/B", "Camera", 2));
            m.RemoveAt(0);
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual("/B", m.PositionedChips[0].Chip.Path);
            Assert.AreEqual(5,   m.PositionedChips[0].TextOffset);
        }

        // A5: AdjustOffsets — insert 3 chars at pos 5: only chips STRICTLY AFTER offset 5 shift.
        // Chip AT offset 5 stays — standard text-editor bookmark semantic (> not >=).
        [Test]
        public void AdjustOffsets_Insert_ShiftsChipsStrictlyAfterChangeAt()
        {
            var m = new InlineChipModel();
            m.InsertAt(3,  new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1)); // before 5 → unchanged
            m.InsertAt(5,  new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2)); // AT 5 → stays (not >=)
            m.InsertAt(10, new ChipData(ChipKindKeys.Hierarchy, "/C", "C", 3)); // after 5 → shifts
            m.AdjustOffsetsAfterTextChange(5, +3);
            Assert.AreEqual(3,  m.PositionedChips[0].TextOffset); // unchanged
            Assert.AreEqual(5,  m.PositionedChips[1].TextOffset); // stays at 5, NOT 8
            Assert.AreEqual(13, m.PositionedChips[2].TextOffset); // 10+3
        }

        // A6: AdjustOffsets — delete 2 chars at pos 3: chip AT pos 3 stays, chip after shifts.
        [Test]
        public void AdjustOffsets_Delete_ShiftsChipsStrictlyAfterChangeAt()
        {
            var m = new InlineChipModel();
            m.InsertAt(1,  new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1)); // before 3 → unchanged
            m.InsertAt(3,  new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2)); // AT 3 → stays
            m.InsertAt(8,  new ChipData(ChipKindKeys.Hierarchy, "/C", "C", 3)); // after 3 → shifts
            m.AdjustOffsetsAfterTextChange(3, -2);
            Assert.AreEqual(1, m.PositionedChips[0].TextOffset); // unchanged
            Assert.AreEqual(3, m.PositionedChips[1].TextOffset); // stays at 3, NOT 1
            Assert.AreEqual(6, m.PositionedChips[2].TextOffset); // 8-2
        }

        // A7: change before first chip: chip offsets unchanged
        [Test]
        public void AdjustOffsets_ChangeBeforeAllChips_NoChange()
        {
            var m = new InlineChipModel();
            m.InsertAt(10, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.AdjustOffsetsAfterTextChange(0, +5);
            Assert.AreEqual(15, m.PositionedChips[0].TextOffset);
        }

        // A8: SerializeForReload roundtrip includes TextOffsets via GetTextOffsets
        [Test]
        public void SerializeForReload_Roundtrip_IncludesOffsets()
        {
            var m = new InlineChipModel();
            m.InsertAt(3,  new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.InsertAt(10, new ChipData(ChipKindKeys.Script,    "B.cs", "B", 0));
            var (paths, kindKeys) = m.SerializeForReload();
            var offsets = m.GetTextOffsets();
            Assert.AreEqual(2, offsets.Length);
            Assert.AreEqual(3,  offsets[0]);
            Assert.AreEqual(10, offsets[1]);
            var m2 = new InlineChipModel();
            m2.RestoreFromReload(paths, kindKeys, offsets);
            Assert.AreEqual(3,  m2.PositionedChips[0].TextOffset);
            Assert.AreEqual(10, m2.PositionedChips[1].TextOffset);
        }

        // A9: RestoreFromReload with null offsets — chips restored at offset 0, no throw
        [Test]
        public void RestoreFromReload_NullOffsets_DefaultsToZero()
        {
            var m = new InlineChipModel();
            Assert.DoesNotThrow(() =>
                m.RestoreFromReload(new[] { "/A", "/B" },
                    new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Script },
                    null));
            Assert.AreEqual(2, m.Count);
            Assert.AreEqual(0, m.PositionedChips[0].TextOffset);
            Assert.AreEqual(0, m.PositionedChips[1].TextOffset);
        }

        // ── Group R: Regression — offset drift bug (>= vs >) ─────────────────

        // R1: chip inserted at offset 0, then user types 7 chars one-by-one starting at 0.
        // Each keystroke calls AdjustOffsetsAfterTextChange(cursorPos, 1).
        // Cursor advances: 0,1,2,3,4,5,6. Chip must STAY at 0 (not drift to 7).
        [Test]
        public void InsertAt0_Type7Chars_ChipStaysAtOffset0()
        {
            var m = new InlineChipModel();
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            m.InsertAt(0, chipA);

            // Simulate typing 7 chars at cursor positions 0..6
            for (int i = 0; i < 7; i++)
                m.AdjustOffsetsAfterTextChange(i, 1);

            Assert.AreEqual(0, m.PositionedChips[0].TextOffset);
        }

        // R2: chip at 0, type 7 chars, then insert second chip at offset 7.
        // Result: chipA at 0, chipB at 7.
        [Test]
        public void InsertAt0_Type7_InsertAt7_CorrectOffsets()
        {
            var m = new InlineChipModel();
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Directional Light", "Directional Light", -99);
            m.InsertAt(0, chipA);

            for (int i = 0; i < 7; i++)
                m.AdjustOffsetsAfterTextChange(i, 1);

            m.InsertAt(7, chipB);

            Assert.AreEqual(2, m.Count);
            Assert.AreEqual(0, m.PositionedChips[0].TextOffset);
            Assert.AreEqual(7, m.PositionedChips[1].TextOffset);
            Assert.AreEqual("/Main Camera",       m.PositionedChips[0].Chip.Path);
            Assert.AreEqual("/Directional Light", m.PositionedChips[1].Chip.Path);
        }

        // R3: chip at 5, delete 1 char at pos 2 (before chip) → chip shifts to 4.
        [Test]
        public void AdjustOffsets_DeleteBeforeChip_ShiftsLeft()
        {
            var m = new InlineChipModel();
            m.InsertAt(5, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.AdjustOffsetsAfterTextChange(2, -1);
            Assert.AreEqual(4, m.PositionedChips[0].TextOffset);
        }

        // R4: chip at 5, delete 1 char AT chip's position → chip stays at 5 (> not >=).
        [Test]
        public void AdjustOffsets_DeleteAtChipPosition_ChipStays()
        {
            var m = new InlineChipModel();
            m.InsertAt(5, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.AdjustOffsetsAfterTextChange(5, -1);
            Assert.AreEqual(5, m.PositionedChips[0].TextOffset);
        }

        // R5: two duplicate chips (same display name) at different offsets after typing scenario.
        // Simulate: insert chipA at 0, type 7 chars, insert chipB at 7.
        // Then remove chipB (index 1). chipA at 0 must survive.
        [Test]
        public void TwoDuplicateChips_RemoveSecond_FirstSurvivesAtOffset0()
        {
            var m = new InlineChipModel();
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -12345);
            m.InsertAt(0, chipA);

            for (int i = 0; i < 7; i++)
                m.AdjustOffsetsAfterTextChange(i, 1);

            m.InsertAt(7, chipB);

            Assert.AreEqual(2, m.Count);
            Assert.AreEqual(0, m.PositionedChips[0].TextOffset);
            Assert.AreEqual(7, m.PositionedChips[1].TextOffset);

            m.RemoveAt(1); // remove chipB

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(0, m.PositionedChips[0].TextOffset);
            Assert.AreEqual("/Main Camera", m.PositionedChips[0].Chip.Path);
        }

        // ── Group I: AdjustOffsetsAfterTextChangeInclusive (CH3.test.3) ──────────

        // I1: AdjustOffsetsAfterTextChangeInclusive — chip AT changeAt shifts (inclusive semantics)
        [Test]
        public void AdjustOffsetsInclusive_ChipAtChangeAt_Shifts()
        {
            var m = new InlineChipModel();
            m.InsertAt(5, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.AdjustOffsetsAfterTextChangeInclusive(5, +3);
            Assert.AreEqual(8, m.PositionedChips[0].TextOffset, "chip AT changeAt must shift with inclusive semantics");
        }

        // I2: AdjustOffsetsAfterTextChangeInclusive — chip BEFORE changeAt stays
        [Test]
        public void AdjustOffsetsInclusive_ChipBeforeChangeAt_Stays()
        {
            var m = new InlineChipModel();
            m.InsertAt(3, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.AdjustOffsetsAfterTextChangeInclusive(5, +3);
            Assert.AreEqual(3, m.PositionedChips[0].TextOffset, "chip before changeAt must not shift");
        }

        // I3: AdjustOffsetsAfterTextChangeInclusive — chip AFTER changeAt also shifts
        [Test]
        public void AdjustOffsetsInclusive_ChipAfterChangeAt_Shifts()
        {
            var m = new InlineChipModel();
            m.InsertAt(10, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.AdjustOffsetsAfterTextChangeInclusive(5, +3);
            Assert.AreEqual(13, m.PositionedChips[0].TextOffset, "chip after changeAt must shift");
        }

        // I4: contrast exclusive vs inclusive at the boundary
        [Test]
        public void Exclusive_vs_Inclusive_AtBoundary()
        {
            // Exclusive: chip AT position stays; inclusive: chip AT position shifts
            var mExcl = new InlineChipModel();
            mExcl.InsertAt(5, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            mExcl.AdjustOffsetsAfterTextChange(5, +2); // exclusive
            Assert.AreEqual(5, mExcl.PositionedChips[0].TextOffset, "exclusive: chip at boundary stays");

            var mIncl = new InlineChipModel();
            mIncl.InsertAt(5, new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            mIncl.AdjustOffsetsAfterTextChangeInclusive(5, +2); // inclusive
            Assert.AreEqual(7, mIncl.PositionedChips[0].TextOffset, "inclusive: chip at boundary shifts");
        }

        // ── Group D: DeriveDisplayName (pipe-path fix) ────────────────────────

        // D1: pipe path with 3 parts → "CompType.fieldName" dot notation
        [Test]
        public void DeriveDisplayName_PipePath_ThreeParts_ReturnsDotNotation()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(
                new[] { "Player|Transform|m_localPosition" },
                new[] { ChipKindKeys.Field });
            Assert.AreEqual("Transform.m_localPosition", m.Chips[0].DisplayName);
        }

        // D2: pipe path with 2 parts → returns suffix after pipe
        [Test]
        public void DeriveDisplayName_PipePath_TwoParts_ReturnsSuffix()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(
                new[] { "Player|Transform" },
                new[] { ChipKindKeys.Component });
            Assert.AreEqual("Transform", m.Chips[0].DisplayName);
        }

        // D3: slash path → returns last segment (regression)
        [Test]
        public void DeriveDisplayName_SlashPath_ReturnsLastSegment()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(
                new[] { "/Root/Child" },
                new[] { ChipKindKeys.Hierarchy });
            Assert.AreEqual("Child", m.Chips[0].DisplayName);
        }

        // R6: full scenario — chip, type "test", chip, build message, verify segment order.
        [Test]
        public void FullScenario_ChipTypeChip_CorrectSegmentOrder()
        {
            var m = new InlineChipModel();
            var chipA = new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1);
            var chipB = new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2);

            m.InsertAt(0, chipA);
            // type "test" (4 chars), cursor advances 0→1→2→3
            for (int i = 0; i < 4; i++)
                m.AdjustOffsetsAfterTextChange(i, 1);
            m.InsertAt(4, chipB);

            Assert.AreEqual(0, m.PositionedChips[0].TextOffset);
            Assert.AreEqual(4, m.PositionedChips[1].TextOffset);

            var msg = ChipTextInterleaver.Build("test", m.PositionedChips);
            // Expected segments: chip(A), text("test"), chip(B)
            Assert.AreEqual(3, msg.Segments.Count);
            Assert.IsTrue(msg.Segments[0].IsChip);
            Assert.AreEqual("/A", msg.Segments[0].Chip.Path);
            Assert.IsFalse(msg.Segments[1].IsChip);
            Assert.AreEqual("test", msg.Segments[1].Text);
            Assert.IsTrue(msg.Segments[2].IsChip);
            Assert.AreEqual("/B", msg.Segments[2].Chip.Path);
            Assert.AreEqual(2, msg.Chips.Count);
        }
    }
}
