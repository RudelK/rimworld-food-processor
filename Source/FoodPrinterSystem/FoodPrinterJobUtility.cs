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

        private struct PrinterCandidateScore
        {
            public Building_FoodPrinter Printer;
            public ThingDef MealDef;
            public float RawDistance;
            public float PreferenceScore;
            public float AdjustedDistance;
        }

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
            List<PrinterCandidateScore> candidates = new List<PrinterCandidateScore>();

            for (int i = 0; i < printers.Count; i++)
            {
                Building_FoodPrinter printer = printers[i] as Building_FoodPrinter;
                if (!TryBuildPrinterCandidate(printer, getter, eater, pawnPolicy, allowDispenserFull, allowForbidden, allowSociallyImproper, excludedPrinter, out PrinterCandidateScore candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            candidates.Sort(ComparePrinterCandidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                PrinterCandidateScore candidate = candidates[i];
                if (CanReachPrinterCandidate(candidate.Printer, getter, ignoreReservations))
                {
                    return candidate.Printer;
                }
            }

            return null;
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
            if (!TryBuildPrinterCandidate(printer, getter, eater, pawnPolicy, allowDispenserFull, allowForbidden, allowSociallyImproper, excludedPrinter, out PrinterCandidateScore candidate))
            {
                return false;
            }

            return CanReachPrinterCandidate(candidate.Printer, getter, ignoreReservations);
        }

        private static bool TryBuildPrinterCandidate(Building_FoodPrinter printer, Pawn getter, Pawn eater, PawnPrinterFoodPolicy pawnPolicy, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper, Building_FoodPrinter excludedPrinter, out PrinterCandidateScore candidate)
        {
            candidate = default;

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

            float rawDistance = getter.Position.DistanceTo(printer.InteractionCell);
            if (rawDistance > MaxPrinterSearchDistance)
            {
                return false;
            }

            float preferenceScore = FoodPrinterPawnUtility.GetPrinterPreferenceScore(pawnPolicy, eater, printer);
            candidate = new PrinterCandidateScore
            {
                Printer = printer,
                MealDef = availableMealDef,
                RawDistance = rawDistance,
                PreferenceScore = preferenceScore,
                AdjustedDistance = rawDistance - (preferenceScore * PreferenceDistanceCreditPerPoint)
            };
            return true;
        }

        private static bool CanReachPrinterCandidate(Building_FoodPrinter printer, Pawn getter, bool ignoreReservations)
        {
            if (printer == null || getter == null || getter.Map == null)
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

        private static int ComparePrinterCandidates(PrinterCandidateScore left, PrinterCandidateScore right)
        {
            if (IsBetterPrinterCandidate(left.AdjustedDistance, left.RawDistance, left.PreferenceScore, right.AdjustedDistance, right.RawDistance, right.PreferenceScore))
            {
                return -1;
            }

            if (IsBetterPrinterCandidate(right.AdjustedDistance, right.RawDistance, right.PreferenceScore, left.AdjustedDistance, left.RawDistance, left.PreferenceScore))
            {
                return 1;
            }

            return 0;
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
