using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_FoodPrinter : Building_NutrientPasteDispenser
    {
        private static readonly Color ColonistMaskColor = new Color(232f / 255f, 1f, 191f / 255f, 1f);
        private static readonly Color PrisonerMaskColor = new Color(1f, 0.78f, 0.46f, 1f);

        private static readonly string[] HopperWarningKeywords =
        {
            "hopper",
            "hoppers",
            "feedstock",
            "input required",
            "food hopper",
            "호퍼",
            "ホッパー",
            "料斗"
        };

        public CompFoodPrinter FoodPrinterComp
        {
            get { return GetComp<CompFoodPrinter>(); }
        }

        public TonerPipeNet TonerNet
        {
            get { return TonerPipeNetManager.GetConnectedTonerNet(this); }
        }

        public bool IsPrinting
        {
            get { return FoodPrinterComp != null && FoodPrinterComp.IsPrinting; }
        }

        public Pawn CurrentProcessingPawn
        {
            get { return FoodPrinterComp == null ? null : FoodPrinterComp.CurrentProcessingPawn; }
        }

        public ThingDef AvailableMealDef
        {
            get
            {
                CompFoodPrinter comp = FoodPrinterComp;
                if (comp == null)
                {
                    return null;
                }

                ThingDef mealDef = comp.GetMealToPrint(this, false);
                return comp.IsMealResearched(mealDef) ? mealDef : null;
            }
        }

        public int CurrentPrintCost
        {
            get
            {
                ThingDef mealDef = AvailableMealDef;
                return mealDef == null ? 0 : FoodPrinterSystemUtility.GetPrintCost(mealDef);
            }
        }

        public bool IsPowered
        {
            get
            {
                CompPowerTrader power = GetComp<CompPowerTrader>();
                return power == null || power.PowerOn;
            }
        }

        public bool CanPrintNow
        {
            get
            {
                CompFoodPrinter comp = FoodPrinterComp;
                ThingDef mealDef = AvailableMealDef;
                return comp != null && comp.CanPrintMeal(this, mealDef);
            }
        }

        public override ThingDef DispensableDef
        {
            get
            {
                CompFoodPrinter comp = FoodPrinterComp;
                ThingDef previewMeal = comp == null ? null : comp.GetPreviewMealDef(this);
                return previewMeal ?? ThingDefOf.MealNutrientPaste;
            }
        }

        public override Color DrawColor
        {
            get
            {
                if (!Spawned || Map == null)
                {
                    return ColonistMaskColor;
                }

                return IsPrisonerServingRoom() ? PrisonerMaskColor : ColonistMaskColor;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            NotifyMaskStateChanged();
            FoodPrinterAlertHarmony.NotifyConsumerRegistryChanged();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            FoodPrinterPawnUtility.NotifyPrinterDespawned(this);
            base.DeSpawn(mode);
            FoodPrinterAlertHarmony.NotifyConsumerRegistryChanged();
        }

        public bool HasValidFeedSource()
        {
            return TonerPipeNetManager.HasConnectedStorage(this);
        }

        public override bool HasEnoughFeedstockInHoppers()
        {
            return HasValidFeedSource();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (!IsHopperBuildGizmo(gizmo))
                {
                    yield return gizmo;
                }
            }
        }

        public override string GetInspectString()
        {
            return StripHopperInspectWarnings(base.GetInspectString());
        }

        public override Thing TryDispenseFood()
        {
            CompFoodPrinter comp = FoodPrinterComp;
            if (comp == null || !comp.IsPrinting)
            {
                return null;
            }

            return comp.CompleteProcessing(this, comp.CurrentProcessingPawn);
        }

        internal void NotifyMaskStateChanged()
        {
            MarkGraphicDirty();
        }

        private bool IsPrisonerServingRoom()
        {
            if (InteractionCell.IsValid && InteractionCell.InBounds(Map))
            {
                Room interactionRoom = InteractionCell.GetRoom(Map);
                if (interactionRoom != null)
                {
                    return interactionRoom.IsPrisonCell;
                }
            }
            return false;
        }

        internal bool UsesRoom(Room room)
        {
            if (room == null || room.Map != Map)
            {
                return false;
            }

            return InteractionCell.IsValid && InteractionCell.InBounds(Map) && InteractionCell.GetRoom(Map) == room;
        }

        private void MarkGraphicDirty()
        {
            if (Map == null || Map.mapDrawer == null)
            {
                return;
            }

            Notify_ColorChanged();

            CellRect occupiedRect = GenAdj.OccupiedRect(Position, Rotation, def.size);
            foreach (IntVec3 cell in occupiedRect)
            {
                if (cell.InBounds(Map))
                {
                    Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Things);
                    Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Buildings);
                }
            }
        }

        private static bool IsHopperBuildGizmo(Gizmo gizmo)
        {
            Command command = gizmo as Command;
            if (command == null)
            {
                return false;
            }

            if (ThingDefOf.Hopper != null && command.icon != null && ThingDefOf.Hopper.uiIcon != null)
            {
                if (ReferenceEquals(command.icon, ThingDefOf.Hopper.uiIcon)
                    || string.Equals(command.icon.name, ThingDefOf.Hopper.uiIcon.name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return ContainsHopperWarningKeyword(command.defaultLabel)
                || ContainsHopperWarningKeyword(command.defaultDesc);
        }

        private static string StripHopperInspectWarnings(string text)
        {
            if (text.NullOrEmpty())
            {
                return text;
            }

            string[] lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            List<string> keptLines = new List<string>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!ContainsHopperWarningKeyword(lines[i]))
                {
                    keptLines.Add(lines[i]);
                }
            }

            return string.Join("\n", keptLines.ToArray());
        }

        private static bool ContainsHopperWarningKeyword(string text)
        {
            if (text.NullOrEmpty())
            {
                return false;
            }

            if (ThingDefOf.Hopper != null)
            {
                if (ContainsIgnoreCase(text, ThingDefOf.Hopper.label)
                    || ContainsIgnoreCase(text, ThingDefOf.Hopper.LabelCap.ToString()))
                {
                    return true;
                }
            }

            for (int i = 0; i < HopperWarningKeywords.Length; i++)
            {
                if (ContainsIgnoreCase(text, HopperWarningKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return !source.NullOrEmpty()
                && !value.NullOrEmpty()
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
