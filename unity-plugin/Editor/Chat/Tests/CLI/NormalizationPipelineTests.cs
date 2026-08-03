// TDD — F20-F26 unification: verifies that Path B (SceneNameLinker underline links)
// is gone and ALL object refs terminate at the pill path. v2: SceneNameLinker deleted.
// Pure headless — no UnityEditor/UnityEngine runtime deps.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class NormalizationPipelineTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // ── Test 1 (GREEN after deletion) ────────────────────────────────────
        // SceneNameLinker is gone: ToRichText must not emit underline-link format.
        // Was RED when SceneNameLinker + MarkdownInline.Linker seam existed.
        // GREEN after: both are deleted — ToRichText can't produce <u> object links.
        [Test]
        public void SceneNameLinker_Deleted_NoUnderlineLinks()
        {
            // After deletion: no seam exists, so no way to inject underline-linking.
            var text   = "I created Boss_01 here.";
            var result = MarkdownInline.ToRichText(text);
            // ToRichText must not produce the Path-B underline-link format.
            StringAssert.DoesNotContain("<u>Boss_01</u>", result,
                "Path B (SceneNameLinker underline link) must be gone — all refs must be pills");
        }

        // ── Test 2 (GREEN now — regression guard) ────────────────────────────
        // FreezeNormalization idempotent: applying At+BareName pipeline twice == once.
        [TestCase("hello world",        null,          null,        "no chips")]
        [TestCase("I moved @Player",    "/Player",     "Player",    "atMention")]
        [TestCase("fix Player health",  "/Player",     "Player",    "bareName")]
        [TestCase("Boss_01 created",    "/Boss_01",    "Boss_01",   "bareName with digit-underscore")]
        [TestCase("check @NPC_02 now",  "/NPC_02",     "NPC_02",   "atMention underscore")]
        public void FreezeNormalization_Idempotent(
            string rawText, string path, string name, string label)
        {
            var chips = (path != null)
                ? new List<ChipData> { new ChipData(ChipKindKeys.Hierarchy, path, name, 0) }
                : null;

            var once  = Normalize(rawText, chips);
            var twice = Normalize(once,   chips);
            Assert.AreEqual(once, twice, $"[{label}] Pipeline must be idempotent");
        }

        // ── Test 3 (GREEN now — regression guard) ────────────────────────────
        // BareNameNormalizer with a sceneMap-style chip produces [kind:ref], NOT underline link.
        [Test]
        public void BareNameNormalizer_SceneObjects_ProducesBracketTag_NotUnderlineLink()
        {
            // Simulates FreezeAssistantBubble scene-wide pass:
            // sceneMap {"Boss_01" -> "/Boss_01"} → chip with length>1
            var sceneChips = new List<ChipData>
            {
                new ChipData(ChipKindKeys.Hierarchy, "/Boss_01", "Boss_01", 0)
            };
            var result = BareNameNormalizer.Normalize("I created Boss_01.", sceneChips);

            // Must produce [hierarchy:...] bracket tag
            StringAssert.Contains("[hierarchy:/Boss_01]", result,
                "BareNameNormalizer must produce bracket tag for scene objects");
            // Must NOT produce SceneNameLinker-style underline link
            StringAssert.DoesNotContain("<u>Boss_01</u>", result,
                "Must not produce underline-link — all refs must go through pill path");
            StringAssert.DoesNotContain("<link=\"chip:hierarchy:", result,
                "Raw chip: link tags must not appear in normalizer output — that is renderer territory");
        }

        // ── private helper ────────────────────────────────────────────────────

        private static string Normalize(string text, IReadOnlyList<ChipData> chips)
        {
            if (chips == null || chips.Count == 0) return text;
            var r = AtMentionNormalizer.Normalize(text, chips);
            r     = BareNameNormalizer.Normalize(r,     chips);
            return r;
        }
    }
}
