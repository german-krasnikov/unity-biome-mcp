using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    public abstract class MultiSceneTestBase : SceneTestBase
    {
        protected Scene _additiveScene;
        private readonly List<Scene> _extraScenes = new();
        private string _tempPath;
        private string _additiveTempPath;
        protected string _savedMainSceneName;

        [SetUp]
        public void PrepareMultiSceneFixture()
        {
            _extraScenes.Clear();
            TestPaths.EnsureFolder();
            var current = SceneManager.GetActiveScene();
            _tempPath = TestPaths.TempFolder + $"/{GetType().Name}_temp.unity";
            TrackOwnedAsset(_tempPath);
            if (string.IsNullOrEmpty(current.path))
            {
                if (!EditorSceneManager.SaveScene(current, _tempPath))
                    throw new InvalidOperationException($"Could not save owned test scene '{_tempPath}'.");
            }
            _savedMainSceneName = current.name;

            _additiveScene = CreateOwnedAdditiveScene();
            _additiveTempPath = TestPaths.TempFolder + $"/{GetType().Name}_additive_temp.unity";
            TrackOwnedAsset(_additiveTempPath);
            if (!EditorSceneManager.SaveScene(_additiveScene, _additiveTempPath))
                throw new InvalidOperationException(
                    $"Could not save owned additive scene '{_additiveTempPath}'.");
            // Restore main scene as active — NewScene(Additive) hijacks it
            SceneManager.SetActiveScene(current);
        }

        protected GameObject CreateIn(Scene scene, string name)
        {
            var go = TrackOwnedObject(new GameObject(name));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        protected GameObject CreateChild(GameObject parent, string name)
        {
            var go = TrackOwnedObject(new GameObject(name));
            go.transform.SetParent(parent.transform);
            return go;
        }

        protected Scene AddScene()
        {
            var active = SceneManager.GetActiveScene();
            var s = CreateOwnedAdditiveScene();
            var path = TestPaths.TempFolder + $"/{GetType().Name}_extra_{_extraScenes.Count}.unity";
            TrackOwnedAsset(path);
            if (!EditorSceneManager.SaveScene(s, path))
                throw new InvalidOperationException($"Could not save owned test scene '{path}'.");
            _extraScenes.Add(s);
            // Restore active scene — NewScene(Additive) hijacks it
            SceneManager.SetActiveScene(active);
            return s;
        }

        protected string MainSceneName => SceneManager.GetActiveScene().name;
    }
}
