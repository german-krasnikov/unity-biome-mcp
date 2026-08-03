// TDD — InlineChipModel additional tests (Wave 1).
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipModelExtraTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();
        private static ChipData H(string path) => new ChipData(ChipKindKeys.Hierarchy, path, path, 0);

        [Test]
        public void Add5Chips_CountIs5()
        {
            var m = new InlineChipModel();
            for (int i = 0; i < 5; i++) m.Add(H("/N" + i));
            Assert.AreEqual(5, m.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual("/N" + i, m.Chips[i].Path);
        }

        [Test]
        public void Add10Chips_AllPreserved()
        {
            var m = new InlineChipModel();
            for (char c = 'A'; c <= 'J'; c++) m.Add(H("/" + c));
            Assert.AreEqual(10, m.Count);
            Assert.AreEqual("/A", m.Chips[0].Path);
            Assert.AreEqual("/J", m.Chips[9].Path);
        }

        [Test]
        public void RemoveFirst_ShiftsRemaining()
        {
            var m = new InlineChipModel();
            m.Add(H("/A")); m.Add(H("/B")); m.Add(H("/C"));
            m.RemoveAt(0);
            Assert.AreEqual(2, m.Count);
            Assert.AreEqual("/B", m.Chips[0].Path);
            Assert.AreEqual("/C", m.Chips[1].Path);
        }

        [Test]
        public void RemoveLast_PreservesOthers()
        {
            var m = new InlineChipModel();
            m.Add(H("/A")); m.Add(H("/B")); m.Add(H("/C"));
            m.RemoveAt(2);
            Assert.AreEqual(2, m.Count);
            Assert.AreEqual("/A", m.Chips[0].Path);
            Assert.AreEqual("/B", m.Chips[1].Path);
        }

        [Test]
        public void RemoveMiddle_ClosesGap()
        {
            var m = new InlineChipModel();
            foreach (char c in "ABCDE") m.Add(H("/" + c));
            m.RemoveAt(2);
            Assert.AreEqual(4, m.Count);
            Assert.AreEqual("/A", m.Chips[0].Path);
            Assert.AreEqual("/B", m.Chips[1].Path);
            Assert.AreEqual("/D", m.Chips[2].Path);
            Assert.AreEqual("/E", m.Chips[3].Path);
        }

        [Test]
        public void RemoveAll_OneByOne_EmptyModel()
        {
            var m = new InlineChipModel();
            m.Add(H("/A")); m.Add(H("/B")); m.Add(H("/C"));
            m.RemoveAt(2); m.RemoveAt(1); m.RemoveAt(0);
            Assert.AreEqual(0, m.Count);
        }

        [Test]
        public void ClearThenAdd_WorksNormally()
        {
            var m = new InlineChipModel();
            m.Add(H("/A")); m.Add(H("/B")); m.Add(H("/C"));
            m.Clear();
            m.Add(H("/X")); m.Add(H("/Y"));
            Assert.AreEqual(2, m.Count);
            Assert.AreEqual("/X", m.Chips[0].Path);
            Assert.AreEqual("/Y", m.Chips[1].Path);
        }

        [Test]
        public void ReAddSameChip_DuplicateAllowed()
        {
            var m = new InlineChipModel();
            m.Add(H("/A")); m.Add(H("/A"));
            Assert.AreEqual(2, m.Count);
        }

        [Test]
        public void DuplicatePaths_DifferentKinds_BothKept()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/X", "X", 0));
            m.Add(new ChipData(ChipKindKeys.Script,    "/X", "X", 0));
            Assert.AreEqual(2, m.Count);
            Assert.AreEqual(ChipKindKeys.Hierarchy, m.Chips[0].KindKey);
            Assert.AreEqual(ChipKindKeys.Script,    m.Chips[1].KindKey);
        }
        [Test]
        public void SerializeForReload_FiveKinds_AllPreserved()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/H",             "H",             0));
            m.Add(new ChipData(ChipKindKeys.Script,    "Assets/S.cs",    "S.cs",          0));
            m.Add(new ChipData(ChipKindKeys.Prefab,    "Assets/P.prefab","P.prefab",      0));
            m.Add(new ChipData(ChipKindKeys.Material,  "Assets/M.mat",   "M.mat",         0));
            m.Add(new ChipData(ChipKindKeys.Asset,     "Assets/A.fbx",   "A.fbx",         0));
            var (paths, kindKeys) = m.SerializeForReload();
            var m2 = new InlineChipModel();
            m2.RestoreFromReload(paths, kindKeys);
            Assert.AreEqual(5, m2.Count);
            Assert.AreEqual(ChipKindKeys.Hierarchy, m2.Chips[0].KindKey);
            Assert.AreEqual(ChipKindKeys.Script,    m2.Chips[1].KindKey);
            Assert.AreEqual(ChipKindKeys.Prefab,    m2.Chips[2].KindKey);
            Assert.AreEqual(ChipKindKeys.Material,  m2.Chips[3].KindKey);
            Assert.AreEqual(ChipKindKeys.Asset,     m2.Chips[4].KindKey);
        }

        [Test]
        public void SerializeForReload_OrderPreserved()
        {
            var m = new InlineChipModel();
            m.Add(H("/Z")); m.Add(H("/A")); m.Add(H("/M"));
            var (paths, kindKeys) = m.SerializeForReload();
            var m2 = new InlineChipModel();
            m2.RestoreFromReload(paths, kindKeys);
            Assert.AreEqual("/Z", m2.Chips[0].Path);
            Assert.AreEqual("/A", m2.Chips[1].Path);
            Assert.AreEqual("/M", m2.Chips[2].Path);
        }

        [Test]
        public void RestoreFromReload_ClearsPreviousChips()
        {
            var m = new InlineChipModel();
            m.Add(H("/Old1")); m.Add(H("/Old2"));
            m.RestoreFromReload(new[] { "/New1" }, new[] { ChipKindKeys.Hierarchy });
            Assert.AreEqual(1, m.Count);
            Assert.AreEqual("/New1", m.Chips[0].Path);
        }

        [Test]
        public void RestoreFromReload_MorePathsThanKindKeys_DefaultsEmptyKind()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "/A", "/B", "/C" }, new[] { ChipKindKeys.Hierarchy });
            Assert.AreEqual(3, m.Count);
            Assert.AreEqual("", m.Chips[1].KindKey);
            Assert.AreEqual("", m.Chips[2].KindKey);
        }

        [Test]
        public void RestoreFromReload_DerivesDisplayNameFromPath()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "/World/Player/Sword" }, new[] { ChipKindKeys.Hierarchy });
            Assert.AreEqual("Sword", m.Chips[0].DisplayName);
        }

        [Test]
        public void RestoreFromReload_AssetPath_DerivesFilename()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "Assets/Scripts/Health.cs" }, new[] { ChipKindKeys.Script });
            Assert.AreEqual("Health.cs", m.Chips[0].DisplayName);
        }

        [Test]
        public void RestoreFromReload_NoSlash_DisplayNameEqualsPath()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "Player" }, new[] { ChipKindKeys.Hierarchy });
            Assert.AreEqual("Player", m.Chips[0].DisplayName);
        }

        [Test]
        public void RestoreFromReload_EmptyPath_EmptyDisplayName()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "" }, new[] { ChipKindKeys.Asset });
            Assert.AreEqual("", m.Chips[0].DisplayName);
        }

        [Test]
        public void RestoreFromReload_TrailingSlash_EmptyDisplay()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "/Root/" }, new[] { ChipKindKeys.Hierarchy });
            Assert.AreEqual("", m.Chips[0].DisplayName);
        }
    }
}
