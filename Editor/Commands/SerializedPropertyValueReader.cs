using System;
using System.Collections.Generic;
using Unity.Pipeline.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    internal static class SerializedPropertyValueReader
    {
        internal const int MaxStringLength = 4096;
        internal const int MaxCollectionLength = 100;
        internal const int MaxSerializationDepth = 4;

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
                    return true;
                case SerializedPropertyType.ObjectReference:
                    return !(property.objectReferenceValue is MonoScript);
                default:
                    return false;
            }
        }

        public static bool TryRead(
            SerializedProperty property,
            out SerializedPropertyReadResult result)
        {
            result = null;
            if (!CanRead(property))
                return false;

            ConversionObserver?.Invoke(property.propertyPath);
            var truncations = new List<SerializationTruncationInspection>();
            if (!TryReadValue(property, 0, truncations, out var value))
                return false;

            result = new SerializedPropertyReadResult(value, truncations);
            return true;
        }

        private static bool TryReadValue(
            SerializedProperty property,
            int depth,
            List<SerializationTruncationInspection> truncations,
            out object value)
        {
            value = null;
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
                return TryReadArray(property, depth, truncations, out value);

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
                    return TryReadObject(property, depth, truncations, out value);
                default:
                    return false;
            }
        }

        private static bool TryReadArray(
            SerializedProperty property,
            int depth,
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

            var originalCount = property.arraySize;
            var count = Mathf.Min(originalCount, MaxCollectionLength);
            var values = new List<object>(count);
            for (var index = 0; index < count; index++)
            {
                var element = property.GetArrayElementAtIndex(index);
                values.Add(TryReadValue(element, depth + 1, truncations, out var elementValue)
                    ? elementValue
                    : null);
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

            var values = new Dictionary<string, object>();
            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            var enterChildren = true;
            var supportedCount = 0;
            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, end))
            {
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

                supportedCount++;
                if (TryReadValue(iterator, depth + 1, truncations, out var childValue))
                {
                    var key = iterator.name ?? string.Empty;
                    if (key.Length > 256)
                    {
                        truncations.Add(Truncation(
                            iterator,
                            "propertyNameLength",
                            256,
                            key.Length));
                        key = key.Substring(0, 256);
                    }
                    values[key] = childValue;
                }
            }

            value = values;
            return true;
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
