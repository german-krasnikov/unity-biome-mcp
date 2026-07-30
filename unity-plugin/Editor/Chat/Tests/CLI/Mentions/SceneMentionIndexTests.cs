// TDD Phase 2: SceneMentionIndex tests.
// Tests focus on testable logic: scoring, caps, dirty-flag, char mask.
// GameObject creation is used for Search_MatchesByName.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneMentionIndexTests
    {
        private SceneMentionIndex _index;
        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp() => _index = new SceneMentionIndex();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        private GameObject CreateGO(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // 1. Empty query returns nothing
        [Test]
        public void Search_EmptyQuery_ReturnsNothing()
        {
            var results = new List<MentionCandidate>();
            _index.Search("", 10, results);
            Assert.That(results, Is.Empty);
        }

        // 2. Never exceeds maxResults
        [Test]
        public void Search_CapsAtMaxResults()
        {
            for (int i = 0; i < 20; i++) CreateGO($"Player{i}");
            _index.RefreshIfDirty();

            var results = new List<MentionCandidate>();
            _index.Search("player", 5, results);
            Assert.That(results.Count, Is.LessThanOrEqualTo(5));
        }

        // 3. RefreshIfDirty skips when version unchanged
        [Test]
        public void RefreshIfDirty_SkipsWhenClean()
        {
            _index.RefreshIfDirty(); // initial build
            var v1 = _index.CachedVersion;
            _index.RefreshIfDirty(); // should NOT rebuild (same VersionTracker.Version)
            Assert.That(_index.CachedVersion, Is.EqualTo(v1));
        }

        // 4. Search finds created GameObject by name
        [Test]
        public void Search_MatchesByName()
        {
            CreateGO("PlayerController");
            _index.RefreshIfDirty();

            var results = new List<MentionCandidate>();
            _index.Search("player", 10, results);
            Assert.That(results.Count, Is.GreaterThan(0));
            Assert.That(results[0].Chip.KindKey, Is.EqualTo(ChipKindKeys.Hierarchy));
        }

        // 5. Non-matching query returns nothing
        [Test]
        public void Search_RejectsNonMatch()
        {
            CreateGO("Camera");
            _index.RefreshIfDirty();

            var results = new List<MentionCandidate>();
            _index.Search("xyzqwerty", 10, results);
            Assert.That(results, Is.Empty);
        }

        // 6. Entry char masks are non-zero for typical names
        [Test]
        public void CharMask_PrecomputedNonZero()
        {
            CreateGO("MainCamera");
            _index.RefreshIfDirty();

            // Access first entry mask via the index
            Assert.That(_index.EntryCount, Is.GreaterThan(0));
            Assert.That(_index.GetEntryMask(0), Is.Not.EqualTo(0u));
        }

        // 7. After domain reload (fresh index), dirty flag is set
        [Test]
        public void DomainReload_FreshIndex_IsDirty()
        {
            // A brand-new SceneMentionIndex (simulates post-domain-reload state)
            var fresh = new SceneMentionIndex();
            Assert.That(fresh.IsDirty, Is.True);
        }
    }
}
