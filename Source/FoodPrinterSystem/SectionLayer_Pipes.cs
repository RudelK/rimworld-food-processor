using RimWorld;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public class SectionLayer_Pipes : SectionLayer
    {
        private const float BasePipeAltitudeOffset = 0.003f;
        private const float BridgePipeAltitudeOffset = 0.0038f;

        public SectionLayer_Pipes(Section section) : base(section)
        {
            relevantChangeTypes = MapMeshFlagDefOf.PowerGrid;
        }

        public override void Regenerate()
        {
            ClearSubMeshes(MeshParts.All);

            Map map = Map;
            float floorAltitude = Altitudes.AltitudeFor(AltitudeLayer.FloorEmplacement);
            float baseAltitude = floorAltitude + BasePipeAltitudeOffset;
            float bridgeAltitude = floorAltitude + BridgePipeAltitudeOffset;

            foreach (IntVec3 cell in section.CellRect)
            {
                if (!PipeCellQueryUtility.TryGetStandalonePipeAt(map, cell, out Thing pipeThing))
                {
                    continue;
                }

                PipeDirectionMask mask = PipeMaskUtility.BuildPipeMask(map, cell);
                if (!PipeGraphicResolver.TryResolveGraphicKey(pipeThing, mask, out PipeGraphicKey graphicKey))
                {
                    continue;
                }

                Material material = PipeGraphicCache.GetMaterial(graphicKey);
                if (material == null)
                {
                    continue;
                }

                PipeVisualRenderer.PrintSectionCell(this, cell, mask, material, baseAltitude, bridgeAltitude);
            }

            FinalizeMesh(MeshParts.All);
        }
    }
}
