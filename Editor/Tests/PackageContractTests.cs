using System.IO;
using System.Linq;
using McpUnity.Extensions.Commands;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace McpUnity.Extensions.Tests
{
    public class PackageContractTests
    {
        [Test]
        public void PackageManifest_DeclaresUnityCliExtensionIdentityAndExactDependencies()
        {
            var package = PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
            Assert.That(package, Is.Not.Null);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(package.assetPath, "package.json")));

            Assert.That((string)manifest["version"], Is.EqualTo("2.0.0"));
            Assert.That((string)manifest["unity"], Is.EqualTo("6000.0"));
            Assert.That((string)manifest["displayName"],
                Does.Contain("MCP Unity Extensions for Unity CLI"));
            Assert.That((string)manifest["description"],
                Does.Contain("MCP Unity Extensions for Unity CLI"));

            var dependencies = (JObject)manifest["dependencies"];
            Assert.That(dependencies.Properties().Select(property => property.Name), Is.EquivalentTo(new[]
            {
                "com.unity.pipeline",
                "com.unity.test-framework"
            }));
            Assert.That((string)dependencies["com.unity.pipeline"], Is.EqualTo("0.3.1-exp.1"));
            Assert.That((string)dependencies["com.unity.test-framework"], Is.EqualTo("1.3.3"));
        }

        [Test]
        public void EditorAssembly_IsEditorOnlyAndReferencesOnlyPipelineAssemblies()
        {
            var package = PackageInfo.FindForAssembly(typeof(AssignMaterialCommand).Assembly);
            var asmdef = JObject.Parse(File.ReadAllText(
                Path.Combine(package.assetPath, "Editor/McpUnity.Editor.asmdef")));

            Assert.That((string)asmdef["name"], Is.EqualTo("McpUnity.Extensions"));
            Assert.That((string)asmdef["rootNamespace"], Is.EqualTo("McpUnity.Extensions"));
            Assert.That(
                asmdef["includePlatforms"].Values<string>(),
                Is.EquivalentTo(new[] { "Editor" }));
            Assert.That(
                asmdef["references"].Values<string>(),
                Is.EquivalentTo(new[] { "Unity.Pipeline", "Unity.Pipeline.Editor" }));
        }
    }
}
