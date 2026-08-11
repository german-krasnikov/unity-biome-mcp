// TDD — Phase 1.2b: NavBindingHelper + FileLineNavigator tests.
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class NavBindingHelperTests : UnityMcpTestBase
    {
        // Minimal IChipKindProvider that forwards Navigate to a lambda.
        private class LambdaChipProvider : IChipKindProvider
        {
            private readonly string _key;
            private readonly Action<string> _navigate;

            public LambdaChipProvider(string key, Action<string> navigate)
            {
                _key = key; _navigate = navigate;
            }

            public string   Key              => _key;
            public int      Priority         => 500;
            public string   IconName         => "";
            public string   HexColor         => "#000000";
            public string   DefaultDepth     => "summary";
            public string[] BarePathExtensions => Array.Empty<string>();
            public bool     CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath)    => default;
            public string   FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";
            public void     Navigate(string reference) => _navigate?.Invoke(reference);
            public void     Ping(string reference) { }
            public void     AppendContextMenuItems(DropdownMenu menu, string reference) { }
        }

        [Test]
        public void Navigate_CallsProviderNavigate()
        {
            string called = null;
            ChipKindRegistry.Register(new LambdaChipProvider("testkey", r => called = r));
            NavBindingHelper.Navigate(new NavTarget("testkey", "Assets/Foo.cs"));
            Assert.AreEqual("Assets/Foo.cs", called);
        }

        [Test]
        public void Navigate_ScriptWithLine_UsesFileLineNavigator()
        {
            string capturedPath = null;
            int    capturedLine = 0;
            FileLineNavigator.OpenAtLineOverride = (p, l) => { capturedPath = p; capturedLine = l; };
            try
            {
                NavBindingHelper.Navigate(new NavTarget(ChipKindKeys.Script, "Assets/Foo.cs", 42));
                Assert.AreEqual("Assets/Foo.cs", capturedPath);
                Assert.AreEqual(42, capturedLine);
            }
            finally
            {
                FileLineNavigator.OpenAtLineOverride = null;
            }
        }

        [Test]
        public void Navigate_EmptyTarget_DoesNotThrow()
            => Assert.DoesNotThrow(() => NavBindingHelper.Navigate(default));

        [Test]
        public void Navigate_UnknownKind_DoesNotThrow()
            => Assert.DoesNotThrow(() =>
                NavBindingHelper.Navigate(new NavTarget("nope", "Assets/X")));

        [Test]
        public void Attach_NullElement_DoesNotThrow()
            => Assert.DoesNotThrow(() =>
                NavBindingHelper.Attach(null, new NavTarget("hierarchy", "/Player")));

        [Test]
        public void Attach_EmptyTarget_DoesNotThrow()
            => Assert.DoesNotThrow(() =>
                NavBindingHelper.Attach(new VisualElement(), default));
    }
}
