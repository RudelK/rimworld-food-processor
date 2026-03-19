using System.Collections.Generic;
using FoodPrinterSystem;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    [StaticConstructorOnStartup]
    public static class PipeOverlayDrawer
    {
        public static void DrawBuiltOverlay(Thing ownerThing, IEmbeddedPipeNode node)
        {
            if (ownerThing?.Map == null || node == null)
            {
                return;
            }

            HashSet<IntVec3> localCells = new HashSet<IntVec3>(node.VisualPipeCells);
            Thing overlaySourceThing = FindOverlaySourceThing(ownerThing);
            DrawLocalCells(ownerThing.Map, ownerThing, localCells, overlaySourceThing, PipeOverlayStyle.Built, !(ownerThing is Building_Pipe));
        }

        public static void DrawActiveOverlay(Map map)
        {
            if (map == null)
            {
                return;
            }

            PipeOverlayMapComponent component = PipeOverlayMapUtility.Get(map);
            if (component == null)
            {
                return;
            }

            if (!component.BeginActiveDrawFrame())
            {
                return;
            }

            PipeOverlayState state = component.GetVisibleState();
            if (state == null || state.IsEmpty)
            {
                return;
            }

            DrawNetwork(state, PipeOverlayStyle.Selected);
        }

        public static void DrawGhostOverlay(Map map, ThingDef def, IntVec3 center, Rot4 rotation)
        {
            if (map == null || def == null || !PipeVisualNodeResolver.HasVisualPipeSupport(def))
            {
                return;
            }

            bool isStandalonePipe = def.thingClass != null && typeof(Building_Pipe).IsAssignableFrom(def.thingClass);
            ThingDef overlaySourceDef = isStandalonePipe ? def : PipeGraphicResolver.ResolveDefaultPipeDef();
            HashSet<IntVec3> localCells = new HashSet<IntVec3>(PipeVisualCellProvider.GetVisualCells(def, center, rotation));
            foreach (IntVec3 cell in localCells)
            {
                PipeDirectionMask mask = PipeMaskUtility.BuildGhostMask(map, def, center, rotation, cell);
                if (!PipeGraphicResolver.TryResolveGraphicKey(overlaySourceDef, mask, out PipeGraphicKey baseGraphicKey))
                {
                    continue;
                }

                Material overlayMaterial = PipeOverlayGraphicCache.GetOverlayMaterial(baseGraphicKey, PipeOverlayStyle.Ghost, !isStandalonePipe);
                DrawCell(cell, baseGraphicKey, overlayMaterial, PipeOverlayStyle.Ghost, !isStandalonePipe);
            }
        }

        private static void DrawNetwork(PipeOverlayState state, PipeOverlayStyle style)
        {
            DrawCells(state, state.PipeCells, style, false);
            DrawCells(state, state.BuildingCells, style, true);
        }

        private static void DrawCells(PipeOverlayState state, IEnumerable<IntVec3> cells, PipeOverlayStyle style, bool buildingCell)
        {
            foreach (IntVec3 cell in cells)
            {
                if (!state.CellMasks.TryGetValue(cell, out PipeDirectionMask mask))
                {
                    continue;
                }

                PipeGraphicKey baseGraphicKey = PipeGraphicResolver.ResolveBestGraphicKey(state, cell, mask);
                Material overlayMaterial = PipeOverlayGraphicCache.GetOverlayMaterial(baseGraphicKey, style, buildingCell);
                DrawCell(cell, baseGraphicKey, overlayMaterial, style, buildingCell);
            }
        }

        private static void DrawLocalCells(Map map, Thing ownerThing, ISet<IntVec3> localCells, Thing overlaySourceThing, PipeOverlayStyle style, bool buildingCell)
        {
            ThingDef overlaySourceDef = overlaySourceThing == null ? PipeGraphicResolver.ResolveDefaultPipeDef() : overlaySourceThing.def;
            foreach (IntVec3 cell in localCells)
            {
                PipeDirectionMask mask = PipeMaskUtility.BuildMask(map, localCells, cell, ownerThing);
                PipeGraphicKey baseGraphicKey = PipeGraphicResolver.ResolveBestGraphicKey(map, overlaySourceThing, overlaySourceDef, cell, mask);
                Material overlayMaterial = PipeOverlayGraphicCache.GetOverlayMaterial(baseGraphicKey, style, buildingCell);
                DrawCell(cell, baseGraphicKey, overlayMaterial, style, buildingCell);
            }
        }

        private static Thing FindOverlaySourceThing(Thing ownerThing)
        {
            if (ownerThing is Building_Pipe)
            {
                return ownerThing;
            }

            CompPipe pipeComp = ownerThing == null ? null : ownerThing.TryGetComp<CompPipe>();
            if (pipeComp?.PipeNet == null)
            {
                return null;
            }

            Thing fallback = null;
            List<CompPipe> pipes = pipeComp.PipeNet.Pipes;
            for (int i = 0; i < pipes.Count; i++)
            {
                Thing thing = pipes[i]?.parent;
                if (!(thing is Building_Pipe))
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = thing;
                }

                if (thing.def.defName != "FPS_HiddenPipe")
                {
                    return thing;
                }
            }

            return fallback;
        }

        private static void DrawCell(IntVec3 cell, PipeGraphicKey baseGraphicKey, Material material, PipeOverlayStyle style, bool buildingCell)
        {
            if (material == null)
            {
                return;
            }

            float floorAltitude = Altitudes.AltitudeFor(AltitudeLayer.FloorEmplacement);
            float baseAltitude = floorAltitude + GetAltitudeOffset(style, buildingCell, false);
            float bridgeAltitude = floorAltitude + GetAltitudeOffset(style, buildingCell, true);
            PipeVisualRenderer.DrawDynamicCell(cell, baseGraphicKey.Mask, material, baseAltitude, bridgeAltitude);
        }

        private static float GetAltitudeOffset(PipeOverlayStyle style, bool buildingCell, bool bridgePiece)
        {
            float offset;
            switch (style)
            {
                case PipeOverlayStyle.Selected:
                    offset = buildingCell ? 0.0050f : 0.0048f;
                    break;
                case PipeOverlayStyle.Ghost:
                    offset = buildingCell ? 0.0054f : 0.0052f;
                    break;
                default:
                    offset = buildingCell ? 0.0046f : 0.0044f;
                    break;
            }

            return bridgePiece ? offset + 0.0008f : offset;
        }
    }
}
