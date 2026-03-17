using System.Collections.Generic;
using FoodPrinterSystem;
using Verse;

namespace FoodSystemPipe
{
    public static class PipeOverlaySelectionUtility
    {
        public static PipeOverlayState BuildMapWideState(Map map)
        {
            if (map?.listerThings?.AllThings == null)
            {
                return PipeOverlayState.Empty(null);
            }

            HashSet<IntVec3> pipeCells = new HashSet<IntVec3>();
            HashSet<IntVec3> buildingCells = new HashSet<IntVec3>();
            Thing overlaySourceThing = null;

            List<Thing> allThings = map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (thing == null || !thing.Spawned || thing.Map != map)
                {
                    continue;
                }

                if (PipeCellQueryUtility.IsStandalonePipeThing(thing))
                {
                    CollectStandalonePipeCells(map, thing, pipeCells, buildingCells);
                    ConsiderOverlaySourceThing(ref overlaySourceThing, thing);
                    continue;
                }

                if (PipeVisualNodeResolver.TryResolveNode(thing, out IEmbeddedPipeNode node))
                {
                    CollectBuildingNodeCells(map, node, pipeCells, buildingCells);
                }
            }

            return CreateState(map, GetMapWideOverlayKey(map), pipeCells, buildingCells, overlaySourceThing);
        }

        private static PipeOverlayState CreateState(Map map, int networkKey, HashSet<IntVec3> pipeCells, HashSet<IntVec3> buildingCells, Thing overlaySourceThing)
        {
            Dictionary<IntVec3, PipeDirectionMask> cellMasks = BuildCellMasks(pipeCells, buildingCells);
            ThingDef overlaySourceDef = overlaySourceThing == null ? PipeGraphicResolver.ResolveDefaultPipeDef() : overlaySourceThing.def;
            Dictionary<PipeDirectionMask, PipeGraphicKey> baseGraphicKeysByMask = CollectBaseGraphicKeys(map, pipeCells, cellMasks);

            return new PipeOverlayState(
                map,
                -1,
                networkKey,
                pipeCells,
                buildingCells,
                cellMasks,
                baseGraphicKeysByMask,
                overlaySourceThing,
                overlaySourceDef);
        }

        private static void CollectStandalonePipeCells(Map map, Thing thing, HashSet<IntVec3> pipeCells, HashSet<IntVec3> buildingCells)
        {
            foreach (IntVec3 cell in PipeVisualCellProvider.GetVisualCells(thing))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                pipeCells.Add(cell);
                buildingCells.Remove(cell);
            }
        }

        private static void CollectBuildingNodeCells(Map map, IEmbeddedPipeNode node, HashSet<IntVec3> pipeCells, HashSet<IntVec3> buildingCells)
        {
            foreach (IntVec3 cell in node.VisualPipeCells)
            {
                if (!cell.InBounds(map) || pipeCells.Contains(cell))
                {
                    continue;
                }

                buildingCells.Add(cell);
            }
        }

        private static Dictionary<IntVec3, PipeDirectionMask> BuildCellMasks(HashSet<IntVec3> pipeCells, HashSet<IntVec3> buildingCells)
        {
            HashSet<IntVec3> allCells = new HashSet<IntVec3>(pipeCells);
            allCells.UnionWith(buildingCells);

            Dictionary<IntVec3, PipeDirectionMask> cellMasks = new Dictionary<IntVec3, PipeDirectionMask>();
            foreach (IntVec3 cell in allCells)
            {
                cellMasks[cell] = PipeMaskUtility.BuildMask(allCells, cell);
            }

            return cellMasks;
        }

        private static Dictionary<PipeDirectionMask, PipeGraphicKey> CollectBaseGraphicKeys(Map map, HashSet<IntVec3> pipeCells, Dictionary<IntVec3, PipeDirectionMask> cellMasks)
        {
            Dictionary<PipeDirectionMask, PipeGraphicKey> baseGraphicKeysByMask = new Dictionary<PipeDirectionMask, PipeGraphicKey>();
            foreach (IntVec3 cell in pipeCells)
            {
                if (!cellMasks.TryGetValue(cell, out PipeDirectionMask mask) || baseGraphicKeysByMask.ContainsKey(mask))
                {
                    continue;
                }

                if (!PipeGraphicResolver.TryGetPreferredPipeThingAt(map, cell, out Thing pipeThing))
                {
                    continue;
                }

                if (PipeGraphicResolver.TryResolveGraphicKey(pipeThing, mask, out PipeGraphicKey graphicKey))
                {
                    baseGraphicKeysByMask[mask] = graphicKey;
                }
            }

            return baseGraphicKeysByMask;
        }

        private static void ConsiderOverlaySourceThing(ref Thing overlaySourceThing, Thing candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (overlaySourceThing == null || overlaySourceThing.def.defName == "FPS_HiddenPipe")
            {
                overlaySourceThing = candidate;
            }
        }

        private static int GetMapWideOverlayKey(Map map)
        {
            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(map);
            return networkComponent == null ? 0 : networkComponent.NetworkRevision;
        }
    }
}
