using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using FoodSystemPipe;

namespace FoodPrinterSystem
{
    public class Building_NutrientFeeder : Building
    {
        private const int BedSearchRetryTicks = GenTicks.TickRareInterval * 4;
        private Building_Bed linkedBed;
        private CompPowerTrader powerComp;
        private int activeTicksRemaining;
        private int nextBedSearchTick;

        public Building_Bed LinkedBed => linkedBed;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            FindLinkedBed();
            ApplyPowerSetting();
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

            if (BuildingActivityUtility.TickDownActiveWindow(ref activeTicksRemaining))
            {
                ApplyPowerSetting();
            }

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

            if (TonerPipeNetManager.TryDrawToner(this, FoodPrinterSystemUtility.NormalizeTonerAmount(FoodPrinterSystemMod.Settings.nutrientFeederTonerCost)))
            {
                // 0.9 nutrition is equivalent to one Nutrient Paste meal
                pawn.needs.food.CurLevel = Mathf.Min(pawn.needs.food.MaxLevel, pawn.needs.food.CurLevel + 0.9f);
                BuildingActivityUtility.MarkActiveNow(ref activeTicksRemaining);
                ApplyPowerSetting();
            }
        }

        public void ApplyPowerSetting()
        {
            BuildingActivityUtility.ApplyIdleActivePower(
                this,
                ref powerComp,
                activeTicksRemaining,
                FoodPrinterSystemUtility.GetNutrientFeederIdlePowerDraw(),
                FoodPrinterSystemUtility.GetNutrientFeederActivePowerDraw());
        }

        private void FindLinkedBed()
        {
            TryFindAdjacentHeadBed(Map, Position, out linkedBed);
        }

        public static bool TryFindAdjacentHeadBed(Map map, IntVec3 feederPos, out Building_Bed bed)
        {
            bed = null;
            if (map == null)
            {
                return false;
            }

            for (int i = 0; i < 8; i++)
            {
                IntVec3 adjacentCell = feederPos + GenAdj.AdjacentCells[i];
                if (!adjacentCell.InBounds(map))
                {
                    continue;
                }

                List<Thing> thingList = adjacentCell.GetThingList(map);
                for (int j = 0; j < thingList.Count; j++)
                {
                    if (thingList[j] is Building_Bed candidate && IsAtBedHead(candidate, feederPos))
                    {
                        bed = candidate;
                        return true;
                    }
                }
            }

            return false;
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

            TonerNetworkSummary summary = TonerPipeNetManager.GetSummary(this);
            text += FoodPrinterSystemUtility.FormatSummary(summary);

            return text.TrimEndNewlines();
        }
    }
}

namespace FoodSystemPipe
{
    using FoodPrinterSystem;

    public class PlaceWorker_NutrientFeeder : PlaceWorker_EmbeddedPipePreview
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            AcceptanceReport baseReport = base.AllowsPlacing(checkingDef, loc, rot, map, thingToIgnore, thing);
            if (!baseReport.Accepted) return baseReport;

            if (!Building_NutrientFeeder.TryFindAdjacentHeadBed(map, loc, out _))
            {
                return "FPS_MustPlaceAtBedHead".Translate();
            }

            return true;
        }
    }
}
