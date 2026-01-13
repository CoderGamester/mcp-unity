using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using McpUnity.Tools;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tests
{
    /// <summary>
    /// Tests for Material Tools functionality
    /// </summary>
    public class MaterialToolsTests
    {
        private string _testMaterialPath;
        private string _testMaterialDir;

        [SetUp]
        public void SetUp()
        {
            // Create test directory
            _testMaterialDir = "Assets/TestMaterials";
            _testMaterialPath = Path.Combine(_testMaterialDir, "TestMaterial.mat");

            // Ensure test directory exists
            if (!AssetDatabase.IsValidFolder(_testMaterialDir))
            {
                AssetDatabase.CreateFolder("Assets", "TestMaterials");
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test materials
            if (File.Exists(_testMaterialPath))
            {
                AssetDatabase.DeleteAsset(_testMaterialPath);
            }

            // Clean up test directory
            if (AssetDatabase.IsValidFolder(_testMaterialDir))
            {
                AssetDatabase.DeleteAsset(_testMaterialDir);
            }

            AssetDatabase.Refresh();
        }

        #region MaterialToolUtils Tests

        [Test]
        public void FindShader_WithStandardShader_ReturnsShader()
        {
            // Act
            Shader shader = MaterialToolUtils.FindShader("Standard");

            // Assert
            Assert.IsNotNull(shader, "Standard shader should be found");
            Assert.AreEqual("Standard", shader.name);
        }

        [Test]
        public void FindShader_WithUnlitColor_ReturnsShader()
        {
            // Act
            Shader shader = MaterialToolUtils.FindShader("Unlit/Color");

            // Assert
            Assert.IsNotNull(shader, "Unlit/Color shader should be found");
        }

        [Test]
        public void FindShader_WithNonExistentShader_ReturnsNull()
        {
            // Act
            Shader shader = MaterialToolUtils.FindShader("NonExistent/Shader/12345");

            // Assert
            Assert.IsNull(shader, "Non-existent shader should return null");
        }

        [Test]
        public void LoadMaterial_WithNonExistentPath_ReturnsNull()
        {
            // Act
            Material material = MaterialToolUtils.LoadMaterial("Assets/NonExistent/Material.mat");

            // Assert
            Assert.IsNull(material, "Non-existent material should return null");
        }

        [Test]
        public void LoadMaterial_WithNullPath_ReturnsNull()
        {
            // Act
            Material material = MaterialToolUtils.LoadMaterial(null);

            // Assert
            Assert.IsNull(material, "Null path should return null");
        }

        [Test]
        public void LoadMaterial_WithEmptyPath_ReturnsNull()
        {
            // Act
            Material material = MaterialToolUtils.LoadMaterial("");

            // Assert
            Assert.IsNull(material, "Empty path should return null");
        }

        #endregion

        #region CreateMaterialTool Tests

        [Test]
        public void CreateMaterialTool_WithValidParameters_CreatesMaterial()
        {
            // Arrange
            CreateMaterialTool tool = new CreateMaterialTool();
            JObject parameters = new JObject
            {
                ["name"] = "TestMaterial",
                ["shader"] = "Standard",
                ["savePath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should succeed");
            Assert.IsTrue(File.Exists(_testMaterialPath), "Material file should be created");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(_testMaterialPath);
            Assert.IsNotNull(material, "Material asset should be loadable");
            Assert.AreEqual("TestMaterial", material.name);
            Assert.AreEqual("Standard", material.shader.name);
        }

        [Test]
        public void CreateMaterialTool_WithProperties_AppliesProperties()
        {
            // Arrange
            CreateMaterialTool tool = new CreateMaterialTool();
            JObject parameters = new JObject
            {
                ["name"] = "TestMaterialWithProps",
                ["shader"] = "Standard",
                ["savePath"] = _testMaterialPath,
                ["properties"] = new JObject
                {
                    ["_Color"] = new JObject { ["r"] = 1f, ["g"] = 0f, ["b"] = 0f, ["a"] = 1f },
                    ["_Metallic"] = 0.5f
                }
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should succeed");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(_testMaterialPath);
            Assert.IsNotNull(material);

            Color color = material.GetColor("_Color");
            Assert.AreEqual(1f, color.r, 0.01f, "Red should be 1");
            Assert.AreEqual(0f, color.g, 0.01f, "Green should be 0");

            float metallic = material.GetFloat("_Metallic");
            Assert.AreEqual(0.5f, metallic, 0.01f, "Metallic should be 0.5");
        }

        [Test]
        public void CreateMaterialTool_WithMissingName_ReturnsError()
        {
            // Arrange
            CreateMaterialTool tool = new CreateMaterialTool();
            JObject parameters = new JObject
            {
                ["shader"] = "Standard",
                ["savePath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void CreateMaterialTool_WithMissingSavePath_ReturnsError()
        {
            // Arrange
            CreateMaterialTool tool = new CreateMaterialTool();
            JObject parameters = new JObject
            {
                ["name"] = "TestMaterial",
                ["shader"] = "Standard"
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void CreateMaterialTool_WithInvalidShader_ReturnsError()
        {
            // Arrange
            CreateMaterialTool tool = new CreateMaterialTool();
            JObject parameters = new JObject
            {
                ["name"] = "TestMaterial",
                ["shader"] = "NonExistent/Shader/12345",
                ["savePath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("not_found_error", result["error"]["type"].ToString());
        }

        [Test]
        public void CreateMaterialTool_WithDefaultShader_UsesAutoDetectedShader()
        {
            // Arrange
            CreateMaterialTool tool = new CreateMaterialTool();
            JObject parameters = new JObject
            {
                ["name"] = "TestMaterial",
                ["savePath"] = _testMaterialPath
            };
            string expectedShader = MaterialToolUtils.GetDefaultShaderName();

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should succeed");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(_testMaterialPath);
            Assert.IsNotNull(material);
            Assert.AreEqual(expectedShader, material.shader.name, "Default shader should match auto-detected render pipeline shader");
        }

        #endregion

        #region GetMaterialInfoTool Tests

        [Test]
        public void GetMaterialInfoTool_WithValidMaterial_ReturnsInfo()
        {
            // Arrange - Create a material first
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            GetMaterialInfoTool tool = new GetMaterialInfoTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should succeed");
            Assert.AreEqual("TestMaterial", result["materialName"].ToString());
            Assert.AreEqual("Standard", result["shaderName"].ToString());
            Assert.IsNotNull(result["properties"], "Should have properties array");
            Assert.IsTrue(((JArray)result["properties"]).Count > 0, "Should have at least one property");
        }

        [Test]
        public void GetMaterialInfoTool_WithMissingMaterialPath_ReturnsError()
        {
            // Arrange
            GetMaterialInfoTool tool = new GetMaterialInfoTool();
            JObject parameters = new JObject();

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void GetMaterialInfoTool_WithNonExistentMaterial_ReturnsError()
        {
            // Arrange
            GetMaterialInfoTool tool = new GetMaterialInfoTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = "Assets/NonExistent/Material.mat"
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("not_found_error", result["error"]["type"].ToString());
        }

        [Test]
        public void GetMaterialInfoTool_ReturnsCorrectPropertyTypes()
        {
            // Arrange - Create a material first
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            GetMaterialInfoTool tool = new GetMaterialInfoTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>());
            JArray properties = (JArray)result["properties"];

            // Find _Color property
            JObject colorProp = null;
            foreach (JObject prop in properties)
            {
                if (prop["name"].ToString() == "_Color")
                {
                    colorProp = prop;
                    break;
                }
            }

            Assert.IsNotNull(colorProp, "_Color property should exist");
            Assert.AreEqual("Color", colorProp["type"].ToString());
            Assert.IsNotNull(colorProp["value"], "Color value should exist");
        }

        #endregion

        #region ModifyMaterialTool Tests

        [Test]
        public void ModifyMaterialTool_WithValidProperties_ModifiesMaterial()
        {
            // Arrange - Create a material first
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            testMat.SetColor("_Color", Color.white);
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            ModifyMaterialTool tool = new ModifyMaterialTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = _testMaterialPath,
                ["properties"] = new JObject
                {
                    ["_Color"] = new JObject { ["r"] = 0f, ["g"] = 1f, ["b"] = 0f, ["a"] = 1f }
                }
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should succeed");

            // Reload material to verify changes were saved
            Material modifiedMat = AssetDatabase.LoadAssetAtPath<Material>(_testMaterialPath);
            Color color = modifiedMat.GetColor("_Color");
            Assert.AreEqual(0f, color.r, 0.01f, "Red should be 0");
            Assert.AreEqual(1f, color.g, 0.01f, "Green should be 1");
        }

        [Test]
        public void ModifyMaterialTool_WithMissingMaterialPath_ReturnsError()
        {
            // Arrange
            ModifyMaterialTool tool = new ModifyMaterialTool();
            JObject parameters = new JObject
            {
                ["properties"] = new JObject { ["_Color"] = new JObject { ["r"] = 1f } }
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void ModifyMaterialTool_WithEmptyProperties_ReturnsError()
        {
            // Arrange
            ModifyMaterialTool tool = new ModifyMaterialTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = _testMaterialPath,
                ["properties"] = new JObject()
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void ModifyMaterialTool_WithUnknownProperty_ReportsUnknown()
        {
            // Arrange - Create a material first
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            ModifyMaterialTool tool = new ModifyMaterialTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = _testMaterialPath,
                ["properties"] = new JObject
                {
                    ["_Color"] = new JObject { ["r"] = 1f, ["g"] = 0f, ["b"] = 0f, ["a"] = 1f },
                    ["_NonExistentProperty"] = 0.5f
                }
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should still succeed");
            Assert.IsNotNull(result["unknownProperties"], "Should report unknown properties");
            JArray unknownProps = (JArray)result["unknownProperties"];
            Assert.IsTrue(unknownProps.Count > 0, "Should have at least one unknown property");
        }

        #endregion

        #region AssignMaterialTool Tests

        [Test]
        public void AssignMaterialTool_WithMissingGameObjectIdentifier_ReturnsError()
        {
            // Arrange
            AssignMaterialTool tool = new AssignMaterialTool();
            JObject parameters = new JObject
            {
                ["materialPath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void AssignMaterialTool_WithMissingMaterialPath_ReturnsError()
        {
            // Arrange
            AssignMaterialTool tool = new AssignMaterialTool();
            JObject parameters = new JObject
            {
                ["objectPath"] = "/TestCube"
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("validation_error", result["error"]["type"].ToString());
        }

        [Test]
        public void AssignMaterialTool_WithNonExistentGameObject_ReturnsError()
        {
            // Arrange - Create a material first
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            AssignMaterialTool tool = new AssignMaterialTool();
            JObject parameters = new JObject
            {
                ["objectPath"] = "/NonExistentGameObject12345",
                ["materialPath"] = _testMaterialPath
            };

            // Act
            JObject result = tool.Execute(parameters);

            // Assert
            Assert.IsNotNull(result["error"], "Should return error");
            Assert.AreEqual("not_found_error", result["error"]["type"].ToString());
        }

        [Test]
        public void AssignMaterialTool_WithNonExistentMaterial_ReturnsError()
        {
            // Arrange - Create a test GameObject
            GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testObj.name = "TestCube";

            try
            {
                AssignMaterialTool tool = new AssignMaterialTool();
                JObject parameters = new JObject
                {
                    ["instanceId"] = testObj.GetInstanceID(),
                    ["materialPath"] = "Assets/NonExistent/Material.mat"
                };

                // Act
                JObject result = tool.Execute(parameters);

                // Assert
                Assert.IsNotNull(result["error"], "Should return error");
                Assert.AreEqual("not_found_error", result["error"]["type"].ToString());
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(testObj);
            }
        }

        [Test]
        public void AssignMaterialTool_WithValidParameters_AssignsMaterial()
        {
            // Arrange - Create a material and a test GameObject
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            testMat.SetColor("_Color", Color.red);
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testObj.name = "TestCube";

            try
            {
                AssignMaterialTool tool = new AssignMaterialTool();
                JObject parameters = new JObject
                {
                    ["instanceId"] = testObj.GetInstanceID(),
                    ["materialPath"] = _testMaterialPath,
                    ["slot"] = 0
                };

                // Act
                JObject result = tool.Execute(parameters);

                // Assert
                Assert.IsTrue(result["success"].ToObject<bool>(), "Tool should succeed");

                Renderer renderer = testObj.GetComponent<Renderer>();
                Assert.AreEqual(testMat, renderer.sharedMaterial, "Material should be assigned");
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(testObj);
            }
        }

        [Test]
        public void AssignMaterialTool_WithInvalidSlot_ReturnsError()
        {
            // Arrange - Create a material and a test GameObject
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            GameObject testObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testObj.name = "TestCube";

            try
            {
                AssignMaterialTool tool = new AssignMaterialTool();
                JObject parameters = new JObject
                {
                    ["instanceId"] = testObj.GetInstanceID(),
                    ["materialPath"] = _testMaterialPath,
                    ["slot"] = 99 // Invalid slot for a cube with 1 material
                };

                // Act
                JObject result = tool.Execute(parameters);

                // Assert
                Assert.IsNotNull(result["error"], "Should return error");
                Assert.AreEqual("validation_error", result["error"]["type"].ToString());
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(testObj);
            }
        }

        [Test]
        public void AssignMaterialTool_WithGameObjectWithoutRenderer_ReturnsError()
        {
            // Arrange - Create a material and an empty test GameObject
            Material testMat = new Material(Shader.Find("Standard"));
            testMat.name = "TestMaterial";
            AssetDatabase.CreateAsset(testMat, _testMaterialPath);
            AssetDatabase.SaveAssets();

            GameObject testObj = new GameObject("TestEmpty"); // No renderer

            try
            {
                AssignMaterialTool tool = new AssignMaterialTool();
                JObject parameters = new JObject
                {
                    ["instanceId"] = testObj.GetInstanceID(),
                    ["materialPath"] = _testMaterialPath
                };

                // Act
                JObject result = tool.Execute(parameters);

                // Assert
                Assert.IsNotNull(result["error"], "Should return error");
                Assert.AreEqual("component_error", result["error"]["type"].ToString());
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(testObj);
            }
        }

        #endregion
    }
}
