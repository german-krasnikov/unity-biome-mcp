using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class BiomeTestPolicyAttributeTests : UnityMcpTestBase
    {
        private static readonly HashSet<string> ForbiddenPolicyInterfaces =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "NUnit.Framework.Interfaces.IFixtureBuilder",
                "NUnit.Framework.Interfaces.ISimpleTestBuilder",
                "NUnit.Framework.Interfaces.ITestBuilder",
                "NUnit.Framework.Interfaces.IWrapSetUpTearDown",
                "NUnit.Framework.Interfaces.IWrapTestMethod",
                "NUnit.Framework.ITestAction",
                "UnityEngine.TestTools.IOuterUnityTestAction"
            };

        [Test]
        public void WorkerOnlyPolicy_IsNUnitExplicitAndRequiresReason()
        {
            var attribute = new BiomeWorkerOnlyAttribute("mutates package files");

            Assert.That(attribute, Is.InstanceOf<ExplicitAttribute>());
            Assert.That(attribute.Reason, Is.EqualTo("mutates package files"));
            Assert.Throws<ArgumentException>(() => new BiomeWorkerOnlyAttribute("  "));
        }

        [Test]
        public void CustomUnityMcpAttributes_DoNotReplaceDiscoveryOrLifecycle()
        {
            var offenders = DiscoverUnityMcpTypes()
                .Where(type => typeof(Attribute).IsAssignableFrom(type))
                .SelectMany(type => type.GetInterfaces()
                    .Where(contract => ForbiddenPolicyInterfaces.Contains(contract.FullName ?? ""))
                    .Select(contract => $"{type.FullName} implements {contract.FullName}"))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "Custom attributes may declare run policy but must not replace NUnit/UTF " +
                "discovery or lifecycle:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void WorkerOnlyTests_DoNotUseOneTimeLifecycleBeforeTheBaseGuard()
        {
            var offenders = DiscoverUnityMcpTypes()
                .Where(type =>
                    type.GetCustomAttributes(typeof(BiomeWorkerOnlyAttribute), true).Any() ||
                    type.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                    BindingFlags.Public | BindingFlags.NonPublic)
                        .Any(method => method
                            .GetCustomAttributes(typeof(BiomeWorkerOnlyAttribute), true).Any()))
                .SelectMany(type => type
                    .GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method =>
                        method.GetCustomAttributes(typeof(OneTimeSetUpAttribute), true).Any() ||
                        method.GetCustomAttributes(typeof(OneTimeTearDownAttribute), true).Any())
                    .Select(method => type.FullName + "." + method.Name))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(offenders, Is.Empty,
                "Worker-only tests rely on UnityMcpTestBase to verify the disposable " +
                "worker marker before derived setup. One-time lifecycle runs too early:\n" +
                string.Join("\n", offenders));
        }

        private static IEnumerable<Type> DiscoverUnityMcpTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => (assembly.GetName().Name ?? "")
                    .StartsWith("UnityMCP.", StringComparison.Ordinal))
                .SelectMany(GetLoadableTypes)
                .Where(type => type != null);
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
    }
}
