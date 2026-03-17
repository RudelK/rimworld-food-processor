using System.Collections.Generic;
using FoodPrinterSystem;
using Verse;

namespace FoodSystemPipe
{
    public static class PipeVisualNodeResolver
    {
        public static CompProperties_Pipe GetPipeProperties(ThingDef def)
        {
            if (def?.comps == null)
            {
                return null;
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                CompProperties_Pipe pipeProps = def.comps[i] as CompProperties_Pipe;
                if (pipeProps != null)
                {
                    return pipeProps;
                }
            }

            return null;
        }

        public static bool HasVisualPipeSupport(ThingDef def)
        {
            return GetPipeProperties(def) != null;
        }

        public static bool TryResolveNode(Thing thing, out IEmbeddedPipeNode node)
        {
            if (thing is IEmbeddedPipeNode directNode)
            {
                node = directNode;
                return true;
            }

            ThingWithComps thingWithComps = thing as ThingWithComps;
            if (thingWithComps != null)
            {
                for (int i = 0; i < thingWithComps.AllComps.Count; i++)
                {
                    IEmbeddedPipeNode compNode = thingWithComps.AllComps[i] as IEmbeddedPipeNode;
                    if (compNode != null)
                    {
                        node = compNode;
                        return true;
                    }
                }
            }

            node = null;
            return false;
        }

        public static bool TryGetNodeAt(Map map, IntVec3 targetCell, Thing ownerThing, out IEmbeddedPipeNode node)
        {
            return TryGetNodeAt(map, targetCell, ownerThing, out _, out node);
        }

        public static bool TryGetNodeAt(Map map, IntVec3 targetCell, Thing ownerThing, out Thing nodeThing, out IEmbeddedPipeNode node)
        {
            nodeThing = null;
            node = null;
            if (map == null || !targetCell.InBounds(map))
            {
                return false;
            }

            List<Thing> things = targetCell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || thing == ownerThing)
                {
                    continue;
                }

                if (!TryResolveNode(thing, out IEmbeddedPipeNode candidate) || !candidate.ConnectsPipeAt(targetCell))
                {
                    continue;
                }

                nodeThing = thing;
                node = candidate;
                return true;
            }

            return false;
        }
    }
}
