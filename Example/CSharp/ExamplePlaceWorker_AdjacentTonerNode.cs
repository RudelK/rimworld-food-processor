using System.Collections.Generic;
using FoodSystemPipe;
using Verse;

namespace FoodPrinterSystem.Examples
{
    public class ExamplePlaceWorker_AdjacentTonerNode : PlaceWorker_EmbeddedPipePreview
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport baseReport = base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!baseReport.Accepted)
            {
                return baseReport;
            }

            for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
            {
                IntVec3 adjacent = loc + GenAdj.CardinalDirections[i];
                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = adjacent.GetThingList(map);
                for (int j = 0; j < things.Count; j++)
                {
                    Thing currentThing = things[j];
                    if (currentThing == null)
                    {
                        continue;
                    }

                    if (currentThing.TryGetComp<CompTonerNode>() != null)
                    {
                        return true;
                    }

                    if (PipeVisualNodeResolver.TryResolveNode(currentThing, out IEmbeddedPipeNode node)
                        && node.ConnectsPipeAt(adjacent))
                    {
                        return true;
                    }
                }
            }

            return "FPS_Example_MustPlaceAdjacentToTonerNode".Translate();
        }
    }
}
