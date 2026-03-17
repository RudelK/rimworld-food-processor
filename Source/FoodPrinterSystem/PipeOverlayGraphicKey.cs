using System;
using UnityEngine;

namespace FoodSystemPipe
{
    public struct PipeOverlayGraphicKey : IEquatable<PipeOverlayGraphicKey>
    {
        public PipeOverlayGraphicKey(PipeGraphicKey baseGraphicKey, PipeOverlayStyle style, bool buildingCell)
        {
            AtlasPath = baseGraphicKey.AtlasPath;
            Shader = baseGraphicKey.Shader;
            Color = baseGraphicKey.Color;
            Style = style;
            BuildingCell = buildingCell;
        }

        public string AtlasPath { get; private set; }
        public Shader Shader { get; private set; }
        public Color32 Color { get; private set; }
        public PipeOverlayStyle Style { get; private set; }
        public bool BuildingCell { get; private set; }

        private string ShaderName
        {
            get { return Shader == null ? string.Empty : Shader.name; }
        }

        public bool Equals(PipeOverlayGraphicKey other)
        {
            return string.Equals(AtlasPath, other.AtlasPath, StringComparison.Ordinal)
                && string.Equals(ShaderName, other.ShaderName, StringComparison.Ordinal)
                && Color.Equals(other.Color)
                && Style == other.Style
                && BuildingCell == other.BuildingCell;
        }

        public override bool Equals(object obj)
        {
            return obj is PipeOverlayGraphicKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AtlasPath == null ? 0 : StringComparer.Ordinal.GetHashCode(AtlasPath);
                hash = (hash * 397) ^ (ShaderName == null ? 0 : StringComparer.Ordinal.GetHashCode(ShaderName));
                hash = (hash * 397) ^ Color.GetHashCode();
                hash = (hash * 397) ^ (int)Style;
                hash = (hash * 397) ^ (BuildingCell ? 1 : 0);
                return hash;
            }
        }
    }
}
