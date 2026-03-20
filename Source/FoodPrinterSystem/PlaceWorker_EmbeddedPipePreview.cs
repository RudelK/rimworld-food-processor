using RimWorld;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public class PlaceWorker_EmbeddedPipePreview : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport report = base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!report.Accepted)
            {
                return report;
            }

            return PipePlacementValidationUtility.ValidateNoDuplicatePipeInfrastructure(checkingDef, loc, rot, map, thingToIgnore, thing);
        }

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map map = Find.CurrentMap;
            base.DrawGhost(def, center, rot, ghostCol, thing);

            if (map == null)
            {
                return;
            }

            PipeOverlayDrawer.DrawActiveOverlay(map);

            if (!ShouldDrawLocalPipeGhost(def))
            {
                return;
            }

            PipeOverlayDrawer.DrawGhostOverlay(map, def, center, rot, thing);
        }

        private static bool ShouldDrawLocalPipeGhost(ThingDef def)
        {
            return def != null
                && PipeVisualNodeResolver.HasVisualPipeSupport(def)
                && def.thingClass != null
                && typeof(FoodPrinterSystem.Building_Pipe).IsAssignableFrom(def.thingClass);
        }
    }
}
