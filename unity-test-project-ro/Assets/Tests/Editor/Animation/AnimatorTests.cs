using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;

namespace UnityMCP.TestProject.Animation
{
    [TestFixture]
    public class AnimatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/AnimatorTests";
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(AnimationHelper.ResetAssetDirectoryForTests);
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
            AnimationHelper.SetAssetDirectoryForTests(TempFolder);
            _go = TrackOwnedObject(new GameObject("AnimTestObj"));
        }

        [Test]
        public void AddParam_CreatesControllerAndAddsParams()
        {
            var result = AnimatorControllerHelper.AddParameters("/AnimTestObj", "Speed:float:0; Jump:trigger");
            StringAssert.Contains("Speed(float)", result);
            StringAssert.Contains("Jump(trigger)", result);

            var ctrl = GetCtrl();
            Assert.AreEqual(2, ctrl.parameters.Length);
            Assert.AreEqual("Speed", ctrl.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Float, ctrl.parameters[0].type);
            Assert.AreEqual(AnimatorControllerParameterType.Trigger, ctrl.parameters[1].type);
        }

        [Test]
        public void AddParam_Bool_WithDefault()
        {
            AnimatorControllerHelper.AddParameters("/AnimTestObj", "IsGrounded:bool:true");
            var ctrl = GetCtrl();
            Assert.AreEqual(1, ctrl.parameters.Length);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, ctrl.parameters[0].type);
            Assert.IsTrue(ctrl.parameters[0].defaultBool);
        }

        [Test]
        public void AddParam_DuplicateSkipped()
        {
            AnimatorControllerHelper.AddParameters("/AnimTestObj", "Speed:float");
            var result = AnimatorControllerHelper.AddParameters("/AnimTestObj", "Speed:float; Health:int:100");
            StringAssert.Contains("Speed(exists)", result);
            StringAssert.Contains("Health(int)", result);

            var ctrl = GetCtrl();
            Assert.AreEqual(2, ctrl.parameters.Length);
        }

        [Test]
        public void AddState_CreatesStates()
        {
            var result = AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk; Run");
            StringAssert.Contains("Idle", result);
            StringAssert.Contains("Walk", result);

            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            Assert.AreEqual(3, sm.states.Length);
        }

        [Test]
        public void AddState_WithClip()
        {
            // Create a test clip asset
            var clip = new AnimationClip { name = "TestIdle" };
            AssetDatabase.CreateAsset(clip, TempFolder + "/TestIdle.anim");
            AssetDatabase.SaveAssets();

            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle:TestIdle.anim");
            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            Assert.IsNotNull(sm.states[0].state.motion);
        }

        [Test]
        public void AddTransition_CreatesTransition()
        {
            AnimatorControllerHelper.AddParameters("/AnimTestObj", "Speed:float");
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk");

            var result = AnimatorControllerHelper.AddTransition("/AnimTestObj", "Idle", "Walk", "Speed>0.1", 0.15f, null, null);
            StringAssert.Contains("Idle", result);
            StringAssert.Contains("Walk", result);

            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            var idle = AnimatorControllerHelper.FindState(sm, "Idle");
            Assert.AreEqual(1, idle.transitions.Length);
            Assert.AreEqual("Walk", idle.transitions[0].destinationState.name);
            Assert.AreEqual(1, idle.transitions[0].conditions.Length);
            Assert.AreEqual(AnimatorConditionMode.Greater, idle.transitions[0].conditions[0].mode);
        }

        [Test]
        public void AddTransition_AnyState()
        {
            AnimatorControllerHelper.AddParameters("/AnimTestObj", "Jump:trigger");
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Jump");

            AnimatorControllerHelper.AddTransition("/AnimTestObj", "*", "Jump", "Jump", 0.1f, null, null);
            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            Assert.AreEqual(1, sm.anyStateTransitions.Length);
            Assert.AreEqual("Jump", sm.anyStateTransitions[0].destinationState.name);
        }

        [Test]
        public void AddTransition_WithExitTime()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Jump; Idle");
            AnimatorControllerHelper.AddTransition("/AnimTestObj", "Jump", "Idle", null, 0.15f, 0.9f, true);

            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            var jump = AnimatorControllerHelper.FindState(sm, "Jump");
            Assert.IsTrue(jump.transitions[0].hasExitTime);
            Assert.AreEqual(0.9f, jump.transitions[0].exitTime, 0.01f);
        }

        [Test]
        public void SetDefault_ChangesDefaultState()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk");
            AnimatorControllerHelper.SetDefault("/AnimTestObj", "Walk");

            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            Assert.AreEqual("Walk", sm.defaultState.name);
        }

        [Test]
        public void RemoveParam_RemovesParameter()
        {
            AnimatorControllerHelper.AddParameters("/AnimTestObj", "Speed:float; Jump:trigger");
            AnimatorControllerHelper.Remove("/AnimTestObj", "param", "Speed", null, null);

            var ctrl = GetCtrl();
            Assert.AreEqual(1, ctrl.parameters.Length);
            Assert.AreEqual("Jump", ctrl.parameters[0].name);
        }

        [Test]
        public void RemoveState_RemovesState()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk; Run");
            AnimatorControllerHelper.Remove("/AnimTestObj", "state", "Walk", null, null);

            var ctrl = GetCtrl();
            var sm = ctrl.layers[0].stateMachine;
            Assert.AreEqual(2, sm.states.Length);
            Assert.IsNull(AnimatorControllerHelper.FindState(sm, "Walk"));
        }

        [Test]
        public void RemoveTransition_RemovesTransition()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk");
            AnimatorControllerHelper.AddTransition("/AnimTestObj", "Idle", "Walk", null, 0.25f, null, null);
            AnimatorControllerHelper.Remove("/AnimTestObj", "transition", "", "Idle", "Walk");

            var ctrl = GetCtrl();
            var idle = AnimatorControllerHelper.FindState(ctrl.layers[0].stateMachine, "Idle");
            Assert.AreEqual(0, idle.transitions.Length);
        }

        [Test]
        public void RemoveTransition_AnyState()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Jump");
            AnimatorControllerHelper.AddTransition("/AnimTestObj", "*", "Jump", null, 0.1f, null, null);
            AnimatorControllerHelper.Remove("/AnimTestObj", "transition", "", "*", "Jump");

            var ctrl = GetCtrl();
            Assert.AreEqual(0, ctrl.layers[0].stateMachine.anyStateTransitions.Length);
        }

        [Test]
        public void Serialize_Overview()
        {
            AnimatorControllerHelper.AddParameters("/AnimTestObj", "Speed:float:0");
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk");

            var result = AnimatorControllerSerializer.Serialize("/AnimTestObj", null);
            StringAssert.Contains("AnimatorController:", result);
            StringAssert.Contains("2 states", result);
            StringAssert.Contains("1 params", result);
            StringAssert.Contains("Speed : float = 0", result);
        }

        [Test]
        public void Serialize_StateDetail()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle; Walk");
            AnimatorControllerHelper.AddTransition("/AnimTestObj", "Idle", "Walk", null, 0.2f, null, null);

            var result = AnimatorControllerSerializer.Serialize("/AnimTestObj", "Idle");
            StringAssert.Contains("state: Idle", result);
            StringAssert.Contains("→ Walk", result);
        }

        [Test]
        public void Serialize_NoController_ThrowsError()
        {
            var go2 = new GameObject("NoCtrlObj");
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    AnimatorControllerSerializer.Serialize("/NoCtrlObj", null));
            }
            finally
            {
                Object.DestroyImmediate(go2);
            }
        }

        [TestCase("Speed>0.1", AnimatorConditionMode.Greater, "Speed", 0.1f)]
        [TestCase("Speed<0.5", AnimatorConditionMode.Less, "Speed", 0.5f)]
        [TestCase("Type=2", AnimatorConditionMode.Equals, "Type", 2f)]
        [TestCase("State!=0", AnimatorConditionMode.NotEqual, "State", 0f)]
        [TestCase("IsGrounded", AnimatorConditionMode.If, "IsGrounded", 0f)]
        [TestCase("!IsGrounded", AnimatorConditionMode.IfNot, "IsGrounded", 0f)]
        public void ParseCondition_Works(string input, AnimatorConditionMode expectedMode, string expectedParam, float expectedThreshold)
        {
            var ctrl = GetOrCreateCtrl();
            var c = AnimatorControllerHelper.ParseCondition(input, ctrl);
            Assert.AreEqual(expectedMode, c.mode);
            Assert.AreEqual(expectedParam, c.parameter);
            if (expectedThreshold != 0f)
                Assert.AreEqual(expectedThreshold, c.threshold, 0.001f);
        }

        // Cycle 6b: == operator support
        [Test]
        public void ParseCondition_DoubleEquals_BoolTrue()
        {
            var ctrl = GetOrCreateCtrl();
            var c = AnimatorControllerHelper.ParseCondition("IsGrounded==true", ctrl);
            Assert.AreEqual(AnimatorConditionMode.If, c.mode);
            Assert.AreEqual(0f, c.threshold, 0.001f);
            Assert.AreEqual("IsGrounded", c.parameter);
        }

        [Test]
        public void ParseCondition_DoubleEquals_BoolFalse()
        {
            var ctrl = GetOrCreateCtrl();
            var c = AnimatorControllerHelper.ParseCondition("IsGrounded==false", ctrl);
            Assert.AreEqual(AnimatorConditionMode.IfNot, c.mode);
            Assert.AreEqual(0f, c.threshold, 0.001f);
            Assert.AreEqual("IsGrounded", c.parameter);
        }

        [Test]
        public void ParseCondition_DoubleEquals_Numeric()
        {
            var ctrl = GetOrCreateCtrl();
            var c = AnimatorControllerHelper.ParseCondition("State==2", ctrl);
            Assert.AreEqual(AnimatorConditionMode.Equals, c.mode);
            Assert.AreEqual(2.0f, c.threshold, 0.001f);
            Assert.AreEqual("State", c.parameter);
        }

        [Test]
        public void ParseCondition_SingleEquals_BackCompat()
        {
            var ctrl = GetOrCreateCtrl();
            var c = AnimatorControllerHelper.ParseCondition("Type=2", ctrl);
            Assert.AreEqual(AnimatorConditionMode.Equals, c.mode);
            Assert.AreEqual(2.0f, c.threshold, 0.001f);
        }

        [Test]
        public void ParseCondition_NotEquals_StillWorks()
        {
            var ctrl = GetOrCreateCtrl();
            var c = AnimatorControllerHelper.ParseCondition("State!=0", ctrl);
            Assert.AreEqual(AnimatorConditionMode.NotEqual, c.mode);
            Assert.AreEqual(0.0f, c.threshold, 0.001f);
        }

        [Test]
        public void CommandRouter_Animator_Get()
        {
            AnimatorControllerHelper.AddStates("/AnimTestObj", "Idle");
            var json = "{\"id\":\"t1\",\"cmd\":\"animator\",\"args\":{\"action\":\"get\",\"path\":\"/AnimTestObj\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("AnimatorController", result);
        }

        [Test]
        public void CommandRouter_Animator_InvalidAction()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Unknown action"));
            var json = "{\"id\":\"t1\",\"cmd\":\"animator\",\"args\":{\"action\":\"bad\",\"path\":\"/AnimTestObj\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        private AnimatorController GetCtrl()
        {
            var animator = _go.GetComponent<Animator>();
            Assert.IsNotNull(animator, "Animator component should exist");
            var ctrl = animator.runtimeAnimatorController as AnimatorController;
            Assert.IsNotNull(ctrl, "AnimatorController should exist");
            return ctrl;
        }

        private AnimatorController GetOrCreateCtrl()
        {
            var animator = _go.GetComponent<Animator>();
            if (animator == null) animator = _go.AddComponent<Animator>();
            var ctrl = animator.runtimeAnimatorController as AnimatorController;
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(
                    TempFolder + "/AnimTestObj.controller");
                animator.runtimeAnimatorController = ctrl;
            }
            return ctrl;
        }
    }
}
