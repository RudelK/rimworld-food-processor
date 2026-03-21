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
            public int CharacteristicsRevision;
            public PrinterFoodCharacteristics Characteristics;
        }

        private sealed class CachedPawnPrinterFoodPolicy
        {
            public int ExpiresAtTick;
            public PawnPrinterFoodPolicy Policy;
        }

        private struct MealAllowanceCacheKey
        {
            public int PawnId;
            public int PrinterId;
            public int MealDefId;
            public int FoodPolicyId;
            public int CharacteristicsRevision;
            public int SettingsRevision;
            public bool HardFoodTypeCheckEnabled;

            public override int GetHashCode()
            {
                int hash = PawnId;
                hash = Gen.HashCombineInt(hash, PrinterId);
                hash = Gen.HashCombineInt(hash, MealDefId);
                hash = Gen.HashCombineInt(hash, FoodPolicyId);
                hash = Gen.HashCombineInt(hash, CharacteristicsRevision);
                hash = Gen.HashCombineInt(hash, SettingsRevision);
                return Gen.HashCombineInt(hash, HardFoodTypeCheckEnabled ? 1 : 0);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is MealAllowanceCacheKey other))
                {
                    return false;
                }

                return PawnId == other.PawnId
                    && PrinterId == other.PrinterId
                    && MealDefId == other.MealDefId
                    && FoodPolicyId == other.FoodPolicyId
                    && CharacteristicsRevision == other.CharacteristicsRevision
                    && SettingsRevision == other.SettingsRevision
                    && HardFoodTypeCheckEnabled == other.HardFoodTypeCheckEnabled;
            }
        }

        private sealed class CachedMealAllowanceVerdict
        {
            public int ExpiresAtTick;
            public bool Allowed;
            public string Reason;
        }

        private static readonly Dictionary<int, CachedPrinterFoodCharacteristics> CachedCharacteristicsByPrinterId = new Dictionary<int, CachedPrinterFoodCharacteristics>();
        private static readonly Dictionary<int, CachedPawnPrinterFoodPolicy> CachedPolicyByPawnId = new Dictionary<int, CachedPawnPrinterFoodPolicy>();
        private static readonly Dictionary<MealAllowanceCacheKey, CachedMealAllowanceVerdict> CachedMealAllowanceByKey = new Dictionary<MealAllowanceCacheKey, CachedMealAllowanceVerdict>();
        private const int PawnPolicyCacheDurationTicks = 30;
        private const int PawnPolicyCachePruneIntervalTicks = 120;
        private const int PawnPolicyCachePruneThreshold = 256;
        private const int MealAllowanceCacheDurationTicks = 30;
        private const int MealAllowanceCachePruneIntervalTicks = 120;
        private const int MealAllowanceCachePruneThreshold = 256;
        private static int lastPawnPolicyCachePruneTick;
        private static int lastMealAllowanceCachePruneTick;
        private static TraitDef cachedCannibalTraitDef;
        private static bool cachedCannibalTraitResolved;

        public static bool CanPawnUsePrinter(Pawn pawn)
        {
            return pawn != null
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike
                && pawn.needs != null
                && pawn.needs.food != null;
        }

        public static PrinterFoodCharacteristics GetPredictedFoodCharacteristics(Building_FoodPrinter printer)
        {
            if (printer == null || printer.Map == null)
            {
                return new PrinterFoodCharacteristics(FoodTypeFlags.None, new List<ThingDef>(), FoodKind.Any, false, false);
            }

            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(printer.Map);
            int characteristicsRevision = networkComponent == null
                ? 0
                : unchecked((networkComponent.NetworkRevision * 397) ^ networkComponent.IngredientRevision);
            // Cache by combined toner network topology/content revision so both
            // pipe connectivity changes and ingredient/storage mutations invalidate
            // stale printer food predictions without recomputing every search.
            CachedPrinterFoodCharacteristics cachedCharacteristics;
            if (CachedCharacteristicsByPrinterId.TryGetValue(printer.thingIDNumber, out cachedCharacteristics)
                && cachedCharacteristics != null
                && cachedCharacteristics.CharacteristicsRevision == characteristicsRevision
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
            HashSet<ThingDef> seenIngredients = new HashSet<ThingDef>();
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
                    if (ingredientDef == null || ingredientDef.ingestible == null || !seenIngredients.Add(ingredientDef))
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
                CharacteristicsRevision = characteristicsRevision,
                Characteristics = characteristics
            };
            return characteristics;
        }

        public static PawnPrinterFoodPolicy ResolvePawnFoodPolicy(Pawn pawn)
        {
            if (pawn != null)
            {
                int currentTick = GetCurrentTick();
                PrunePawnPolicyCacheIfNeeded(currentTick);
                if (CachedPolicyByPawnId.TryGetValue(pawn.thingIDNumber, out CachedPawnPrinterFoodPolicy cachedPolicy)
                    && cachedPolicy != null
                    && cachedPolicy.ExpiresAtTick > currentTick
                    && cachedPolicy.Policy != null)
                {
                    return cachedPolicy.Policy;
                }
            }

            PawnPrinterFoodPolicy policy = new PawnPrinterFoodPolicy();
            if (pawn == null)
            {
                policy.UsedNeutralFallback = true;
                LogPolicyResolution(pawn, policy);
                return policy;
            }

            if (!CanPawnUsePrinter(pawn))
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
            if (pawn != null)
            {
                CachedPolicyByPawnId[pawn.thingIDNumber] = new CachedPawnPrinterFoodPolicy
                {
                    ExpiresAtTick = GetCurrentTick() + PawnPolicyCacheDurationTicks,
                    Policy = policy
                };
            }
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
            return IsPrinterAllowedForPawn(policy, pawn, printer);
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
            if (pawn == null || printer == null)
            {
                LogPrinterAllowance(pawn, printer, policy, false, "missing_pawn_or_printer");
                return false;
            }

            if (!CanPawnUsePrinter(pawn))
            {
                LogPrinterAllowance(pawn, printer, policy, false, "pawn_cannot_use_printer");
                return false;
            }

            PrinterFoodCharacteristics characteristics = GetPredictedFoodCharacteristics(printer);
            if (TryGetHardFoodTypeBlockReason(policy, characteristics, out string hardBlockReason))
            {
                LogPrinterAllowance(pawn, printer, policy, false, hardBlockReason);
                return false;
            }

            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            if (printerComp == null)
            {
                LogPrinterAllowance(pawn, printer, policy, false, "printer_has_no_valid_meal");
                return false;
            }

            ThingDef processingMeal = printerComp.ProcessingMealDef;
            if (processingMeal != null)
            {
                if (!printerComp.IsMealResearched(processingMeal))
                {
                    LogPrinterAllowance(pawn, printer, policy, false, "printer_has_no_valid_meal");
                    return false;
                }

                bool allowed = IsMealAllowedForPawn(policy, pawn, printer, processingMeal, false, characteristics, out string reason);
                LogPrinterAllowance(pawn, printer, policy, allowed, reason);
                return allowed;
            }

            bool hasConsumableMeal = printerComp.HasConsumableMealForPawn(printer, policy, pawn);
            LogPrinterAllowance(pawn, printer, policy, hasConsumableMeal, hasConsumableMeal ? "consumable_meal_available" : "printer_has_no_valid_meal");
            return hasConsumableMeal;
        }

        public static bool CanPawnConsumePrinterMeal(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer)
        {
            return IsPrinterAllowedForPawn(policy, pawn, printer);
        }

        public static bool IsMealAllowedForPawn(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef);
        }

        public static bool IsMealAllowedForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef, true, null, out _);
        }

        public static bool CanPawnConsumeMeal(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            PawnPrinterFoodPolicy policy = ResolvePawnFoodPolicy(pawn);
            return CanPawnConsumeMeal(policy, pawn, printer, mealDef);
        }

        public static bool CanPawnConsumeMeal(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef, true, null, out _);
        }

        internal static bool TryDiagnoseMealAllowance(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, out string reason)
        {
            return IsMealAllowedForPawn(policy, pawn, printer, mealDef, false, null, out reason);
        }

        internal static bool TryDiagnoseFoodPolicyAllowance(Pawn pawn, ThingDef mealDef, out string reason)
        {
            if (mealDef == null)
            {
                reason = "missing_meal_def";
                return false;
            }

            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;
            if (currentPolicy != null && !currentPolicy.Allows(mealDef))
            {
                reason = "food_policy_blocks_meal_def";
                return false;
            }

            reason = "food_policy_allows_meal_def";
            return true;
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

            if (!CanPawnUsePrinter(pawn))
            {
                return null;
            }

            return printerComp.GetMealToPrint(printer, policy, pawn, FoodPrinterSystemUtility.RandomMealSelectionEnabled);
        }

        private static bool IsMealAllowedForPawn(PawnPrinterFoodPolicy policy, Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, bool logResult, PrinterFoodCharacteristics characteristics, out string reason)
        {
            if (pawn == null || printer == null || mealDef == null)
            {
                reason = "missing_pawn_printer_or_meal";
                if (logResult)
                {
                    LogMealAllowance(pawn, printer, mealDef, policy, false, reason);
                }

                return false;
            }

            CompFoodPrinter printerComp = printer.FoodPrinterComp;
            if (printerComp == null || !printerComp.IsMealResearched(mealDef))
            {
                reason = "printer_has_no_valid_meal";
                if (logResult)
                {
                    LogMealAllowance(pawn, printer, mealDef, policy, false, reason);
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
                    LogMealAllowance(pawn, printer, mealDef, policy, false, reason);
                }

                return false;
            }

            if (!TryGetCachedMealAllowanceVerdict(pawn, printer, mealDef, characteristics, out bool allowed, out string cachedReason))
            {
                reason = "vanilla_will_eat_blocks_meal";
                if (logResult)
                {
                    LogMealAllowance(pawn, printer, mealDef, policy, false, reason);
                }

                return false;
            }

            reason = cachedReason;
            if (logResult)
            {
                LogMealAllowance(pawn, printer, mealDef, policy, allowed, reason);
            }

            return allowed;
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
            bool shouldDebugLog = ShouldDebugLog();

            // Soft preference only. This never overrides the hard allow/deny rules in
            // IsPrinterAllowedForPawn; it only ranks printers that are already usable.
            float score = 0f;
            List<string> modifiers = shouldDebugLog ? new List<string>() : null;
            if (policy != null && policy.PrefersHumanMeat && hasHumanMeat)
            {
                score += 2f;
                modifiers?.Add("human_meat:+2");
            }

            if (policy != null && policy.PrefersMeat)
            {
                if (hasMeat)
                {
                    score += 1f;
                    modifiers?.Add("meat:+1");
                }
                else
                {
                    score -= 0.5f;
                    modifiers?.Add("no_meat:-0.5");
                }
            }

            if (policy != null && policy.RequiresVegetarianFood && !hasVegetarianForbiddenIngredients)
            {
                score += 0.5f;
                modifiers?.Add("vegetarian_safe:+0.5");
            }

            LogPrinterPreference(pawn, printer, policy, score, modifiers == null || modifiers.Count == 0 ? "none" : string.Join(", ", modifiers.ToArray()));
            return score;
        }

        private static bool TryGetCachedMealAllowanceVerdict(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, PrinterFoodCharacteristics characteristics, out bool allowed, out string reason)
        {
            MealAllowanceCacheKey cacheKey = CreateMealAllowanceCacheKey(pawn, printer, mealDef);
            int currentTick = GetCurrentTick();
            PruneMealAllowanceCacheIfNeeded(currentTick);
            if (CachedMealAllowanceByKey.TryGetValue(cacheKey, out CachedMealAllowanceVerdict cachedVerdict)
                && cachedVerdict != null
                && cachedVerdict.ExpiresAtTick > currentTick)
            {
                allowed = cachedVerdict.Allowed;
                reason = cachedVerdict.Reason;
                return allowed;
            }

            allowed = EvaluateVanillaMealAllowance(pawn, printer, mealDef, characteristics);
            reason = allowed
                ? GetVanillaMealAllowedReason(printer, mealDef)
                : "vanilla_will_eat_blocks_meal";
            CachedMealAllowanceByKey[cacheKey] = new CachedMealAllowanceVerdict
            {
                ExpiresAtTick = currentTick + MealAllowanceCacheDurationTicks,
                Allowed = allowed,
                Reason = reason
            };
            return allowed;
        }

        internal static void NotifyPrinterDespawned(Building_FoodPrinter printer)
        {
            if (printer == null)
            {
                return;
            }

            CachedCharacteristicsByPrinterId.Remove(printer.thingIDNumber);

            if (CachedMealAllowanceByKey.Count == 0)
            {
                return;
            }

            List<MealAllowanceCacheKey> printerKeys = null;
            foreach (KeyValuePair<MealAllowanceCacheKey, CachedMealAllowanceVerdict> pair in CachedMealAllowanceByKey)
            {
                if (pair.Key.PrinterId != printer.thingIDNumber)
                {
                    continue;
                }

                if (printerKeys == null)
                {
                    printerKeys = new List<MealAllowanceCacheKey>();
                }

                printerKeys.Add(pair.Key);
            }

            if (printerKeys == null)
            {
                return;
            }

            for (int i = 0; i < printerKeys.Count; i++)
            {
                CachedMealAllowanceByKey.Remove(printerKeys[i]);
            }
        }

        private static bool EvaluateVanillaMealAllowance(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, PrinterFoodCharacteristics characteristics)
        {
            if (pawn == null || mealDef == null)
            {
                return false;
            }

            try
            {
                Thing previewMeal = BuildPredictedMealPreview(mealDef, characteristics);
                LogMealPreviewEvaluation(pawn, printer, mealDef, previewMeal, characteristics, null, "before_will_eat");
                bool allowed = FoodUtility.WillEat(pawn, previewMeal, pawn, false, false);
                LogMealPreviewEvaluation(pawn, printer, mealDef, previewMeal, characteristics, allowed, "after_will_eat");
                return allowed;
            }
            catch (Exception ex)
            {
                LogMealPreviewException(pawn, printer, mealDef, characteristics, ex);
                LogResolutionFallback("vanilla_will_eat", pawn, ex);
                return true;
            }
        }

        private static string GetVanillaMealAllowedReason(Building_FoodPrinter printer, ThingDef mealDef)
        {
            if (printer == null || mealDef == null)
            {
                return "vanilla_meal_check_allowed";
            }

            CompFoodPrinter comp = printer.FoodPrinterComp;
            return comp != null && !comp.IsBaseConfiguredMeal(mealDef)
                ? "vanilla_meal_check_allowed_mod_meal"
                : "vanilla_meal_check_allowed";
        }

        private static MealAllowanceCacheKey CreateMealAllowanceCacheKey(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef)
        {
            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;
            return new MealAllowanceCacheKey
            {
                PawnId = pawn == null ? 0 : pawn.thingIDNumber,
                PrinterId = printer == null ? 0 : printer.thingIDNumber,
                MealDefId = mealDef == null ? 0 : mealDef.shortHash,
                FoodPolicyId = GetFoodPolicyCacheId(currentPolicy),
                CharacteristicsRevision = printer == null ? 0 : GetPrinterCharacteristicsRevisionForCaching(printer),
                SettingsRevision = FoodPrinterSystemMod.SettingsRevision,
                HardFoodTypeCheckEnabled = IsHardFoodTypeCheckEnabled()
            };
        }

        private static int GetFoodPolicyCacheId(FoodPolicy policy)
        {
            if (policy == null)
            {
                return 0;
            }

            string uniqueId = policy.GetUniqueLoadID();
            return uniqueId.NullOrEmpty() ? policy.label.GetHashCode() : uniqueId.GetHashCode();
        }

        internal static int GetPolicyStateHash(PawnPrinterFoodPolicy policy)
        {
            if (policy == null)
            {
                return 0;
            }

            int hash = 17;
            hash = Gen.HashCombineInt(hash, policy.IdeologyResolutionSucceeded ? 1 : 0);
            hash = Gen.HashCombineInt(hash, policy.TraitResolutionSucceeded ? 1 : 0);
            hash = Gen.HashCombineInt(hash, policy.UsedNeutralFallback ? 1 : 0);
            hash = Gen.HashCombineInt(hash, policy.RequiresVegetarianFood ? 1 : 0);
            hash = Gen.HashCombineInt(hash, policy.PrefersMeat ? 1 : 0);
            return Gen.HashCombineInt(hash, policy.PrefersHumanMeat ? 1 : 0);
        }

        internal static int GetPrinterCharacteristicsRevisionForCaching(Building_FoodPrinter printer)
        {
            if (printer == null || printer.Map == null)
            {
                return 0;
            }

            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(printer.Map);
            return networkComponent == null
                ? 0
                : unchecked((networkComponent.NetworkRevision * 397) ^ networkComponent.IngredientRevision);
        }

        private static int GetCurrentTick()
        {
            return Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
        }

        private static void PruneMealAllowanceCacheIfNeeded(int currentTick)
        {
            if (CachedMealAllowanceByKey.Count == 0)
            {
                lastMealAllowanceCachePruneTick = currentTick;
                return;
            }

            if (CachedMealAllowanceByKey.Count <= MealAllowanceCachePruneThreshold
                && currentTick - lastMealAllowanceCachePruneTick < MealAllowanceCachePruneIntervalTicks)
            {
                return;
            }

            List<MealAllowanceCacheKey> expiredKeys = null;
            foreach (KeyValuePair<MealAllowanceCacheKey, CachedMealAllowanceVerdict> pair in CachedMealAllowanceByKey)
            {
                CachedMealAllowanceVerdict verdict = pair.Value;
                if (verdict == null || verdict.ExpiresAtTick <= currentTick)
                {
                    if (expiredKeys == null)
                    {
                        expiredKeys = new List<MealAllowanceCacheKey>();
                    }

                    expiredKeys.Add(pair.Key);
                }
            }

            if (expiredKeys != null)
            {
                for (int i = 0; i < expiredKeys.Count; i++)
                {
                    CachedMealAllowanceByKey.Remove(expiredKeys[i]);
                }
            }

            lastMealAllowanceCachePruneTick = currentTick;
        }

        private static void PrunePawnPolicyCacheIfNeeded(int currentTick)
        {
            if (CachedPolicyByPawnId.Count == 0)
            {
                lastPawnPolicyCachePruneTick = currentTick;
                return;
            }

            if (CachedPolicyByPawnId.Count <= PawnPolicyCachePruneThreshold
                && currentTick - lastPawnPolicyCachePruneTick < PawnPolicyCachePruneIntervalTicks)
            {
                return;
            }

            List<int> expiredPawnIds = null;
            foreach (KeyValuePair<int, CachedPawnPrinterFoodPolicy> pair in CachedPolicyByPawnId)
            {
                CachedPawnPrinterFoodPolicy cachedPolicy = pair.Value;
                if (cachedPolicy == null || cachedPolicy.ExpiresAtTick <= currentTick)
                {
                    if (expiredPawnIds == null)
                    {
                        expiredPawnIds = new List<int>();
                    }

                    expiredPawnIds.Add(pair.Key);
                }
            }

            if (expiredPawnIds != null)
            {
                for (int i = 0; i < expiredPawnIds.Count; i++)
                {
                    CachedPolicyByPawnId.Remove(expiredPawnIds[i]);
                }
            }

            lastPawnPolicyCachePruneTick = currentTick;
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

        private static void LogMealAllowance(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, PawnPrinterFoodPolicy policy, bool allowed, string reason)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            PrinterFoodCharacteristics characteristics = printer == null ? null : GetPredictedFoodCharacteristics(printer);
            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;
            Log.Message("[FPS] Printer meal allow check for " + GetPawnDebugLabel(pawn)
                + " -> " + GetPrinterDebugLabel(printer)
                + ": meal=" + GetMealDebugLabel(mealDef)
                + ", mealLabel=" + GetMealLabelDebug(mealDef)
                + ", mealOrigin=" + GetMealOriginDebugLabel(printer, mealDef)
                + ", mealCategory=" + GetMealCategoryDebugLabel(mealDef)
                + ", allowed=" + allowed
                + ", reason=" + reason
                + ", foodPolicy=" + GetFoodPolicyDebugLabel(currentPolicy)
                + ", policy=" + (policy == null ? "null" : policy.ToDebugString())
                + ", hardCheckFoodType=" + IsHardFoodTypeCheckEnabled()
                + ", foodTypes=" + (characteristics == null ? FoodTypeFlags.None.ToString() : characteristics.PredictedFoodTypes.ToString()));
        }

        private static void LogMealPreviewEvaluation(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, Thing previewMeal, PrinterFoodCharacteristics characteristics, bool? allowed, string stage)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;

            Log.Message("[FPS] Printer meal preview " + stage + " for " + GetPawnDebugLabel(pawn)
                + " -> " + GetPrinterDebugLabel(printer)
                + ": allowed=" + (allowed.HasValue ? allowed.Value.ToString() : "pending")
                + ", foodPolicy=" + GetFoodPolicyDebugLabel(currentPolicy)
                + ", policyAllowsMealDef=" + GetFoodPolicyAllowsMealDefDebugLabel(currentPolicy, mealDef)
                + ", meal=" + GetMealDebugLabel(mealDef)
                + ", mealDetail={" + GetMealFullDebug(mealDef) + "}"
                + ", previewDetail={" + GetPreviewMealFullDebug(previewMeal) + "}"
                + ", predictedCharacteristics={" + GetPredictedCharacteristicsDebug(characteristics) + "}");
        }

        private static void LogMealPreviewException(Pawn pawn, Building_FoodPrinter printer, ThingDef mealDef, PrinterFoodCharacteristics characteristics, Exception ex)
        {
            if (!ShouldDebugLog())
            {
                return;
            }

            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;

            Log.Message("[FPS] Printer meal preview exception for " + GetPawnDebugLabel(pawn)
                + " -> " + GetPrinterDebugLabel(printer)
                + ": foodPolicy=" + GetFoodPolicyDebugLabel(currentPolicy)
                + ", policyAllowsMealDef=" + GetFoodPolicyAllowsMealDefDebugLabel(currentPolicy, mealDef)
                + ": meal=" + GetMealDebugLabel(mealDef)
                + ", mealDetail={" + GetMealFullDebug(mealDef) + "}"
                + ", predictedCharacteristics={" + GetPredictedCharacteristicsDebug(characteristics) + "}"
                + ", exception=" + ex.GetType().Name
                + ", message=" + ex.Message);
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
            return FoodPrinterSystemMod.Settings != null && FoodPrinterSystemMod.Settings.DebugLoggingEnabled;
        }

        private static string GetPawnDebugLabel(Pawn pawn)
        {
            return pawn == null ? "null" : pawn.LabelShortCap + " (" + pawn.thingIDNumber + ")";
        }

        private static string GetPrinterDebugLabel(Building_FoodPrinter printer)
        {
            return printer == null ? "null" : printer.LabelShortCap + " (" + printer.thingIDNumber + ")";
        }

        private static string GetMealDebugLabel(ThingDef mealDef)
        {
            return mealDef == null ? "null" : mealDef.defName;
        }

        private static string GetMealLabelDebug(ThingDef mealDef)
        {
            return mealDef == null ? "null" : mealDef.LabelCap.ToString();
        }

        private static string GetMealOriginDebugLabel(Building_FoodPrinter printer, ThingDef mealDef)
        {
            if (printer == null || mealDef == null)
            {
                return "unknown";
            }

            CompFoodPrinter comp = printer.FoodPrinterComp;
            return comp != null && comp.IsBaseConfiguredMeal(mealDef) ? "base" : "discovered";
        }

        private static string GetMealCategoryDebugLabel(ThingDef mealDef)
        {
            if (mealDef == null || mealDef.ingestible == null)
            {
                return "none";
            }

            if (mealDef.defName == "MealNutrientPaste")
            {
                return FoodPreferability.MealAwful.ToString();
            }

            return mealDef.ingestible.preferability.ToString();
        }

        private static string GetFoodPolicyDebugLabel(FoodPolicy policy)
        {
            if (policy == null)
            {
                return "null";
            }

            return policy.label.NullOrEmpty() ? policy.GetUniqueLoadID() : policy.label;
        }

        private static string GetFoodPolicyAllowsMealDefDebugLabel(FoodPolicy policy, ThingDef mealDef)
        {
            if (policy == null)
            {
                return "no_policy";
            }

            if (mealDef == null)
            {
                return "no_meal_def";
            }

            try
            {
                return policy.Allows(mealDef).ToString();
            }
            catch (Exception ex)
            {
                return "error:" + ex.GetType().Name + ":" + ex.Message;
            }
        }

        private static string GetMealFullDebug(ThingDef mealDef)
        {
            if (mealDef == null)
            {
                return "null";
            }

            IngestibleProperties ingestible = mealDef.ingestible;
            return "defName=" + mealDef.defName
                + ", label=" + GetMealLabelDebug(mealDef)
                + ", origin=" + GetMealDefSourceDebugLabel(mealDef)
                + ", thingClass=" + GetTypeDebugLabel(mealDef.thingClass)
                + ", category=" + GetMealCategoryDebugLabel(mealDef)
                + ", stackLimit=" + mealDef.stackLimit
                + ", isNutritionGiving=" + mealDef.IsNutritionGivingIngestible
                + ", foodType=" + (ingestible == null ? FoodTypeFlags.None.ToString() : ingestible.foodType.ToString())
                + ", preferability=" + (ingestible == null ? "none" : ingestible.preferability.ToString())
                + ", drugCategory=" + (ingestible == null ? "none" : ingestible.drugCategory.ToString())
                + ", joyKind=" + (ingestible == null || ingestible.joyKind == null ? "none" : ingestible.joyKind.defName)
                + ", compProps=" + GetCompPropsDebugLabel(mealDef.comps);
        }

        private static string GetPreviewMealFullDebug(Thing previewMeal)
        {
            if (previewMeal == null)
            {
                return "null";
            }

            ThingDef previewDef = previewMeal.def;
            CompIngredients compIngredients = previewMeal.TryGetComp<CompIngredients>();
            return "defName=" + (previewDef == null ? "null" : previewDef.defName)
                + ", label=" + (previewMeal.LabelCap == null ? "null" : previewMeal.LabelCap.ToString())
                + ", thingClass=" + GetTypeDebugLabel(previewMeal.GetType())
                + ", defThingClass=" + GetTypeDebugLabel(previewDef == null ? null : previewDef.thingClass)
                + ", stackCount=" + previewMeal.stackCount
                + ", stuff=" + (previewMeal.Stuff == null ? "null" : previewMeal.Stuff.defName)
                + ", hitPoints=" + previewMeal.HitPoints
                + ", comps=" + GetThingCompDebugLabel(previewMeal)
                + ", ingredientCompPresent=" + (compIngredients != null)
                + ", ingredientDefs=" + GetIngredientListDebugLabel(compIngredients == null ? null : compIngredients.ingredients)
                + ", sourceDefInfo={" + GetMealFullDebug(previewDef) + "}";
        }

        private static string GetPredictedCharacteristicsDebug(PrinterFoodCharacteristics characteristics)
        {
            if (characteristics == null)
            {
                return "null";
            }

            return "foodTypes=" + characteristics.PredictedFoodTypes
                + ", sourceFoodKind=" + characteristics.PredictedSourceFoodKind
                + ", containsVegetarianForbiddenIngredients=" + characteristics.ContainsVegetarianForbiddenIngredients
                + ", containsHumanMeatIngredient=" + characteristics.ContainsHumanMeatIngredient
                + ", ingredientDefs=" + GetIngredientListDebugLabel(characteristics.PredictedIngredientDefs);
        }

        private static string GetIngredientListDebugLabel(List<ThingDef> ingredientDefs)
        {
            if (ingredientDefs == null || ingredientDefs.Count == 0)
            {
                return "[]";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < ingredientDefs.Count; i++)
            {
                ThingDef ingredientDef = ingredientDefs[i];
                if (ingredientDef == null)
                {
                    parts.Add("null");
                    continue;
                }

                parts.Add(ingredientDef.defName + "(" + ingredientDef.LabelCap + ")");
            }

            return "[" + string.Join(", ", parts.ToArray()) + "]";
        }

        private static string GetMealDefSourceDebugLabel(ThingDef mealDef)
        {
            if (mealDef == null || mealDef.modContentPack == null)
            {
                return "unknown";
            }

            return mealDef.modContentPack.Name;
        }

        private static string GetCompPropsDebugLabel(List<CompProperties> compProps)
        {
            if (compProps == null || compProps.Count == 0)
            {
                return "[]";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < compProps.Count; i++)
            {
                CompProperties compProp = compProps[i];
                names.Add(compProp == null ? "null" : GetTypeDebugLabel(compProp.GetType()));
            }

            return "[" + string.Join(", ", names.ToArray()) + "]";
        }

        private static string GetThingCompDebugLabel(Thing thing)
        {
            ThingWithComps thingWithComps = thing as ThingWithComps;
            if (thingWithComps == null || thingWithComps.AllComps == null || thingWithComps.AllComps.Count == 0)
            {
                return "[]";
            }

            List<string> names = new List<string>();
            for (int i = 0; i < thingWithComps.AllComps.Count; i++)
            {
                ThingComp comp = thingWithComps.AllComps[i];
                names.Add(comp == null ? "null" : GetTypeDebugLabel(comp.GetType()));
            }

            return "[" + string.Join(", ", names.ToArray()) + "]";
        }

        private static string GetTypeDebugLabel(Type type)
        {
            return type == null ? "null" : type.FullName;
        }
    }
}
