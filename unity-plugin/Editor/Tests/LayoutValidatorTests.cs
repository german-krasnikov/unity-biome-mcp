// TDD — M11: LayoutValidator's :F1 interpolations must use CultureInfo.InvariantCulture.
// Without it, a comma-decimal OS locale (ru-RU, de-DE) turns "(1.5,2.3,3.1)" into
// "(1,5,2,3,3,1)" — ambiguous/unparseable on the Python side.
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class LayoutValidatorTests
    {
        [Test]
        public void GetSpatialContext_UnderCommaDecimalLocale_UsesDotSeparator()
        {
            var original = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            try
            {
                var go = new GameObject("LV_Test");
                go.transform.position = new Vector3(1.5f, 2.3f, 3.1f);
                try
                {
                    var result = LayoutValidator.GetSpatialContext($"/{go.name}", 5f);
                    StringAssert.Contains("Position: (1.5,2.3,3.1)", result);
                    StringAssert.DoesNotContain(",5,", result); // would appear if comma-decimal leaked in
                }
                finally { Object.DestroyImmediate(go); }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = original; }
        }

        [Test]
        public void Validate_UnderCommaDecimalLocale_UsesDotSeparator()
        {
            var original = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
            try
            {
                var root = new GameObject("LV_Root");
                var a = new GameObject("LV_A");
                var b = new GameObject("LV_B");
                a.transform.SetParent(root.transform);
                b.transform.SetParent(root.transform);
                a.transform.position = new Vector3(0f, 0f, 0f);
                b.transform.position = new Vector3(0.5f, 0f, 0f);
                var colA = a.AddComponent<BoxCollider>();
                var colB = b.AddComponent<BoxCollider>();
                colA.isTrigger = true;
                colB.isTrigger = true;
                try
                {
                    var result = LayoutValidator.Validate($"/{root.name}", 2f);
                    StringAssert.Contains("dist=0.5m", result);
                    StringAssert.DoesNotContain(",5m", result); // would appear if comma-decimal leaked in
                }
                finally
                {
                    Object.DestroyImmediate(a);
                    Object.DestroyImmediate(b);
                    Object.DestroyImmediate(root);
                }
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = original; }
        }
    }
}
