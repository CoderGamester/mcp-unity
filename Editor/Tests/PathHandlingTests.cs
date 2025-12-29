using System.IO;
using NUnit.Framework;
using McpUnity.Utils;
using UnityEngine;

namespace McpUnity.Tests
{
    /// <summary>
    /// Tests for path handling with spaces and special characters
    /// </summary>
    public class PathHandlingTests
    {
        private string _tempDir;
        private string _tempDirWithSpaces;

        [SetUp]
        public void SetUp()
        {
            // Create temp directories for testing
            _tempDir = Path.Combine(Path.GetTempPath(), "McpUnityTest");
            _tempDirWithSpaces = Path.Combine(Path.GetTempPath(), "MCP Unity Test With Spaces");

            // Clean up if exists from previous test
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
            if (Directory.Exists(_tempDirWithSpaces))
                Directory.Delete(_tempDirWithSpaces, true);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up temp directories
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
            if (Directory.Exists(_tempDirWithSpaces))
                Directory.Delete(_tempDirWithSpaces, true);
        }

        [Test]
        public void ValidateServerPath_WithValidPath_ReturnsTrue()
        {
            // Arrange
            Directory.CreateDirectory(_tempDir);
            File.WriteAllText(Path.Combine(_tempDir, "package.json"), "{}");

            // Act
            bool result = McpUtils.ValidateServerPath(_tempDir);

            // Assert
            Assert.IsTrue(result, "ValidateServerPath should return true for valid path with package.json");
        }

        [Test]
        public void ValidateServerPath_WithSpacesInPath_ReturnsTrue()
        {
            // Arrange
            Directory.CreateDirectory(_tempDirWithSpaces);
            File.WriteAllText(Path.Combine(_tempDirWithSpaces, "package.json"), "{}");

            // Act
            bool result = McpUtils.ValidateServerPath(_tempDirWithSpaces);

            // Assert
            Assert.IsTrue(result, "ValidateServerPath should return true for path with spaces");
        }

        [Test]
        public void ValidateServerPath_WithNonExistentPath_ReturnsFalse()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistentMcpUnityPath12345");

            // Act
            bool result = McpUtils.ValidateServerPath(nonExistentPath);

            // Assert
            Assert.IsFalse(result, "ValidateServerPath should return false for non-existent path");
        }

        [Test]
        public void ValidateServerPath_WithMissingPackageJson_ReturnsFalse()
        {
            // Arrange
            Directory.CreateDirectory(_tempDir);
            // Don't create package.json

            // Act
            bool result = McpUtils.ValidateServerPath(_tempDir);

            // Assert
            Assert.IsFalse(result, "ValidateServerPath should return false when package.json is missing");
        }

        [Test]
        public void ValidateServerPath_WithNullPath_ReturnsFalse()
        {
            // Act
            bool result = McpUtils.ValidateServerPath(null);

            // Assert
            Assert.IsFalse(result, "ValidateServerPath should return false for null path");
        }

        [Test]
        public void ValidateServerPath_WithEmptyPath_ReturnsFalse()
        {
            // Act
            bool result = McpUtils.ValidateServerPath("");

            // Assert
            Assert.IsFalse(result, "ValidateServerPath should return false for empty path");
        }

        [Test]
        public void EncodePathForFileUrl_WithSpaces_EncodesCorrectly()
        {
            // Arrange
            string pathWithSpaces = "/Users/John Doe/My Project/package.json";
            string expected = "/Users/John%20Doe/My%20Project/package.json";

            // Act
            string result = McpUtils.EncodePathForFileUrl(pathWithSpaces);

            // Assert
            Assert.AreEqual(expected, result, "Spaces should be encoded as %20");
        }

        [Test]
        public void EncodePathForFileUrl_WithoutSpaces_ReturnsUnchanged()
        {
            // Arrange
            string pathWithoutSpaces = "/Users/JohnDoe/MyProject/package.json";

            // Act
            string result = McpUtils.EncodePathForFileUrl(pathWithoutSpaces);

            // Assert
            Assert.AreEqual(pathWithoutSpaces, result, "Path without spaces should remain unchanged");
        }

        [Test]
        public void EncodePathForFileUrl_WithMultipleSpaces_EncodesAll()
        {
            // Arrange
            string pathWithMultipleSpaces = "C:/Users/John Doe/Game Projects/My Unity Game/Assets";
            string expected = "C:/Users/John%20Doe/Game%20Projects/My%20Unity%20Game/Assets";

            // Act
            string result = McpUtils.EncodePathForFileUrl(pathWithMultipleSpaces);

            // Assert
            Assert.AreEqual(expected, result, "All spaces should be encoded as %20");
        }
    }
}
