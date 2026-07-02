using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class TempDirScopeTests
    {
        [Test]
        public void Constructor_CreatesDirectory()
        {
            using var scope = new TempDirScope("tds_ctor");
            Assert.IsTrue(Directory.Exists(scope.Path));
        }

        [Test]
        public void Dispose_RemovesDirectory()
        {
            var scope = new TempDirScope("tds_dispose");
            var path = scope.Path;
            scope.Dispose();
            Assert.IsFalse(Directory.Exists(path));
        }

        [Test]
        public void Dispose_IsIdempotent_WhenDirectoryAlreadyDeleted()
        {
            var scope = new TempDirScope("tds_idempotent");
            Directory.Delete(scope.Path, recursive: true);
            Assert.DoesNotThrow(() => scope.Dispose());
        }

        [Test]
        public void UsingBlock_CleansUpAutomatically()
        {
            string capturedPath;
            using (var scope = new TempDirScope("tds_using"))
            {
                capturedPath = scope.Path;
                Assert.IsTrue(Directory.Exists(capturedPath));
            }
            Assert.IsFalse(Directory.Exists(capturedPath));
        }
    }
}
