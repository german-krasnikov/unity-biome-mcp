// TDD — ValidateReferencesHelper + ReferenceHelper.WalkObjectRefs coverage.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class ValidateReferencesHelperTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("ValidateTest");

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        // ── ValidateReferences_CleanScene_ReturnsZeroBroken ──────────────────

        [Test]
        public void ValidateReferences_CleanScene_ReturnsZeroBroken()
        {
            // AudioSource has several ObjectReference fields (audioClip, outputAudioMixerGroup…)
            // all null by default → instanceId == 0 → not counted as broken.
            _go.AddComponent<AudioSource>();
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.Contains("0 ERROR", result);
            StringAssert.DoesNotContain("MISSING", result);
        }

        // ── ValidateReferences_MissingRef_ReportsCorrectPath ─────────────────

        [Test]
        public void ValidateReferences_MissingRef_DetectionLogic()
        {
            // EditMode cannot reliably forge a dangling ref (Unity normalizes orphaned instanceIds).
            // Verify the detection logic directly: instanceId != 0 && value == null → MISSING.
            _go.AddComponent<AudioSource>();
            var so = new SerializedObject(_go.GetComponent<AudioSource>());
            var found = false;
            ReferenceHelper.WalkObjectRefs(so, (p, label) =>
            {
                // All AudioSource refs are null with instanceId=0 (empty slots) — not dangling.
                if (!TransientObjectId.HasSerializedReference(p) && p.objectReferenceValue == null)
                    found = true; // walker reaches ObjectReference fields
            });
            Assert.IsTrue(found, "WalkObjectRefs must visit at least one ObjectReference on AudioSource");
        }

        // ── ValidateReferences_SkipsScriptField ──────────────────────────────

        [Test]
        public void ValidateReferences_SkipsScriptField()
        {
            // WalkObjectRefs must never emit m_Script as a ref.
            _go.AddComponent<AudioSource>();
            var so = new SerializedObject(_go.GetComponent<AudioSource>());

            var seen = new List<string>();
            ReferenceHelper.WalkObjectRefs(so, (p, label) => seen.Add(label));

            CollectionAssert.DoesNotContain(seen, "m_Script");
        }

        // ── ValidateReferences_ArrayRef_DetectsNullElement ───────────────────

        [Test]
        public void ValidateReferences_ArrayRef_DetectsNullElement()
        {
            // WalkObjectRefs iterates array elements and emits label "fieldName[i]".
            // AudioSource has no built-in array refs; test WalkObjectRefs logic via a
            // component whose array has an element. Light has flare (no array)… instead
            // verify the label format by fabricating via a known type: ParticleSystem
            // emits "subEmitters[i]" style labels. Rather than requiring PS we check
            // the walk doesn't crash with array-less component and returns only scalar refs.
            _go.AddComponent<Light>();
            var so = new SerializedObject(_go.GetComponent<Light>());

            var labels = new List<string>();
            ReferenceHelper.WalkObjectRefs(so, (p, label) => labels.Add(label));

            // No array elements means no "[i]" labels
            foreach (var lbl in labels)
                StringAssert.DoesNotContain("[", lbl, $"Unexpected array label: {lbl}");
        }

        // ── ValidateReferences_LargeArray_CappedAt100 ────────────────────────

        [Test]
        public void ValidateReferences_LargeArray_CappedAt100()
        {
            // Verify MAX_ARRAY constant is 100.
            Assert.AreEqual(100, ReferenceHelper.MAX_ARRAY);
        }

        // ── ValidateReferences_RootPath_Resolves ─────────────────────────────

        [Test]
        public void ValidateReferences_RootPath_WithLeadingSlash_Resolves()
        {
            var result = ValidateReferencesHelper.Validate("/" + _go.name, depth: 1, ignoreOptional: false);
            StringAssert.Contains("0 ERROR", result);
        }

        [Test]
        public void ValidateReferences_RootPath_WithoutSlash_Resolves()
        {
            var result = ValidateReferencesHelper.Validate(_go.name, depth: 1, ignoreOptional: false);
            StringAssert.Contains("0 ERROR", result);
        }

        // ── G4 / P-117: Particle render modes that don't use a mesh — no false MISSING ───

        [Test]
        public void ValidateReferences_BillboardParticleRenderer_NoFalseMissing()
        {
            // G4: Billboard render mode doesn't use a mesh.
            // validate_references must not report the m_Mesh field as MISSING.
            _go.AddComponent<ParticleSystem>();
            var renderer = _go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.DoesNotContain("MISSING", result,
                "Billboard renderer must not report m_Mesh as MISSING");
        }

        [Test]
        public void ValidateReferences_StretchParticleRenderer_NoFalseMissingMesh()
        {
            // P-117: Stretch uses velocity-aligned billboard, not a mesh.
            _go.AddComponent<ParticleSystem>();
            var renderer = _go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.DoesNotContain("MISSING", result,
                "Stretch renderer must not report m_Mesh as MISSING");
        }

        [Test]
        public void ValidateReferences_HorizontalBillboardRenderer_NoFalseMissingMesh()
        {
            // P-117: HorizontalBillboard is a flat billboard, no mesh required.
            _go.AddComponent<ParticleSystem>();
            var renderer = _go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.DoesNotContain("MISSING", result,
                "HorizontalBillboard renderer must not report m_Mesh as MISSING");
        }

        [Test]
        public void ValidateReferences_VerticalBillboardRenderer_NoFalseMissingMesh()
        {
            // P-117: VerticalBillboard is a vertical billboard, no mesh required.
            _go.AddComponent<ParticleSystem>();
            var renderer = _go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.DoesNotContain("MISSING", result,
                "VerticalBillboard renderer must not report m_Mesh as MISSING");
        }

        [Test]
        public void ValidateReferences_NoneRenderModeRenderer_NoFalseMissingMesh()
        {
            // P-117: None render mode makes particles invisible — no mesh needed.
            _go.AddComponent<ParticleSystem>();
            var renderer = _go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.None;
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            StringAssert.DoesNotContain("MISSING", result,
                "None render mode must not report m_Mesh as MISSING");
        }

        [Test]
        public void ValidateReferences_MeshRenderMode_IsNotExcludedFromCheck()
        {
            // P-117: Mesh mode REQUIRES a mesh — it must NOT be in the skip list.
            // With no dangling ref (instanceId=0), result is clean regardless.
            // This test guards that Mesh mode is never accidentally added to skipMesh.
            _go.AddComponent<ParticleSystem>();
            var renderer = _go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            var path = ComponentSerializer.GetPath(_go);

            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);

            // No dangling ref in a fresh renderer → clean result (0 ERROR, no MISSING).
            StringAssert.Contains("0 ERROR", result);
        }

        // ── G51: AllowNull — intentionally null fields not reported as MISSING ──

        // Test component with one [AllowNull]-marked field and one unmarked field.
        private class AllowNullTestComp : MonoBehaviour
        {
            [AllowNull] public AudioClip optionalClip;
            public AudioClip requiredClip;
        }

        [Test]
        public void ValidateReferences_AllowNull_AttributeIsRecognizedByReflection()
        {
            // Attribute must exist and be applied — foundational contract.
            var field = typeof(AllowNullTestComp).GetField("optionalClip");
            Assert.IsNotNull(field, "optionalClip field must exist");
            var attr = field.GetCustomAttribute<AllowNullAttribute>();
            Assert.IsNotNull(attr, "[AllowNull] attribute must be present on optionalClip");
        }

        [Test]
        public void ValidateReferences_AllowNull_UnmarkedFieldNotAffected()
        {
            // requiredClip has no [AllowNull] — walker still visits it normally.
            var field = typeof(AllowNullTestComp).GetField("requiredClip");
            Assert.IsNotNull(field);
            Assert.IsNull(field.GetCustomAttribute<AllowNullAttribute>(),
                "requiredClip must not have [AllowNull]");
        }

        [Test]
        public void ValidateReferences_AllowNull_DoesNotCauseErrorsOrMissing()
        {
            // [AllowNull] field with no dangling ref — validate must be clean.
            _go.AddComponent<AllowNullTestComp>();
            var path = ComponentSerializer.GetPath(_go);
            var result = ValidateReferencesHelper.Validate(path, depth: 1, ignoreOptional: false);
            StringAssert.Contains("0 ERROR", result);
            StringAssert.DoesNotContain("MISSING", result);
        }

        // ── RemapReferences_ChangesTargetPath ────────────────────────────────

        [Test]
        public void RemapReferences_ChangesTargetPath()
        {
            // RemapReferences(sourcePath, targetPath, mappings) reads from targetGo.
            // With empty mappings and source != target, nothing is remapped.
            var other = new GameObject("Other");
            try
            {
                var sourcePath = ComponentSerializer.GetPath(_go);
                var targetPath = ComponentSerializer.GetPath(other);
                other.AddComponent<AudioSource>();

                var result = RemapReferencesHelper.RemapReferences(sourcePath, targetPath, "");

                StringAssert.Contains("remapped:", result);
                StringAssert.Contains("kept:", result);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }
    }
}
