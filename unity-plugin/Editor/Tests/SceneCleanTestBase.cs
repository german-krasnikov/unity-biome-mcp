using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    public abstract class SceneCleanTestBase
    {
        HashSet<int> _rootsBefore;

        [SetUp]
        public void SnapshotSceneRoots()
        {
            _rootsBefore = new HashSet<int>(
                SceneManager.GetActiveScene().GetRootGameObjects()
                    .Select(go => go.GetInstanceID()));
        }

        [TearDown]
        public void AssertNoSceneLeaks()
        {
            var after = SceneManager.GetActiveScene().GetRootGameObjects();
            var leaked = after.Where(go => go && !_rootsBefore.Contains(go.GetInstanceID())).ToList();
            var leakedNames = leaked.Select(g => g.name).ToList();
            foreach (var go in leaked)
                Object.DestroyImmediate(go);
            if (leakedNames.Count > 0)
                Assert.Fail($"Test leaked {leakedNames.Count} root objects (cleaned up): " +
                    string.Join(", ", leakedNames));
        }
    }
}
