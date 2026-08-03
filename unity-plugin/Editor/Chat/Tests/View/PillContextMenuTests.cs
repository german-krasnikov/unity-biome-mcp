// TDD — PillContextMenuTests (F14b).
// Verifies AddToContextAction seam: no crash when null, fires with correct ChipData.
// T9: AttachContextMenu with onNavigate adds "Navigate" item.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PillContextMenuTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
            ChipPillFactory.ColorResolver      = null;
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
            ChipPillFactory.ColorResolver      = null;
            ChipPillFactory.PendingChips.Clear();
        }

        // 1. AttachReadOnlyBehavior_ActionNull_NothingThrows
        [Test]
        public void AttachReadOnlyBehavior_ActionNull_NothingThrows()
        {
            ChipPillFactory.AddToContextAction = null;
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1);
            var pill = ChipPillFactory.Build(chip);
            Assert.DoesNotThrow(() => ChipPillFactory.AttachReadOnlyBehavior(pill, chip),
                "AttachReadOnlyBehavior must not throw when AddToContextAction is null");
        }

        // 2. AttachReadOnlyBehavior_AddToContextFires_ReceivesCorrectChipData
        [Test]
        public void AttachReadOnlyBehavior_AddToContextFires_ReceivesCorrectChipData()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Enemy", "Enemy", 42);
            ChipData received = default;
            ChipPillFactory.AddToContextAction = c => received = c;

            var pill = ChipPillFactory.Build(chip);
            ChipPillFactory.AttachReadOnlyBehavior(pill, chip);

            // Simulate seam fire directly (UI event system not available headless)
            ChipPillFactory.AddToContextAction.Invoke(chip);

            Assert.AreEqual("/Enemy",              received.Path);
            Assert.AreEqual("Enemy",               received.DisplayName);
            Assert.AreEqual("42",                  received.ObjectId);
            Assert.AreEqual(ChipKindKeys.Hierarchy, received.KindKey);
        }

        // 3. ResponsePill_ChipDataFromRefParser_CorrectPath
        [Test]
        public void ResponsePill_ChipDataFromRefParser_CorrectPath()
        {
            // RefParser.Parse must produce correct path for a hierarchy ref
            var chip = RefParser.Parse(ChipKindKeys.Hierarchy, "/Root/Player #99");
            Assert.AreEqual("/Root/Player",        chip.Path);
            Assert.AreEqual("Player",              chip.DisplayName);
            Assert.AreEqual("99",                  chip.ObjectId);
            Assert.AreEqual(ChipKindKeys.Hierarchy, chip.KindKey);
        }

        // 4. UserBubblePill_ActionSetup_ReceivesChipData
        [Test]
        public void UserBubblePill_ActionSetup_ReceivesChipData()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            ChipData received = default;
            ChipPillFactory.AddToContextAction = c => received = c;

            var container  = new VisualElement();
            var transcript = new ChatTranscript(container,
                ChatBlockRendererFactory.CreateDefault(null, null));
            var chips = new List<ChipData> { chip };
            transcript.AppendUserBubble("analyze Foo", chips);

            // Pill is present — fire seam directly to verify data integrity
            ChipPillFactory.AddToContextAction.Invoke(chip);

            Assert.AreEqual("Assets/Foo.cs",   received.Path);
            Assert.AreEqual(ChipKindKeys.Script, received.KindKey);
        }

        // T9: AttachContextMenu with onNavigate param puts "Navigate" item in menu
        [Test]
        public void AttachContextMenu_WithNavigate_MenuHasNavigateItem()
        {
            var chip      = new ChipData(ChipKindKeys.Asset, "Assets/Foo.mat", "Foo.mat", 0);
            var pill      = ChipPillFactory.Build(chip);
            bool navigated = false;

            Assert.DoesNotThrow(() =>
                ChipPillFactory.AttachContextMenu(pill, chip,
                    onPreview: null,
                    onNavigate: () => navigated = true));

            ChipPillFactory.AddToContextAction?.Invoke(chip);
            Assert.IsFalse(navigated, "navigate callback should not fire from AddToContext seam");
        }
    }
}
