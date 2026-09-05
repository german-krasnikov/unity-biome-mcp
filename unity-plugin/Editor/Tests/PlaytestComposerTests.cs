// TDD: PlaytestStepElement — NUnit EditMode tests
// Note: callbacks require a Panel to dispatch; tests here rely only on
// SetValueWithoutNotify (called by Bind) and direct state, not value setter events.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class PlaytestComposerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Red 1: Bind sets the restricted type dropdown to step.type
        [Test]
        public void PlaytestStepElement_Bind_SetsTypeField()
        {
            var elem = new PlaytestStepElement();
            elem.Bind(new VisualStep { type = StepType.Assert }, () => { });
            Assert.AreEqual(StepType.Assert.ToString(), elem.Q<DropdownField>("step-type-field").value);
        }

        [Test]
        public void PlaytestStepElement_TypeDropdown_ContainsOnlyExporterSupportedTypes()
        {
            var elem = new PlaytestStepElement();
            var dropdown = elem.Q<DropdownField>("step-type-field");
            var expected = new List<string>();
            foreach (var type in PlaytestDslExporter.SelectableTypes)
                expected.Add(type.ToString());

            CollectionAssert.AreEquivalent(expected, dropdown.choices);
            CollectionAssert.DoesNotContain(dropdown.choices, StepType.Snapshot.ToString());
            CollectionAssert.DoesNotContain(dropdown.choices, StepType.Fill.ToString());
        }

        [Test]
        public void PlaytestStepElement_BindUnsupportedStep_DisplaysButDoesNotOfferType()
        {
            var elem = new PlaytestStepElement();
            elem.Bind(new VisualStep(new PlaytestStep
            {
                Type = StepType.Snapshot,
                RawLine = "SNAPSHOT hp,pos",
            }), () => { });
            var dropdown = elem.Q<DropdownField>("step-type-field");

            Assert.AreEqual(StepType.Snapshot.ToString(), dropdown.value);
            CollectionAssert.DoesNotContain(dropdown.choices, StepType.Snapshot.ToString());
        }

        // Red 2: Bind sets description TextField
        [Test]
        public void PlaytestStepElement_Bind_SetsDescription()
        {
            var elem = new PlaytestStepElement();
            elem.Bind(new VisualStep { type = StepType.Wait, description = "hello" }, () => { });
            // Q<TextField>() returns the first TextField in tree order = description field
            Assert.AreEqual("hello", elem.Q<TextField>().value);
        }

        // Red 3: After Bind, exactly one step-panel is visible
        [Test]
        public void PlaytestStepElement_Bind_ShowsExactlyOnePanel()
        {
            var elem = new PlaytestStepElement();
            elem.Bind(new VisualStep { type = StepType.Wait }, () => { });
            int shown = 0;
            foreach (var p in elem.Query<VisualElement>(null, "step-panel").ToList())
                if (p.style.display.value == DisplayStyle.Flex) shown++;
            Assert.AreEqual(1, shown);
        }

        // Red 4: Bind with null does not throw
        [Test]
        public void PlaytestStepElement_Bind_NullStep_NoThrow()
        {
            var elem = new PlaytestStepElement();
            Assert.DoesNotThrow(() => elem.Bind(null, () => { }));
        }

        // Red 5: Rebind with different type switches the visible panel
        [Test]
        public void PlaytestStepElement_Rebind_DifferentType_SwitchesPanel()
        {
            var elem = new PlaytestStepElement();
            elem.Bind(new VisualStep { type = StepType.Wait }, () => { });
            var firstPanel = elem.Query<VisualElement>(null, "step-panel").ToList().Find(p => p.style.display.value == DisplayStyle.Flex);

            elem.Bind(new VisualStep { type = StepType.Assert }, () => { });
            var secondPanel = elem.Query<VisualElement>(null, "step-panel").ToList().Find(p => p.style.display.value == DisplayStyle.Flex);

            Assert.IsNotNull(firstPanel, "Wait should have a panel");
            Assert.IsNotNull(secondPanel, "Assert should have a panel");
            Assert.AreNotSame(firstPanel, secondPanel, "Different types must show different panels");
        }

        // Red 6: DSL round-trip (pure logic, no UI)
        [Test]
        public void BuildDsl_WaitStep_ProducesCorrectDsl()
        {
            var steps = new System.Collections.Generic.List<VisualStep>
            {
                new VisualStep { type = StepType.Wait, delay = 2f }
            };
            var dsl = PlaytestDslExporter.Export(steps, false);
            StringAssert.Contains("WAIT 2", dsl);
        }

        // #32: Clone produces a separate object with same field values
        [Test]
        public void VisualStep_Clone_IsDeepCopy()
        {
            var s = new VisualStep { type = StepType.Assert, query = "x|Y|z", value = "42" };
            var c = s.Clone();
            Assert.AreNotSame(s, c);
            Assert.AreEqual(s.type, c.type);
            Assert.AreEqual(s.query, c.query);
            c.query = "modified";
            Assert.AreEqual("x|Y|z", s.query);
        }

    }
}
