using System.Collections.Generic;
using Verse;

namespace FoodSystemPipe
{
    public static class PipeVisualCellProvider
    {
        public static IEnumerable<IntVec3> GetVisualCells(Thing thing)
        {
            if (thing == null)
            {
                yield break;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(thing.Position, thing.Rotation, thing.def.size);
            foreach (IntVec3 cell in occupiedRect)
            {
                yield return cell;
            }
        }

        public static IEnumerable<IntVec3> GetVisualCells(ThingDef def, IntVec3 center, Rot4 rotation)
        {
            if (def == null)
            {
                yield break;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(center, rotation, def.size);
            foreach (IntVec3 cell in occupiedRect)
            {
                yield return cell;
            }
        }

        public static bool ContainsCell(Thing thing, IntVec3 targetCell)
        {
            return thing != null && GenAdj.OccupiedRect(thing.Position, thing.Rotation, thing.def.size).Contains(targetCell);
        }

        public static bool ContainsCell(ThingDef def, IntVec3 center, Rot4 rotation, IntVec3 targetCell)
        {
            return def != null && GenAdj.OccupiedRect(center, rotation, def.size).Contains(targetCell);
        }
    }
}
