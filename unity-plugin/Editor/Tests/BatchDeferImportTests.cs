// TDD: Category D — BatchHelper.Execute defer_asset_import opt-in.
// Tests cover the _startEditing/_stopEditing seam, isRoot guard, and finally-always guarantee.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchDeferImportTests : UnityMcpTestBase
    {
        private int _startCount;
        private int _stopCount;
        private Action _origStart;
        private Action _origStop;
        private Func<bool> _origIsReadOnly;

        [SetUp]
        public void SetUp()
        {
            _startCount = 0;
            _stopCount = 0;
            _origStart = BatchHelper._startEditing;
            _origStop = BatchHelper._stopEditing;
            _origIsReadOnly = CommandRouter.IsReadOnly;
            BatchHelper._startEditing = () => _startCount++;
            BatchHelper._stopEditing = () => _stopCount++;
            BatchHelper.IsCompiling = () => false;
            BatchHelper.IsPlayMode = () => false;
            CommandRouter.IsReadOnly = () => false;
            CommandRouter.IsCompiling = () => false;
        }

        [TearDown]
        public void TearDown()
        {
            BatchHelper._startEditing = _origStart;
            BatchHelper._stopEditing = _origStop;
            CommandRouter.IsReadOnly = _origIsReadOnly;
            BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
            BatchHelper.IsPlayMode = () => EditorApplication.isPlaying;
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
        }

        // Scenario 1: default (false) — no editing scope opened at all.
        [Test]
        public void DeferFalse_NoEditingScope()
        {
            BatchHelper.Execute("get_hierarchy path=/", "continue", deferAssetImport: false);
            Assert.That(_startCount, Is.EqualTo(0), "_startEditing must not be called when defer=false");
            Assert.That(_stopCount, Is.EqualTo(0), "_stopEditing must not be called when defer=false");
        }

        // Scenario 2: defer=true at root depth — opens and closes exactly once.
        [Test]
        public void DeferTrue_Root_OpensAndClosesScope()
        {
            BatchHelper.Execute("get_hierarchy path=/", "continue", deferAssetImport: true);
            Assert.That(_startCount, Is.EqualTo(1), "_startEditing must be called once");
            Assert.That(_stopCount, Is.EqualTo(1), "_stopEditing must be called once");
        }

        // Scenario 3: nested batch with defer=true in inner command — isRoot guard prevents double-open.
        [Test]
        public void DeferTrue_Nested_OnlyRootOpensScope()
        {
            // Outer batch has defer=true. Inner `batch` sub-command also passes defer_asset_import=true,
            // but isRoot==false at depth==2, so deferImport=false there.
            BatchHelper.Execute(
                "batch commands=\"get_hierarchy path=/\" defer_asset_import=true",
                "continue", deferAssetImport: true);
            Assert.That(_startCount, Is.EqualTo(1), "_startEditing must be called only by the outermost batch");
            Assert.That(_stopCount, Is.EqualTo(1), "_stopEditing must be called only by the outermost batch");
        }

        // Scenario 4: atomic batch with failing op + defer=true — finally still closes scope.
        [Test]
        public void DeferTrue_AtomicRollback_StillClosesScope()
        {
            // nonexistent_command fails validation → AtomicFail → break → finally must still call _stopEditing.
            BatchHelper.Execute("nonexistent_command arg=val", "continue",
                atomic: true, deferAssetImport: true);
            Assert.That(_stopCount, Is.EqualTo(1), "_stopEditing must be called even after atomic rollback");
        }

        // Scenario 5: mutation_mode ON with no explicit defer_asset_import → auto-defer activates.
        [Test]
        public void MutationModeOn_AutoDefer_OpensScope()
        {
            HotReloadDetector._overrideForTest = () => true;
            RegisterCleanup(() => HotReloadDetector._overrideForTest = null);
            // Execute via the registered command (defer_asset_import absent → auto-detect)
            var result = CommandRegistry.Execute("batch",
                "{\"commands\":\"get_hierarchy path=/\"}");
            Assert.That(_startCount, Is.EqualTo(1), "Auto-defer must open editing scope when mutation_mode is ON");
            Assert.That(_stopCount, Is.EqualTo(1), "Auto-defer must close editing scope when mutation_mode is ON");
        }

        // Scenario 6: mutation_mode OFF with no explicit defer_asset_import → no auto-defer.
        [Test]
        public void MutationModeOff_NoAutoDefer_NoScope()
        {
            HotReloadDetector._overrideForTest = () => false;
            RegisterCleanup(() => HotReloadDetector._overrideForTest = null);
            var result = CommandRegistry.Execute("batch",
                "{\"commands\":\"get_hierarchy path=/\"}");
            Assert.That(_startCount, Is.EqualTo(0), "No auto-defer when mutation_mode is OFF");
        }
    }
}
