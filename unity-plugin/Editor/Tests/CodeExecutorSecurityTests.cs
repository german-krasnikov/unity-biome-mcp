using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CodeExecutorSecurityTests
    {
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
        [TestCase("typeof(Foo).GetProperties();")]
        [TestCase("typeof(Foo).GetFields();")]
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
    }
}
