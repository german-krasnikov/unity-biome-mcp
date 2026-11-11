using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Decisions.Tests
{
    // The registration flags playOk and noUndo (step 7.2, M3.0) of the real CommandRegistry.cs: data of a registration, separate from
    // `mutating`. The router reads them (CommandRouter.IsAllowedMutationInPlayMode, Process, BatchHelper); that part needs Unity and is
    // covered by the EditMode tests of CommandRouterTests.cs.
    public sealed class RegistryFlagsTests
    {
        [SetUp]
        public void EmptyRegistry() => CommandRegistry.Clear();

        [Test]
        public void A_command_registered_without_the_flags_has_neither()
        {
            CommandRegistry.Register("a", _ => "ok", mutating: true);
            Assert.That(CommandRegistry.IsMutating("a"), Is.True);
            Assert.That(CommandRegistry.IsPlayOk("a"), Is.False);
            Assert.That(CommandRegistry.IsNoUndo("a"), Is.False);
        }

        [Test]
        public void An_unregistered_command_has_neither()
        {
            Assert.That(CommandRegistry.IsPlayOk("nope"), Is.False);
            Assert.That(CommandRegistry.IsNoUndo("nope"), Is.False);
        }

        [Test]
        public void Register_carries_both_flags_and_they_are_independent()
        {
            CommandRegistry.Register("both", _ => "ok", mutating: true, playOk: true, noUndo: true);
            CommandRegistry.Register("play_only", _ => "ok", mutating: true, playOk: true);
            CommandRegistry.Register("undo_only", _ => "ok", mutating: true, noUndo: true);
            Assert.That((CommandRegistry.IsPlayOk("both"), CommandRegistry.IsNoUndo("both")), Is.EqualTo((true, true)));
            Assert.That((CommandRegistry.IsPlayOk("play_only"), CommandRegistry.IsNoUndo("play_only")), Is.EqualTo((true, false)));
            Assert.That((CommandRegistry.IsPlayOk("undo_only"), CommandRegistry.IsNoUndo("undo_only")), Is.EqualTo((false, true)));
        }

        [Test]
        public void RegisterAction_and_RegisterAsync_carry_both_flags()
        {
            CommandRegistry.RegisterAction("act", (action, args) => "ok", mutating: true, playOk: true, noUndo: true);
            CommandRegistry.RegisterAsync("later", (id, args, tcs) => tcs.TrySetResult("ok"), mutating: true, playOk: true);
            Assert.That((CommandRegistry.IsPlayOk("act"), CommandRegistry.IsNoUndo("act")), Is.EqualTo((true, true)));
            Assert.That((CommandRegistry.IsPlayOk("later"), CommandRegistry.IsNoUndo("later")), Is.EqualTo((true, false)));
        }

        [Test]
        public void The_flags_do_not_make_a_command_mutating()
        {
            CommandRegistry.Register("quiet", _ => "ok", playOk: true, noUndo: true);
            Assert.That(CommandRegistry.IsMutating("quiet"), Is.False);
            Assert.That(CommandRegistry.IsMutating("quiet", "{}"), Is.False);
        }
    }
}
