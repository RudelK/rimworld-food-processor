using RimWorld;
using Verse;

namespace FoodSystemPipe
{
    public static class PipePlacementValidationUtility
    {
        public static AcceptanceReport ValidateNoDuplicatePipeInfrastructure(BuildableDef buildableDef, IntVec3 center, Rot4 rotation, Map map, Thing thingToIgnore = null, Thing placingThing = null)
        {
            ThingDef thingDef = buildableDef as ThingDef;
            if (thingDef == null || map == null || !PipeVisualNodeResolver.HasVisualPipeSupport(thingDef))
            {
                return AcceptanceReport.WasAccepted;
            }

            if (!TryFindBlockingPipeInfrastructure(map, thingDef, center, rotation, thingToIgnore, placingThing, out Thing blocker))
            {
                return AcceptanceReport.WasAccepted;
            }

            string reason = "FPS_PipeDuplicatePlacementBlocked".Translate(GetBlockerLabel(blocker)).ToString();
            return new AcceptanceReport(reason);
        }

        public static bool TryRejectDuplicateSpawn(Thing spawnedThing)
        {
            if (spawnedThing == null || !spawnedThing.Spawned || spawnedThing.Map == null)
            {
                return false;
            }

            if (!TryFindBlockingPipeInfrastructure(spawnedThing.Map, spawnedThing.def, spawnedThing.Position, spawnedThing.Rotation, spawnedThing, spawnedThing, out Thing blocker))
            {
                return false;
            }

            Messages.Message(
                "FPS_PipeDuplicateSpawnBlocked".Translate(spawnedThing.LabelCap, GetBlockerLabel(blocker)),
                spawnedThing,
                MessageTypeDefOf.RejectInput,
                false);

            spawnedThing.Destroy(DestroyMode.Vanish);
            return true;
        }

        public static bool TryFindBlockingPipeInfrastructure(Map map, ThingDef placingDef, IntVec3 center, Rot4 rotation, Thing thingToIgnore, Thing placingThing, out Thing blocker)
        {
            blocker = null;
            if (map == null || placingDef == null || !PipeVisualNodeResolver.HasVisualPipeSupport(placingDef))
            {
                return false;
            }

            foreach (IntVec3 cell in PipeVisualCellProvider.GetVisualCells(placingDef, center, rotation))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                if (TryFindBlockingPipeInfrastructureAtCell(map, cell, thingToIgnore, placingThing, out blocker))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindBlockingPipeInfrastructureAtCell(Map map, IntVec3 cell, Thing thingToIgnore, Thing placingThing, out Thing blocker)
        {
            blocker = null;
            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null || thing == thingToIgnore || thing == placingThing)
                {
                    continue;
                }

                if (HasBuiltPipeInfrastructureAt(thing, cell) || HasPlannedPipeInfrastructureAt(thing, cell))
                {
                    blocker = thing;
                    return true;
                }
            }

            return false;
        }

        private static bool HasBuiltPipeInfrastructureAt(Thing thing, IntVec3 cell)
        {
            CompPipe pipeComp = thing == null ? null : thing.TryGetComp<CompPipe>();
            if (pipeComp != null)
            {
                return pipeComp.TransmitsResource && pipeComp.ConnectsPipeAt(cell);
            }

            return PipeVisualNodeResolver.TryResolveNode(thing, out IEmbeddedPipeNode node) && node.ConnectsPipeAt(cell);
        }

        private static bool HasPlannedPipeInfrastructureAt(Thing thing, IntVec3 cell)
        {
            return TryGetPlannedBuildDef(thing, out ThingDef buildDef)
                && PipeVisualNodeResolver.HasVisualPipeSupport(buildDef)
                && PipeVisualCellProvider.ContainsCell(buildDef, thing.Position, thing.Rotation, cell);
        }

        private static bool TryGetPlannedBuildDef(Thing thing, out ThingDef buildDef)
        {
            buildDef = null;
            if (thing is Blueprint_Build buildBlueprint)
            {
                buildDef = buildBlueprint.BuildDef;
            }
            else if (thing is Blueprint_Install installBlueprint)
            {
                buildDef = installBlueprint.ThingToInstall?.def;
            }
            else if (thing is Frame frame)
            {
                buildDef = frame.BuildDef;
            }

            return buildDef != null;
        }

        private static string GetBlockerLabel(Thing blocker)
        {
            return blocker == null ? "pipe infrastructure" : blocker.LabelShortCap;
        }
    }
}
