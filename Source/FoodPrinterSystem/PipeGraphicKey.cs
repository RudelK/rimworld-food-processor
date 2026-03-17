using System;
using UnityEngine;

namespace FoodSystemPipe
{
    public struct PipeGraphicKey : IEquatable<PipeGraphicKey>
    {
        public PipeGraphicKey(string atlasPath, PipeDirectionMask mask, Shader shader, Color32 color)
        {
            AtlasPath = atlasPath;
            Mask = mask;
            Shader = shader;
            Color = color;
        }

        public string AtlasPath { get; private set; }
        public PipeDirectionMask Mask { get; private set; }
        public Shader Shader { get; private set; }
        public Color32 Color { get; private set; }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(AtlasPath) && Shader != null; }
        }

        public string ShaderName
        {
            get { return Shader == null ? string.Empty : Shader.name; }
        }

        public bool Equals(PipeGraphicKey other)
        {
            return string.Equals(AtlasPath, other.AtlasPath, StringComparison.Ordinal)
                && Mask == other.Mask
                && string.Equals(ShaderName, other.ShaderName, StringComparison.Ordinal)
                && Color.Equals(other.Color);
        }

        public override bool Equals(object obj)
        {
            return obj is PipeGraphicKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AtlasPath == null ? 0 : StringComparer.Ordinal.GetHashCode(AtlasPath);
                hash = (hash * 397) ^ (int)Mask;
                hash = (hash * 397) ^ (ShaderName == null ? 0 : StringComparer.Ordinal.GetHashCode(ShaderName));
                hash = (hash * 397) ^ Color.GetHashCode();
                return hash;
            }
        }
    }
}
