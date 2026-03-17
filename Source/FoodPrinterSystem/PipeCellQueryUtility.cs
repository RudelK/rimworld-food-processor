using FoodPrinterSystem;
using Verse;

namespace FoodSystemPipe
{
    public static class PipeCellQueryUtility
    {
        private const string HiddenPipeDefName = "FPS_HiddenPipe";

        private static readonly IntVec3[] CardinalDirections =
        {
            IntVec3.North,
            IntVec3.East,
            IntVec3.South,
            IntVec3.West
        };

        private static readonly PipeDirectionMask[] CardinalMasks =
        {
            PipeDirectionMask.North,
            PipeDirectionMask.East,
            PipeDirectionMask.South,
            PipeDirectionMask.West
        };

        public static int CardinalDirectionCount
        {
            get { return CardinalDirections.Length; }
        }

        public static IntVec3 GetAdjacentCell(IntVec3 cell, int directionIndex)
        {
            return cell + CardinalDirections[directionIndex];
        }

        public static PipeDirectionMask BuildMaskForConnections(IntVec3 cell, System.Func<IntVec3, bool> hasConnectionToCell)
        {
            PipeDirectionMask mask = PipeDirectionMask.None;
            if (hasConnectionToCell == null)
            {
                return mask;
            }

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                if (hasConnectionToCell(cell + CardinalDirections[i]))
                {
                    mask |= CardinalMasks[i];
                }
            }

            return mask;
        }

        public static bool IsStandalonePipeThing(Thing thing)
        {
            CompPipe pipeComp = thing == null ? null : thing.TryGetComp<CompPipe>();
            return thing is Building_Pipe && pipeComp != null && pipeComp.TransmitsResource;
        }

        public static bool TryGetStandalonePipeAt(Map map, IntVec3 cell, out Thing pipeThing)
        {
            pipeThing = null;
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!IsStandalonePipeThing(thing))
                {
                    continue;
                }

                pipeThing = thing;
                return true;
            }

            return false;
        }

        public static bool HasStandalonePipeAt(Map map, IntVec3 cell)
        {
            return TryGetStandalonePipeAt(map, cell, out _);
        }

        public static bool TryGetPreferredPipeThingAt(Map map, IntVec3 cell, out Thing pipeThing)
        {
            pipeThing = null;
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            Thing hiddenPipe = null;
            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!IsStandalonePipeThing(thing))
                {
                    continue;
                }

                if (thing.def != null && thing.def.defName != HiddenPipeDefName)
                {
                    pipeThing = thing;
                    return true;
                }

                if (hiddenPipe == null)
                {
                    hiddenPipe = thing;
                }
            }

            pipeThing = hiddenPipe;
            return pipeThing != null;
        }

        public static bool TryGetPipeNodeThingAt(Map map, IntVec3 cell, Thing ownerThing, out Thing nodeThing, out IEmbeddedPipeNode node)
        {
            return PipeVisualNodeResolver.TryGetNodeAt(map, cell, ownerThing, out nodeThing, out node);
        }

        public static bool HasPipeNodeAt(Map map, IntVec3 cell, Thing ownerThing = null)
        {
            return PipeVisualNodeResolver.TryGetNodeAt(map, cell, ownerThing, out _);
        }
    }
}
