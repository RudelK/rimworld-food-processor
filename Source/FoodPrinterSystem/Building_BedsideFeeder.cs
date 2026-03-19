using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using FoodSystemPipe;

namespace FoodPrinterSystem
{
    public class Building_BedsideFeeder : Building
    {
        private const int BedSearchRetryTicks = GenTicks.TickRareInterval * 4;
        private Building_Bed linkedBed;
        private CompPowerTrader powerComp;
        private int nextBedSearchTick;

        public Building_Bed LinkedBed => linkedBed;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            FindLinkedBed();
            FoodPrinterAlertHarmony.NotifyConsumerRegistryChanged();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            FoodPrinterAlertHarmony.NotifyConsumerRegistryChanged();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref linkedBed, "linkedBed");
        }

        public override void TickRare()
        {
            base.TickRare();

            if (powerComp != null && !powerComp.PowerOn)
                return;

            if (linkedBed == null || linkedBed.Destroyed || !IsAtBedHead(linkedBed, Position))
            {
                int currentTick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
                if (currentTick < nextBedSearchTick)
                {
                    return;
                }

                FindLinkedBed();
                if (linkedBed == null)
                {
                    nextBedSearchTick = currentTick + BedSearchRetryTicks;
                    return;
                }

                nextBedSearchTick = 0;
            }

            foreach (Pawn occupant in linkedBed.CurOccupants)
            {
                if (occupant != null && occupant.RaceProps.Humanlike && occupant.needs?.food != null)
                {
                    TryFeedPawn(occupant);
                }
            }
        }

        private void TryFeedPawn(Pawn pawn)
        {
            if (pawn.needs.food.CurLevelPercentage > 0.3f) return;

            // Consume toner cost defined in settings (default 3.0)
            float tonerCost = FoodPrinterSystemMod.Settings.bedsideFeederTonerCost;
            
            // 0.9 nutrition is equivalent to one Nutrient Paste meal
            float nutritionToFeed = 0.9f;

            if (TonerNetworkUtility.TryConsumeToner(this, (int)tonerCost))
            {
                pawn.needs.food.CurLevel = Mathf.Min(pawn.needs.food.MaxLevel, pawn.needs.food.CurLevel + nutritionToFeed);
                // Optional: Play a sound or effect? Building_NutrientPasteDispenser.DispenseSound is a thing but we are direct-feeding.
            }
        }

        private void FindLinkedBed()
        {
            linkedBed = null;
            foreach (IntVec3 adj in GenAdj.CellsAdjacent8Way(this))
            {
                if (!adj.InBounds(Map)) continue;
                List<Thing> thingList = adj.GetThingList(Map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    if (thingList[i] is Building_Bed bed && IsAtBedHead(bed, Position))
                    {
                        linkedBed = bed;
                        return;
                    }
                }
            }
        }

        public static bool IsAtBedHead(Building_Bed bed, IntVec3 feederPos)
        {
            if (bed == null || bed.Destroyed) return false;
            CellRect rect = GenAdj.OccupiedRect(bed.Position, bed.Rotation, bed.def.size);
            if (rect.Contains(feederPos)) return false;

            int fx = feederPos.x;
            int fz = feederPos.z;
            Rot4 rot = bed.Rotation;

            // Define a 'Head Zone' which is the head row/column and the adjacent row/column outside the bed.
            // In RimWorld, a bed's head (pillow) is in the OPPOSITE direction of its Rotation.
            if (rot == Rot4.North) // Head is South
                return fz >= rect.minZ - 1 && fz <= rect.minZ && fx >= rect.minX - 1 && fx <= rect.maxX + 1;
            if (rot == Rot4.South) // Head is North
                return fz >= rect.maxZ && fz <= rect.maxZ + 1 && fx >= rect.minX - 1 && fx <= rect.maxX + 1;
            if (rot == Rot4.East) // Head is West
                return fx >= rect.minX - 1 && fx <= rect.minX && fz >= rect.minZ - 1 && fz <= rect.maxZ + 1;
            if (rot == Rot4.West) // Head is East
                return fx >= rect.maxX && fx <= rect.maxX + 1 && fz >= rect.minZ - 1 && fz <= rect.maxZ + 1;

            return false;
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (!text.NullOrEmpty()) text += "\n";

            if (linkedBed != null)
            {
                text += "FPS_LinkedBed".Translate(linkedBed.LabelCap) + "\n";
                bool hasOccupant = false;
                foreach (var occupant in linkedBed.CurOccupants)
                {
                    if (occupant != null)
                    {
                        text += "FPS_FeedingOccupant".Translate(occupant.LabelShortCap) + "\n";
                        hasOccupant = true;
                    }
                }
                if (!hasOccupant)
                {
                    text += "FPS_NoOccupant".Translate() + "\n";
                }
            }
            else
            {
                text += "FPS_NoLinkedBed".Translate() + "\n";
            }

            TonerNetworkSummary summary = TonerNetworkUtility.GetSummary(this);
            text += FoodPrinterSystemUtility.FormatSummary(summary);

            return text.TrimEndNewlines();
        }
    }
}

namespace FoodSystemPipe
{
    using FoodPrinterSystem;

    public class PlaceWorker_BedsideFeeder : PlaceWorker_EmbeddedPipePreview
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport baseReport = base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!baseReport.Accepted) return baseReport;

            Building_Bed bed = null;
            // Scan 8 cells around 'loc' to find a bed
            for (int i = 0; i < 8; i++)
            {
                IntVec3 adj = loc + GenAdj.AdjacentCells[i];
                if (!adj.InBounds(map)) continue;
                List<Thing> thingList = adj.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    if (thingList[j] is Building_Bed b && Building_BedsideFeeder.IsAtBedHead(b, loc))
                    {
                        bed = b;
                        break;
                    }
                }
                if (bed != null) break;
            }

            if (bed == null)
            {
                return "FPS_MustPlaceAtBedHead".Translate();
            }

            return true;
        }
    }
}
