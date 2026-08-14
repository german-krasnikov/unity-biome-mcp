// T16: ChangeSetViewModel unit tests (5 tests). Pure NUnit — no Unity API.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChangeSetViewModelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static OperationViewModel Op(string kind) =>
            new OperationViewModel(kind, "scene_object", "/Obj", null, null, null, true);

        [Test]
        public void EmptyOps_NoException()
        {
            var vm = new ChangeSetViewModel("id", "open", System.Array.Empty<OperationViewModel>());
            Assert.That(vm.CreateCount, Is.EqualTo(0));
            Assert.That(vm.ModifyCount, Is.EqualTo(0));
            Assert.That(vm.DeleteCount, Is.EqualTo(0));
        }

        [Test]
        public void CreateCount_CountsCreateOps()
        {
            var vm = new ChangeSetViewModel("id", "open", new[] { Op("create"), Op("create"), Op("modify") });
            Assert.That(vm.CreateCount, Is.EqualTo(2));
        }

        [Test]
        public void ModifyCount_CountsModifyOps()
        {
            var vm = new ChangeSetViewModel("id", "open", new[] { Op("modify"), Op("create") });
            Assert.That(vm.ModifyCount, Is.EqualTo(1));
        }

        [Test]
        public void DeleteCount_CountsDeleteOps()
        {
            var vm = new ChangeSetViewModel("id", "open", new[] { Op("delete"), Op("delete") });
            Assert.That(vm.DeleteCount, Is.EqualTo(2));
        }

        [Test]
        public void Summary_FormatsCorrectly()
        {
            var vm = new ChangeSetViewModel("abc12345", "finalized",
                new[] { Op("create"), Op("modify"), Op("modify"), Op("delete") });
            Assert.That(vm.Summary, Is.EqualTo("abc12345 | finalized | +1 ~2 -1"));
        }
    }
}
