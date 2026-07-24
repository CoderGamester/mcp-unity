using System.Collections.Generic;
using Unity.Pipeline.Models;

namespace McpUnity.Extensions.Commands
{
    public sealed class InspectGameObjectResult
    {
        public GameObjectInspection Root { get; set; }
        public int MaxDepth { get; set; }
        public int MaxNodes { get; set; }
        public int MaxPropertiesPerComponent { get; set; }
        public int NodesReturned { get; set; }
        public bool NodeLimitReached { get; set; }
    }

    public sealed class GameObjectInspection
    {
        public AuthoringResult Identity { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string ScenePath { get; set; }
        public bool ActiveSelf { get; set; }
        public bool ActiveInHierarchy { get; set; }
        public int Layer { get; set; }
        public string LayerName { get; set; }
        public string Tag { get; set; }
        public bool IsStatic { get; set; }
        public TransformInspection Transform { get; set; }
        public int ChildCount { get; set; }
        public List<GameObjectInspection> Children { get; set; } = new List<GameObjectInspection>();
        public bool ChildrenTruncated { get; set; }
        public bool ComponentsIncluded { get; set; }
        public int ComponentCount { get; set; }
        public List<ComponentInspection> Components { get; set; } = new List<ComponentInspection>();
    }

    public sealed class TransformInspection
    {
        public Vector3Inspection LocalPosition { get; set; }
        public Vector3Inspection LocalEulerAngles { get; set; }
        public Vector3Inspection LocalScale { get; set; }
        public Vector3Inspection WorldPosition { get; set; }
        public Vector3Inspection WorldEulerAngles { get; set; }
    }

    public sealed class Vector3Inspection
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class ComponentInspection
    {
        public AuthoringResult Identity { get; set; }
        public string Type { get; set; }
        public bool Missing { get; set; }
        public bool? Enabled { get; set; }
        public bool PropertiesIncluded { get; set; }
        public int SerializedPropertyCount { get; set; }
        public List<SerializedPropertyInspection> Properties { get; set; } =
            new List<SerializedPropertyInspection>();
        public bool PropertiesTruncated { get; set; }
        public string PropertiesError { get; set; }
    }

    public sealed class SerializedPropertyInspection
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public object Value { get; set; }
    }

    public sealed class UnloadSceneResult
    {
        public string UnloadedPath { get; set; }
        public string ActiveSceneName { get; set; }
        public string ActiveScenePath { get; set; }
    }

    public sealed class EditorStepResult
    {
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
    }

    public sealed class AssignMaterialResult
    {
        public AuthoringResult GameObject { get; set; }
        public AuthoringResult Material { get; set; }
        public int Slot { get; set; }
    }
}
