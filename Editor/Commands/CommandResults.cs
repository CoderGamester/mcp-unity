using System.Collections.Generic;
using System.Runtime.Serialization;
using Unity.Pipeline.Models;

namespace McpUnity.Extensions.Commands
{
    [DataContract]
    public sealed class InspectGameObjectResult
    {
        [DataMember(Name = "root")]
        public GameObjectInspection Root { get; set; }

        [DataMember(Name = "maxDepth")]
        public int MaxDepth { get; set; }

        [DataMember(Name = "maxNodes")]
        public int MaxNodes { get; set; }

        [DataMember(Name = "maxPropertiesPerComponent")]
        public int MaxPropertiesPerComponent { get; set; }

        [DataMember(Name = "nodesReturned")]
        public int NodesReturned { get; set; }

        [DataMember(Name = "nodeLimitReached")]
        public bool NodeLimitReached { get; set; }
    }

    [DataContract]
    public sealed class GameObjectInspection
    {
        [DataMember(Name = "identity")]
        public AuthoringResult Identity { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "scenePath")]
        public string ScenePath { get; set; }

        [DataMember(Name = "activeSelf")]
        public bool ActiveSelf { get; set; }

        [DataMember(Name = "activeInHierarchy")]
        public bool ActiveInHierarchy { get; set; }

        [DataMember(Name = "layer")]
        public int Layer { get; set; }

        [DataMember(Name = "layerName")]
        public string LayerName { get; set; }

        [DataMember(Name = "tag")]
        public string Tag { get; set; }

        [DataMember(Name = "isStatic")]
        public bool IsStatic { get; set; }

        [DataMember(Name = "transform")]
        public TransformInspection Transform { get; set; }

        [DataMember(Name = "childCount")]
        public int ChildCount { get; set; }

        [DataMember(Name = "children")]
        public List<GameObjectInspection> Children { get; set; } = new List<GameObjectInspection>();

        [DataMember(Name = "childrenTruncated")]
        public bool ChildrenTruncated { get; set; }

        [DataMember(Name = "componentsIncluded")]
        public bool ComponentsIncluded { get; set; }

        [DataMember(Name = "componentCount")]
        public int ComponentCount { get; set; }

        [DataMember(Name = "components")]
        public List<ComponentInspection> Components { get; set; } = new List<ComponentInspection>();
    }

    [DataContract]
    public sealed class TransformInspection
    {
        [DataMember(Name = "localPosition")]
        public Vector3Inspection LocalPosition { get; set; }

        [DataMember(Name = "localEulerAngles")]
        public Vector3Inspection LocalEulerAngles { get; set; }

        [DataMember(Name = "localScale")]
        public Vector3Inspection LocalScale { get; set; }

        [DataMember(Name = "worldPosition")]
        public Vector3Inspection WorldPosition { get; set; }

        [DataMember(Name = "worldEulerAngles")]
        public Vector3Inspection WorldEulerAngles { get; set; }
    }

    [DataContract]
    public sealed class Vector3Inspection
    {
        [DataMember(Name = "x")]
        public float X { get; set; }

        [DataMember(Name = "y")]
        public float Y { get; set; }

        [DataMember(Name = "z")]
        public float Z { get; set; }
    }

    [DataContract]
    public sealed class ComponentInspection
    {
        [DataMember(Name = "identity")]
        public AuthoringResult Identity { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "missing")]
        public bool Missing { get; set; }

        [DataMember(Name = "enabled")]
        public bool? Enabled { get; set; }

        [DataMember(Name = "propertiesIncluded")]
        public bool PropertiesIncluded { get; set; }

        [DataMember(Name = "serializedPropertyCount")]
        public int SerializedPropertyCount { get; set; }

        [DataMember(Name = "properties")]
        public List<SerializedPropertyInspection> Properties { get; set; } =
            new List<SerializedPropertyInspection>();

        [DataMember(Name = "propertiesTruncated")]
        public bool PropertiesTruncated { get; set; }

        [DataMember(Name = "propertiesError")]
        public string PropertiesError { get; set; }
    }

    [DataContract]
    public sealed class SerializedPropertyInspection
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "value")]
        public object Value { get; set; }

        [DataMember(Name = "valueTruncated")]
        public bool ValueTruncated { get; set; }

        [DataMember(Name = "valueTruncations")]
        public List<SerializationTruncationInspection> ValueTruncations { get; set; } =
            new List<SerializationTruncationInspection>();
    }

    [DataContract]
    public sealed class SerializationTruncationInspection
    {
        [DataMember(Name = "path")]
        public string Path { get; set; }

        [DataMember(Name = "reason")]
        public string Reason { get; set; }

        [DataMember(Name = "limit")]
        public int Limit { get; set; }

        [DataMember(Name = "originalCount")]
        public int? OriginalCount { get; set; }
    }

    [DataContract]
    public sealed class UnloadSceneResult
    {
        [DataMember(Name = "unloadedPath")]
        public string UnloadedPath { get; set; }

        [DataMember(Name = "activeSceneName")]
        public string ActiveSceneName { get; set; }

        [DataMember(Name = "activeScenePath")]
        public string ActiveScenePath { get; set; }
    }

    [DataContract]
    public sealed class EditorStepResult
    {
        [DataMember(Name = "isPlaying")]
        public bool IsPlaying { get; set; }

        [DataMember(Name = "isPaused")]
        public bool IsPaused { get; set; }
    }

    [DataContract]
    public sealed class AssignMaterialResult
    {
        [DataMember(Name = "gameObject")]
        public AuthoringResult GameObject { get; set; }

        [DataMember(Name = "material")]
        public AuthoringResult Material { get; set; }

        [DataMember(Name = "slot")]
        public int Slot { get; set; }
    }
}
