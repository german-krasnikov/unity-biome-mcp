using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    public abstract class SceneCleanTestBase : SceneTestBase
    {
        private HashSet<GameObject> _rootsBefore;

        [SetUp]
        public void SnapshotSceneRoots()
        {
            _rootsBefore = new HashSet<GameObject>(
                SceneManager.GetActiveScene().GetRootGameObjects()
                .Where(go => go != null));
        }

        [TearDown]
        public void AssertNoSceneLeaks()
        {
            _rootsBefore ??= new HashSet<GameObject>();
            var after = SceneManager.GetActiveScene().GetRootGameObjects();
            var leaked = after.Where(go => go && !_rootsBefore.Contains(go)).ToList();
            var leakedNames = leaked.Select(g => g.name).ToList();

            var cleanupErrors = new List<System.Exception>();
            foreach (var go in leaked)
            {
                try
                {
                    Object.DestroyImmediate(go);
                }
                catch (System.Exception ex)
                {
                    cleanupErrors.Add(ex);
                }
            }

            // The common base repeats this as the final safety cleanup. Keeping the
            // direct reset preserves this method's existing standalone contract.
            CleanDirtyScene();

            if (cleanupErrors.Count > 0)
                throw new System.AggregateException(
                    "Failed to destroy one or more leaked scene roots.", cleanupErrors);
            if (leakedNames.Count > 0)
                Assert.Fail($"Test leaked {leakedNames.Count} root objects (cleaned up): " +
                    string.Join(", ", leakedNames));
        }
    }
}
