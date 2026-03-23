using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FoodPrinterSystem
{
    public static class FoodPrinterJobUtility
    {
        private const float MaxPrinterSearchDistance = 9999f;
        private const float PreferenceDistanceCreditPerPoint = 4f;

        public static Building_FoodPrinter FindClosestValidPrinter(Pawn getter, Pawn eater, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper, bool ignoreReservations, Building_FoodPrinter excludedPrinter = null)
        {
            if (getter == null
                || eater == null
                || getter.Map == null
                || !allowDispenserFull
                || FoodPrinterSystemDefOf.FPS_FoodPrinter == null
                || !CanGetterOperatePrinter(getter)
                || !FoodPrinterPawnUtility.CanPawnUsePrinter(eater))
            {
                return null;
            }

            List<Thing> printers = getter.Map.listerThings.ThingsOfDef(FoodPrinterSystemDefOf.FPS_FoodPrinter);
            if (printers == null || printers.Count == 0)
            {
                return null;
            }

            PawnPrinterFoodPolicy pawnPolicy = FoodPrinterPawnUtility.ResolvePawnFoodPolicy(eater);
            Building_FoodPrinter bestPrinter = null;
            float bestAdjustedDistance = float.MaxValue;
            float bestRawDistance = float.MaxValue;
            float bestPreferenceScore = float.MinValue;

            for (int i = 0; i < printers.Count; i++)
            {
                Building_FoodPrinter printer = printers[i] as Building_FoodPrinter;
                if (!IsPrinterJobCandidate(printer, getter, eater, pawnPolicy, allowDispenserFull, allowForbidden, allowSociallyImproper, ignoreReservations, excludedPrinter))
                {
                    continue;
                }

                // Soft preference scoring only ranks printers that already passed the
                // hard allow/deny filters in IsPrinterJobCandidate.
                float rawDistance = getter.Position.DistanceTo(printer.InteractionCell);
                float preferenceScore = FoodPrinterPawnUtility.GetPrinterPreferenceScore(pawnPolicy, eater, printer);
                float adjustedDistance = rawDistance - (preferenceScore * PreferenceDistanceCreditPerPoint);
                if (IsBetterPrinterCandidate(adjustedDistance, rawDistance, preferenceScore, bestAdjustedDistance, bestRawDistance, bestPreferenceScore))
                {
                    bestPrinter = printer;
                    bestAdjustedDistance = adjustedDistance;
                    bestRawDistance = rawDistance;
                    bestPreferenceScore = preferenceScore;
                }
            }

            return bestPrinter;
        }

        public static bool IsPrinterJobCandidate(Building_FoodPrinter printer, Pawn getter, Pawn eater, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper, bool ignoreReservations, Building_FoodPrinter excludedPrinter = null)
        {
            if (!CanGetterOperatePrinter(getter) || !FoodPrinterPawnUtility.CanPawnUsePrinter(eater))
            {
                return false;
            }

            return IsPrinterJobCandidate(
                printer,
                getter,
                eater,
                FoodPrinterPawnUtility.ResolvePawnFoodPolicy(eater),
                allowDispenserFull,
                allowForbidden,
                allowSociallyImproper,
                ignoreReservations,
                excludedPrinter);
        }

        private static bool IsPrinterJobCandidate(Building_FoodPrinter printer, Pawn getter, Pawn eater, PawnPrinterFoodPolicy pawnPolicy, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper, bool ignoreReservations, Building_FoodPrinter excludedPrinter = null)
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
                || !FoodPrinterPawnUtility.CanPawnUsePrinter(eater)
                || !printer.CanPrintNow)
            {
                return false;
            }

            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            ThingDef availableMealDef = FoodPrinterPawnUtility.GetResolvedMealDefForPawn(pawnPolicy, eater, printer);
            if (printerComp == null || availableMealDef == null || !printerComp.IsMealResearched(availableMealDef))
            {
                return false;
            }

            if (!TonerPipeNetManager.HasConnectedTonerNet(printer))
            {
                return false;
            }

            int tonerCost = FoodPrinterSystemUtility.GetPrintCost(availableMealDef);
            if (tonerCost <= 0 || !TonerPipeNetManager.CanDraw(printer, tonerCost))
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

            // Hard filter: never allow fallback selection to use a printer that the
            // eater is not actually allowed to consume from.
            if (!FoodPrinterPawnUtility.IsPrinterAllowedForPawn(pawnPolicy, eater, printer))
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

        private static bool CanGetterOperatePrinter(Pawn getter)
        {
            return getter != null
                && getter.RaceProps != null
                && getter.RaceProps.ToolUser
                && !getter.IsWildMan()
                && getter.health != null
                && getter.health.capacities != null
                && getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        }

        private static bool IsBetterPrinterCandidate(float adjustedDistance, float rawDistance, float preferenceScore, float bestAdjustedDistance, float bestRawDistance, float bestPreferenceScore)
        {
            if (adjustedDistance < bestAdjustedDistance - 0.001f)
            {
                return true;
            }

            if (adjustedDistance > bestAdjustedDistance + 0.001f)
            {
                return false;
            }

            if (preferenceScore > bestPreferenceScore + 0.001f)
            {
                return true;
            }

            if (preferenceScore < bestPreferenceScore - 0.001f)
            {
                return false;
            }

            return rawDistance < bestRawDistance;
        }
    }
}
