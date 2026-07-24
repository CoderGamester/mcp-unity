using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor;

namespace McpUnity.Extensions.Tests
{
    public class CommandDiscoveryTests
    {
        private static readonly string[] ExpectedNames =
        {
            "assign_material",
            "duplicate_gameobject",
            "editor_step",
            "inspect_gameobject",
            "unload_scene"
        };

        [SetUp]
        public void SetUp()
        {
            CommandRegistry.SetDiscovery(new TypeCacheCommandDiscovery());
        }

        [Test]
        public void ExtensionAssembly_DeclaresExactlyFivePublicStaticCommandsWithExactNames()
        {
            var methods = FindExtensionCommandMethods();

            Assert.That(methods, Has.Count.EqualTo(ExpectedNames.Length));
            Assert.That(
                methods.Select(method => method.GetCustomAttribute<CliCommandAttribute>().Name),
                Is.EquivalentTo(ExpectedNames));
            Assert.That(methods, Has.All.Matches<MethodInfo>(method => method.IsPublic && method.IsStatic));
        }

        [Test]
        public void PipelineDiscovery_ContainsEachExtensionCommandExactlyOnce()
        {
            var discoveredNames = CommandRegistry.DiscoverCommands()
                .Select(command => command.Name)
                .ToList();

            foreach (var expectedName in ExpectedNames)
            {
                Assert.That(
                    discoveredNames.Count(name => string.Equals(name, expectedName, StringComparison.Ordinal)),
                    Is.EqualTo(1),
                    $"Pipeline discovery should contain exactly one '{expectedName}' command.");
            }
        }

        private static List<MethodInfo> FindExtensionCommandMethods()
        {
            var methods = new List<MethodInfo>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type.Namespace == null ||
                        !type.Namespace.StartsWith("McpUnity.Extensions", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    methods.AddRange(type
                        .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .Where(method => method.GetCustomAttribute<CliCommandAttribute>() != null));
                }
            }

            return methods;
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }
    }
}
