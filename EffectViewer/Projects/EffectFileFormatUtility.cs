using System;
using System.IO;

namespace EffectViewer.Projects
{
    public static class EffectFileFormatUtility
    {
        public static EffectFileFormat GetFormatFromPath(string path)
        {
            return string.Equals(Path.GetExtension(path), ".compiled", StringComparison.OrdinalIgnoreCase)
                ? EffectFileFormat.Compiled
                : EffectFileFormat.Source;
        }

        public static string GetSourceExtension(EffectAssetKind kind)
        {
            return kind switch
            {
                EffectAssetKind.Reanim => ".reanim",
                EffectAssetKind.Particle => ".xml",
                EffectAssetKind.Trail => ".trail",
                _ => string.Empty
            };
        }

        public static string GetCompiledSuffix(EffectAssetKind kind)
        {
            return kind switch
            {
                EffectAssetKind.Reanim => ".reanim.compiled",
                EffectAssetKind.Particle => ".xml.compiled",
                EffectAssetKind.Trail => ".trail.compiled",
                _ => ".compiled"
            };
        }

        public static string GetExportFileName(EffectAssetKind kind, string assetId, EffectFileFormat format)
        {
            string safeName = ProjectPathUtility.CreateSafeName(assetId, "effect");
            return format == EffectFileFormat.Compiled
                ? safeName + GetCompiledSuffix(kind)
                : safeName + GetSourceExtension(kind);
        }
    }
}
