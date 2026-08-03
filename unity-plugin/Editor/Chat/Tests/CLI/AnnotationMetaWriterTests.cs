using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationMetaWriterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Read_NullPath_ReturnsEmpty()
            => Assert.AreEqual("", AnnotationMetaWriter.Read(null));

        [Test] public void Read_EmptyPath_ReturnsEmpty()
            => Assert.AreEqual("", AnnotationMetaWriter.Read(""));

        [Test] public void Read_NonexistentFile_ReturnsEmpty()
            => Assert.AreEqual("", AnnotationMetaWriter.Read("/tmp/nonexistent_test.png"));

        [Test] public void Write_NullPath_ReturnsNull()
            => Assert.IsNull(AnnotationMetaWriter.Write(null));

        [Test] public void Write_EmptyPath_ReturnsNull()
            => Assert.IsNull(AnnotationMetaWriter.Write(""));

        [Test]
        public void Write_WithAnnotationsText_AppendsToFile()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "mcp_meta_test.png");
            File.WriteAllBytes(tmp, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // fake PNG
            try
            {
                var path = AnnotationMetaWriter.Write(tmp, "annotations:\n  arrow → /Cube\n");
                Assert.IsNotNull(path);
                var content = File.ReadAllText(path);
                StringAssert.Contains("annotations:", content);
                StringAssert.Contains("/Cube", content);
            }
            finally
            {
                File.Delete(tmp);
                File.Delete(tmp + ".meta.txt");
            }
        }

        [Test]
        public void Write_NullAnnotationsText_NoAnnotationsSection()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "mcp_meta_test2.png");
            File.WriteAllBytes(tmp, new byte[] { 0x89, 0x50, 0x4E, 0x47 });
            try
            {
                var path = AnnotationMetaWriter.Write(tmp, null);
                Assert.IsNotNull(path);
                var content = File.ReadAllText(path);
                StringAssert.DoesNotContain("annotations:", content);
            }
            finally
            {
                File.Delete(tmp);
                File.Delete(tmp + ".meta.txt");
            }
        }
    }
}
