using System.IO;
using System.Text;

namespace EffectViewer.Projects
{
    internal static class ProjectPathUtility
    {
        public static string ResolvePath(EffectProject project, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path) || project is null || string.IsNullOrWhiteSpace(project.RootPath)
                ? path
                : Path.Combine(project.RootPath, path);
        }

        public static string ToProjectRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }

        public static string CreateSafeName(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            StringBuilder builder = new();
            bool lastWasSeparator = false;
            foreach (char c in value.Trim())
            {
                char normalized = c;
                if (c >= 'A' && c <= 'Z')
                {
                    normalized = (char)(c + ('a' - 'A'));
                }

                if ((normalized >= 'a' && normalized <= 'z') ||
                    (normalized >= '0' && normalized <= '9') ||
                    normalized == '-' ||
                    normalized == '.')
                {
                    builder.Append(normalized);
                    lastWasSeparator = false;
                    continue;
                }

                if (normalized == '_')
                {
                    builder.Append(normalized);
                    lastWasSeparator = false;
                    continue;
                }

                if (!lastWasSeparator)
                {
                    builder.Append('_');
                    lastWasSeparator = true;
                }
            }

            string safe = builder.ToString().Trim('.', '_', '-');
            return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
        }
    }
}
