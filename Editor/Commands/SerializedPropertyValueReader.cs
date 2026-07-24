using System;
using System.Collections.Generic;
using Unity.Pipeline.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    internal sealed class InspectionBudget
    {
        public InspectionBudget(int workBudget, int contentBudgetBytes)
        {
            WorkBudget = workBudget;
            ContentBudgetBytes = contentBudgetBytes;
        }

        public int WorkBudget { get; }
        public int ContentBudgetBytes { get; }
        public int WorkUsed { get; private set; }
        public int EstimatedContentBytes { get; private set; }
        public int ConversionCount { get; private set; }
        public int PropertiesScanned { get; private set; }
        public bool WorkLimitReached { get; private set; }
        public bool ContentLimitReached { get; private set; }
        public bool ConversionTruncated { get; private set; }
        public bool LimitReached => WorkLimitReached || ContentLimitReached;
        public string LimitReason =>
            WorkLimitReached ? "aggregateWorkBudget" :
            ContentLimitReached ? "aggregateContentBudget" :
            "aggregateBudget";

        public bool TryReserve(
            int workUnits,
            int estimatedContentBytes,
            bool conversion = false,
            bool propertyScan = false)
        {
            workUnits = Math.Max(0, workUnits);
            estimatedContentBytes = Math.Max(0, estimatedContentBytes);
            if (WorkUsed > WorkBudget - workUnits)
            {
                WorkLimitReached = true;
                if (conversion || propertyScan)
                    ConversionTruncated = true;
                return false;
            }
            if (EstimatedContentBytes > ContentBudgetBytes - estimatedContentBytes)
            {
                ContentLimitReached = true;
                if (conversion || propertyScan)
                    ConversionTruncated = true;
                return false;
            }

            WorkUsed += workUnits;
            EstimatedContentBytes += estimatedContentBytes;
            if (conversion)
                ConversionCount++;
            if (propertyScan)
                PropertiesScanned++;
            return true;
        }

        public bool TryScanProperty() =>
            TryReserve(1, 0, propertyScan: true);

        public void MarkConversionTruncated()
        {
            ConversionTruncated = true;
        }
    }

    internal static class SerializedPropertyValueReader
    {
        internal const int MaxStringLength = 4096;
        internal const int MaxCollectionLength = 100;
        internal const int MaxSerializationDepth = 4;
        private const int MaxPropertyNameLength = 256;

        internal static Action<string> ConversionObserver { get; set; }

        public static bool CanRead(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                return true;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.String:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.BoundsInt:
                case SerializedPropertyType.Quaternion:
                case SerializedPropertyType.Hash128:
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ObjectReference:
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryRead(
            SerializedProperty property,
            out SerializedPropertyReadResult result) =>
            TryRead(
                property,
                new InspectionBudget(
                    InspectGameObjectCommand.AggregateWorkBudget,
                    InspectGameObjectCommand.AggregateContentBudgetBytes),
                out result);

        public static bool TryRead(
            SerializedProperty property,
            InspectionBudget budget,
            out SerializedPropertyReadResult result)
        {
            result = null;
            if (!CanRead(property))
                return false;
            if (!budget.TryReserve(1, 128))
            {
                budget.MarkConversionTruncated();
                return false;
            }

            var truncations = new List<SerializationTruncationInspection>();
            if (!TryReadValue(property, 0, budget, truncations, out var value))
                return false;

            result = new SerializedPropertyReadResult(value, truncations);
            return true;
        }

        private static bool TryReadValue(
            SerializedProperty property,
            int depth,
            InspectionBudget budget,
            List<SerializationTruncationInspection> truncations,
            out object value)
        {
            value = null;
            if (!budget.TryReserve(
                    1,
                    EstimatedValueBytes(property),
                    conversion: true))
            {
                return false;
            }

            ConversionObserver?.Invoke(property.propertyPath);
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                return TryReadArray(property, depth, budget, truncations, out value);

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    value = property.boolValue;
                    return true;
                case SerializedPropertyType.Integer:
                    value = property.longValue;
                    return true;
                case SerializedPropertyType.Float:
                    value = property.doubleValue;
                    return true;
                case SerializedPropertyType.String:
                    var stringValue = property.stringValue ?? string.Empty;
                    if (stringValue.Length > MaxStringLength)
                    {
                        value = stringValue.Substring(0, MaxStringLength);
                        truncations.Add(Truncation(
                            property,
                            "stringLength",
                            MaxStringLength,
                            stringValue.Length));
                    }
                    else
                    {
                        value = stringValue;
                    }
                    return true;
                case SerializedPropertyType.Enum:
                    value = property.enumValueIndex >= 0 &&
                            property.enumValueIndex < property.enumNames.Length
                        ? property.enumNames[property.enumValueIndex]
                        : property.intValue.ToString();
                    return true;
                case SerializedPropertyType.Vector2:
                    value = Values(property.vector2Value.x, property.vector2Value.y);
                    return true;
                case SerializedPropertyType.Vector3:
                    value = Values(
                        property.vector3Value.x,
                        property.vector3Value.y,
                        property.vector3Value.z);
                    return true;
                case SerializedPropertyType.Vector4:
                    value = Values(
                        property.vector4Value.x,
                        property.vector4Value.y,
                        property.vector4Value.z,
                        property.vector4Value.w);
                    return true;
                case SerializedPropertyType.Vector2Int:
                    value = Values(property.vector2IntValue.x, property.vector2IntValue.y);
                    return true;
                case SerializedPropertyType.Vector3Int:
                    value = Values(
                        property.vector3IntValue.x,
                        property.vector3IntValue.y,
                        property.vector3IntValue.z);
                    return true;
                case SerializedPropertyType.Color:
                    var color = property.colorValue;
                    value = Values(color.r, color.g, color.b, color.a);
                    return true;
                case SerializedPropertyType.Rect:
                    var rect = property.rectValue;
                    value = NamedValues(
                        ("x", rect.x),
                        ("y", rect.y),
                        ("width", rect.width),
                        ("height", rect.height));
                    return true;
                case SerializedPropertyType.RectInt:
                    var rectInt = property.rectIntValue;
                    value = NamedValues(
                        ("x", rectInt.x),
                        ("y", rectInt.y),
                        ("width", rectInt.width),
                        ("height", rectInt.height));
                    return true;
                case SerializedPropertyType.Bounds:
                    var bounds = property.boundsValue;
                    value = new Dictionary<string, object>
                    {
                        ["center"] = Values(bounds.center.x, bounds.center.y, bounds.center.z),
                        ["size"] = Values(bounds.size.x, bounds.size.y, bounds.size.z)
                    };
                    return true;
                case SerializedPropertyType.BoundsInt:
                    var boundsInt = property.boundsIntValue;
                    value = new Dictionary<string, object>
                    {
                        ["position"] = Values(
                            boundsInt.position.x,
                            boundsInt.position.y,
                            boundsInt.position.z),
                        ["size"] = Values(boundsInt.size.x, boundsInt.size.y, boundsInt.size.z)
                    };
                    return true;
                case SerializedPropertyType.Quaternion:
                    var quaternion = property.quaternionValue;
                    value = Values(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                    return true;
                case SerializedPropertyType.Hash128:
                    value = property.hash128Value.ToString();
                    return true;
                case SerializedPropertyType.ObjectReference:
                    var referenced = property.objectReferenceValue;
                    if (referenced is MonoScript)
                        return false;
                    value = referenced == null
                        ? null
                        : InspectGameObjectCommand.BoundedIdentity(
                            ObjectResolver.Describe(referenced));
                    return true;
                case SerializedPropertyType.Generic:
                    return TryReadObject(
                        property,
                        depth,
                        budget,
                        truncations,
                        out value);
                default:
                    return false;
            }
        }

        private static bool TryReadArray(
            SerializedProperty property,
            int depth,
            InspectionBudget budget,
            List<SerializationTruncationInspection> truncations,
            out object value)
        {
            if (depth >= MaxSerializationDepth)
            {
                value = null;
                truncations.Add(Truncation(
                    property,
                    "serializationDepth",
                    MaxSerializationDepth,
                    null));
                return true;
            }

            if (!budget.TryReserve(1, 64))
            {
                budget.MarkConversionTruncated();
                value = null;
                AddAggregateTruncation(property, budget, truncations);
                return true;
            }
            var originalCount = property.arraySize;
            var count = Mathf.Min(originalCount, MaxCollectionLength);
            if (!budget.TryReserve(1, count * 8))
            {
                budget.MarkConversionTruncated();
                value = null;
                AddAggregateTruncation(property, budget, truncations);
                return true;
            }

            var values = new List<object>(count);
            for (var index = 0; index < count; index++)
            {
                if (!budget.TryReserve(1, 16))
                {
                    budget.MarkConversionTruncated();
                    AddAggregateTruncation(property, budget, truncations);
                    break;
                }
                var element = property.GetArrayElementAtIndex(index);
                if (!TryReadValue(
                        element,
                        depth + 1,
                        budget,
                        truncations,
                        out var elementValue))
                {
                    if (budget.LimitReached)
                    {
                        budget.MarkConversionTruncated();
                        AddAggregateTruncation(property, budget, truncations);
                        break;
                    }
                    values.Add(null);
                    continue;
                }
                values.Add(elementValue);
            }

            if (originalCount > MaxCollectionLength)
            {
                truncations.Add(Truncation(
                    property,
                    "collectionLength",
                    MaxCollectionLength,
                    originalCount));
            }

            value = values;
            return true;
        }

        private static bool TryReadObject(
            SerializedProperty property,
            int depth,
            InspectionBudget budget,
            List<SerializationTruncationInspection> truncations,
            out object value)
        {
            if (depth >= MaxSerializationDepth)
            {
                value = null;
                truncations.Add(Truncation(
                    property,
                    "serializationDepth",
                    MaxSerializationDepth,
                    null));
                return true;
            }

            if (!budget.TryReserve(1, 128))
            {
                budget.MarkConversionTruncated();
                value = null;
                AddAggregateTruncation(property, budget, truncations);
                return true;
            }
            var values = new Dictionary<string, object>();
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            var enterChildren = true;
            var supportedCount = 0;
            while (true)
            {
                if (!budget.TryScanProperty())
                {
                    budget.MarkConversionTruncated();
                    AddAggregateTruncation(property, budget, truncations);
                    break;
                }
                if (!iterator.NextVisible(enterChildren) ||
                    SerializedProperty.EqualContents(iterator, end))
                {
                    break;
                }

                enterChildren = false;
                if (!CanRead(iterator))
                    continue;

                if (supportedCount >= MaxCollectionLength)
                {
                    truncations.Add(Truncation(
                        property,
                        "collectionLength",
                        MaxCollectionLength,
                        null));
                    break;
                }

                if (!budget.TryReserve(1, 64))
                {
                    budget.MarkConversionTruncated();
                    AddAggregateTruncation(property, budget, truncations);
                    break;
                }
                var rawKey = iterator.name ?? string.Empty;
                var key = rawKey.Length > MaxPropertyNameLength
                    ? rawKey.Substring(0, MaxPropertyNameLength)
                    : rawKey;
                if (!budget.TryReserve(0, WorstCaseJsonStringBytes(key)))
                {
                    budget.MarkConversionTruncated();
                    AddAggregateTruncation(property, budget, truncations);
                    break;
                }
                if (rawKey.Length > MaxPropertyNameLength)
                {
                    truncations.Add(Truncation(
                        iterator,
                        "propertyNameLength",
                        MaxPropertyNameLength,
                        rawKey.Length));
                }

                supportedCount++;
                if (!TryReadValue(
                        iterator,
                        depth + 1,
                        budget,
                        truncations,
                        out var childValue))
                {
                    if (budget.LimitReached)
                    {
                        budget.MarkConversionTruncated();
                        AddAggregateTruncation(property, budget, truncations);
                        break;
                    }
                    continue;
                }
                values[key] = childValue;
            }

            value = values;
            return true;
        }

        private static int EstimatedValueBytes(SerializedProperty property)
        {
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                return 32;
            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    return WorstCaseJsonStringBytes(MaxStringLength);
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.RectInt:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.BoundsInt:
                    return 256;
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Vector2Int:
                case SerializedPropertyType.Vector3Int:
                case SerializedPropertyType.Color:
                case SerializedPropertyType.Quaternion:
                    return 128;
                case SerializedPropertyType.ObjectReference:
                    return 8 * 1024;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Hash128:
                    return 512;
                case SerializedPropertyType.Generic:
                    return 32;
                default:
                    return 64;
            }
        }

        private static int WorstCaseJsonStringBytes(string value) =>
            value == null ? 4 : 2 + value.Length * 6;

        private static int WorstCaseJsonStringBytes(int characterCount) =>
            2 + characterCount * 6;

        private static void AddAggregateTruncation(
            SerializedProperty property,
            InspectionBudget budget,
            List<SerializationTruncationInspection> truncations)
        {
            if (truncations.Exists(marker => marker.Reason == budget.LimitReason))
                return;
            truncations.Add(Truncation(
                property,
                budget.LimitReason,
                budget.WorkLimitReached
                    ? budget.WorkBudget
                    : budget.ContentBudgetBytes,
                null));
        }

        private static SerializationTruncationInspection Truncation(
            SerializedProperty property,
            string reason,
            int limit,
            int? originalCount) =>
            new SerializationTruncationInspection
            {
                Path = property.propertyPath?.Length > 1024
                    ? property.propertyPath.Substring(0, 1024)
                    : property.propertyPath,
                Reason = reason,
                Limit = limit,
                OriginalCount = originalCount
            };

        private static object[] Values(params object[] values) => values;

        private static Dictionary<string, object> NamedValues(
            params (string Name, object Value)[] values)
        {
            var result = new Dictionary<string, object>();
            foreach (var value in values)
                result[value.Name] = value.Value;
            return result;
        }
    }

    internal sealed class SerializedPropertyReadResult
    {
        public SerializedPropertyReadResult(
            object value,
            List<SerializationTruncationInspection> truncations)
        {
            Value = value;
            Truncations = truncations;
        }

        public object Value { get; }
        public List<SerializationTruncationInspection> Truncations { get; }
    }
}
