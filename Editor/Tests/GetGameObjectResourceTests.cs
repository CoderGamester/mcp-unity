using NUnit.Framework;
using McpUnity.Resources;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace McpUnity.Tests
{
    /// <summary>
    /// Regression tests for GameObject serialization safety.
    /// </summary>
    public class GetGameObjectResourceTests
    {
        private GameObject _testObject;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("GetGameObjectResourceTests_Object");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
                _testObject = null;
            }
        }

        [Test]
        public void GameObjectToJObject_WithCollider_SkipsDetailedPropertiesForSafety()
        {
            // Arrange
            _testObject.AddComponent<BoxCollider>();

            // Act
            JObject result = GetGameObjectResource.GameObjectToJObject(_testObject, true);

            // Assert
            Assert.IsNotNull(result);

            JArray components = (JArray)result["components"];
            Assert.IsNotNull(components);

            JObject colliderJson = null;
            foreach (JToken component in components)
            {
                if (component?["type"]?.ToString() == nameof(BoxCollider))
                {
                    colliderJson = (JObject)component;
                    break;
                }
            }

            Assert.IsNotNull(colliderJson, "Expected serialized component list to include BoxCollider.");
            Assert.AreEqual(true, colliderJson["enabled"]?.ToObject<bool>());
            Assert.AreEqual(
                "Detailed property serialization skipped for safety",
                colliderJson["properties"]?["_skipped"]?.ToString());
        }
    }
}
