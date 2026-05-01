using System;
using System.Collections.Generic;
using System.IO;
using EffectViewer.Projects;
using EffectViewer.TodLib.Common;
using EffectViewer.TodLib.Graphics;

namespace EffectViewer.Runtime
{
    public sealed class ProjectResourceProvider : IResourceProvider
    {
        private readonly EffectProject _project;
        private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);

        public ProjectResourceProvider(EffectProject project)
        {
            _project = project;
        }

        public Image GetImage(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (_images.TryGetValue(id, out Image cached))
            {
                return cached;
            }

            if (!_project.Assets.TryGetImage(id, out Assets.ImageAsset asset))
            {
                return null;
            }

            Image image = new()
            {
                mId = asset.Id,
                mNumRows = Math.Max(1, asset.Rows),
                mNumCols = Math.Max(1, asset.Cols)
            };

            string fullPath = ProjectPathUtility.ResolvePath(_project, asset.Path);

            if (ImageFileSizeReader.TryReadSize(fullPath, out int width, out int height))
            {
                image.mWidth = width;
                image.mHeight = height;
            }

            if (File.Exists(fullPath))
            {
                image.mPlatformImage = fullPath;
            }

            _images[id] = image;
            return image;
        }

        public Font GetFont(string id)
        {
            return null;
        }

    }
}
