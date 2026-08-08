// TDD — BlendTree authoring in AnimatorController.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    [RequiresReadWrite("creates AnimatorController assets on disk")]
    public class BlendTreeTests : UnityMcpTestBase
    {
        private AnimatorController _ctrl;
        private static readonly string CtrlFolder = TestPaths.ForFixture("BlendTreeTests");
        private static readonly string CtrlPath = CtrlFolder + "/Tests_BlendTree.controller";

        [SetUp]
        public void SetUp()
        {
            CommandRegistry.InitDefaults();
            TrackOwnedAsset(CtrlFolder);
            TestPaths.EnsureFolder(CtrlFolder);
            _ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        }

        // 1. AddBlendTree 1D creates state with BlendTree motion + children
        [Test]
        public void AddBlendTree_1D_CreatesWithChildren()
        {
            var result = AnimatorControllerHelper.AddBlendTree(
                _ctrl, "Locomotion", "1d", "Speed", null, "CharIdle:0; CharWalk:0.5; CharRun:1");

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var state = AnimatorControllerHelper.FindState(sm, "Locomotion");
            Assert.IsNotNull(state, "State should exist");
            Assert.IsInstanceOf<BlendTree>(state.motion, "Motion should be BlendTree");

            var bt = (BlendTree)state.motion;
            Assert.AreEqual(BlendTreeType.Simple1D, bt.blendType);
            Assert.AreEqual("Speed", bt.blendParameter);
            Assert.AreEqual(3, bt.children.Length);
            Assert.That(result, Does.Contain("blend_tree:Locomotion"));
            Assert.That(result, Does.Contain("children:3"));
        }

        // 2. AddBlendTree 2D creates with positions
        [Test]
        public void AddBlendTree_2D_CreatesWithPositions()
        {
            var result = AnimatorControllerHelper.AddBlendTree(
                _ctrl, "Move2D", "2d_simple", "VelX", "VelY", "CharIdle:0,0; CharWalk:0,1; CharRun:1,0");

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var state = AnimatorControllerHelper.FindState(sm, "Move2D");
            Assert.IsNotNull(state);

            var bt = (BlendTree)state.motion;
            Assert.AreEqual(BlendTreeType.SimpleDirectional2D, bt.blendType);
            Assert.AreEqual("VelX", bt.blendParameter);
            Assert.AreEqual("VelY", bt.blendParameterY);
            Assert.AreEqual(3, bt.children.Length);
        }

        // 3. AddBlendTree auto-adds missing blend parameter
        [Test]
        public void AddBlendTree_AutoAddsParam()
        {
            AnimatorControllerHelper.AddBlendTree(
                _ctrl, "BT1", "1d", "MyBlend", null, null);

            bool found = false;
            foreach (var p in _ctrl.parameters)
                if (p.name == "MyBlend" && p.type == AnimatorControllerParameterType.Float)
                    found = true;
            Assert.IsTrue(found, "Blend parameter should be auto-created as float");
        }

        // 4. EditBlendTree add_child works
        [Test]
        public void EditBlendTree_AddChild_Works()
        {
            AnimatorControllerHelper.AddBlendTree(
                _ctrl, "BT_Edit", "1d", "Speed", null, "CharIdle:0");

            var result = AnimatorControllerHelper.EditBlendTree(
                _ctrl, "BT_Edit", "add_child", "CharWalk:0.5; CharRun:1", null, null, null);

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var bt = (BlendTree)AnimatorControllerHelper.FindState(sm, "BT_Edit").motion;
            Assert.AreEqual(3, bt.children.Length);
            Assert.That(result, Does.Contain("edited:BT_Edit"));
        }

        // 5. EditBlendTree remove_child works
        [Test]
        public void EditBlendTree_RemoveChild_Works()
        {
            AnimatorControllerHelper.AddBlendTree(
                _ctrl, "BT_Rm", "1d", "Speed", null, "CharIdle:0; CharWalk:0.5; CharRun:1");

            var result = AnimatorControllerHelper.EditBlendTree(
                _ctrl, "BT_Rm", "remove_child", "1", null, null, null);

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var bt = (BlendTree)AnimatorControllerHelper.FindState(sm, "BT_Rm").motion;
            Assert.AreEqual(2, bt.children.Length);
            Assert.That(result, Does.Contain("edited:BT_Rm"));
        }

        // 6. EditBlendTree set_thresholds works
        [Test]
        public void EditBlendTree_SetThresholds_Works()
        {
            AnimatorControllerHelper.AddBlendTree(
                _ctrl, "BT_Th", "1d", "Speed", null, "CharIdle:0; CharWalk:0.5; CharRun:1");

            var result = AnimatorControllerHelper.EditBlendTree(
                _ctrl, "BT_Th", "set_thresholds", "0:0; 1:0.3; 2:0.8", null, null, null);

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var bt = (BlendTree)AnimatorControllerHelper.FindState(sm, "BT_Th").motion;
            Assert.AreEqual(0.3f, bt.children[1].threshold, 0.001f);
            Assert.AreEqual(0.8f, bt.children[2].threshold, 0.001f);
            Assert.That(result, Does.Contain("edited:BT_Th"));
        }

        // 7. GetBlendTreeDetail returns children info
        [Test]
        public void GetBlendTreeDetail_Returns_ChildrenInfo()
        {
            AnimatorControllerHelper.AddBlendTree(
                _ctrl, "BlendTree_Detail", "1d", "Speed", null, "CharIdle:0; CharWalk:0.5");

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var bt = (BlendTree)AnimatorControllerHelper.FindState(sm, "BlendTree_Detail").motion;
            var detail = AnimatorControllerSerializer.SerializeBlendTree(bt);

            Assert.That(detail, Does.Contain("type:Simple1D"));
            Assert.That(detail, Does.Contain("param:Speed"));
            Assert.That(detail, Does.Contain("children:2"));
            Assert.That(detail, Does.Contain("[0]"));
            Assert.That(detail, Does.Contain("[1]"));
            Assert.That(detail, Does.Contain("threshold:"));
        }

        // 8. AddBlendTree invalid type throws (no orphan state left behind)
        [Test]
        public void AddBlendTree_InvalidType_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                AnimatorControllerHelper.AddBlendTree(_ctrl, "BT_Bad", "nonsense_type", "Speed", null, null));

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            Assert.IsNull(AnimatorControllerHelper.FindState(sm, "BT_Bad"));
        }

        // 10. AddBlendTree with an unresolvable clip name throws and leaves no orphan state
        [Test]
        public void AddBlendTree_UnknownClip_ThrowsAndLeavesNoOrphanState()
        {
            Assert.Throws<ArgumentException>(() =>
                AnimatorControllerHelper.AddBlendTree(_ctrl, "BT_Orphan", "1d", "Speed", null, "NoSuchClip:0.5"));

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            Assert.IsNull(AnimatorControllerHelper.FindState(sm, "BT_Orphan"),
                "state must not be added if any child clip fails to resolve");
        }

        // 11. AddBlendTree with a malformed threshold throws and leaves no orphan state
        [Test]
        public void AddBlendTree_MalformedThreshold_ThrowsAndLeavesNoOrphanState()
        {
            Assert.Throws<ArgumentException>(() =>
                AnimatorControllerHelper.AddBlendTree(_ctrl, "BT_Malformed", "1d", "Speed", null, "NoSuchClip:notanumber"));

            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            Assert.IsNull(AnimatorControllerHelper.FindState(sm, "BT_Malformed"));
        }

        // 12. EditBlendTree add_child with an unresolvable clip name does not mutate the BlendTree
        [Test]
        public void EditBlendTree_AddChild_UnknownClip_DoesNotMutateBlendTree()
        {
            AnimatorControllerHelper.AddBlendTree(_ctrl, "BT_Edit2", "1d", "Speed", null, null);
            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            var bt = (BlendTree)AnimatorControllerHelper.FindState(sm, "BT_Edit2").motion;
            var childCountBefore = bt.children.Length;

            Assert.Throws<ArgumentException>(() =>
                AnimatorControllerHelper.EditBlendTree(_ctrl, "BT_Edit2", "add_child", "NoSuchClip:1.0", null, null, null));

            Assert.AreEqual(childCountBefore, bt.children.Length);
        }

        // 9. add_blend_tree is a valid action in ExecAnimatorConsolidated
        [Test]
        public void AddBlendTree_IsRegisteredAction()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("animator"),
                "animator command should be registered");

            Assert.IsTrue(
                CommandRegistry.TryGetContract("animator", out _, out var optional, out _),
                "animator should have a contract");

            Assert.That(optional, Does.Contain("blend_type"));
            Assert.That(optional, Does.Contain("param"));
            Assert.That(optional, Does.Contain("children"));
            Assert.That(optional, Does.Contain("edit_action"));
        }
    }
}
