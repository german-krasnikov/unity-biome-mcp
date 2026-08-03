using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class PlaytestStepValidatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // --- GetValidationError ---

        [Test]
        public void GetValidationError_NullStep_ReturnsError()
        {
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(null));
        }

        [Test]
        public void GetValidationError_MoveStep_EmptyPathZeroPos_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Move, path = "", position = Vector3.zero };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_MoveStep_WithPath_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Move, path = "Player" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_MoveStep_WithNonZeroPosition_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Move, path = "", position = Vector3.one };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_TeleportStep_EmptyPathZeroPos_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Teleport, path = "", position = Vector3.zero };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_TeleportStep_WithPath_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Teleport, path = "Enemy" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_WaitStep_ZeroDelay_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Wait, delay = 0f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_WaitStep_NegativeDelay_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Wait, delay = -1f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_WaitStep_PositiveDelay_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Wait, delay = 1f };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [TestCase(0f)]
        [TestCase(2f)]
        public void GetValidationError_TimeScaleStep_NonNegative_ReturnsNull(float scale)
        {
            Assert.IsNull(PlaytestStepValidator.GetValidationError(new VisualStep { type = StepType.TimeScale, delay = scale }));
        }

        [Test]
        public void GetValidationError_TimeScaleStep_NegativeScale_ReturnsError()
        {
            var step = new VisualStep { type = StepType.TimeScale, delay = -1f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_WaitUntilStep_EmptyQuery_ReturnsError()
        {
            var step = new VisualStep { type = StepType.WaitUntil, query = "", timeout = 5f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_WaitUntilStep_ZeroTimeout_ReturnsError()
        {
            var step = new VisualStep { type = StepType.WaitUntil, query = "Health == 0", timeout = 0f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_WaitUntilStep_ValidQueryAndTimeout_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.WaitUntil, query = "Health == 0", timeout = 5f };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_AssertStep_EmptyQuery_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Assert, query = "" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_AssertStep_WithQuery_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Assert, query = "Health == 100" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_InvokeStep_EmptyPath_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Invoke, path = "", component = "Foo", method = "Bar" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_InvokeStep_EmptyComponent_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Invoke, path = "Player", component = "", method = "Bar" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_InvokeStep_EmptyMethod_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Invoke, path = "Player", component = "Foo", method = "" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_InvokeStep_AllFieldsSet_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Invoke, path = "Player", component = "Foo", method = "Bar" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        [TestCase(StepType.Section)]
        [TestCase(StepType.Log)]
        public void GetValidationError_UnconstrainedTypes_ReturnsNull(StepType t)
        {
            Assert.IsNull(PlaytestStepValidator.GetValidationError(new VisualStep { type = t }));
        }

        // --- Set ---
        [Test]
        public void GetValidationError_SetStep_EmptyPath_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Set, path = "", component = "Foo", method = "hp" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_SetStep_EmptyComponent_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Set, path = "/Player", component = "", method = "hp" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_SetStep_EmptyField_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Set, path = "/Player", component = "Foo", method = "" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_SetStep_Complete_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Set, path = "/Player", component = "Foo", method = "hp" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        // --- Click ---
        [Test]
        public void GetValidationError_ClickStep_EmptyPath_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Click, path = "" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_ClickStep_WithPath_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Click, path = "/UI/StartBtn" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        // --- Capture ---
        [Test]
        public void GetValidationError_CaptureStep_EmptyLabel_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Capture, message = "", query = "/P|Health|hp" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_CaptureStep_EmptyQuery_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Capture, message = "label1", query = "" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        // --- Invariant ---
        [Test]
        public void GetValidationError_InvariantStep_EmptyQuery_ReturnsError()
        {
            var step = new VisualStep { type = StepType.Invariant, query = "" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_InvariantStep_WithQuery_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.Invariant, query = "/P|Health|hp" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        // --- AssertNear ---
        [Test]
        public void GetValidationError_AssertNear_EmptyPath_ReturnsError()
        {
            var step = new VisualStep { type = StepType.AssertNear, path = "", value = "/B", delay = 1f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_AssertNear_EmptyValue_ReturnsError()
        {
            var step = new VisualStep { type = StepType.AssertNear, path = "/A", value = "", delay = 1f };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_AssertNear_Complete_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.AssertNear, path = "/A", value = "/B", delay = 1f };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        // --- AssertCaptured ---
        [Test]
        public void GetValidationError_AssertCaptured_EmptyLabel_ReturnsError()
        {
            var step = new VisualStep { type = StepType.AssertCaptured, message = "", op = "DELTA" };
            Assert.IsNotNull(PlaytestStepValidator.GetValidationError(step));
        }

        [Test]
        public void GetValidationError_AssertCaptured_WithLabel_ReturnsNull()
        {
            var step = new VisualStep { type = StepType.AssertCaptured, message = "hp_snap", op = "DELTA" };
            Assert.IsNull(PlaytestStepValidator.GetValidationError(step));
        }

        // --- IsScriptValid ---

        [Test]
        public void IsScriptValid_NullList_ReturnsTrue()
        {
            Assert.IsTrue(PlaytestStepValidator.IsScriptValid(null));
        }

        [Test]
        public void IsScriptValid_EmptyList_ReturnsTrue()
        {
            Assert.IsTrue(PlaytestStepValidator.IsScriptValid(new List<VisualStep>()));
        }

        [Test]
        public void IsScriptValid_AllValidSteps_ReturnsTrue()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Wait, delay = 1f },
                new VisualStep { type = StepType.Assert, query = "Health == 100" }
            };
            Assert.IsTrue(PlaytestStepValidator.IsScriptValid(steps));
        }

        [Test]
        public void IsScriptValid_OneInvalidStep_ReturnsFalse()
        {
            var steps = new List<VisualStep>
            {
                new VisualStep { type = StepType.Wait, delay = 1f },
                new VisualStep { type = StepType.Assert, query = "" }  // invalid: empty query
            };
            Assert.IsFalse(PlaytestStepValidator.IsScriptValid(steps));
        }
    }
}
