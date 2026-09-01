// ROI Fix 1 (P0-70 pre-effect boundary check): pure string/Path logic, no
// Unity API — projectRoot is a fake string, so every case below needs no
// disk access, TrackOwnedAsset, or real file. See
// Plans/roi-fix-1-2-blueprint.md "Fix 1" section.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SourcePatchPathGuardTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string FakeRoot = "/fake/project/root";

        [Test]
        public void Validate_NormalAssetsPath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SourcePatchPathGuard.Validate("Assets/Foo.cs", FakeRoot));
        }

        [Test]
        public void Validate_NestedAssetsPath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SourcePatchPathGuard.Validate("Assets/Sub/Dir/Foo.cs", FakeRoot));
        }

        [Test]
        public void Validate_FilenameContainsDotDotButNotSegment_DoesNotThrow()
        {
            // Regression guard: a substring ".." inside a filename (not its
            // own path segment) must never false-positive as traversal.
            Assert.DoesNotThrow(() => SourcePatchPathGuard.Validate("Assets/My..File.cs", FakeRoot));
        }

        [Test]
        public void Validate_PackagesPrefix_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("Packages/com.foo/Bar.cs", FakeRoot));
        }

        [Test]
        public void Validate_ParentTraversal_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("../outside.cs", FakeRoot));
        }

        [Test]
        public void Validate_LexicalTraversalWithinAssets_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("Assets/../x.cs", FakeRoot));
        }

        [Test]
        public void Validate_NestedLexicalTraversal_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("Assets/Sub/../../outside.cs", FakeRoot));
        }

        [Test]
        public void Validate_BackslashTraversal_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("Assets\\..\\..\\evil.cs", FakeRoot));
        }

        [Test]
        public void Validate_AbsolutePathUnix_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("/Users/x/evil.cs", FakeRoot));
        }

        [Test]
        public void Validate_AbsolutePathWindows_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("C:/Windows/evil.cs", FakeRoot));
        }

        [Test]
        public void Validate_NonCsExtension_Throws()
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("Assets/Foo.txt", FakeRoot));
        }

        [Test]
        public void Validate_AssetsPrefixWithoutSlash_Throws()
        {
            // Must require the literal "Assets/" prefix, not a bare "Assets" match.
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate("AssetsEvil/x.cs", FakeRoot));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Validate_NullOrBlankPath_Throws(string path)
        {
            Assert.Throws<ArgumentException>(() => SourcePatchPathGuard.Validate(path, FakeRoot));
        }
    }
}
