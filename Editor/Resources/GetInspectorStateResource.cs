using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;
using McpUnity.Utils;

namespace McpUnity.Resources
{
    /// <summary>
    /// Resource for retrieving the Unity Editor's current inspector focus:
    /// the selected GameObject and, when determinable, the component the
    /// user is looking at. Provides the unity://inspector_state resource
    /// the Unity Dashboard app polls for its inspector-focus highlight.
    /// </summary>
    public class GetInspectorStateResource : McpResourceBase
    {
        public GetInspectorStateResource()
        {
            Name = "get_inspector_state";
            Description = "Current Editor selection and inspected component (Unity Dashboard inspector focus)";
            Uri = "unity://inspector_state";
        }

        /// <summary>
        /// Fetch the current selection state from the Unity Editor
        /// </summary>
        /// <param name="parameters">Resource parameters as a JObject (not used)</param>
        /// <returns>A JObject with activeGameObject {instanceId, name, path} and focusedComponent</returns>
        public override JObject Fetch(JObject parameters)
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return new JObject
                {
                    ["success"] = true,
                    ["message"] = "No GameObject selected",
                    ["activeGameObject"] = null,
                    ["focusedComponent"] = null
                };
            }

            return new JObject
            {
                ["success"] = true,
                ["message"] = $"Selected GameObject: '{selected.name}'",
                ["activeGameObject"] = new JObject
                {
                    ["instanceId"] = UnityObjectId.GetObjectId(selected),
                    ["name"] = selected.name,
                    ["path"] = GetHierarchyPath(selected.transform)
                },
                ["focusedComponent"] = GetFocusedComponentName()
            };
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        /// <summary>
        /// Best-effort "component the user is looking at": the first expanded
        /// non-Transform component editor in the active inspector. Unity has no
        /// true focused-component API, so null is a legitimate answer.
        /// </summary>
        private static string GetFocusedComponentName()
        {
            ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
            if (tracker == null)
            {
                return null;
            }

            UnityEditor.Editor[] editors = tracker.activeEditors;
            for (int i = 0; i < editors.Length; i++)
            {
                UnityEditor.Editor editor = editors[i];
                if (editor == null || editor.target == null || !(editor.target is Component component) || component is Transform)
                {
                    continue;
                }

                if (tracker.GetVisible(i) == 1)
                {
                    return component.GetType().Name;
                }
            }

            return null;
        }
    }
}
