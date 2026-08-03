// TDD tests for ImageAttachmentStore — pure IO, no Unity deps.
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ImageAttachmentStoreTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;

        // Valid PNG header: 89 50 4E 47 0D 0A 1A 0A
        private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        // Valid JPEG header: FF D8 FF E0 ...
        private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ImgStoreTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void ImportBytes_WritesPngToDisk()
        {
            var path = ImageAttachmentStore.ImportBytes(PngHeader, _tempDir, "test");
            Assert.IsTrue(File.Exists(path), "File must exist on disk");
            Assert.AreEqual(PngHeader, File.ReadAllBytes(path));
        }

        [Test]
        public void ImportBytes_HasPngExtension()
        {
            var path = ImageAttachmentStore.ImportBytes(PngHeader, _tempDir, "shot");
            Assert.IsTrue(path.EndsWith(".png"), "Must end with .png");
        }

        [Test]
        public void ImportFile_CopiesFile()
        {
            var src = Path.Combine(_tempDir, "src.png");
            File.WriteAllBytes(src, PngHeader);
            var dst = ImageAttachmentStore.ImportFile(src, _tempDir);
            Assert.IsNotNull(dst);
            Assert.IsTrue(File.Exists(dst));
            Assert.AreEqual(File.ReadAllBytes(src), File.ReadAllBytes(dst));
        }

        [Test]
        public void ImportFile_DeduplicatesBySameContent()
        {
            var src1 = Path.Combine(_tempDir, "a.png");
            File.WriteAllBytes(src1, PngHeader);

            var storeDir = Path.Combine(_tempDir, "store");
            var dst1 = ImageAttachmentStore.ImportFile(src1, storeDir);
            var dst2 = ImageAttachmentStore.ImportFile(src1, storeDir);
            Assert.AreEqual(dst1, dst2, "Same file imported twice → same dest path (dedup)");
        }

        [Test]
        public void ImportBytes_Dedup_SameContentReturnsSamePath()
        {
            var storeDir = Path.Combine(_tempDir, "store2");
            var path1 = ImageAttachmentStore.ImportBytes(PngHeader, storeDir, "x");
            var path2 = ImageAttachmentStore.ImportBytes(PngHeader, storeDir, "x");
            Assert.AreEqual(path1, path2, "Same bytes + same name → same dest path");
        }

        [Test]
        public void Purge_DeletesOldFiles()
        {
            var storeDir = Path.Combine(_tempDir, "store3");
            Directory.CreateDirectory(storeDir);
            var old = Path.Combine(storeDir, "old.png");
            File.WriteAllBytes(old, new byte[] { 1 });
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-31));

            ImageAttachmentStore.Purge(storeDir, maxAgeDays: 30);
            Assert.IsFalse(File.Exists(old), "Old file must be deleted");
        }

        [Test]
        public void Purge_KeepsRecentFiles()
        {
            var storeDir = Path.Combine(_tempDir, "store4");
            Directory.CreateDirectory(storeDir);
            var recent = Path.Combine(storeDir, "recent.png");
            File.WriteAllBytes(recent, new byte[] { 1 });

            ImageAttachmentStore.Purge(storeDir, maxAgeDays: 30);
            Assert.IsTrue(File.Exists(recent), "Recent file must be kept");
        }

        [Test]
        public void ImportFile_MissingSource_ReturnsNull()
        {
            var result = ImageAttachmentStore.ImportFile("/nonexistent/path/img.png", _tempDir);
            Assert.IsNull(result);
        }

        // ── C1: size limit ────────────────────────────────────────────────────

        [Test]
        public void ImportBytes_OversizeBytes_ReturnsNull()
        {
            var huge = new byte[ImageAttachmentStore.MaxBytes + 1];
            // Add PNG header so magic-byte check passes; only size check should reject it
            PngHeader.CopyTo(huge, 0);
            var result = ImageAttachmentStore.ImportBytes(huge, _tempDir, "big");
            Assert.IsNull(result, "File exceeding MaxBytes must be rejected");
        }

        [Test]
        public void ImportFile_OversizeFile_ReturnsNull()
        {
            var src = Path.Combine(_tempDir, "big.png");
            var huge = new byte[ImageAttachmentStore.MaxBytes + 1];
            PngHeader.CopyTo(huge, 0);
            File.WriteAllBytes(src, huge);
            var result = ImageAttachmentStore.ImportFile(src, _tempDir);
            Assert.IsNull(result, "File exceeding MaxBytes must be rejected");
        }

        [Test]
        public void ImportBytes_ExactlyMaxBytes_Accepted()
        {
            // Guard is > MaxBytes (exclusive), so exactly 4MB is accepted
            var exact = new byte[ImageAttachmentStore.MaxBytes];
            PngHeader.CopyTo(exact, 0);
            var result = ImageAttachmentStore.ImportBytes(exact, _tempDir, "exact");
            Assert.IsNotNull(result, "Exactly MaxBytes should be accepted (guard is exclusive >)");
        }

        // ── C2: magic-byte validation ─────────────────────────────────────────

        [Test]
        public void ImportBytes_InvalidMagicBytes_ReturnsNull()
        {
            var exe = new byte[] { 0x4D, 0x5A, 0x00, 0x00 }; // MZ header (Windows EXE)
            var result = ImageAttachmentStore.ImportBytes(exe, _tempDir, "bad");
            Assert.IsNull(result, "Non-image magic bytes must be rejected");
        }

        [Test]
        public void ImportBytes_PngMagicBytes_Accepted()
        {
            var result = ImageAttachmentStore.ImportBytes(PngHeader, _tempDir, "ok");
            Assert.IsNotNull(result, "Valid PNG bytes must be accepted");
        }

        [Test]
        public void ImportBytes_JpegMagicBytes_Accepted()
        {
            var result = ImageAttachmentStore.ImportBytes(JpegHeader, _tempDir, "jpg");
            Assert.IsNotNull(result, "Valid JPEG bytes must be accepted");
        }

        [Test]
        public void ImportFile_InvalidMagicBytes_ReturnsNull()
        {
            var src = Path.Combine(_tempDir, "fake.png");
            File.WriteAllBytes(src, new byte[] { 0x4D, 0x5A, 0x00, 0x00 });
            var result = ImageAttachmentStore.ImportFile(src, _tempDir);
            Assert.IsNull(result, "File with non-image magic bytes must be rejected");
        }

        // ── C4: path canonicalization ─────────────────────────────────────────

        [Test]
        public void ImportFile_RelativePath_CanonicalizedBeforeRead()
        {
            // Relative paths are not expected in production (Finder drag gives absolute),
            // but Path.GetFullPath should still handle them without throwing.
            Assert.DoesNotThrow(() => ImageAttachmentStore.ImportFile("relative/path.png", _tempDir));
        }
    }
}
