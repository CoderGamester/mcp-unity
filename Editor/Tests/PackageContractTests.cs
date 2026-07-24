using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using McpUnity.Extensions.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Pipeline.Models;
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

        [Test]
        public void PublicResultDtos_DeclareExplicitStableCamelCaseWireNames()
        {
            var dtoTypes = typeof(InspectGameObjectResult).Assembly.GetTypes()
                .Where(type =>
                    type.IsPublic &&
                    type.IsClass &&
                    type.Namespace == typeof(InspectGameObjectResult).Namespace &&
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 0)
                .ToList();

            Assert.That(dtoTypes, Is.Not.Empty);
            foreach (var type in dtoTypes)
            {
                Assert.That(type.GetCustomAttribute<DataContractAttribute>(), Is.Not.Null,
                    $"{type.Name} must declare DataContract.");
                foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    var member = property.GetCustomAttribute<DataMemberAttribute>();
                    Assert.That(member, Is.Not.Null,
                        $"{type.Name}.{property.Name} must declare DataMember.");
                    Assert.That(member.Name, Is.EqualTo(CamelCase(property.Name)),
                        $"{type.Name}.{property.Name} must have a stable camelCase wire name.");
                }
            }
        }

        [Test]
        public void PipelineJsonSerialization_UsesStableInspectUnloadAndMaterialShapes()
        {
            var inspect = new InspectGameObjectResult
            {
                Root = new GameObjectInspection
                {
                    Name = "Root",
                    Transform = new TransformInspection
                    {
                        LocalPosition = new Vector3Inspection { X = 1f, Y = 2f, Z = 3f }
                    },
                    Components =
                    {
                        new ComponentInspection
                        {
                            Type = "Fixture",
                            Properties =
                            {
                                new SerializedPropertyInspection
                                {
                                    Name = "Text",
                                    Path = "Text",
                                    Type = "String",
                                    Value = "bounded"
                                }
                            }
                        }
                    }
                }
            };
            var unload = new UnloadSceneResult
            {
                UnloadedPath = "Assets/Old.unity",
                ActiveSceneName = "Main",
                ActiveScenePath = "Assets/Main.unity"
            };
            var material = new AssignMaterialResult
            {
                GameObject = new AuthoringResult { HierarchyPath = "/Root" },
                Material = new AuthoringResult { AssetPath = "Assets/Material.mat" },
                Slot = 2
            };

            var inspectJson = JObject.Parse(JsonConvert.SerializeObject(inspect));
            Assert.That(inspectJson.Properties().Select(property => property.Name),
                Is.EquivalentTo(new[]
                {
                    "root",
                    "maxDepth",
                    "maxNodes",
                    "maxPropertiesPerComponent",
                    "nodesReturned",
                    "nodeLimitReached",
                    "maxComponentsPerGameObject",
                    "maxTotalComponents",
                    "componentsReturned",
                    "componentLimitReached",
                    "aggregateWorkBudget",
                    "aggregateWorkUsed",
                    "aggregateWorkLimitReached",
                    "aggregateConversionCount",
                    "aggregatePropertiesScanned",
                    "aggregateContentBudgetBytes",
                    "aggregateEstimatedContentBytes",
                    "aggregateContentLimitReached",
                    "conversionTruncated",
                    "payloadBudgetBytes",
                    "payloadBytes",
                    "payloadTruncated",
                    "payloadTruncationReason"
                }));
            Assert.That((string)inspectJson["root"]["name"], Is.EqualTo("Root"));
            Assert.That((float)inspectJson["root"]["transform"]["localPosition"]["x"], Is.EqualTo(1f));
            Assert.That((string)inspectJson["root"]["components"][0]["properties"][0]["value"],
                Is.EqualTo("bounded"));

            var unloadJson = JObject.Parse(JsonConvert.SerializeObject(unload));
            Assert.That(unloadJson.Properties().Select(property => property.Name),
                Is.EquivalentTo(new[] { "unloadedPath", "activeSceneName", "activeScenePath" }));
            Assert.That((string)unloadJson["activeScenePath"], Is.EqualTo("Assets/Main.unity"));

            var materialJson = JObject.Parse(JsonConvert.SerializeObject(material));
            Assert.That(materialJson.Properties().Select(property => property.Name),
                Is.EquivalentTo(new[] { "gameObject", "material", "slot" }));
            Assert.That((string)materialJson["gameObject"]["hierarchyPath"], Is.EqualTo("/Root"));
            Assert.That((string)materialJson["material"]["assetPath"],
                Is.EqualTo("Assets/Material.mat"));
        }

        [Test]
        public void InspectCommand_DoesNotRewalkMaterializedPropertyValuesForBudgeting()
        {
            var package = PackageInfo.FindForAssembly(typeof(InspectGameObjectCommand).Assembly);
            var source = File.ReadAllText(
                Path.Combine(package.assetPath, "Editor/Commands/InspectGameObjectCommand.cs"));

            Assert.That(source, Does.Not.Contain("EstimateValueBytes("));
            Assert.That(source, Does.Not.Contain("Stack<object>"));
            Assert.That(source, Does.Not.Contain("DictionaryEntry"));
        }

        private static string CamelCase(string name) =>
            char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
