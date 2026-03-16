using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FoodPrinterSystem
{
    public static class FoodPrinterJobUtility
    {
        private const float MaxPrinterSearchDistance = 9999f;

        public static Building_FoodPrinter FindClosestValidPrinter(Pawn getter, Pawn eater, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper, bool ignoreReservations, Building_FoodPrinter excludedPrinter = null)
        {
            if (getter == null || eater == null || getter.Map == null || !allowDispenserFull || FoodPrinterSystemDefOf.FPS_FoodPrinter == null)
            {
                return null;
            }

            List<Thing> printers = getter.Map.listerThings.ThingsOfDef(FoodPrinterSystemDefOf.FPS_FoodPrinter);
            if (printers == null || printers.Count == 0)
            {
                return null;
            }

            TraverseParms traverseParms = TraverseParms.For(getter, Danger.Some, TraverseMode.ByPawn, false, false, false, false);
            Thing result = GenClosest.ClosestThingReachable(
                getter.Position,
                getter.Map,
                ThingRequest.ForDef(FoodPrinterSystemDefOf.FPS_FoodPrinter),
                PathEndMode.InteractionCell,
                traverseParms,
                MaxPrinterSearchDistance,
                delegate(Thing thing)
                {
                    return IsPrinterJobCandidate(thing as Building_FoodPrinter, getter, eater, allowDispenserFull, allowForbidden, allowSociallyImproper, ignoreReservations, excludedPrinter);
                },
                printers,
                0,
                -1,
                true,
                RegionType.Set_Passable,
                false,
                false);
            return result as Building_FoodPrinter;
        }

        public static bool IsPrinterJobCandidate(Building_FoodPrinter printer, Pawn getter, Pawn eater, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper, bool ignoreReservations, Building_FoodPrinter excludedPrinter = null)
        {
            if (printer == null
                || printer == excludedPrinter
                || getter == null
                || eater == null
                || getter.Map == null
                || printer.Map != getter.Map
                || !allowDispenserFull
                || getter.IsWildMan()
                || !getter.RaceProps.ToolUser
                || !getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                || !printer.CanPrintNow)
            {
                return false;
            }

            ThingDef availableMealDef = printer.AvailableMealDef;
            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            if (printerComp == null || availableMealDef == null || !printerComp.IsMealResearched(availableMealDef))
            {
                return false;
            }

            if (!IsPrinterFactionValid(printer, getter, eater))
            {
                return false;
            }

            if (!allowForbidden && printer.IsForbidden(getter))
            {
                return false;
            }

            if (!printer.InteractionCell.IsValid || !printer.InteractionCell.Standable(printer.Map))
            {
                return false;
            }

            if (!allowSociallyImproper && !IsFoodSourceOnMapSociallyProper(printer, getter, eater))
            {
                return false;
            }

            if (!printer.CanPawnPrint(eater))
            {
                return false;
            }

            if (ignoreReservations)
            {
                TraverseParms traverseParms = TraverseParms.For(getter, Danger.Some, TraverseMode.ByPawn, false, false, false, false);
                return getter.Map.reachability.CanReachNonLocal(getter.Position, new TargetInfo(printer.InteractionCell, printer.Map), PathEndMode.OnCell, traverseParms);
            }

            return getter.CanReserveAndReach(printer, PathEndMode.InteractionCell, Danger.Some, 1, -1, null, false);
        }

        private static bool IsPrinterFactionValid(Building_FoodPrinter printer, Pawn getter, Pawn eater)
        {
            if (printer == null)
            {
                return false;
            }

            Faction printerFaction = printer.Faction;
            if (printerFaction == null)
            {
                return true;
            }

            if (getter != null && (printerFaction == getter.Faction || printerFaction == getter.HostFaction))
            {
                return true;
            }

            if (eater != null && (printerFaction == eater.Faction || printerFaction == eater.HostFaction))
            {
                return true;
            }

            return printerFaction == Faction.OfPlayer && ((getter != null && getter.IsPrisonerOfColony) || (eater != null && eater.IsPrisonerOfColony));
        }

        public static bool IsFoodSourceOnMapSociallyProper(Thing thing, Pawn getter, Pawn eater)
        {
            bool animalsCare = !getter.RaceProps.Animal;
            return thing.IsSociallyProper(getter) || thing.IsSociallyProper(eater, eater.IsPrisonerOfColony, animalsCare);
        }
    }
}
