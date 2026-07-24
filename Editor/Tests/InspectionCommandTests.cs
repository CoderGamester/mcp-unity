using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using McpUnity.Extensions.Commands;
using Newtonsoft.Json;
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
            Assert.That(Property<bool>(result, "PayloadTruncated"), Is.True);
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
            Assert.That(Property<bool>(result, "PayloadTruncated"), Is.True);
            Assert.That(Property<bool>(Property(result, "Root"), "ChildrenTruncated"), Is.True);
        }

        [Test]
        public void Inspect_BoundsDeepAndWideHierarchiesWithHonestMarkers()
        {
            var root = new GameObject("Root");
            var parent = root.transform;
            for (var depth = 0; depth < 100; depth++)
            {
                var child = new GameObject($"Deep-{depth}");
                child.transform.SetParent(parent);
                parent = child.transform;
            }
            for (var index = 0; index < 2000; index++)
                new GameObject($"Wide-{index}").transform.SetParent(root.transform);

            var result = InspectGameObjectCommand.Inspect(
                Ref(root),
                maxDepth: 999,
                maxNodes: 9999,
                includeComponents: false);
            var serializedBytes = System.Text.Encoding.UTF8.GetByteCount(
                JsonConvert.SerializeObject(result));

            Assert.That(result.MaxDepth, Is.EqualTo(8));
            Assert.That(result.MaxNodes, Is.EqualTo(1000));
            Assert.That(result.NodesReturned, Is.LessThanOrEqualTo(1000));
            Assert.That(result.PayloadTruncated, Is.True);
            Assert.That(serializedBytes, Is.LessThanOrEqualTo(512 * 1024));
            Assert.That(
                Flatten(result.Root).Any(node => node.ChildrenTruncated),
                Is.True);
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

        [Test]
        public void Inspect_BoundsLargeStringsCollectionsAndNestedValuesWithExplicitMarkers()
        {
            var root = new GameObject("Root");
            var fixture = root.AddComponent<LargeInspectionFixtureComponent>();
            fixture.LargeString = new string('x', 5000);
            fixture.LargeArray = Enumerable.Range(0, 150).ToArray();

            var result = InspectGameObjectCommand.Inspect(
                Ref(root),
                includeProperties: true,
                maxPropertiesPerComponent: 10);
            Assert.That(result.PayloadTruncated, Is.True);

            var components = Property<IList>(result.Root, "Components");
            var component = components.Cast<object>()
                .Single(item => Property<string>(item, "Type") == nameof(LargeInspectionFixtureComponent));
            var properties = Property<IList>(component, "Properties").Cast<object>().ToList();

            var largeString = properties.Single(item => Property<string>(item, "Path") == "LargeString");
            Assert.That(Property<string>(largeString, "Value"), Has.Length.EqualTo(4096));
            AssertValueTruncation(largeString, "stringLength", 4096, 5000);

            var largeArray = properties.Single(item => Property<string>(item, "Path") == "LargeArray");
            Assert.That(Property<IList>(largeArray, "Value"), Has.Count.EqualTo(100));
            AssertValueTruncation(largeArray, "collectionLength", 100, 150);

            var nested = properties.Single(item => Property<string>(item, "Path") == "Nested");
            AssertValueTruncation(nested, "serializationDepth", 4, null);
        }

        [Test]
        public void Inspect_SerializesEnumsWithoutMaterializingNameCollections()
        {
            var root = new GameObject("Root");
            root.AddComponent<OversizedEnumInspectionFixtureComponent>();

            var result = InspectGameObjectCommand.Inspect(
                Ref(root),
                includeProperties: true,
                maxPropertiesPerComponent: 10);
            var component = result.Root.Components.Single(
                item => item.Type == nameof(OversizedEnumInspectionFixtureComponent));
            var property = component.Properties.Single(
                item => item.Path == nameof(OversizedEnumInspectionFixtureComponent.Value));

            Assert.That(property.Value, Is.EqualTo(0));
            Assert.That(property.ValueTruncated, Is.False);
            Assert.That(
                JsonConvert.SerializeObject(result),
                Does.Not.Contain(OversizedEnumInspectionFixtureComponent.SelectedName));
        }

        [Test]
        public void Inspect_StopsConvertingValuesAfterFirstSupportedPropertyBeyondCap()
        {
            var root = new GameObject("Root");
            root.AddComponent<InspectionFixtureComponent>();
            var readerType = typeof(InspectGameObjectCommand).Assembly.GetType(
                "McpUnity.Extensions.Commands.SerializedPropertyValueReader",
                throwOnError: true);
            var observer = readerType.GetProperty(
                "ConversionObserver",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(observer, Is.Not.Null, "Expected an internal value-conversion test seam.");

            var convertedPaths = new List<string>();
            observer.SetValue(null, (Action<string>)(path => convertedPaths.Add(path)));
            try
            {
                var result = InspectGameObjectCommand.Inspect(
                    Ref(root),
                    includeProperties: true,
                    maxPropertiesPerComponent: 1);

                var component = result.Root.Components
                    .Single(item => item.Type == nameof(InspectionFixtureComponent));
                Assert.That(component.PropertiesTruncated, Is.True);
            }
            finally
            {
                observer.SetValue(null, null);
            }

            Assert.That(
                convertedPaths.Where(path =>
                    path == "First" ||
                    path == "Second" ||
                    path == "Third"),
                Is.EqualTo(new[] { "First" }),
                "The omitted property and all later values must not be materialized.");
        }

        [Test]
        public void Inspect_CapsComponentsPerNodeAndAcrossTheWholeInspection()
        {
            var root = new GameObject("Root");
            for (var index = 0; index < 80; index++)
                root.AddComponent<InspectionFixtureComponent>();
            for (var childIndex = 0; childIndex < 10; childIndex++)
            {
                var child = new GameObject($"Child-{childIndex}");
                child.transform.SetParent(root.transform);
                for (var componentIndex = 0; componentIndex < 40; componentIndex++)
                    child.AddComponent<InspectionFixtureComponent>();
            }

            var result = InspectGameObjectCommand.Inspect(
                Ref(root),
                maxDepth: 2,
                maxNodes: 100,
                includeProperties: false,
                maxPropertiesPerComponent: 100);

            Assert.That(result.ComponentsReturned, Is.LessThanOrEqualTo(result.MaxTotalComponents));
            Assert.That(result.ComponentLimitReached, Is.True);
            Assert.That(result.Root.Components, Has.Count.LessThanOrEqualTo(result.MaxComponentsPerGameObject));
            Assert.That(result.Root.ComponentsTruncated, Is.True);
            Assert.That(
                result.Root.Children.SelectMany(child => child.Components).Count() +
                result.Root.Components.Count,
                Is.EqualTo(result.ComponentsReturned));
        }

        [Test]
        public void Inspect_StopsAtAggregatePayloadBudgetWithHonestMarkers()
        {
            var root = new GameObject(new string('R', 5000));
            for (var childIndex = 0; childIndex < 200; childIndex++)
            {
                var child = new GameObject($"Child-{childIndex}-{new string('N', 5000)}");
                child.transform.SetParent(root.transform);
                for (var componentIndex = 0; componentIndex < 8; componentIndex++)
                {
                    var fixture = child.AddComponent<LargeInspectionFixtureComponent>();
                    fixture.LargeString = new string('"', 200_000);
                    fixture.LargeArray = Enumerable.Range(0, 1000).ToArray();
                }
            }

            var result = InspectGameObjectCommand.Inspect(
                Ref(root),
                maxDepth: 8,
                maxNodes: 1000,
                includeProperties: true,
                maxPropertiesPerComponent: 200);
            var json = JsonConvert.SerializeObject(result);
            var serializedBytes = System.Text.Encoding.UTF8.GetByteCount(json);

            Assert.That(serializedBytes, Is.LessThanOrEqualTo(512 * 1024));
            Assert.That(result.PayloadBudgetBytes, Is.EqualTo(512 * 1024));
            Assert.That(result.PayloadBytes, Is.EqualTo(serializedBytes));
            Assert.That(result.PayloadTruncated, Is.True);
            Assert.That(result.ComponentsReturned, Is.LessThanOrEqualTo(result.MaxTotalComponents));
            Assert.That(result.Root.Name.Length, Is.LessThanOrEqualTo(256));
            Assert.That(
                Flatten(result.Root).Any(node =>
                    node.ComponentsTruncated ||
                    node.Components.Any(component => component.PropertiesTruncated)),
                Is.True);
        }

        [Test]
        public void Inspect_FinalSerializedSizeGuardCannotReportAnOversizedPayload()
        {
            var result = new InspectGameObjectResult
            {
                Root = new GameObjectInspection
                {
                    Name = new string('x', 600_000)
                },
                NodesReturned = 1,
                PayloadBudgetBytes = 512 * 1024
            };
            var stabilize = typeof(InspectGameObjectCommand).GetMethod(
                "StabilizePayloadBytes",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(stabilize, Is.Not.Null);
            stabilize.Invoke(null, new object[] { result });

            var serializedBytes = System.Text.Encoding.UTF8.GetByteCount(
                JsonConvert.SerializeObject(result));
            Assert.That(serializedBytes, Is.LessThanOrEqualTo(512 * 1024));
            Assert.That(result.PayloadBytes, Is.EqualTo(serializedBytes));
            Assert.That(result.PayloadTruncated, Is.True);
            Assert.That(result.PayloadTruncationReason, Is.EqualTo("serializedPayloadBudget"));
            Assert.That(result.Root, Is.Null);
            Assert.That(result.NodesReturned, Is.Zero);
            Assert.That(result.ComponentsReturned, Is.Zero);
        }

        [Test]
        public void Inspect_FailsClosedWhenPayloadSerializerIsUnavailable()
        {
            var serializerOverride = typeof(InspectGameObjectCommand).GetProperty(
                "SerializationOverride",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(
                serializerOverride,
                Is.Not.Null,
                "Expected a serializer-failure test seam.");
            if (serializerOverride == null)
                return;

            serializerOverride.SetValue(null, (Func<object, string>)(_ => null));
            try
            {
                var root = new GameObject("Root");
                Assert.Throws<InvalidOperationException>(() =>
                    InspectGameObjectCommand.Inspect(Ref(root)));
            }
            finally
            {
                serializerOverride.SetValue(null, null);
            }
        }

        [Test]
        public void Inspect_SharesAggregateConversionBudgetAcrossBroadNestedComponents()
        {
            var root = new GameObject("Root");
            for (var componentIndex = 0; componentIndex < 12; componentIndex++)
            {
                var fixture = root.AddComponent<BroadInspectionFixtureComponent>();
                fixture.Groups = Enumerable.Range(0, 8)
                    .Select(groupIndex => new BroadInspectionGroup
                    {
                        Values = Enumerable.Range(0, 10)
                            .Select(valueIndex => new BroadInspectionValue
                            {
                                First = valueIndex,
                                Second = componentIndex,
                                Third = groupIndex
                            })
                            .ToArray()
                    })
                    .ToArray();
            }

            var readerType = typeof(InspectGameObjectCommand).Assembly.GetType(
                "McpUnity.Extensions.Commands.SerializedPropertyValueReader",
                throwOnError: true);
            var observer = readerType.GetProperty(
                "ConversionObserver",
                BindingFlags.Static | BindingFlags.NonPublic);
            var convertedPaths = new List<string>();
            observer.SetValue(null, (Action<string>)(path => convertedPaths.Add(path)));
            InspectGameObjectResult result;
            try
            {
                result = InspectGameObjectCommand.Inspect(
                    Ref(root),
                    includeProperties: true,
                    maxPropertiesPerComponent: 200);
            }
            finally
            {
                observer.SetValue(null, null);
            }

            var serializedBytes = System.Text.Encoding.UTF8.GetByteCount(
                JsonConvert.SerializeObject(result));
            var fixtures = result.Root.Components
                .Where(component => component.Type == nameof(BroadInspectionFixtureComponent))
                .ToList();

            Assert.That(fixtures, Has.Count.GreaterThan(1));
            Assert.That(result.AggregateWorkBudget, Is.GreaterThan(0));
            Assert.That(result.AggregateWorkUsed, Is.LessThanOrEqualTo(result.AggregateWorkBudget));
            Assert.That(result.AggregateWorkLimitReached, Is.True);
            Assert.That(result.AggregateConversionCount, Is.EqualTo(convertedPaths.Count));
            Assert.That(convertedPaths.Count, Is.LessThanOrEqualTo(result.AggregateWorkBudget));
            Assert.That(result.ConversionTruncated, Is.True);
            Assert.That(result.AggregatePropertiesScanned, Is.LessThanOrEqualTo(result.AggregateWorkBudget));
            Assert.That(
                fixtures.Count(component =>
                    component.PropertiesTruncationReason == "aggregateWorkBudget"),
                Is.GreaterThanOrEqualTo(1),
                "The one inspect-call budget must remain exhausted for a later component.");
            Assert.That(
                fixtures[0].PropertiesTruncationReason,
                Is.Not.EqualTo("aggregateWorkBudget"),
                "The work limit must be consumed by earlier conversion work before it affects a later component.");
            Assert.That(serializedBytes, Is.LessThanOrEqualTo(512 * 1024));
        }

        [Test]
        public void InspectionBudget_ExactBoundaryIsExhaustedWithoutClaimingTruncation()
        {
            var budgetType = typeof(InspectGameObjectCommand).Assembly.GetType(
                "McpUnity.Extensions.Commands.InspectionBudget",
                throwOnError: true);
            var budget = Activator.CreateInstance(budgetType, 3, 128);
            var reserve = budgetType.GetMethod("TryReserve");

            Assert.That(
                reserve.Invoke(budget, new object[] { 3, 128, false, false }),
                Is.True);
            Assert.That(Property<int>(budget, "WorkUsed"), Is.EqualTo(3));
            Assert.That(Property<int>(budget, "EstimatedContentBytes"), Is.EqualTo(128));
            Assert.That(Property<bool>(budget, "WorkLimitReached"), Is.True);
            Assert.That(Property<bool>(budget, "ContentLimitReached"), Is.True);
            Assert.That(Property<bool>(budget, "LimitReached"), Is.True);
            Assert.That(Property<bool>(budget, "ConversionTruncated"), Is.False);
            Assert.That(
                reserve.Invoke(budget, new object[] { 1, 0, false, false }),
                Is.False);
            Assert.That(Property<bool>(budget, "ConversionTruncated"), Is.False);
        }

        [Test]
        public void Inspect_DoesNotAllocatePropertyReadersAfterExactWorkExhaustion()
        {
            var root = new GameObject("Root");
            var fixture = root.AddComponent<InspectionFixtureComponent>();
            var commandType = typeof(InspectGameObjectCommand);
            var contextType = commandType.GetNestedType(
                "InspectionContext",
                BindingFlags.NonPublic);
            var context = Activator.CreateInstance(
                contextType,
                0,
                1,
                true,
                true,
                10);
            var budget = Property(context, "Budget");
            var reserve = budget.GetType().GetMethod("TryReserve");
            var workBudget = Property<int>(budget, "WorkBudget");
            Assert.That(
                reserve.Invoke(budget, new object[] { workBudget, 0, false, false }),
                Is.True);

            var allocationObserver = commandType.GetProperty(
                "PropertyReaderAllocationObserver",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(
                allocationObserver,
                Is.Not.Null,
                "Expected a reader-allocation test seam after successful reservations.");
            var allocations = new List<string>();
            allocationObserver.SetValue(
                null,
                (Action<string>)(stage => allocations.Add(stage)));
            var summary = new ComponentInspection
            {
                Type = nameof(InspectionFixtureComponent),
                PropertiesIncluded = true
            };
            try
            {
                var readProperties = commandType.GetMethod(
                    "ReadProperties",
                    BindingFlags.Static | BindingFlags.NonPublic);
                readProperties.Invoke(null, new[] { fixture, summary, context });
            }
            finally
            {
                allocationObserver.SetValue(null, null);
            }

            Assert.That(allocations, Is.Empty);
            Assert.That(summary.Properties, Is.Empty);
            Assert.That(summary.PropertiesTruncated, Is.True);
            Assert.That(
                summary.PropertiesTruncationReason,
                Is.EqualTo("aggregateWorkBudget"));
            Assert.That(Property<bool>(budget, "ConversionTruncated"), Is.True);
        }

        [Test]
        public void SerializedPropertyReader_ReportsItsPreMaterializationReservationDeltas()
        {
            var resultType = typeof(InspectGameObjectCommand).Assembly.GetType(
                "McpUnity.Extensions.Commands.SerializedPropertyReadResult",
                throwOnError: true);

            Assert.That(
                resultType.GetProperty("ReservedWorkUnits"),
                Is.Not.Null);
            Assert.That(
                resultType.GetProperty("ReservedContentBytes"),
                Is.Not.Null);
        }

        private static void AssertValueTruncation(
            object property,
            string reason,
            int limit,
            int? originalCount)
        {
            Assert.That(Property<bool>(property, "ValueTruncated"), Is.True);
            var markers = Property<IList>(property, "ValueTruncations").Cast<object>().ToList();
            var marker = markers.Single(item => Property<string>(item, "Reason") == reason);
            Assert.That(Property<int>(marker, "Limit"), Is.EqualTo(limit));
            Assert.That(Property<int?>(marker, "OriginalCount"), Is.EqualTo(originalCount));
        }

        private static ObjectRef Ref(UnityEngine.Object obj) =>
            new ObjectRef { InstanceId = PipelineUtils.GetObjectId(obj) };

        private static IEnumerable<GameObjectInspection> Flatten(GameObjectInspection root)
        {
            var pending = new Stack<GameObjectInspection>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                yield return current;
                for (var index = current.Children.Count - 1; index >= 0; index--)
                    pending.Push(current.Children[index]);
            }
        }

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

    public sealed class LargeInspectionFixtureComponent : MonoBehaviour
    {
        public string LargeString;
        public int[] LargeArray;
        public InspectionNestedLevel1 Nested = new InspectionNestedLevel1();
    }

    public sealed class BroadInspectionFixtureComponent : MonoBehaviour
    {
        public BroadInspectionGroup[] Groups;
    }

    public sealed class OversizedEnumInspectionFixtureComponent : MonoBehaviour
    {
        public const string SelectedName =
            nameof(OversizedEnum.ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput_ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput_ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput);

        public OversizedEnum Value =
            OversizedEnum.ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput_ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput_ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput;
    }

    public enum OversizedEnum
    {
        ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput_ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput_ThisEnumMemberNameIsIntentionallyLongToExerciseBoundedSerializedEnumOutput
    }

    [Serializable]
    public sealed class BroadInspectionGroup
    {
        public BroadInspectionValue[] Values;
    }

    [Serializable]
    public sealed class BroadInspectionValue
    {
        public int First;
        public int Second;
        public int Third;
    }

    [Serializable]
    public sealed class InspectionNestedLevel1
    {
        public InspectionNestedLevel2 Child = new InspectionNestedLevel2();
    }

    [Serializable]
    public sealed class InspectionNestedLevel2
    {
        public InspectionNestedLevel3 Child = new InspectionNestedLevel3();
    }

    [Serializable]
    public sealed class InspectionNestedLevel3
    {
        public InspectionNestedLevel4 Child = new InspectionNestedLevel4();
    }

    [Serializable]
    public sealed class InspectionNestedLevel4
    {
        public InspectionNestedLevel5 Child = new InspectionNestedLevel5();
    }

    [Serializable]
    public sealed class InspectionNestedLevel5
    {
        public int Value = 42;
    }
}
