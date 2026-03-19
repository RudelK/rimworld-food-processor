using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    public sealed class PrinterFoodCharacteristics
    {
        public PrinterFoodCharacteristics(FoodTypeFlags predictedFoodTypes, List<ThingDef> predictedIngredientDefs, FoodKind predictedSourceFoodKind, bool containsVegetarianForbiddenIngredients, bool containsHumanMeatIngredient)
        {
            PredictedFoodTypes = predictedFoodTypes;
            PredictedIngredientDefs = predictedIngredientDefs ?? new List<ThingDef>();
            PredictedSourceFoodKind = predictedSourceFoodKind;
            ContainsVegetarianForbiddenIngredients = containsVegetarianForbiddenIngredients;
            ContainsHumanMeatIngredient = containsHumanMeatIngredient;
        }

        public FoodTypeFlags PredictedFoodTypes { get; private set; }
        public List<ThingDef> PredictedIngredientDefs { get; private set; }
        public FoodKind PredictedSourceFoodKind { get; private set; }
        public bool ContainsVegetarianForbiddenIngredients { get; private set; }
        public bool ContainsHumanMeatIngredient { get; private set; }
    }

    public sealed class PawnPrinterFoodPolicy
    {
        public bool IdeologyResolutionSucceeded { get; set; }
        public bool TraitResolutionSucceeded { get; set; }
        public bool UsedNeutralFallback { get; set; }
        public bool RequiresVegetarianFood { get; set; }
        public bool PrefersMeat { get; set; }
        public bool PrefersHumanMeat { get; set; }

        public string ToDebugString()
        {
            return "ideoResolved=" + IdeologyResolutionSucceeded
                + ", traitResolved=" + TraitResolutionSucceeded
                + ", neutralFallback=" + UsedNeutralFallback
                + ", requiresVegetarian=" + RequiresVegetarianFood
                + ", prefersMeat=" + PrefersMeat
                + ", prefersHumanMeat=" + PrefersHumanMeat;
        }
    }

    public static class FoodPrinterPawnUtility
    {
        private sealed class CachedPrinterFoodCharacteristics
        {
            public int NetworkRevision;
            public PrinterFoodCharacteristics Characteristics;
        }

        private static readonly Dictionary<int, CachedPrinterFoodCharacteristics> CachedCharacteristicsByPrinterId = new Dictionary<int, CachedPrinterFoodCharacteristics>();
        private static TraitDef cachedCannibalTraitDef;
        private static bool cachedCannibalTraitResolved;

        public static PrinterFoodCharacteristics GetPredictedFoodCharacteristics(Building_FoodPrinter printer)
        {
            if (printer == null || printer.Map == null)
            {
                return new PrinterFoodCharacteristics(FoodTypeFlags.None, new List<ThingDef>(), FoodKind.Any, false, false);
            }

            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(printer.Map);
            int networkRevision = networkComponent == null ? 0 : networkComponent.NetworkRevision;
            // Cache by toner network revision. Tank ingredient mutations mark the
            // network dirty so stale printer food predictions get invalidated here.
            CachedPrinterFoodCharacteristics cachedCharacteristics;
            if (CachedCharacteristicsByPrinterId.TryGetValue(printer.thingIDNumber, out cachedCharacteristics)
                && cachedCharacteristics != null
                && cachedCharacteristics.NetworkRevision == networkRevision
                && cachedCharacteristics.Characteristics != null)
            {
                return cachedCharacteristics.Characteristics;
            }

            // The printer predicts its current food profile from the final ingestible
            // ingredient defs stored on connected toner tanks. We union those
            // ingredientDef.ingestible.foodType flags so selection can reason about
            // meat/vegetarian/cannibal constraints before a meal is printed.
            List<Thing> connectedNodes = TonerPipeNetManager.GetConnectedNodes(printer);
            List<ThingDef> predictedIngredientDefs = new List<ThingDef>();
            FoodTypeFlags predictedFoodTypes = FoodTypeFlags.None;
            bool hasMeat = false;
            bool hasNonMeat = false;
            bool containsVegetarianForbiddenIngredients = false;
            bool containsHumanMeatIngredient = false;
            for (int i = 0; i < connectedNodes.Count; i++)
            {
                CompTonerTank tank = connectedNodes[i].TryGetComp<CompTonerTank>();
                if (tank == null)
                {
                    continue;
                }

                predictedFoodTypes |= tank.CachedIngredientFoodTypes;
                if (tank.CachedIngredientFoodKind == FoodKind.Any)
                {
                    hasMeat = true;
                    hasNonMeat = true;
                }
                else if (tank.CachedIngredientFoodKind == FoodKind.Meat)
                {
                    hasMeat = true;
                }
                else if (tank.CachedIngredientFoodKind == FoodKind.NonMeat)
                {
                    hasNonMeat = true;
                }

                containsVegetarianForbiddenIngredients |= tank.CachedContainsVegetarianForbiddenIngredient;
                containsHumanMeatIngredient |= tank.CachedContainsHumanMeatIngredient;

                List<ThingDef> tankIngredients = tank.StoredIngredients;
                for (int j = 0; j < tankIngredients.Count; j++)
                {
                    ThingDef ingredientDef = tankIngredients[j];
                    if (ingredientDef == null || ingredientDef.ingestible == null || predictedIngredientDefs.Contains(ingredientDef))
                    {
                        continue;
                    }

                    predictedIngredientDefs.Add(ingredientDef);
                }
            }

            PrinterFoodCharacteristics characteristics = new PrinterFoodCharacteristics(
                predictedFoodTypes,
                predictedIngredientDefs,
                DetermineFoodKind(hasMeat, hasNonMeat),
                containsVegetarianForbiddenIngredients,
                containsHumanMeatIngredient);
            CachedCharacteristicsByPrinterId[printer.thingIDNumber] = new CachedPrinterFoodCharacteristics
            {
                NetworkRevision = networkRevision,
                Characteristics = characteristics
            };
            return characteristics;
        }

        public static PawnPrinterFoodPolicy ResolvePawnFoodPolicy(Pawn pawn)
        {
            PawnPrinterFoodPolicy policy = new PawnPrinterFoodPolicy();
            if (pawn == null)
            {
                policy.UsedNeutralFallback = true;
                LogPolicyResolution(pawn, policy);
                return policy;
            }

            if (!ModsConfig.IdeologyActive)
            {
                // When Ideology is disabled there is no meaningful precept state to
                // resolve, so printer policy must stay neutral and skip all ideology
                // API access entirely.
                policy.UsedNeutralFallback = true;
            }
            else
            {
                try
                {
                    Ideo ideo = pawn.Ideo;
                    if (ideo != null)
                    {
                        policy.IdeologyResolutionSucceeded = true;
                        policy.RequiresVegetarianFood = FoodUtility.HasVegetarianRequiredPrecept(ideo);
                        policy.PrefersMeat = FoodUtility.HasMeatEatingRequiredPrecept(ideo);
                        policy.PrefersHumanMeat = FoodUtility.HasHumanMeatEatingRequiredPrecept(ideo);
                    }
                    else
                    {
                        policy.UsedNeutralFallback = true;
                    }
                }
                catch (Exception ex)
                {
                    policy.UsedNeutralFallback = true;
                    LogResolutionFallback("ideology", pawn, ex);
                }
            }

            try
            {
                if (pawn.story != null && pawn.story.traits != null)
                {
                    policy.TraitResolutionSucceeded = true;
                    if (PawnHasCannibalTrait(pawn))
                    {
                        policy.PrefersHumanMeat = true;
                    }
                }
                else
                {
                    policy.UsedNeutralFallback = true;
                }
            }
            catch (Exception ex)
            {
                policy.UsedNeutralFallback = true;
                LogResolutionFallback("trait", pawn, ex);
            }

            LogPolicyResolution(pawn, policy);
            return policy;
        }

        public static bool IsPrinterAllowedForPawn(Pawn pawn, Building_FoodPrinter printer)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return IsPrinterAllowedForPawn(policy, pawn, printer);
        }

        public static bool CanPawnConsumePrinterMeal(Pawn pawn, Building_FoodPrinter printer)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return IsPrinterAllowedForPawn(policy, pawn, printer, true);
        }

        public static bool CanPawnUsePrinter(Pawn pawn, Building_FoodPrinter printer)
        {
            return IsPrinterAllowedForPawn(pawn, printer);
        }

        public static float GetPrinterPreferenceScore(Pawn pawn, Building_FoodPrinter printer)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return GetPrinterPreferenceScore(policy, pawn, printer);
        }

        public static bool IsPrinterAllowedForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer)
        {
            return IsPrinterAllowedForPawn(policy, pawn, printer, false);
        }

        public static bool CanPawnConsumePrinterMeal(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer)
        {
            return IsPrinterAllowedForPawn(policy, pawn, printer, true);
        }

        public static bool IsPrinterAllowedForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, bool allowVanillaFallback)
        {
            if (pawn == null || printer == null)
            {
                LogPrinterAllowance(pawn, printer, policy, false, "missing_pawn_or_printer");
                return false;
            }

            PrinterFoodCharacteristics characteristics = GetPredictedFoodCharacteristics(printer);
            if (TryGetHardFoodTypeBlockReason(policy, characteristics, out string hardBlockReason))
            {
                LogPrinterAllowance(pawn, printer, policy, false, hardBlockReason);
                return false;
            }

            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            ThingDef mealDef = GetResolvedMealDefForPawn(policy, pawn, printer);
            if (printerComp == null || mealDef == null || !printerComp.IsMealResearched(mealDef))
            {
                LogPrinterAllowance(pawn, printer, policy, false, "printer_has_no_valid_meal");
                return false;
            }

            bool allowed = IsMealAllowedForPawn(policy, pawn, printer, mealDef, false, allowVanillaFallback, characteristics, out string reason);
            LogPrinterAllowance(pawn, printer, policy, allowed, reason);
            return allowed;
        }

        public static bool IsMealAllowedForPawn(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef);
        }

        public static bool IsMealAllowedForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef, true, false, null, out _);
        }

        public static bool CanPawnConsumeMeal(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return CanPawnConsumeMeal(policy, pawn, printer, mealDef);
        }

        public static bool CanPawnConsumeMeal(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef, true, true, null, out _);
        }

        public static ThingDef GetResolvedMealDefForPawn(Pawn pawn, Building_FoodPrinter printer)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return GetResolvedMealDefForPawn(policy, pawn, printer);
        }

        public static ThingDef GetResolvedMealDefForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer)
        {
            if (printer == null)
            {
                return null;
            }

            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            if (printerComp == null)
            {
                return null;
            }

            // While actively printing, policy checks must use the already-selected
            // processing meal instead of AvailableMealDef, which is null during the
            // in-progress state. Otherwise completion-time revalidation falsely
            // denies valid printer jobs right before the meal is produced.
            if (printerComp.ProcessingMealDef != null)
            {
                return printerComp.ProcessingMealDef;
            }

            return printerComp.GetMealToPrint(printer, policy, pawn, false);
        }

        private static bool IsMealAllowedForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, bool logResult, bool allowVanillaFallback, PrinterFoodCharacteristics characteristics, out string reason)
        {
            if (pawn == null || printer == null || mealDef == null)
            {
                reason = "missing_pawn_printer_or_meal";
                if (logResult)
                {
                    LogPrinterAllowance(pawn, printer, policy, false, reason);
                }

                return false;
            }

            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            if (printerComp == null || !printerComp.IsMealResearched(mealDef))
            {
                reason = "printer_has_no_valid_meal";
                if (logResult)
                {
                    LogPrinterAllowance(pawn, printer, policy, false, reason);
                }

                return false;
            }

            FoodPolicy currentPolicy = pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;
            if (currentPolicy != null && !currentPolicy.Allows(mealDef))
            {
                reason = "food_policy_blocks_meal_def";
                if (logResult)
                {
                    LogPrinterAllowance(pawn, printer, policy, false, reason);
                }

                return false;
            }

            characteristics = characteristics ?? GetPredictedFoodCharacteristics(printer);

            // Hard filter: this method decides whether the pawn may use the printer at
            // all. Any printer rejected here must never enter fallback selection or
            // proceed to meal generation later in the ingest job.
            if (TryGetHardFoodTypeBlockReason(policy, characteristics, out string hardBlockReason))
            {
                reason = hardBlockReason;
                if (logResult)
                {
                    LogPrinterAllowance(pawn, printer, policy, false, reason);
                }

                return false;
            }

            // Selection uses the cached signature fast-path. The heavier vanilla
            // ingestibility fallback is reserved for job start/completion checks so
            // printer search does not allocate preview meals every tick.
            if (allowVanillaFallback && !PassesVanillaMealCheck(pawn, mealDef, characteristics))
            {
                reason = "vanilla_will_eat_blocks_meal";
                if (logResult)
                {
                    LogPrinterAllowance(pawn, printer, policy, false, reason);
                }

                return false;
            }

            reason = "allowed";
            if (logResult)
            {
                LogPrinterAllowance(pawn, printer, policy, true, reason);
            }

            return true;
        }

        public static float GetPrinterPreferenceScore(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer)
        {
            if (pawn == null || printer == null)
            {
                LogPrinterPreference(pawn, printer, policy, 0f, "missing_pawn_or_printer");
                return 0f;
            }

            PrinterFoodCharacteristics characteristics = GetPredictedFoodCharacteristics(printer);
            bool hasHumanMeat = characteristics.ContainsHumanMeatIngredient;
            bool hasMeat = (characteristics.PredictedFoodTypes & FoodTypeFlags.Meat) != FoodTypeFlags.None;
            bool hasVegetarianForbiddenIngredients = characteristics.ContainsVegetarianForbiddenIngredients;

            // Soft preference only. This never overrides the hard allow/deny rules in
            // IsPrinterAllowedForPawn; it only ranks printers that are already usable.
            float score = 0f;
            List<string> modifiers = new List<string>();
            if (policy != null && policy.PrefersHumanMeat && hasHumanMeat)
            {
                score += 2f;
                modifiers.Add("human_meat:+2");
            }

            if (policy != null && policy.PrefersMeat)
            {
                if (hasMeat)
                {
                    score += 1f;
                    modifiers.Add("meat:+1");
                }
                else
                {
                    score -= 0.5f;
                    modifiers.Add("no_meat:-0.5");
                }
            }

            if (policy != null && policy.RequiresVegetarianFood && !hasVegetarianForbiddenIngredients)
            {
                score += 0.5f;
                modifiers.Add("vegetarian_safe:+0.5");
            }

            LogPrinterPreference(pawn, printer, policy, score, modifiers.Count == 0 ? "none" : string.Join(", ", modifiers.ToArray()));
            return score;
        }

        private static bool PassesVanillaMealCheck(Pawn pawn, ThingDef mealDef, PrinterFoodCharacteristics characteristics)
        {
            if (pawn == null || mealDef == null)
            {
                return false;
            }

            try
            {
                Thing previewMeal = BuildPredictedMealPreview(mealDef, characteristics);
                return FoodUtility.WillEat(pawn, previewMeal, pawn, false, false);
            }
            catch (Exception ex)
            {
                LogResolutionFallback("vanilla_will_eat", pawn, ex);
                return true;
            }
        }

        // Hard printer checks use the cached toner-derived food characteristics so
        // search-time selection and execution-time revalidation agree without
        // building preview meals every tick. Vegetarian restrictions remain hard if
        // they were positively resolved. Preference-based hard denial only applies
        // when ideology resolution succeeded; otherwise preference data stays soft so
        // pawns without a resolved ideoligion do not get blocked from printing.
        private static bool TryGetHardFoodTypeBlockReason(PawnPrinterFoodPolicy policy, PrinterFoodCharacteristics characteristics, out string reason)
        {
            reason = null;
            if (policy == null || characteristics == null)
            {
                return false;
            }

            if (policy.RequiresVegetarianFood && characteristics.ContainsVegetarianForbiddenIngredients)
            {
                reason = "vegetarian_policy_blocks_meat_printer";
                return true;
            }

            if (!IsHardFoodTypeCheckEnabled())
            {
                return false;
            }

            if (!policy.IdeologyResolutionSucceeded)
            {
                return false;
            }

            if (policy.PrefersHumanMeat && !characteristics.ContainsHumanMeatIngredient)
            {
                reason = "hard_food_type_blocks_non_human_meat_printer";
                return true;
            }

            if (policy.PrefersMeat && (characteristics.PredictedFoodTypes & FoodTypeFlags.Meat) == FoodTypeFlags.None)
            {
                reason = "hard_food_type_blocks_non_meat_printer";
                return true;
            }

            return false;
        }

        private static bool IsHardFoodTypeCheckEnabled()
        {
            return FoodPrinterSystemMod.Settings == null || FoodPrinterSystemMod.Settings.HardCheckFoodType;
        }

        private static Thing BuildPredictedMealPreview(ThingDef mealDef, PrinterFoodCharacteristics characteristics)
        {
            Thing previewMeal = ThingMaker.MakeThing(mealDef);
            CompIngredients compIngredients = previewMeal.TryGetComp<CompIngredients>();
            if (compIngredients != null)
            {
                compIngredients.ingredients = new List<ThingDef>();
                if (characteristics != null && characteristics.PredictedIngredientDefs != null)
                {
                    for (int i = 0; i < characteristics.PredictedIngredientDefs.Count; i++)
                    {
                        ThingDef ingredientDef = characteristics.PredictedIngredientDefs[i];
                        if (ingredientDef != null && !compIngredients.ingredients.Contains(ingredientDef))
                        {
                            compIngredients.ingredients.Add(ingredientDef);
                        }
                    }
                }
            }

            return previewMeal;
        }

        private static FoodKind DetermineFoodKind(bool hasMeat, bool hasNonMeat)
        {
            if (hasMeat && hasNonMeat)
            {
                return FoodKind.Any;
            }

            if (hasMeat)
            {
                return FoodKind.Meat;
            }

            if (hasNonMeat)
            {
                return FoodKind.NonMeat;
            }

            return FoodKind.Any;
        }

        private static bool PawnHasCannibalTrait(Pawn pawn)
        {
            if (pawn == null || pawn.story == null || pawn.story.traits == null)
            {
                return false;
            }

            TraitDef cannibalTraitDef = GetCannibalTraitDef();
            return cannibalTraitDef != null && pawn.story.traits.HasTrait(cannibalTraitDef);
        }

        private static TraitDef GetCannibalTraitDef()
        {
            if (!cachedCannibalTraitResolved)
            {
                cachedCannibalTraitDef = DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
                cachedCannibalTraitResolved = true;
            }

            return cachedCannibalTraitDef;
        }

        private static void LogPolicyResolution(Pawn pawn, PawnPrinterFoodPolicy policy)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            Log.Message("[FPS] Resolved pawn food policy for " + GetPawnDebugLabel(pawn) + ": "
                + (policy == null ? "null" : policy.ToDebugString()));
        }

        private static void LogResolutionFallback(string source, Pawn pawn, Exception ex)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            Log.Message("[FPS] Pawn food policy resolution fell back to neutral for " + GetPawnDebugLabel(pawn)
                + " while resolving " + source + ": " + ex.Message);
        }

        private static void LogPrinterAllowance(Pawn pawn, Building_FoodPrinter printer, PawnPrinterFoodPolicy policy, bool allowed, string reason)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            PrinterFoodCharacteristics characteristics = printer == null ? null : GetPredictedFoodCharacteristics(printer);
            Log.Message("[FPS] Printer hard allow check for " + GetPawnDebugLabel(pawn)
                + " -> " + GetPrinterDebugLabel(printer)
                + ": allowed=" + allowed
                + ", reason=" + reason
                + ", policy=" + (policy == null ? "null" : policy.ToDebugString())
                + ", hardCheckFoodType=" + IsHardFoodTypeCheckEnabled()
                + ", foodTypes=" + (characteristics == null ? FoodTypeFlags.None.ToString() : characteristics.PredictedFoodTypes.ToString()));
        }

        private static void LogPrinterPreference(Pawn pawn, Building_FoodPrinter printer, PawnPrinterFoodPolicy policy, float score, string modifiers)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            Log.Message("[FPS] Printer preference score for " + GetPawnDebugLabel(pawn)
                + " -> " + GetPrinterDebugLabel(printer)
                + ": score=" + score.ToString("0.##")
                + ", modifiers=" + modifiers
                + ", hardCheckFoodType=" + IsHardFoodTypeCheckEnabled()
                + ", policy=" + (policy == null ? "null" : policy.ToDebugString()));
        }

        private static bool ShouldDebugLog()
        {
            return Prefs.DevMode;
        }

        private static string GetPawnDebugLabel(Pawn pawn)
        {
            return pawn == null ? "null" : pawn.LabelShortCap + " (" + pawn.thingIDNumber + ")";
        }

        private static string GetPrinterDebugLabel(Building_FoodPrinter printer)
        {
            return printer == null ? "null" : printer.LabelShortCap + " (" + printer.thingIDNumber + ")";
        }
    }
}
