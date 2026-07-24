using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    public static class InspectGameObjectCommand
    {
        internal const int MaxComponentsPerGameObject = 32;
        internal const int MaxTotalComponents = 128;
        internal const int PayloadBudgetBytes = 512 * 1024;
        internal const int AggregateWorkBudget = 4096;
        internal const int AggregateContentBudgetBytes = 384 * 1024;

        [CliCommand("inspect_gameobject", "Inspect a bounded GameObject hierarchy with optional component and serialized-property details.")]
        public static InspectGameObjectResult Inspect(
            [CliArg("target", "GameObject reference to inspect.", Required = true)] ObjectRef target,
            [CliArg("max_depth", "Maximum child depth to include.")] int maxDepth = 2,
            [CliArg("max_nodes", "Maximum number of GameObjects to include.")] int maxNodes = 200,
            [CliArg("include_components", "Include component summaries.")] bool includeComponents = true,
            [CliArg("include_properties", "Include serialized component properties.")] bool includeProperties = false,
            [CliArg("max_properties_per_component", "Maximum serialized properties per component.")] int maxPropertiesPerComponent = 100)
        {
            var gameObject = CommandObjectResolver.Resolve<GameObject>(target, "target");
            var context = new InspectionContext(
                Mathf.Clamp(maxDepth, 0, 8),
                Mathf.Clamp(maxNodes, 1, 1000),
                includeComponents,
                includeProperties,
                Mathf.Clamp(maxPropertiesPerComponent, 1, 200));

            var root = BuildNode(gameObject, 0, context);
            var result = new InspectGameObjectResult
            {
                Root = root,
                MaxDepth = context.MaxDepth,
                MaxNodes = context.MaxNodes,
                MaxPropertiesPerComponent = context.MaxPropertiesPerComponent,
                NodesReturned = context.NodesReturned,
                NodeLimitReached = context.NodeLimitReached,
                MaxComponentsPerGameObject = MaxComponentsPerGameObject,
                MaxTotalComponents = MaxTotalComponents,
                ComponentsReturned = context.ComponentsReturned,
                ComponentLimitReached = context.ComponentLimitReached,
                AggregateWorkBudget = context.Budget.WorkBudget,
                AggregateWorkUsed = context.Budget.WorkUsed,
                AggregateWorkLimitReached = context.Budget.WorkLimitReached,
                AggregateConversionCount = context.Budget.ConversionCount,
                AggregatePropertiesScanned = context.Budget.PropertiesScanned,
                AggregateContentBudgetBytes = context.Budget.ContentBudgetBytes,
                AggregateEstimatedContentBytes = context.Budget.EstimatedContentBytes,
                AggregateContentLimitReached = context.Budget.ContentLimitReached,
                ConversionTruncated = context.Budget.ConversionTruncated,
                PayloadBudgetBytes = PayloadBudgetBytes,
                PayloadTruncated = context.PayloadTruncated,
                PayloadTruncationReason = context.PayloadTruncationReason
            };
            StabilizePayloadBytes(result);
            return result;
        }

        private static GameObjectInspection BuildNode(
            GameObject gameObject,
            int depth,
            InspectionContext context)
        {
            if (context.NodesReturned >= context.MaxNodes)
            {
                context.NodeLimitReached = true;
                context.MarkPayloadTruncated("nodeLimit");
                return null;
            }

            var identity = BoundedIdentity(ObjectResolver.Describe(gameObject), context);
            var boundedName = BoundedString(gameObject.name, 256, context);
            var boundedPath = BoundedString(identity?.HierarchyPath, 1024, context);
            var boundedScenePath = BoundedString(
                gameObject.scene.IsValid() ? gameObject.scene.path : null,
                1024,
                context);
            var boundedLayerName = BoundedString(
                LayerMask.LayerToName(gameObject.layer),
                128,
                context);
            var boundedTag = BoundedString(gameObject.tag, 128, context);
            if (!context.TryConsume(
                    2048 +
                    IdentityBudget(identity) +
                    WorstCaseJsonStringBytes(boundedName) +
                    WorstCaseJsonStringBytes(boundedPath) +
                    WorstCaseJsonStringBytes(boundedScenePath) +
                    WorstCaseJsonStringBytes(boundedLayerName) +
                    WorstCaseJsonStringBytes(boundedTag)))
            {
                return null;
            }

            context.NodesReturned++;
            var transform = gameObject.transform;
            var node = new GameObjectInspection
            {
                Identity = identity,
                Name = boundedName,
                Path = boundedPath,
                ScenePath = boundedScenePath,
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                Layer = gameObject.layer,
                LayerName = boundedLayerName,
                Tag = boundedTag,
                IsStatic = gameObject.isStatic,
                Transform = new TransformInspection
                {
                    LocalPosition = Vector(transform.localPosition),
                    LocalEulerAngles = Vector(transform.localEulerAngles),
                    LocalScale = Vector(transform.localScale),
                    WorldPosition = Vector(transform.position),
                    WorldEulerAngles = Vector(transform.eulerAngles)
                },
                ChildCount = transform.childCount,
                ComponentsIncluded = context.IncludeComponents
            };

            AddComponents(gameObject, node, context);

            if (depth >= context.MaxDepth)
            {
                node.ChildrenTruncated = transform.childCount > 0;
                if (node.ChildrenTruncated)
                    context.MarkPayloadTruncated("depthLimit");
                return node;
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                if (context.NodesReturned >= context.MaxNodes)
                {
                    context.NodeLimitReached = true;
                    context.MarkPayloadTruncated("nodeLimit");
                    node.ChildrenTruncated = true;
                    break;
                }

                var child = BuildNode(transform.GetChild(index).gameObject, depth + 1, context);
                if (child == null)
                {
                    node.ChildrenTruncated = true;
                    break;
                }

                node.Children.Add(child);
            }

            return node;
        }

        private static void AddComponents(
            GameObject gameObject,
            GameObjectInspection node,
            InspectionContext context)
        {
            var components = gameObject.GetComponents<Component>();
            node.ComponentCount = components.Length;
            if (!context.IncludeComponents)
                return;

            var perObjectLimit = Mathf.Min(components.Length, MaxComponentsPerGameObject);
            for (var index = 0; index < perObjectLimit; index++)
            {
                if (context.ComponentsReturned >= MaxTotalComponents)
                {
                    context.ComponentLimitReached = true;
                    node.ComponentsTruncated = true;
                    break;
                }

                var component = components[index];
                if (component == null)
                {
                    if (!context.TryConsume(256))
                    {
                        node.ComponentsTruncated = true;
                        break;
                    }
                    node.Components.Add(new ComponentInspection
                    {
                        Type = "<missing>",
                        Missing = true
                    });
                    context.ComponentsReturned++;
                    continue;
                }

                var typeName = BoundedString(component.GetType().Name, 256, context);
                var identity = BoundedIdentity(ObjectResolver.Describe(component), context);
                if (!context.TryConsume(
                        1024 +
                        WorstCaseJsonStringBytes(typeName) +
                        IdentityBudget(identity)))
                {
                    node.ComponentsTruncated = true;
                    break;
                }
                var summary = new ComponentInspection
                {
                    Identity = identity,
                    Type = typeName,
                    Enabled = GetEnabled(component),
                    PropertiesIncluded = context.IncludeProperties
                };
                context.ComponentsReturned++;

                if (context.IncludeProperties && context.Budget.LimitReached)
                    MarkAggregatePropertyTruncation(summary, context);
                else if (context.IncludeProperties)
                    ReadProperties(component, summary, context);

                node.Components.Add(summary);
            }

            if (node.Components.Count < components.Length)
            {
                node.ComponentsTruncated = true;
                node.ComponentsOmitted = components.Length - node.Components.Count;
                context.MarkPayloadTruncated(
                    context.ComponentLimitReached
                        ? "totalComponentLimit"
                        : components.Length > MaxComponentsPerGameObject
                            ? "perGameObjectComponentLimit"
                            : "payloadBudget");
            }
        }

        private static void ReadProperties(
            Component component,
            ComponentInspection summary,
            InspectionContext context)
        {
            try
            {
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                var enterChildren = true;
                while (true)
                {
                    if (!context.Budget.TryScanProperty())
                    {
                        MarkAggregatePropertyTruncation(summary, context);
                        break;
                    }
                    if (!iterator.NextVisible(enterChildren))
                        break;

                    enterChildren = false;
                    if (iterator.propertyPath == "m_Script")
                        continue;

                    if (!SerializedPropertyValueReader.CanRead(iterator))
                        continue;

                    summary.SerializedPropertyCount++;
                    if (summary.Properties.Count >= context.MaxPropertiesPerComponent)
                    {
                        summary.PropertiesTruncated = true;
                        summary.PropertiesOmittedAtLeast++;
                        summary.PropertiesTruncationReason = "perComponentPropertyLimit";
                        context.MarkPayloadTruncated("perComponentPropertyLimit");
                        break;
                    }

                    if (!SerializedPropertyValueReader.TryRead(
                            iterator,
                            context.Budget,
                            out var readResult))
                    {
                        if (context.Budget.LimitReached)
                        {
                            MarkAggregatePropertyTruncation(summary, context);
                            break;
                        }
                        continue;
                    }

                    var property = new SerializedPropertyInspection
                    {
                        Name = BoundedString(iterator.displayName, 256, context),
                        Path = BoundedString(iterator.propertyPath, 1024, context),
                        Type = BoundedString(iterator.propertyType.ToString(), 128, context),
                        Value = readResult.Value,
                        ValueTruncated = readResult.Truncations.Count > 0,
                        ValueTruncations = readResult.Truncations
                    };
                    if (property.ValueTruncated)
                        context.MarkPayloadTruncated("serializedValueBounds");
                    if (!context.TryConsume(PropertyBudget(property)))
                    {
                        summary.PropertiesTruncated = true;
                        summary.PropertiesOmittedAtLeast++;
                        summary.PropertiesTruncationReason = "payloadBudget";
                        break;
                    }

                    summary.Properties.Add(property);
                    summary.PropertiesReturned = summary.Properties.Count;
                }
            }
            catch (Exception exception)
            {
                var boundedError = BoundedString(exception.Message, 1024, context);
                summary.PropertiesError = context.TryConsume(
                    256 + WorstCaseJsonStringBytes(boundedError))
                    ? boundedError
                    : "Inspection failed; error omitted at payload budget.";
            }
        }

        private static void MarkAggregatePropertyTruncation(
            ComponentInspection summary,
            InspectionContext context)
        {
            summary.PropertiesTruncated = true;
            summary.PropertiesOmittedAtLeast++;
            summary.PropertiesTruncationReason = context.Budget.LimitReason;
            context.Budget.MarkConversionTruncated();
            context.MarkPayloadTruncated(context.Budget.LimitReason);
        }

        internal static AuthoringResult BoundedIdentity(
            AuthoringResult source,
            InspectionContext context = null)
        {
            if (source == null)
                return null;
            return new AuthoringResult
            {
                GlobalId = BoundedString(source.GlobalId, 1024, context),
                AssetPath = BoundedString(source.AssetPath, 1024, context),
                Guid = BoundedString(source.Guid, 128, context),
                FileId = source.FileId,
                InstanceId = source.InstanceId,
                HierarchyPath = BoundedString(source.HierarchyPath, 1024, context),
                Type = BoundedString(source.Type, 128, context)
            };
        }

        private static string BoundedString(
            string value,
            int maxLength,
            InspectionContext context)
        {
            if (value == null || value.Length <= maxLength)
                return value;
            context?.MarkPayloadTruncated("stringLength");
            return value.Substring(0, maxLength);
        }

        private static int IdentityBudget(AuthoringResult identity)
        {
            if (identity == null)
                return 16;
            return 256 +
                   WorstCaseJsonStringBytes(identity.GlobalId) +
                   WorstCaseJsonStringBytes(identity.AssetPath) +
                   WorstCaseJsonStringBytes(identity.Guid) +
                   WorstCaseJsonStringBytes(identity.HierarchyPath) +
                   WorstCaseJsonStringBytes(identity.Type);
        }

        private static int WorstCaseJsonStringBytes(string value) =>
            value == null ? 4 : 2 + value.Length * 6;

        private static int PropertyBudget(SerializedPropertyInspection property) =>
            1024 +
            WorstCaseJsonStringBytes(property.Name) +
            WorstCaseJsonStringBytes(property.Path) +
            WorstCaseJsonStringBytes(property.Type) +
            EstimateValueBytes(property.Value) +
            property.ValueTruncations.Count * 512;

        private static int EstimateValueBytes(object value)
        {
            var total = 0;
            var pending = new Stack<object>();
            pending.Push(value);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current == null)
                {
                    total += 4;
                    continue;
                }
                if (current is string text)
                {
                    total += WorstCaseJsonStringBytes(text);
                    continue;
                }
                if (current is AuthoringResult identity)
                {
                    total += IdentityBudget(identity);
                    continue;
                }
                if (current is IDictionary dictionary)
                {
                    total += 2 + dictionary.Count * 8;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        total += WorstCaseJsonStringBytes(entry.Key?.ToString());
                        pending.Push(entry.Value);
                    }
                    continue;
                }
                if (current is IEnumerable enumerable)
                {
                    total += 2;
                    foreach (var item in enumerable)
                    {
                        total += 1;
                        pending.Push(item);
                    }
                    continue;
                }
                total += 64;
            }
            return total;
        }

        private static void StabilizePayloadBytes(InspectGameObjectResult result)
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                var serialized = SerializeWithPipelineJson(result);
                if (serialized == null)
                {
                    result.PayloadBytes = 0;
                    return;
                }
                var bytes = Encoding.UTF8.GetByteCount(serialized);
                if (result.PayloadBytes == bytes)
                    return;
                result.PayloadBytes = bytes;
            }
        }

        private static string SerializeWithPipelineJson(object value)
        {
            var jsonType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Newtonsoft.Json.JsonConvert", false))
                .FirstOrDefault(type => type != null);
            var method = jsonType?.GetMethod(
                "SerializeObject",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(object) },
                null);
            return method?.Invoke(null, new[] { value }) as string;
        }

        private static bool? GetEnabled(Component component)
        {
            if (component is Behaviour behaviour)
                return behaviour.enabled;
            if (component is Renderer renderer)
                return renderer.enabled;
            if (component is Collider collider)
                return collider.enabled;
            return null;
        }

        private static Vector3Inspection Vector(Vector3 value) =>
            new Vector3Inspection { X = value.x, Y = value.y, Z = value.z };

        internal sealed class InspectionContext
        {
            public InspectionContext(
                int maxDepth,
                int maxNodes,
                bool includeComponents,
                bool includeProperties,
                int maxPropertiesPerComponent)
            {
                MaxDepth = maxDepth;
                MaxNodes = maxNodes;
                IncludeComponents = includeComponents;
                IncludeProperties = includeProperties;
                MaxPropertiesPerComponent = maxPropertiesPerComponent;
                Budget = new InspectionBudget(
                    AggregateWorkBudget,
                    AggregateContentBudgetBytes);
            }

            public int MaxDepth { get; }
            public int MaxNodes { get; }
            public bool IncludeComponents { get; }
            public bool IncludeProperties { get; }
            public int MaxPropertiesPerComponent { get; }
            public InspectionBudget Budget { get; }
            public int NodesReturned { get; set; }
            public bool NodeLimitReached { get; set; }
            public int ComponentsReturned { get; set; }
            public bool ComponentLimitReached { get; set; }
            public bool PayloadTruncated { get; private set; }
            public string PayloadTruncationReason { get; private set; }
            public bool TryConsume(int bytes)
            {
                if (Budget.TryReserve(0, bytes))
                    return true;
                MarkPayloadTruncated("payloadBudget");
                return false;
            }

            public void MarkPayloadTruncated(string reason)
            {
                PayloadTruncated = true;
                if (string.IsNullOrEmpty(PayloadTruncationReason))
                    PayloadTruncationReason = reason;
            }
        }
    }
}
