using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestAuthoringGuardTests : UnityMcpTestBase
    {
        private static readonly IReadOnlyDictionary<short, OpCode> IlOpCodes =
            typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(OpCode))
                .Select(field => (OpCode)field.GetValue(null))
                .GroupBy(opCode => opCode.Value)
                .ToDictionary(group => group.Key, group => group.First());

        [Test]
        public void EveryUnityMcpFixture_InheritsCommonIsolationBase()
        {
            var offenders = DiscoverTestFixtureTypes()
                .Where(type => !typeof(UnityMcpTestBase).IsAssignableFrom(type))
                .Select(type => type.Assembly.GetName().Name + ":" + type.FullName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "Every Unity MCP fixture must inherit UnityMcpTestBase or a supported specialization:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void AsyncAndCoroutineTests_FollowTheCanonicalContract()
        {
            var offenders = new List<string>();
            foreach (var type in DiscoverTestFixtureTypes())
            {
                foreach (var method in DeclaredMethods(type).Where(IsTestMethod))
                {
                    var asyncStateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>();
                    if (asyncStateMachine != null && method.ReturnType != typeof(Task))
                    {
                        offenders.Add(
                            $"{type.FullName}.{method.Name}: async tests must return System.Threading.Tasks.Task");
                    }

                    var isUnityTest = method.GetCustomAttributes(false).Any(attribute =>
                        attribute.GetType().FullName ==
                        "UnityEngine.TestTools.UnityTestAttribute");
                    if (isUnityTest ||
                        typeof(IEnumerator).IsAssignableFrom(method.ReturnType))
                    {
                        offenders.Add(
                            $"{type.FullName}.{method.Name}: [UnityTest] and IEnumerator test " +
                            "methods are forbidden; use [Test] async Task and await Task/Awaitable APIs");
                    }
                }
            }

            Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
        }

        [Test]
        public void FixtureLifecycle_DoesNotOwnGlobalSceneOrRefreshCleanup()
        {
            var offenders = DiscoverTestAssemblyTypes()
                .SelectMany(type => DeclaredMethods(type)
                    .Where(IsNUnitLifecycleMethod)
                    .SelectMany(method => InspectableFixtureMethodBodies(type, method)
                        .SelectMany(body => EnumerateCalledMethods(body)
                            .Where(IsForbiddenLifecycleCall)
                            .Select(called =>
                                $"{type.FullName}.{method.Name} calls " +
                                $"{called.DeclaringType?.FullName}.{called.Name}"))))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "Global scene rollback, Undo cleanup, and AssetDatabase refresh belong to " +
                "UnityMcpTestBase, never fixture-local setup or teardown:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void TestBodies_DoNotBlockThreadsOrTriggerBroadRefresh()
        {
            var offenders = DiscoverTestFixtureTypes()
                .SelectMany(type => DeclaredMethods(type)
                    .Where(IsTestMethod)
                    .SelectMany(method => InspectableFixtureMethodBodies(type, method)
                        .SelectMany(body => EnumerateCalledMethods(body)
                            .Where(IsForbiddenTestBodyCall)
                            .Select(called =>
                                $"{type.FullName}.{method.Name} calls " +
                                $"{called.DeclaringType?.FullName}.{called.Name}"))))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "Test bodies must await asynchronous work and use targeted ImportAsset; " +
                "Thread.Sleep, Task.Wait, Task.Result, and AssetDatabase.Refresh are forbidden:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void TestSources_DoNotWriteEditorPrefsOutsideCommonOwnershipHelpers()
        {
            var offenders = DiscoverTestAssemblyTypes()
                .Where(type => type.Assembly.GetName().Name !=
                    "UnityMCP.Editor.TestSupport")
                .SelectMany(type => DeclaredMethods(type)
                    .SelectMany(InspectableMethodBodies)
                    .SelectMany(body => EnumerateCalledMethods(body)
                        .Where(IsEditorPrefsMutation)
                        .Select(called =>
                            $"{type.FullName}.{body.Name} calls " +
                            $"{called.DeclaringType?.FullName}.{called.Name}")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "Tests must mutate EditorPrefs through the typed UnityMcpTestBase " +
                "ownership helpers so the exact prior value is restored:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void EditorWindowTests_DoNotAcquireSharedEditorWindows()
        {
            var offenders = DiscoverTestFixtureTypes()
                .SelectMany(type => DeclaredMethods(type)
                    .SelectMany(method => InspectableFixtureMethodBodies(type, method))
                    .SelectMany(body => EnumerateCalledMethods(body)
                        .Where(IsForbiddenEditorWindowAcquisition)
                        .Select(called =>
                            $"{type.FullName}.{body.Name} calls " +
                            $"{called.DeclaringType?.FullName}.{called.Name}")))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "EditorWindow tests must create an owned instance through " +
                "UnityMcpTestBase.CreateOwnedEditorWindow. Generic GetWindow and " +
                "MCPChatWindow.ShowWindow can capture the user's existing window:\n" +
                string.Join("\n", offenders));
        }

        [Test]
        public void FrameworkLifecycle_IsNonVirtualAndNeverCalledManually()
        {
            var virtualLifecycle = DiscoverFrameworkBaseFixtureTypes()
                .SelectMany(type => DeclaredMethods(type)
                    .Where(IsNUnitLifecycleMethod)
                    .Where(method => method.IsVirtual)
                    .Select(method =>
                        $"{type.FullName}.{method.Name}: framework lifecycle must be non-virtual"))
                .OrderBy(value => value, StringComparer.Ordinal);

            var manualBaseCalls = DiscoverTestAssemblyTypes()
                .SelectMany(type => DeclaredMethods(type)
                    .Where(IsNUnitLifecycleMethod)
                    .SelectMany(method => InspectableFixtureMethodBodies(type, method)
                        .SelectMany(body => EnumerateCalledMethods(body)
                            .OfType<MethodInfo>()
                            .Where(called => IsManualBaseLifecycleCall(type, called))
                            .Select(called =>
                                $"{type.FullName}.{method.Name} manually calls " +
                                $"{called.DeclaringType?.FullName}.{called.Name}"))))
                .OrderBy(value => value, StringComparer.Ordinal);

            var offenders = virtualLifecycle.Concat(manualBaseCalls).ToArray();
            Assert.That(offenders, Is.Empty,
                "NUnit owns base/derived lifecycle ordering. Base fixture lifecycle must be " +
                "non-virtual, and derived lifecycle must never call it manually:\n" +
                string.Join("\n", offenders));
        }

        private static IEnumerable<Type> DiscoverTestFixtureTypes()
        {
            return DiscoverTestAssemblyTypes()
                .Where(type => !type.IsAbstract)
                .Where(type => DeclaredMethods(type).Any(IsTestMethod));
        }

        private static IEnumerable<Type> DiscoverFrameworkBaseFixtureTypes()
        {
            return DiscoverTestFixtureTypes()
                .SelectMany(type => BaseTypes(type))
                .Where(type => typeof(UnityMcpTestBase).IsAssignableFrom(type))
                .Distinct();
        }

        private static IEnumerable<Type> BaseTypes(Type type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
                yield return current;
        }

        private static IEnumerable<Type> DiscoverTestAssemblyTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly =>
                {
                    var name = assembly.GetName().Name ?? "";
                    return name.StartsWith("UnityMCP.", StringComparison.Ordinal)
                        && name.IndexOf("Test", StringComparison.Ordinal) >= 0;
                })
                .SelectMany(GetLoadableTypes)
                .Where(type => type != null && type.IsClass);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                return error.Types.Where(type => type != null);
            }
        }

        private static IEnumerable<MethodInfo> DeclaredMethods(Type type)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.DeclaredOnly);
        }

        private static bool IsNUnitLifecycleMethod(MethodInfo method)
        {
            return method.GetCustomAttributes(false).Any(attribute =>
            {
                var fullName = attribute.GetType().FullName;
                return attribute is SetUpAttribute
                    || attribute is TearDownAttribute
                    || attribute is OneTimeSetUpAttribute
                    || attribute is OneTimeTearDownAttribute
                    || fullName == "UnityEngine.TestTools.UnitySetUpAttribute"
                    || fullName == "UnityEngine.TestTools.UnityTearDownAttribute";
            });
        }

        private static bool IsManualBaseLifecycleCall(Type fixtureType, MethodInfo called)
        {
            var declaringType = called.DeclaringType;
            return declaringType != null
                && declaringType != fixtureType
                && declaringType.IsAssignableFrom(fixtureType)
                && IsNUnitLifecycleMethod(called);
        }

        private static IEnumerable<MethodInfo> InspectableMethodBodies(MethodInfo method)
        {
            yield return method;

            var asyncStateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>();
            if (asyncStateMachine != null)
            {
                var moveNext = asyncStateMachine.StateMachineType.GetMethod(
                    "MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (moveNext != null)
                    yield return moveNext;
            }

            var iteratorStateMachine = method.GetCustomAttribute<IteratorStateMachineAttribute>();
            if (iteratorStateMachine != null)
            {
                var moveNext = iteratorStateMachine.StateMachineType.GetMethod(
                    "MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (moveNext != null)
                    yield return moveNext;
            }
        }

        private static IEnumerable<MethodInfo> InspectableFixtureMethodBodies(
            Type fixtureType,
            MethodInfo rootMethod)
        {
            var pending = new Stack<MethodInfo>();
            var visited = new HashSet<MethodInfo>();
            pending.Push(rootMethod);

            while (pending.Count > 0)
            {
                var method = pending.Pop();
                foreach (var body in InspectableMethodBodies(method))
                {
                    if (!visited.Add(body))
                        continue;

                    yield return body;
                    foreach (var called in EnumerateCalledMethods(body).OfType<MethodInfo>())
                    {
                        if (IsFixtureOwnedMethod(fixtureType, called) && !visited.Contains(called))
                            pending.Push(called);
                    }
                }
            }
        }

        private static bool IsFixtureOwnedMethod(Type fixtureType, MethodInfo method)
        {
            var declaringType = method.DeclaringType;
            if (declaringType == null || declaringType.Assembly != fixtureType.Assembly)
                return false;

            for (var current = declaringType; current != null; current = current.DeclaringType)
            {
                if (current == fixtureType || current.IsAssignableFrom(fixtureType))
                    return true;
            }
            return false;
        }

        private static IEnumerable<MethodBase> EnumerateCalledMethods(MethodInfo method)
        {
            var body = method.GetMethodBody();
            var il = body?.GetILAsByteArray();
            if (il == null)
                yield break;

            var typeArguments = method.DeclaringType?.GetGenericArguments() ?? Type.EmptyTypes;
            var methodArguments = method.IsGenericMethod
                ? method.GetGenericArguments()
                : Type.EmptyTypes;

            for (var offset = 0; offset < il.Length;)
            {
                var firstByte = il[offset++];
                var value = firstByte == 0xFE
                    ? unchecked((short)(0xFE00 | il[offset++]))
                    : (short)firstByte;
                if (!IlOpCodes.TryGetValue(value, out var opCode))
                    throw new InvalidOperationException(
                        $"Unknown IL opcode 0x{(ushort)value:X4} in {method.DeclaringType?.FullName}.{method.Name}.");

                if (opCode.OperandType == OperandType.InlineMethod)
                {
                    var metadataToken = BitConverter.ToInt32(il, offset);
                    MethodBase called = null;
                    try
                    {
                        called = method.Module.ResolveMethod(
                            metadataToken, typeArguments, methodArguments);
                    }
                    catch (ArgumentException)
                    {
                        // A malformed token is still handled by advancing through valid IL below.
                    }

                    if (called != null)
                        yield return called;
                }

                offset += OperandSize(opCode.OperandType, il, offset);
            }
        }

        private static int OperandSize(OperandType operandType, byte[] il, int offset)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    return 4 + (BitConverter.ToInt32(il, offset) * 4);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported IL operand type '{operandType}'.");
            }
        }

        private static bool IsForbiddenLifecycleCall(MethodBase method)
        {
            var declaringType = method.DeclaringType?.FullName;
            return (declaringType == "UnityEditor.SceneManagement.EditorSceneManager"
                    && method.Name == "NewScene")
                || (declaringType == "UnityEditor.AssetDatabase"
                    && method.Name == "Refresh")
                || (declaringType == "UnityEditor.Undo"
                    && method.Name == "ClearAll");
        }

        private static bool IsForbiddenTestBodyCall(MethodBase method)
        {
            var declaringType = method.DeclaringType;
            if (declaringType?.FullName == "System.Threading.Thread" &&
                method.Name == "Sleep")
                return true;
            if (declaringType?.FullName == "UnityEditor.AssetDatabase" &&
                method.Name == "Refresh")
                return true;
            return IsTaskType(declaringType) &&
                   (method.Name == "Wait" || method.Name == "get_Result");
        }

        private static bool IsEditorPrefsMutation(MethodBase method)
        {
            if (method.DeclaringType?.FullName != "UnityEditor.EditorPrefs")
                return false;
            return method.Name == "SetString"
                || method.Name == "SetBool"
                || method.Name == "SetInt"
                || method.Name == "SetFloat"
                || method.Name == "DeleteKey"
                || method.Name == "DeleteAll";
        }

        private static bool IsForbiddenEditorWindowAcquisition(MethodBase method)
        {
            if (method.DeclaringType?.FullName ==
                    "UnityMCP.Editor.Chat.MCPChatWindow" &&
                method.Name == "ShowWindow")
                return true;

            if (method.DeclaringType?.FullName != "UnityEditor.EditorWindow" ||
                (method.Name != "GetWindow" && method.Name != "GetWindowWithRect"))
                return false;

            return method is MethodInfo info && info.IsGenericMethod;
        }

        private static bool IsTaskType(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current == typeof(Task) ||
                    (current.IsGenericType &&
                     current.GetGenericTypeDefinition() == typeof(Task<>)))
                    return true;
            }
            return false;
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            return method.GetCustomAttributes(false).Any(attribute =>
            {
                var type = attribute.GetType();
                return attribute is TestAttribute
                    || attribute is TestCaseAttribute
                    || attribute is TestCaseSourceAttribute
                    || attribute is TheoryAttribute
                    || type.FullName == "UnityEngine.TestTools.UnityTestAttribute";
            });
        }
    }
}
