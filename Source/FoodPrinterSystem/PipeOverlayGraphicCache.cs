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
            if (!baseGraphicKey.IsValid)
            {
                return null;
            }

            PipeOverlayGraphicKey key = new PipeOverlayGraphicKey(baseGraphicKey, style, buildingCell);
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
                color = key.BuildingCell ? GetBuildingTint(key.Style) : GetPipeTint(key.Style),
                name = key.AtlasPath + "_PipeOverlay"
            };
            return material;
        }

        private static Color GetPipeTint(PipeOverlayStyle style)
        {
            switch (style)
            {
                case PipeOverlayStyle.Selected:
                    return new Color(1f, 0.86f, 0.32f, 0.92f);
                case PipeOverlayStyle.Ghost:
                    return new Color(0.40f, 0.96f, 0.78f, 0.70f);
                default:
                    return new Color(0.50f, 0.78f, 0.62f, 0.64f);
            }
        }

        private static Color GetBuildingTint(PipeOverlayStyle style)
        {
            switch (style)
            {
                case PipeOverlayStyle.Selected:
                    return new Color(1f, 0.88f, 0.36f, 0.62f);
                case PipeOverlayStyle.Ghost:
                    return new Color(0.42f, 1f, 0.84f, 0.48f);
                default:
                    return new Color(0.56f, 0.84f, 0.68f, 0.42f);
            }
        }
    }
}
