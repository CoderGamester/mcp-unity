using System;
using System.Collections.Generic;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Models;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    public static class InspectGameObjectCommand
    {
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
            return new InspectGameObjectResult
            {
                Root = root,
                MaxDepth = context.MaxDepth,
                MaxNodes = context.MaxNodes,
                MaxPropertiesPerComponent = context.MaxPropertiesPerComponent,
                NodesReturned = context.NodesReturned,
                NodeLimitReached = context.NodeLimitReached
            };
        }

        private static GameObjectInspection BuildNode(
            GameObject gameObject,
            int depth,
            InspectionContext context)
        {
            if (context.NodesReturned >= context.MaxNodes)
            {
                context.NodeLimitReached = true;
                return null;
            }

            context.NodesReturned++;
            var transform = gameObject.transform;
            var node = new GameObjectInspection
            {
                Identity = ObjectResolver.Describe(gameObject),
                Name = gameObject.name,
                Path = ObjectResolver.Describe(gameObject)?.HierarchyPath,
                ScenePath = gameObject.scene.IsValid() ? gameObject.scene.path : null,
                ActiveSelf = gameObject.activeSelf,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                Layer = gameObject.layer,
                LayerName = LayerMask.LayerToName(gameObject.layer),
                Tag = gameObject.tag,
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
                return node;
            }

            for (var index = 0; index < transform.childCount; index++)
            {
                if (context.NodesReturned >= context.MaxNodes)
                {
                    context.NodeLimitReached = true;
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

            foreach (var component in components)
            {
                if (component == null)
                {
                    node.Components.Add(new ComponentInspection
                    {
                        Type = "<missing>",
                        Missing = true
                    });
                    continue;
                }

                var summary = new ComponentInspection
                {
                    Identity = ObjectResolver.Describe(component),
                    Type = component.GetType().Name,
                    Enabled = GetEnabled(component),
                    PropertiesIncluded = context.IncludeProperties
                };

                if (context.IncludeProperties)
                    ReadProperties(component, summary, context.MaxPropertiesPerComponent);

                node.Components.Add(summary);
            }
        }

        private static void ReadProperties(
            Component component,
            ComponentInspection summary,
            int maxProperties)
        {
            try
            {
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                var enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.propertyPath == "m_Script")
                        continue;

                    if (!SerializedPropertyValueReader.TryRead(iterator, out var value))
                        continue;

                    summary.SerializedPropertyCount++;
                    if (summary.Properties.Count >= maxProperties)
                    {
                        summary.PropertiesTruncated = true;
                        continue;
                    }

                    summary.Properties.Add(new SerializedPropertyInspection
                    {
                        Name = iterator.displayName,
                        Path = iterator.propertyPath,
                        Type = iterator.propertyType.ToString(),
                        Value = value
                    });
                }
            }
            catch (Exception exception)
            {
                summary.PropertiesError = exception.Message;
            }
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

        private sealed class InspectionContext
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
            }

            public int MaxDepth { get; }
            public int MaxNodes { get; }
            public bool IncludeComponents { get; }
            public bool IncludeProperties { get; }
            public int MaxPropertiesPerComponent { get; }
            public int NodesReturned { get; set; }
            public bool NodeLimitReached { get; set; }
        }
    }
}
