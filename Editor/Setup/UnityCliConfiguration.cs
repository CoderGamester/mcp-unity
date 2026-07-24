using System.Text;

namespace McpUnity.Extensions.Setup
{
    public static class UnityCliConfiguration
    {
        public static string CreateOfficial(string executablePath, string projectPath)
        {
            return "{\n  \"mcpServers\": {\n    \"unity\": {\n" +
                "      \"command\": \"" + Escape(executablePath) + "\",\n" +
                "      \"args\": [\"mcp\", \"--project-path\", \"" + Escape(projectPath) + "\"]\n" +
                "    }\n  }\n}";
        }

        public static string CreateCompanion(
            string packagePath,
            string projectPath,
            string executablePath,
            bool includeExplicitCliPath)
        {
            var serverPath = packagePath.TrimEnd('/', '\\') + "/Server~/build/index.js";
            var builder = new StringBuilder();
            builder.Append("{\n  \"mcpServers\": {\n    \"mcp-unity-companion\": {\n");
            builder.Append("      \"command\": \"node\",\n");
            builder.Append("      \"args\": [\"").Append(Escape(serverPath)).Append("\", \"--project-path\", \"")
                .Append(Escape(projectPath)).Append("\"]");
            if (includeExplicitCliPath)
            {
                builder.Append(",\n      \"env\": { \"UNITY_CLI_PATH\": \"")
                    .Append(Escape(executablePath)).Append("\" }");
            }

            builder.Append("\n    }\n  }\n}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            var builder = new StringBuilder();
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '\"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
