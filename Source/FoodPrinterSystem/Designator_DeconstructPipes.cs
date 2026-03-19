using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class Designator_DeconstructPipes : Designator_Deconstruct
    {
        public Designator_DeconstructPipes()
        {
            defaultLabel = "FPS_DeconstructPipes".Translate();
            defaultDesc = "FPS_DeconstructPipesDesc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Deconstruct", true);
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Map))
            {
                return false;
            }

            var things = loc.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!IsPipeThing(thing))
                {
                    continue;
                }

                AcceptanceReport report = base.CanDesignateThing(thing);
                if (report.Accepted)
                {
                    return true;
                }
            }

            return false;
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (!IsPipeThing(t))
            {
                return false;
            }

            return base.CanDesignateThing(t);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (!c.InBounds(Map))
            {
                return;
            }

            var things = c.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!IsPipeThing(thing))
                {
                    continue;
                }

                if (base.CanDesignateThing(thing).Accepted)
                {
                    base.DesignateThing(thing);
                }
            }
        }

        public override void DesignateThing(Thing t)
        {
            if (IsPipeThing(t))
            {
                base.DesignateThing(t);
            }
        }

        private static bool IsPipeThing(Thing thing)
        {
            return thing is Building_Pipe;
        }
    }
}
