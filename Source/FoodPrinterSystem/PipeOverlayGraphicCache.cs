using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public enum PipeOverlayStyle
    {
        Built,
        Selected,
        Ghost
    }

    [StaticConstructorOnStartup]
    public static class PipeOverlayGraphicCache
    {
        private static readonly Dictionary<PipeOverlayGraphicKey, Material> OverlayMaterials = new Dictionary<PipeOverlayGraphicKey, Material>();

        public static Material GetOverlayMaterial(PipeGraphicKey baseGraphicKey, PipeOverlayStyle style, bool buildingCell)
        {
            return GetOverlayMaterial(baseGraphicKey, ResolveTint(style, buildingCell));
        }

        public static Material GetOverlayMaterial(PipeGraphicKey baseGraphicKey, Color tint)
        {
            if (!baseGraphicKey.IsValid)
            {
                return null;
            }

            PipeOverlayGraphicKey key = new PipeOverlayGraphicKey(baseGraphicKey, tint);
            if (!OverlayMaterials.TryGetValue(key, out Material material) || material == null)
            {
                material = BuildOverlayMaterial(key, baseGraphicKey);
                OverlayMaterials[key] = material;
            }

            return material;
        }

        private static Material BuildOverlayMaterial(PipeOverlayGraphicKey key, PipeGraphicKey baseGraphicKey)
        {
            Material baseMaterial = PipeGraphicCache.GetMaterial(baseGraphicKey);
            if (baseMaterial == null)
            {
                return null;
            }

            Material material = new Material(baseMaterial)
            {
                shader = ShaderDatabase.MetaOverlay,
                color = (Color)key.Tint,
                name = key.AtlasPath + "_PipeOverlay"
            };
            return material;
        }

        private static Color ResolveTint(PipeOverlayStyle style, bool buildingCell)
        {
            return buildingCell ? GetBuildingTint(style) : GetPipeTint(style);
        }

        private static Color GetPipeTint(PipeOverlayStyle style)
        {
            switch (style)
            {
                case PipeOverlayStyle.Selected:
                    return new Color(0.32f, 0.94f, 0.88f, 0.92f);
                case PipeOverlayStyle.Ghost:
                    return new Color(0.40f, 0.92f, 0.98f, 0.70f);
                default:
                    return new Color(0.36f, 0.78f, 0.72f, 0.64f);
            }
        }

        private static Color GetBuildingTint(PipeOverlayStyle style)
        {
            switch (style)
            {
                case PipeOverlayStyle.Selected:
                    return new Color(0.38f, 0.96f, 0.90f, 0.62f);
                case PipeOverlayStyle.Ghost:
                    return new Color(0.46f, 0.96f, 1.00f, 0.48f);
                default:
                    return new Color(0.42f, 0.82f, 0.76f, 0.42f);
            }
        }
    }
}
