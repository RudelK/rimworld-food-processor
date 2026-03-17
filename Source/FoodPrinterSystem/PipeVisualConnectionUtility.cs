using Verse;

namespace FoodSystemPipe
{
    public static class PipeVisualConnectionUtility
    {
        public static bool Connects(Map map, IntVec3 cell, IntVec3 adjacentCell)
        {
            return PipeCellQueryUtility.HasPipeNodeAt(map, adjacentCell);
        }
    }
}
