using System.Collections.Generic;
using Verse;

namespace FoodSystemPipe
{
    [System.Flags]
    public enum PipeDirectionMask
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8
    }

    public static class PipeMaskUtility
    {
        public static PipeDirectionMask BuildPipeMask(Map map, IntVec3 cell)
        {
            return PipeCellQueryUtility.BuildMaskForConnections(cell, adjacentCell => PipeVisualConnectionUtility.Connects(map, cell, adjacentCell));
        }

        public static PipeDirectionMask BuildMask(Map map, IntVec3 cell, Thing ownerThing)
        {
            PipeVisualMapComponent component = PipeVisualMapUtility.Get(map);
            if (component != null)
            {
                return component.GetOrBuildMask(ownerThing, cell, () => BuildMaskUncached(map, null, cell, ownerThing));
            }

            return BuildMaskUncached(map, null, cell, ownerThing);
        }

        public static PipeDirectionMask BuildMask(ISet<IntVec3> connectedCells, IntVec3 cell)
        {
            return BuildMaskUncached(null, connectedCells, cell, null);
        }

        public static PipeDirectionMask BuildMask(Map map, ISet<IntVec3> localCells, IntVec3 cell, Thing ownerThing)
        {
            return BuildMaskUncached(map, localCells, cell, ownerThing);
        }

        public static PipeDirectionMask BuildGhostMask(Map map, ThingDef def, IntVec3 center, Rot4 rotation, IntVec3 cell)
        {
            if (!PipeVisualNodeResolver.HasVisualPipeSupport(def))
            {
                return PipeDirectionMask.None;
            }

            HashSet<IntVec3> localCells = new HashSet<IntVec3>(PipeVisualCellProvider.GetVisualCells(def, center, rotation));
            return BuildMaskUncached(map, localCells, cell, null);
        }

        private static PipeDirectionMask BuildMaskUncached(Map map, ISet<IntVec3> localCells, IntVec3 cell, Thing ownerThing)
        {
            return PipeCellQueryUtility.BuildMaskForConnections(cell, adjacentCell => HasConnection(map, localCells, adjacentCell, ownerThing));
        }

        private static bool HasConnection(Map map, ISet<IntVec3> localCells, IntVec3 adjacentCell, Thing ownerThing)
        {
            if (localCells != null && localCells.Contains(adjacentCell))
            {
                return true;
            }

            return map != null && PipeCellQueryUtility.HasPipeNodeAt(map, adjacentCell, ownerThing);
        }
    }
}
