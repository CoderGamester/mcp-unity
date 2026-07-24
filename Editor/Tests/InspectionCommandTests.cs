using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using McpUnity.Extensions.Commands;
using NUnit.Framework;
using Unity.Pipeline;
using Unity.Pipeline.Models;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace McpUnity.Extensions.Tests
{
    public class InspectionCommandTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Inspect_ReturnsIdentityStateTransformAndComponentSummaries()
        {
            var target = new GameObject("Inspectable")
            {
                layer = 5,
                tag = "Untagged",
                isStatic = true
            };
            target.transform.localPosition = new Vector3(1f, 2f, 3f);
            target.AddComponent<BoxCollider>();
            target.SetActive(false);

            object result = null;
            Assert.DoesNotThrow(() => result = InspectGameObjectCommand.Inspect(Ref(target)));

            var root = Property(result, "Root");
            Assert.That(Property<string>(root, "Name"), Is.EqualTo("Inspectable"));
            Assert.That(Property<string>(root, "Path"), Is.EqualTo("/Inspectable"));
            Assert.That(Property<bool>(root, "ActiveSelf"), Is.False);
            Assert.That(Property<bool>(root, "ActiveInHierarchy"), Is.False);
            Assert.That(Property<int>(root, "Layer"), Is.EqualTo(5));
            Assert.That(Property<string>(root, "Tag"), Is.EqualTo("Untagged"));
            Assert.That(Property<bool>(root, "IsStatic"), Is.True);

            var identity = Property<AuthoringResult>(root, "Identity");
            Assert.That(identity.InstanceId, Is.EqualTo(PipelineUtils.GetObjectId(target)));

            var transform = Property(root, "Transform");
            var localPosition = Property(transform, "LocalPosition");
            Assert.That(Property<float>(localPosition, "X"), Is.EqualTo(1f));
            Assert.That(Property<float>(localPosition, "Y"), Is.EqualTo(2f));
            Assert.That(Property<float>(localPosition, "Z"), Is.EqualTo(3f));

            var components = Property<IList>(root, "Components");
            Assert.That(
                components.Cast<object>().Select(component => Property<string>(component, "Type")),
                Does.Contain(nameof(BoxCollider)));
        }

        [Test]
        public void Inspect_ClampsDepthToZeroAndMarksOmittedChildren()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);

            object result = null;
            Assert.DoesNotThrow(() =>
                result = InspectGameObjectCommand.Inspect(Ref(root), maxDepth: -20));

            Assert.That(Property<int>(result, "MaxDepth"), Is.EqualTo(0));
            var rootNode = Property(result, "Root");
            Assert.That(Property<IList>(rootNode, "Children"), Is.Empty);
            Assert.That(Property<bool>(rootNode, "ChildrenTruncated"), Is.True);
        }

        [Test]
        public void Inspect_ClampsNodeLimitAndMarksNodeTruncation()
        {
            var root = new GameObject("Root");
            new GameObject("First").transform.SetParent(root.transform);
            new GameObject("Second").transform.SetParent(root.transform);

            object result = null;
            Assert.DoesNotThrow(() =>
                result = InspectGameObjectCommand.Inspect(Ref(root), maxNodes: 0));

            Assert.That(Property<int>(result, "MaxNodes"), Is.EqualTo(1));
            Assert.That(Property<int>(result, "NodesReturned"), Is.EqualTo(1));
            Assert.That(Property<bool>(result, "NodeLimitReached"), Is.True);
            Assert.That(Property<bool>(Property(result, "Root"), "ChildrenTruncated"), Is.True);
        }

        [Test]
        public void Inspect_ClampsUpperBoundsAndUsesDocumentedDefaults()
        {
            var root = new GameObject("Root");

            object defaults = null;
            Assert.DoesNotThrow(() => defaults = InspectGameObjectCommand.Inspect(Ref(root)));
            Assert.That(Property<int>(defaults, "MaxDepth"), Is.EqualTo(2));
            Assert.That(Property<int>(defaults, "MaxNodes"), Is.EqualTo(200));
            Assert.That(Property<int>(defaults, "MaxPropertiesPerComponent"), Is.EqualTo(100));

            object clamped = null;
            Assert.DoesNotThrow(() =>
                clamped = InspectGameObjectCommand.Inspect(
                    Ref(root),
                    maxDepth: 99,
                    maxNodes: 5000,
                    maxPropertiesPerComponent: 999));
            Assert.That(Property<int>(clamped, "MaxDepth"), Is.EqualTo(8));
            Assert.That(Property<int>(clamped, "MaxNodes"), Is.EqualTo(1000));
            Assert.That(Property<int>(clamped, "MaxPropertiesPerComponent"), Is.EqualTo(200));
        }

        [Test]
        public void Inspect_BoundsSerializedPropertiesAndSkipsScriptReference()
        {
            var root = new GameObject("Root");
            root.AddComponent<InspectionFixtureComponent>();

            object result = null;
            Assert.DoesNotThrow(() =>
                result = InspectGameObjectCommand.Inspect(
                    Ref(root),
                    includeProperties: true,
                    maxPropertiesPerComponent: 1));

            var components = Property<IList>(Property(result, "Root"), "Components");
            var fixture = components.Cast<object>()
                .Single(component => Property<string>(component, "Type") == nameof(InspectionFixtureComponent));
            var properties = Property<IList>(fixture, "Properties");

            Assert.That(properties, Has.Count.EqualTo(1));
            Assert.That(Property<bool>(fixture, "PropertiesTruncated"), Is.True);
            Assert.That(
                properties.Cast<object>().Select(property => Property<string>(property, "Path")),
                Does.Not.Contain("m_Script"));
        }

        private static ObjectRef Ref(UnityEngine.Object obj) =>
            new ObjectRef { InstanceId = PipelineUtils.GetObjectId(obj) };

        private static object Property(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null, $"Cannot read '{name}' from a null result.");
            var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Expected public property '{name}' on {instance.GetType().Name}.");
            return property.GetValue(instance);
        }

        private static T Property<T>(object instance, string name) => (T)Property(instance, name);
    }

    public sealed class InspectionFixtureComponent : MonoBehaviour
    {
        public int First = 1;
        public string Second = "two";
        public Vector3 Third = Vector3.one;
        public AnimationCurve Unsupported = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}
