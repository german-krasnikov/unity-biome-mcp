using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CodeExecutorSecurityTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp] public void SetUp()
        {
            ProtectEditorPrefInt("UnityMCP_SecurityLevel");
            MCPSettings.SetSecurityLevel(SecurityLevel.Standard);
        }

        // ── Blocked patterns ─────────────────────────────────────────────────

        [Test]
        public void SecurityScan_EnvironmentExit_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("Environment.Exit(0); return null;"));

        [Test]
        public void SecurityScan_UsingSystemDiagnostics_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "using System.Diagnostics;\nclass X { static void Run() { Process.Start(\"calc\"); } }"));

        [Test]
        public void SecurityScan_UsingSystemIO_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "using System.IO;\nclass X { static void Run() { File.Delete(\"x\"); } }"));

        // TestCase covers remaining 31 blocked patterns
        [TestCase("System.Diagnostics.Process.Start(\"calc\");")]
        [TestCase("System.IO.File.Delete(\"x\");")]
        [TestCase("System.IO.Directory.Delete(\"x\");")]
        [TestCase("System.IO.Stream s = null;")]
        [TestCase("FileStream fs = null;")]
        [TestCase("StreamWriter sw = null;")]
        [TestCase("StreamReader sr = null;")]
        [TestCase("System.IO.Path.Combine(\"a\",\"b\");")]
        [TestCase("System.Net.WebClient wc = null;")]
        [TestCase("WebClient wc = null;")]
        [TestCase("HttpClient hc = null;")]
        [TestCase("Assembly.Load(new byte[0]);")]
        [TestCase("AppDomain.CurrentDomain.GetAssemblies();")]
        [TestCase("[DllImport(\"lib\")] static extern void Foo();")]
        [TestCase("unsafe void Foo() {}")]
        [TestCase("System.Reflection.Assembly.LoadFrom(\"x\");")]
        [TestCase("Type.GetType(\"X\");")]
        [TestCase("typeof(Foo).GetMethod(\"Bar\");")]
        [TestCase("method.Invoke(null, null);")]
        [TestCase("System.Threading.Thread t = null;")]
        [TestCase("System.Runtime.InteropServices.Marshal.Copy(null,0,default,0);")]
        [TestCase("Environment.GetEnvironmentVariable(\"PATH\");")]
        [TestCase("System.Reflection.Emit.OpCodes.Nop.ToString();")]
        [TestCase("DynamicMethod dm = null;")]
        [TestCase("ILGenerator il = null;")]
        [TestCase("OpCodes.Nop.ToString();")]
        [TestCase("Activator.CreateInstance(typeof(object));")]
        [TestCase("System.Linq.Expressions.Expression.Constant(1);")]
        [TestCase("typeof(Foo).GetMethods();")]
        [TestCase("typeof(Foo).CreateDelegate(null,null);")]
        [TestCase("asm.GetTypes();")]
        [TestCase("typeof(Foo).GetMembers();")]
        [TestCase("typeof(Foo).GetConstructors();")]
        [TestCase("var x = obj.Assembly;")]
        [TestCase("Environment.SetEnvironmentVariable(\"X\",\"Y\");")]
        [TestCase("using System.Net;\nclass X {}")]
        [TestCase("using System.Reflection;\nclass X {}")]
        public void SecurityScan_BlockedPattern_Throws(string code)
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(code),
                $"Expected blocked pattern to throw for: {code}");

        // ── Legit snippets ───────────────────────────────────────────────────

        [Test]
        public void SecurityScan_FindGameObject_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("return GameObject.Find(\"Player\")?.name;"));

        [Test]
        public void SecurityScan_UnityEditorSelection_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "return UnityEditor.Selection.activeGameObject?.name ?? \"none\";"));

        [Test]
        public void SecurityScan_PureLinq_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "var list = new System.Collections.Generic.List<int>{1,2,3}; return list.Count;"));

        [Test]
        public void SecurityScan_EmptyString_DoesNotThrow()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(""));

        // ── IsAllowedAssembly ────────────────────────────────────────────────

        [TestCase("mscorlib")]
        [TestCase("netstandard")]
        [TestCase("System")]
        [TestCase("System.Core")]
        [TestCase("UnityEngine")]
        [TestCase("UnityEngine.CoreModule")]
        [TestCase("UnityEditor")]
        [TestCase("UnityEditor.CoreModule")]
        public void IsAllowedAssembly_AllowedName_ReturnsTrue(string asmName)
        {
            var target = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == asmName);
            Assert.IsNotNull(target, $"Assembly '{asmName}' not loaded in test domain");
            Assert.IsTrue(CodeExecutor.IsAllowedAssembly(target), $"Expected '{asmName}' to be allowed");
        }

        [Test]
        public void IsAllowedAssembly_TestAssembly_ReturnsFalse()
        {
            // UnityMCP.Editor.Tests is on the blocklist (starts with UnityMCP)
            var testAsm = System.Reflection.Assembly.GetExecutingAssembly();
            Assert.IsFalse(CodeExecutor.IsAllowedAssembly(testAsm),
                $"Expected '{testAsm.GetName().Name}' to NOT be allowed");
        }

        [Test]
        public void IsAllowedAssembly_UnityMCPEditorPlugin_ReturnsFalse()
        {
            // The plugin assembly (UnityMCP.Editor) is on the blocklist
            var pluginAsm = typeof(CodeExecutor).Assembly;
            Assert.IsFalse(CodeExecutor.IsAllowedAssembly(pluginAsm),
                $"Expected '{pluginAsm.GetName().Name}' to NOT be allowed");
        }

        [Test]
        public void IsAllowedAssembly_CustomAsmdef_ReturnsTrue()
        {
            // Blocklist is open by default — custom game assemblies pass through
            // UnityEngine.PhysicsModule proxies a "MyGame.Core"-style asmdef
            var asm = typeof(UnityEngine.Physics).Assembly;
            Assert.IsTrue(CodeExecutor.IsAllowedAssembly(asm), asm.GetName().Name);
        }

        [Test]
        public void IsAllowedAssembly_ThirdParty_ReturnsTrue()
        {
            // Third-party packages with disk location are allowed (not on blocklist)
            var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "nunit.framework");
            if (asm == null) Assert.Ignore("nunit.framework not loaded in domain");
            Assert.IsTrue(CodeExecutor.IsAllowedAssembly(asm), asm.GetName().Name);
        }

        [Test]
        public void IsAllowedAssembly_RoslynBlocked_ReturnsFalse()
            => Assert.IsFalse(CodeExecutor.IsAllowedAssembly("Microsoft.CodeAnalysis.CSharp"));

        [Test]
        public void IsAllowedAssembly_CecilBlocked_ReturnsFalse()
        {
            var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.StartsWith("Mono.Cecil"));
            if (asm == null) Assert.Ignore("Mono.Cecil not loaded in domain");
            Assert.IsFalse(CodeExecutor.IsAllowedAssembly(asm), asm.GetName().Name);
        }

        // ── New security fixes ───────────────────────────────────────────────

        [Test]
        public void SecurityScan_InvokeMember_Blocked()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "typeof(System.IO.File).InvokeMember(\"Delete\", System.Reflection.BindingFlags.Static, null, null, new object[]{\"x\"});"));

        [Test]
        public void SecurityScan_EditorApplicationIsPlaying_Blocked()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("EditorApplication.isPlaying = false;"));

        [Test]
        public void SecurityScan_FileUtil_Blocked()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "FileUtil.CopyFileOrDirectory(\"src\", \"dst\");"));

        [Test]
        public void IsAllowedAssembly_NullName_ReturnsFalse()
            => Assert.IsFalse(CodeExecutor.IsAllowedAssembly((string)null),
                "null assembly name must be blocked");

        [Test]
        public void IsAllowedAssembly_EmptyName_ReturnsFalse()
            => Assert.IsFalse(CodeExecutor.IsAllowedAssembly(""),
                "empty assembly name must be blocked");

        // ── Fix #1: TryGetValue false positive ───────────────────────────────

        [Test]
        public void SecurityScan_TryGetValue_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "var d = new System.Collections.Generic.Dictionary<string,int>(); d.TryGetValue(\"k\", out var v);"),
                "TryGetValue is a dict method, not reflection — must not be blocked");

        [Test]
        public void SecurityScan_DotGetValue_StillBlocked()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("fieldInfo.GetValue(null);"),
                ".GetValue() must be blocked");

        // ── Fix #2: GetFields/GetProperties plural unblocked ─────────────────

        [Test]
        public void SecurityScan_GetFields_Plural_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("var fields = typeof(MyComp).GetFields();"),
                "GetFields() is read-only type introspection — must not be blocked");

        [Test]
        public void SecurityScan_GetProperties_Plural_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("var props = typeof(MyComp).GetProperties();"),
                "GetProperties() is read-only type introspection — must not be blocked");

        [Test]
        public void SecurityScan_GetField_Singular_StillBlocked_InStrictMode()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("typeof(MyComp).GetField(\"secret\");", SecurityLevel.Strict),
                "GetField() singular must be blocked in Strict mode");

        [Test]
        public void SecurityScan_GetProperty_Singular_StillBlocked_InStrictMode()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("typeof(MyComp).GetProperty(\"secret\");", SecurityLevel.Strict),
                "GetProperty() singular must be blocked in Strict mode");

        // ── Fix #7: WrapIfBareCode bare return ───────────────────────────────

        [Test]
        public void WrapIfBareCode_BareReturn_ReplacedWithReturnNull()
        {
            var wrapped = CodeExecutor.WrapIfBareCode("if (x) return;");
            Assert.IsFalse(wrapped.Contains("return;"),
                "bare 'return;' must be replaced before wrapping");
            Assert.IsTrue(wrapped.Contains("return null;"),
                "replacement must be 'return null;'");
        }

        [Test]
        public void WrapIfBareCode_ReturnValue_Unchanged()
        {
            var wrapped = CodeExecutor.WrapIfBareCode("return 42;");
            Assert.IsTrue(wrapped.Contains("return 42;"),
                "'return 42;' must not be modified");
        }

        [Test]
        public void WrapIfBareCode_ReturnNull_Unchanged()
        {
            var wrapped = CodeExecutor.WrapIfBareCode("return null;");
            Assert.IsTrue(wrapped.Contains("return null;"));
            Assert.IsFalse(wrapped.Contains("return null null;"));
        }

        // ── Fix #8: Object alias ─────────────────────────────────────────────

        [Test]
        public void WrapIfBareCode_UsingsContainObjectAlias()
        {
            var wrapped = CodeExecutor.WrapIfBareCode("return null;");
            Assert.IsTrue(wrapped.Contains("using Object = UnityEngine.Object;"),
                "Usings must alias Object to UnityEngine.Object to resolve ambiguity");
        }

        // ── Fix #17: Security hints ──────────────────────────────────────────

        [Test]
        public void SecurityScan_GetValue_ExceptionContainsHint()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("field.GetValue(obj);"));
            Assert.IsTrue(ex.Message.Contains("Suggestion:"),
                "blocked pattern with hint must include 'Suggestion:' in message");
            Assert.IsTrue(ex.Message.Contains("SerializedObject"),
                "GetValue hint must mention SerializedObject");
        }

        // ── SecurityLevel enum tests (Wave 3 #3/#4) ──────────────────────────

        [Test]
        public void Standard_GetField_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "var f = typeof(Rigidbody).GetField(\"mass\");", SecurityLevel.Standard));

        [Test]
        public void Standard_GetProperty_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "var p = typeof(Rigidbody).GetProperty(\"mass\");", SecurityLevel.Standard));

        [Test]
        public void Standard_GetValue_IsBlocked()
            => Assert.Throws<System.InvalidOperationException>(() => CodeExecutor.SecurityScan(
                "f.GetValue(obj);", SecurityLevel.Standard));

        [Test]
        public void Standard_Invoke_IsBlocked()
            => Assert.Throws<System.InvalidOperationException>(() => CodeExecutor.SecurityScan(
                "m.Invoke(null, null);", SecurityLevel.Standard));

        [Test]
        public void AllowAll_GetValue_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "var v = field.GetValue(obj);", SecurityLevel.AllowAll));

        [Test]
        public void AllowAll_Invoke_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "method.Invoke(null, null);", SecurityLevel.AllowAll));

        [Test]
        public void AllowAll_FileIO_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "System.IO.File.Delete(\"x\");", SecurityLevel.AllowAll));

        [Test]
        public void AllowAll_ProcessExec_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "System.Diagnostics.Process.Start(\"calc\");", SecurityLevel.AllowAll));

        [Test]
        public void AllowAll_Network_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "var c = new System.Net.WebClient();", SecurityLevel.AllowAll));

        [Test]
        public void AllowAll_Reflection_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "field.GetValue(null);", SecurityLevel.AllowAll));

        [Test]
        public void AllowAll_ExternKeyword_IsAllowed()
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(
                "static extern void Foo();", SecurityLevel.AllowAll));

        [Test]
        public void Strict_GetFields_IsBlocked()
            => Assert.Throws<System.InvalidOperationException>(() => CodeExecutor.SecurityScan(
                "typeof(Rigidbody).GetFields();", SecurityLevel.Strict));

        [Test]
        public void Strict_GetField_IsBlocked()
            => Assert.Throws<System.InvalidOperationException>(() => CodeExecutor.SecurityScan(
                "typeof(Rigidbody).GetField(\"mass\");", SecurityLevel.Strict));

        [Test]
        public void ErrorMessage_IncludesLevelName()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(() =>
                CodeExecutor.SecurityScan("System.IO.File.Delete(\"x\");", SecurityLevel.Standard));
            StringAssert.Contains("Standard", ex.Message);
        }

        [Test]
        public void SecurityScan_UnknownBlockedPattern_FallsBackToGenericMessage()
        {
            var ex = Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("FileUtil.CopyFileOrDirectory(\"a\",\"b\");"));
            Assert.IsTrue(ex.Message.Contains("Security ["),
                "message must identify the security level");
            Assert.IsTrue(ex.Message.Contains("blocked pattern"),
                "message must identify the blocked pattern");
            Assert.IsTrue(ex.Message.Contains("Only UnityEngine/UnityEditor APIs allowed."),
                "fallback must include generic guidance");
        }

        // ── #08: word-boundary false-positive tests ───────────────────────────

        [TestCase("var externalRef = go.transform;")]
        [TestCase("bool isSafeGuarded = true;")]
        [TestCase("string externally = \"ok\";")]
        public void SecurityScan_ExternUnsafe_IdentifierContaining_DoesNotBlock(string code)
            => Assert.DoesNotThrow(() => CodeExecutor.SecurityScan(code),
                $"Identifier containing extern/unsafe must not be blocked: {code}");

        [TestCase("[DllImport(\"lib\")] static extern void Foo();")]
        [TestCase("unsafe void Foo() {}")]
        [TestCase("static extern void Foo();")] // word-boundary regex only (no DllImport)
        public void SecurityScan_ExternUnsafeKeyword_StillBlocked(string code)
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(code),
                $"extern/unsafe keyword must be blocked: {code}");

        // ── Fix #5: WrapIfBareCode using-directive hoisting ──────────────────

        [Test]
        public void WrapIfBareCode_UsingNamespace_IsHoisted_NotWrappedInMethod()
        {
            var code = "using System.Text;\nvar sb = new StringBuilder(); return sb.ToString();";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            var classIdx = wrapped.IndexOf("public static class");
            var usingIdx = wrapped.IndexOf("using System.Text;");
            Assert.Greater(classIdx, -1, "class wrapper missing");
            Assert.Greater(usingIdx, -1, "using directive missing from output");
            Assert.Less(usingIdx, classIdx, "using must appear before class wrapper");
        }

        [Test]
        public void WrapIfBareCode_UsingVar_IsNotHoisted_StaysInMethod()
        {
            var code = "using var x = new System.Text.StringBuilder(); return x.ToString();";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            var methodIdx = wrapped.IndexOf("public static object Run()");
            var usingVarIdx = wrapped.IndexOf("using var x");
            Assert.Greater(usingVarIdx, methodIdx, "using var must stay inside method body");
        }

        [Test]
        public void WrapIfBareCode_UsingBlock_IsNotHoisted()
        {
            var code = "using (var ms = new System.Text.StringBuilder()) { }";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            var methodIdx = wrapped.IndexOf("public static object Run()");
            var usingBlockIdx = wrapped.IndexOf("using (var");
            Assert.Greater(usingBlockIdx, methodIdx, "using-block must stay inside method body");
        }

        [Test]
        public void WrapIfBareCode_MultipleUsings_AllHoisted()
        {
            var code = "using System.Text;\nusing System.Collections;\nvar sb = new StringBuilder();";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            var classIdx = wrapped.IndexOf("public static class");
            Assert.Less(wrapped.IndexOf("using System.Text;"), classIdx,
                "System.Text using must be before class");
            Assert.Less(wrapped.IndexOf("using System.Collections;"), classIdx,
                "System.Collections using must be before class");
        }

        [Test]
        public void WrapIfBareCode_UsingAlias_IsNotHoisted_StaysInCode()
        {
            // `using Alias = Some.Type;` has = before ; — regex won't match [A-Z][\w.]+\s*;
            var code = "using MyType = System.Text.StringBuilder; return null;";
            var wrapped = CodeExecutor.WrapIfBareCode(code);
            StringAssert.Contains("using MyType = System.Text.StringBuilder", wrapped);
        }
    }
}
