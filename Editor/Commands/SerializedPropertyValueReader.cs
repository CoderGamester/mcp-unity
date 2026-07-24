using System.Collections.Generic;
using Unity.Pipeline.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace McpUnity.Extensions.Commands
{
    internal static class SerializedPropertyValueReader
    {
        public static bool TryRead(SerializedProperty property, out object value)
        {
            value = null;
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
                    value = property.stringValue;
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
                    value = referenced == null ? null : ObjectResolver.Describe(referenced);
                    return true;
                default:
                    return false;
            }
        }

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
}
