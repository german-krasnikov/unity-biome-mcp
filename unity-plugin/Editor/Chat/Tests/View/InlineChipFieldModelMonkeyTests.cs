// InlineChipFieldModelMonkeyTests — 25 model-level tests for InlineChipField.
// Tests 176-200. No real EditorWindow panel needed — direct construction.
// Does NOT duplicate InlineChipFieldTests.cs (pill-row/structure tests).
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Category(TestCategories.Stress)]
    public class InlineChipFieldModelMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetForTests();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetForTests();

        static ChipData H(string path, string name = null)
            => new ChipData(ChipKindKeys.Hierarchy, path, name ?? path, 0);

        [Test] public void Text_Default_IsEmpty()
            => Assert.AreEqual("", new InlineChipField().Text);

        [Test] public void Text_Set_Get_Roundtrip()
        {
            var f = new InlineChipField(); f.Text = "hello";
            Assert.AreEqual("hello", f.Text);
        }

        [Test] public void Text_SetNull_DoesNotThrow()
            => Assert.DoesNotThrow(() => new InlineChipField().Text = null);

        [Test] public void Text_SetUnicode_Preserved()
        {
            var f = new InlineChipField(); f.Text = "こんにちは\U0001F30D";
            Assert.AreEqual("こんにちは\U0001F30D", f.Text);
        }

        [Test] public void Text_Set100KB_NoThrow()
            => Assert.DoesNotThrow(() => new InlineChipField().Text = new string('x', 100_000));

        [Test] public void Model_Default_CountZero()
            => Assert.AreEqual(0, new InlineChipField().Model.Count);

        [Test] public void AddChip_IncreasesModelCount()
        {
            var f = new InlineChipField();
            f.AddChip(H("/A"));
            Assert.AreEqual(1, f.Model.Count);
        }

        [Test] public void AddChip_50x_ModelCount50()
        {
            var f = new InlineChipField();
            for (int i = 0; i < 50; i++) f.AddChip(H($"/O{i}", $"O{i}"));
            Assert.AreEqual(50, f.Model.Count);
        }

        [Test] public void AddChip_NullPath_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => new InlineChipField().AddChip(new ChipData(ChipKindKeys.Hierarchy, null, "N", 0)));

        [Test] public void AddChip_NullName_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => new InlineChipField().AddChip(new ChipData(ChipKindKeys.Hierarchy, "/A", null, 0)));

        [Test] public void AddChip_EmptyPath_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => new InlineChipField().AddChip(new ChipData(ChipKindKeys.Hierarchy, "", "", 0)));

        [Test] public void AddChip_SamePath_2x_ModelCount2()
        {
            var f = new InlineChipField();
            f.AddChip(H("/Same", "Same")); f.AddChip(H("/Same", "Same"));
            Assert.AreEqual(2, f.Model.Count);
        }

        [Test] public void ClearChips_AfterAdd_ModelCountZero()
        {
            var f = new InlineChipField();
            f.AddChip(H("/A")); f.ClearChips();
            Assert.AreEqual(0, f.Model.Count);
        }

        [Test] public void ClearChips_OnEmpty_NoException()
            => Assert.DoesNotThrow(() => new InlineChipField().ClearChips());

        [Test] public void ClearChips_5x_ModelCountZero()
        {
            var f = new InlineChipField();
            f.AddChip(H("/A"));
            for (int i = 0; i < 5; i++) f.ClearChips();
            Assert.AreEqual(0, f.Model.Count);
        }

        [Test] public void AddThenClearThenAdd_ModelCount1()
        {
            var f = new InlineChipField();
            f.AddChip(H("/A")); f.ClearChips(); f.AddChip(H("/B"));
            Assert.AreEqual(1, f.Model.Count);
        }

        [Test] public void TextField_IsNotNull()
            => Assert.IsNotNull(new InlineChipField().TextField);

        [Test] public void TextField_IsInstanceOfTextField()
            => Assert.IsInstanceOf<TextField>(new InlineChipField().TextField);

        [Test] public void Text_AfterClearChips_Preserved()
        {
            var f = new InlineChipField(); f.Text = "keep me"; f.ClearChips();
            Assert.AreEqual("keep me", f.Text);
        }

        [Test] public void Model_PositionedChips_IsNotNull()
            => Assert.IsNotNull(new InlineChipField().Model.PositionedChips);

        [Test] public void Model_PositionedChips_Default_IsEmpty()
            => Assert.AreEqual(0, new InlineChipField().Model.PositionedChips.Count);

        [Test] public void AddChip_CustomKindKey_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => new InlineChipField().AddChip(new ChipData("custom-kind", "/A", "A", 0)));

        [Test] public void AddChip_KindKey_HierarchyPreserved()
        {
            var f = new InlineChipField();
            f.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            Assert.AreEqual(ChipKindKeys.Hierarchy, f.Model.PositionedChips[0].Chip.KindKey);
        }

        [Test] public void RebuildFromModel_NoException()
        {
            var f = new InlineChipField();
            f.AddChip(H("/A"));
            Assert.DoesNotThrow(() => f.RebuildFromModel());
        }

        [Test] public void AddChip_AllBuiltInKindKeys_NoException()
        {
            var f = new InlineChipField();
            var kinds = new[]
            {
                ChipKindKeys.Hierarchy, ChipKindKeys.Scene,  ChipKindKeys.Script,
                ChipKindKeys.Prefab,    ChipKindKeys.Material, ChipKindKeys.Texture,
                ChipKindKeys.Asset,     ChipKindKeys.Folder,   ChipKindKeys.Region
            };
            Assert.DoesNotThrow(() =>
            {
                foreach (var k in kinds) f.AddChip(new ChipData(k, $"/{k}", k, 0));
            });
            Assert.AreEqual(kinds.Length, f.Model.Count);
        }
    }
}
