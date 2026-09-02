using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace McpUnity.Unity
{
    /// <summary>
    /// Owns the per-project secret used to authenticate native MCP bridge clients.
    /// The token intentionally lives under Library so it is neither shared nor committed.
    /// </summary>
    internal static class McpUnityAuthentication
    {
        internal const string Username = "mcp-unity";
        internal const string Realm = "MCP Unity";
        internal const int TokenLength = 64;

        private const string TokenDirectory = "McpUnity";
        private const string TokenFileName = "bridge-token";

        internal static string TokenPath
        {
            get
            {
                DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
                if (projectRoot == null)
                {
                    throw new InvalidOperationException("Could not resolve the Unity project root for MCP authentication.");
                }

                return Path.Combine(projectRoot.FullName, "Library", TokenDirectory, TokenFileName);
            }
        }

        internal static string GetOrCreateToken()
        {
            return GetOrCreateTokenAtPath(TokenPath);
        }

        internal static string RegenerateToken()
        {
            return RegenerateTokenAtPath(TokenPath);
        }

        internal static bool TryGetToken(out string token, out string error)
        {
            try
            {
                token = GetOrCreateToken();
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                token = null;
                error = ex.Message;
                return false;
            }
        }

        internal static string GetOrCreateTokenAtPath(string tokenPath)
        {
            if (File.Exists(tokenPath))
            {
                string existingToken = File.ReadAllText(tokenPath).Trim();
                if (!IsValidToken(existingToken))
                {
                    throw new InvalidDataException(
                        $"The MCP authentication token at '{tokenPath}' is malformed. Regenerate it from Tools > MCP Unity > Server Window.");
                }

                return existingToken;
            }

            return WriteNewToken(tokenPath);
        }

        internal static string RegenerateTokenAtPath(string tokenPath)
        {
            return WriteNewToken(tokenPath);
        }

        internal static bool IsValidToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length != TokenLength)
            {
                return false;
            }

            for (int i = 0; i < token.Length; i++)
            {
                char character = token[i];
                bool isHex = (character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static string WriteNewToken(string tokenPath)
        {
            string directory = Path.GetDirectoryName(tokenPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("The MCP authentication token path has no parent directory.");
            }

            Directory.CreateDirectory(directory);

            byte[] bytes = new byte[TokenLength / 2];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            string token = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
            string temporaryPath = tokenPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.WriteAllText(temporaryPath, token, new UTF8Encoding(false));
                if (File.Exists(tokenPath))
                {
                    File.Replace(temporaryPath, tokenPath, null);
                }
                else
                {
                    File.Move(temporaryPath, tokenPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            return token;
        }
    }
}
