using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    internal struct PipeAtlasMaterialKey : IEquatable<PipeAtlasMaterialKey>
    {
        public PipeAtlasMaterialKey(string atlasPath, Shader shader, Color32 color)
        {
            AtlasPath = atlasPath;
            Shader = shader;
            Color = color;
        }

        public string AtlasPath { get; private set; }
        public Shader Shader { get; private set; }
        public Color32 Color { get; private set; }

        private string ShaderName
        {
            get { return Shader == null ? string.Empty : Shader.name; }
        }

        public bool Equals(PipeAtlasMaterialKey other)
        {
            return string.Equals(AtlasPath, other.AtlasPath, StringComparison.Ordinal)
                && string.Equals(ShaderName, other.ShaderName, StringComparison.Ordinal)
                && Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            return obj is PipeAtlasMaterialKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AtlasPath == null ? 0 : StringComparer.Ordinal.GetHashCode(AtlasPath);
                hash = (hash * 397) ^ (ShaderName == null ? 0 : StringComparer.Ordinal.GetHashCode(ShaderName));
                hash = (hash * 397) ^ Color.GetHashCode();
                return hash;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class PipeGraphicCache
    {
        private static readonly Dictionary<PipeAtlasMaterialKey, Material> MaterialCache = new Dictionary<PipeAtlasMaterialKey, Material>();

        public static Material GetMaterial(PipeGraphicKey key)
        {
            if (!key.IsValid)
            {
                return null;
            }

            PipeAtlasMaterialKey materialKey = new PipeAtlasMaterialKey(key.AtlasPath, key.Shader, key.Color);
            if (!MaterialCache.TryGetValue(materialKey, out Material material) || material == null)
            {
                material = MaterialPool.MatFrom(key.AtlasPath, key.Shader, (Color)key.Color);
                MaterialCache[materialKey] = material;
            }

            return material;
        }
    }
}
