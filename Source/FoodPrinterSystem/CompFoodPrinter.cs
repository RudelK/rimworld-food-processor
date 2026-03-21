using System.Collections.Generic;
using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace FoodPrinterSystem
{
    public class CompProperties_FoodPrinter : CompProperties
    {
        public List<string> defaultMealDefs = new List<string>();

        public CompProperties_FoodPrinter()
        {
            compClass = typeof(CompFoodPrinter);
        }
    }

    [StaticConstructorOnStartup]
    public class CompFoodPrinter : ThingComp
    {
        private static readonly Dictionary<FoodPreferability, List<ThingDef>> DiscoveredMealsByCategory = new Dictionary<FoodPreferability, List<ThingDef>>();
        private static bool discoveredMealsInitialized;

        private sealed class CachedAffordableMeals
        {
            public int SettingsRevision;
            public int ResearchStateMask;
            public int AffordabilityRevision;
            public List<ThingDef> Meals;
        }

        private struct ConsumableMealsCacheKey
        {
            public int PrinterId;
            public int PawnId;
            public int FoodPolicyId;
            public int PolicyStateHash;
            public FoodPreferability Category;
            public int AffordabilityRevision;
            public int CharacteristicsRevision;
            public int SettingsRevision;
            public int ResearchStateMask;
            public bool HardFoodTypeCheckEnabled;

            public override int GetHashCode()
            {
                int hash = PrinterId;
                hash = Gen.HashCombineInt(hash, PawnId);
                hash = Gen.HashCombineInt(hash, FoodPolicyId);
                hash = Gen.HashCombineInt(hash, PolicyStateHash);
                hash = Gen.HashCombineInt(hash, (int)Category);
                hash = Gen.HashCombineInt(hash, AffordabilityRevision);
                hash = Gen.HashCombineInt(hash, CharacteristicsRevision);
                hash = Gen.HashCombineInt(hash, SettingsRevision);
                hash = Gen.HashCombineInt(hash, ResearchStateMask);
                return Gen.HashCombineInt(hash, HardFoodTypeCheckEnabled ? 1 : 0);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is ConsumableMealsCacheKey other))
                {
                    return false;
                }

                return PrinterId == other.PrinterId
                    && PawnId == other.PawnId
                    && FoodPolicyId == other.FoodPolicyId
                    && PolicyStateHash == other.PolicyStateHash
                    && Category == other.Category
                    && AffordabilityRevision == other.AffordabilityRevision
                    && CharacteristicsRevision == other.CharacteristicsRevision
                    && SettingsRevision == other.SettingsRevision
                    && ResearchStateMask == other.ResearchStateMask
                    && HardFoodTypeCheckEnabled == other.HardFoodTypeCheckEnabled;
            }
        }

        private sealed class CachedConsumableMeals
        {
            public int LastAccessTick;
            public List<ThingDef> Meals;
        }

        private static readonly FoodPreferability[] CategoryOrder =
        {
            FoodPreferability.MealAwful,
            FoodPreferability.MealSimple,
            FoodPreferability.MealFine,
            FoodPreferability.MealLavish
        };

        private static readonly Material ProcessingBarBackgroundMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.08f, 0.08f, 0.08f, 0.85f));
        private static readonly Material ProcessingBarFillMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.87f, 0.62f, 0.18f, 0.95f));

        private sealed class CategorySelectionContext
        {
            public FoodPreferability? RequestedCategory;
            public FoodPreferability? ResolvedCategory;
            public string FallbackReason;
        }

        private bool initialized;
        private bool autoMode = true;
        private string manualMealDefName;
        private List<string> disabledCategoryIds = new List<string>();
        private List<string> legacyEnabledCategoryIds;
        private List<string> legacyAllowedMealDefNames;
        private string processingMealDefName;
        private int processingTicksRemaining;
        private int processingTonerCost;
        private Pawn currentProcessingPawn;
        private string cachedManualMealDefName;
        private ThingDef cachedManualMealDef;
        private string cachedProcessingMealDefName;
        private ThingDef cachedProcessingMealDef;
        private readonly Dictionary<FoodPreferability, List<ThingDef>> configuredMealsByCategory = new Dictionary<FoodPreferability, List<ThingDef>>();
        private readonly Dictionary<FoodPreferability, CachedAffordableMeals> affordableMealsByCategory = new Dictionary<FoodPreferability, CachedAffordableMeals>();
        private readonly Dictionary<ConsumableMealsCacheKey, CachedConsumableMeals> consumableMealsByCategory = new Dictionary<ConsumableMealsCacheKey, CachedConsumableMeals>();
        private const int PendingMealSelectionRetentionTicks = 180;
        private const int ConsumableMealCacheRetentionTicks = 600;
        private const int ConsumableMealCachePruneIntervalTicks = 120;
        private const int ConsumableMealCachePruneThreshold = 128;
        private int lastConsumableMealCachePruneTick;
        private string pendingSelectedMealDefName;
        private int pendingSelectedMealPawnId;
        private int pendingSelectedMealFoodPolicyId;
        private int pendingSelectedMealPolicyStateHash;
        private int pendingSelectedMealAffordabilityRevision;
        private int pendingSelectedMealCharacteristicsRevision;
        private int pendingSelectedMealResearchStateMask;
        private int pendingSelectedMealSettingsRevision;
        private int pendingSelectedMealExpiresAtTick;

        public CompProperties_FoodPrinter Props
        {
            get { return (CompProperties_FoodPrinter)props; }
        }

        public bool AutoMode
        {
            get { return autoMode; }
            set { autoMode = value; }
        }

        public ThingDef ManualMealDef
        {
            get { return ResolveCachedMealDef(manualMealDefName, ref cachedManualMealDefName, ref cachedManualMealDef); }
        }

        public ThingDef ProcessingMealDef
        {
            get { return ResolveCachedMealDef(processingMealDefName, ref cachedProcessingMealDefName, ref cachedProcessingMealDef); }
        }

        public bool IsProcessing
        {
            get { return !processingMealDefName.NullOrEmpty(); }
        }

        public bool HasActiveProcessing
        {
            get { return IsProcessing && processingTicksRemaining > 0; }
        }

        public bool HasCompletedProcessing
        {
            get { return IsProcessing && processingTicksRemaining <= 0; }
        }

        public bool IsPrinting
        {
            get { return IsProcessing; }
        }

        public Pawn CurrentProcessingPawn
        {
            get { return currentProcessingPawn; }
        }

        public int ProcessingTonerCost
        {
            get { return processingTonerCost; }
        }


        public float ProcessingProgress
        {
            get
            {
                if (!IsProcessing)
                {
                    return 0f;
                }

                if (HasCompletedProcessing)
                {
                    return 1f;
                }

                return 1f - processingTicksRemaining / (float)FoodPrinterSystemUtility.PrintingDelayTicks;
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            EnsureInitialized();
            ApplyPowerSetting();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureInitialized();
            ApplyPowerSetting();
        }


        public override void PostDraw()
        {
            base.PostDraw();
            DrawProcessingBar();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "initialized", false);
            Scribe_Values.Look(ref autoMode, "autoMode", true);
            Scribe_Values.Look(ref manualMealDefName, "manualMealDefName");
            Scribe_Collections.Look(ref disabledCategoryIds, "disabledCategoryIds", LookMode.Value);
            Scribe_Values.Look(ref processingMealDefName, "processingMealDefName");
            Scribe_Values.Look(ref processingTicksRemaining, "processingTicksRemaining", 0);
            Scribe_Values.Look(ref processingTonerCost, "processingTonerCost", 0);
            Scribe_References.Look(ref currentProcessingPawn, "currentProcessingPawn");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Collections.Look(ref legacyEnabledCategoryIds, "enabledCategoryIds", LookMode.Value);
                Scribe_Collections.Look(ref legacyAllowedMealDefNames, "allowedMealDefNames", LookMode.Value);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                initialized = false;
                InvalidateMealCaches();
                EnsureInitialized();
                MigrateLegacyAllowedMeals();
                EnsureManualMealSelection();
                legacyAllowedMealDefNames = null;
                if (processingTicksRemaining < 0)
                {
                    processingTicksRemaining = 0;
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "FPS_PrinterSettings".Translate(),
                defaultDesc = "FPS_PrinterSettingsDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/foodprocess/printer_setting", true),
                iconDrawScale = 0.85f,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_FoodPrinterSettings(this));
                }
            };
        }

        public override string CompInspectStringExtra()
        {
            EnsureInitialized();
            string mode = autoMode ? "FPS_PrinterModeAuto".Translate() : "FPS_PrinterModeManual".Translate();
            string text = "FPS_PrinterMode".Translate(mode);
            string previewLabel = GetPreviewOutputLabel(parent as Building);
            text += "\n" + (!previewLabel.NullOrEmpty()
                ? "FPS_PrinterAvailableOutput".Translate(previewLabel)
                : "FPS_PrinterUnavailableOutput".Translate());

            if (IsProcessing)
            {
                string processingLabel = ProcessingMealDef == null ? "?" : ProcessingMealDef.LabelCap.ToString();
                text += "\n" + "FPS_PrinterProcessing".Translate(processingLabel, Mathf.RoundToInt(ProcessingProgress * 100f).ToString());
            }

            return text;
        }

        public List<FoodPreferability> GetKnownCategories()
        {
            EnsureInitialized();
            List<FoodPreferability> categories = new List<FoodPreferability>();
            for (int i = 0; i < CategoryOrder.Length; i++)
            {
                if (GetConfiguredMealsForCategory(CategoryOrder[i]).Count > 0)
                {
                    categories.Add(CategoryOrder[i]);
                }
            }

            return categories;
        }

        public bool IsCategoryEnabled(FoodPreferability category)
        {
            EnsureInitialized();
            return IsCategoryResearched(category) && !IsCategoryExplicitlyDisabled(category);
        }

        private bool IsCategoryExplicitlyDisabled(FoodPreferability category)
        {
            string categoryId = category.ToString();
            if (disabledCategoryIds == null)
            {
                return false;
            }

            for (int i = 0; i < disabledCategoryIds.Count; i++)
            {
                if (string.Equals(disabledCategoryIds[i], categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void SetCategoryEnabled(FoodPreferability category, bool enabled)
        {
            EnsureInitialized();
            string categoryId = category.ToString();
            if (enabled)
            {
                RemoveDisabledCategory(categoryId);
            }
            else
            {
                AddDisabledCategory(categoryId);
                FoodPreferability? manualCategory = GetCategoryForMealDef(ManualMealDef);
                if (manualCategory == category)
                {
                    ThingDef fallbackMeal = GetRepresentativeMealForCategory(GetHighestEnabledResearchedCategory());
                    manualMealDefName = fallbackMeal == null ? null : fallbackMeal.defName;
                }
            }
        }

        public bool IsCategoryResearched(FoodPreferability category)
        {
            return IsResearchFinished(GetResearchForCategory(category));
        }

        public bool IsMealResearched(ThingDef mealDef)
        {
            FoodPreferability? category = GetCategoryForMealDef(mealDef);
            return category != null && IsCategoryResearched(category.Value);
        }

        public ResearchProjectDef GetResearchForCategory(FoodPreferability category)
        {
            switch (category)
            {
                case FoodPreferability.MealAwful:
                    return FoodPrinterSystemDefOf.FPS_FoodProcessing;
                case FoodPreferability.MealSimple:
                    return FoodPrinterSystemDefOf.FPS_SimpleMealPrinting;
                case FoodPreferability.MealFine:
                    return FoodPrinterSystemDefOf.FPS_FineMealPrinting;
                case FoodPreferability.MealLavish:
                    return FoodPrinterSystemDefOf.FPS_LavishMealPrinting;
                default:
                    return null;
            }
        }

        public string GetCategoryLabel(FoodPreferability category)
        {
            switch (category)
            {
                case FoodPreferability.MealAwful:
                    return "FPS_PrinterCategoryPaste".Translate();
                case FoodPreferability.MealSimple:
                    return "FPS_PrinterCategorySimple".Translate();
                case FoodPreferability.MealFine:
                    return "FPS_PrinterCategoryFine".Translate();
                case FoodPreferability.MealLavish:
                    return "FPS_PrinterCategoryLavish".Translate();
                default:
                    return category.ToString();
            }
        }

        public string GetCategoryCostLabel(FoodPreferability category)
        {
            int printCost = FoodPrinterSystemUtility.GetCategoryPrintCost(category);
            return printCost > 0 ? FoodPrinterSystemUtility.FormatToner(printCost) : "-";
        }

        public string GetCategoryStatusLabel(FoodPreferability category)
        {
            if (IsCategoryResearched(category))
            {
                return "FPS_PrinterCategoryUnlockedRecipes".Translate(GetConfiguredMealsForCategory(category).Count.ToString());
            }

            ResearchProjectDef research = GetResearchForCategory(category);
            string researchLabel = research == null ? "?" : research.LabelCap.ToString();
            return "FPS_PrinterCategoryRequires".Translate(researchLabel);
        }

        public string GetCategoryTooltip(FoodPreferability category)
        {
            List<ThingDef> meals = GetConfiguredMealsForCategory(category);
            if (meals.Count == 0)
            {
                return GetCategoryLabel(category);
            }

            List<string> labels = new List<string>();
            for (int i = 0; i < meals.Count; i++)
            {
                labels.Add(meals[i].LabelCap.ToString());
            }

            return GetCategoryLabel(category) + "\n" + "FPS_PrinterTooltipOutputs".Translate(string.Join(", ", labels.ToArray()));
        }

        public FoodPreferability? GetSelectedManualCategory()
        {
            EnsureInitialized();
            FoodPreferability? category = GetCategoryForMealDef(ManualMealDef);
            if (category != null
                && IsCategoryEnabled(category.Value)
                && IsCategoryResearched(category.Value)
                && GetConfiguredMealsForCategory(category.Value).Count > 0)
            {
                return category;
            }

            return GetHighestEnabledResearchedCategory();
        }

        public void SetManualCategory(FoodPreferability category)
        {
            EnsureInitialized();
            if (!IsCategoryResearched(category))
            {
                return;
            }

            ThingDef representativeMeal = GetRepresentativeMealForCategory(category);
            if (representativeMeal == null)
            {
                return;
            }

            SetCategoryEnabled(category, true);
            manualMealDefName = representativeMeal.defName;
            autoMode = false;
        }

        public ThingDef GetPreviewMealDef(Building printer)
        {
            EnsureInitialized();
            if (IsProcessing)
            {
                return ProcessingMealDef;
            }

            CategorySelectionContext categorySelection = ResolveCategorySelection(printer);
            return GetMealForCategory(categorySelection, printer, false);
        }

        public string GetPreviewOutputLabel(Building printer)
        {
            EnsureInitialized();
            if (IsProcessing)
            {
                return ProcessingMealDef == null ? null : ProcessingMealDef.LabelCap.ToString();
            }

            CategorySelectionContext categorySelection = ResolveCategorySelection(printer);
            FoodPreferability? category = categorySelection == null ? null : categorySelection.ResolvedCategory;
            if (category == null)
            {
                return null;
            }

            List<ThingDef> meals = GetConfiguredMealsForCategory(category.Value);
            if (meals.Count > 1)
            {
                return GetCategoryLabel(category.Value);
            }

            ThingDef previewMeal = GetMealForCategory(categorySelection, printer, false);
            return previewMeal == null ? GetCategoryLabel(category.Value) : previewMeal.LabelCap.ToString();
        }

        public ThingDef GetMealToPrint(Building printer, bool randomize)
        {
            return GetMealToPrint(printer, null, null, randomize);
        }

        public ThingDef GetMealToPrint(Building printer, PawnPrinterFoodPolicy policy, Pawn eater, bool randomize)
        {
            return GetMealToPrint(printer, policy, eater, randomize, null);
        }

        private ThingDef GetMealToPrint(Building printer, PawnPrinterFoodPolicy policy, Pawn eater, bool randomize, CategorySelectionContext categorySelection)
        {
            EnsureInitialized();
            if (HasActiveProcessing)
            {
                return null;
            }

            if (HasCompletedProcessing)
            {
                return ProcessingMealDef;
            }

            CategorySelectionContext resolvedSelection = categorySelection
                ?? (eater == null ? ResolveCategorySelection(printer) : ResolveCategorySelection(printer, policy, eater));
            return eater == null
                ? GetMealForCategory(resolvedSelection, printer, randomize)
                : GetMealForCategory(resolvedSelection, printer, policy, eater, randomize);
        }

        public bool TryStartProcessing(Building printer, Pawn pawn)
        {
            return TryStartProcessing(printer, pawn, pawn);
        }

        public bool TryStartProcessing(Building printer, Pawn reservationPawn, Pawn eaterPawn)
        {
            EnsureInitialized();
            Building_FoodPrinter foodPrinter = printer as Building_FoodPrinter;
            PawnPrinterFoodPolicy eaterPolicy = foodPrinter == null || eaterPawn == null
                ? null
                : FoodPrinterPawnUtility.ResolvePawnFoodPolicy(eaterPawn);
            if (IsPrinting || IsBusyFor(reservationPawn))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, null, processingTonerCost, "printer_busy");
                return false;
            }

            if (foodPrinter != null && !FoodPrinterPawnUtility.IsPrinterAllowedForPawn(eaterPolicy, eaterPawn, foodPrinter))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, null, 0, "policy_blocked");
                return false;
            }

            CategorySelectionContext categorySelection = ResolveCategorySelection(printer, eaterPolicy, eaterPawn);
            LogCategorySelection(printer, eaterPawn, categorySelection);
            ThingDef mealDef = GetMealToPrint(printer, eaterPolicy, eaterPawn, true, categorySelection);
            if (mealDef == null || !IsMealResearched(mealDef))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, mealDef, 0, "no_valid_meal_selected", categorySelection);
                return false;
            }

            if (foodPrinter != null && !FoodPrinterPawnUtility.CanPawnConsumeMeal(eaterPolicy, eaterPawn, foodPrinter, mealDef))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, mealDef, 0, "selected_meal_policy_blocked", categorySelection);
                return false;
            }

            if (!TryReservePrinterForProcessing(printer, reservationPawn))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, null, 0, "reservation_failed", categorySelection);
                return false;
            }

            int tonerCost = FoodPrinterSystemUtility.GetPrintCost(mealDef);
            if (tonerCost <= 0 || !TonerPipeNetManager.CanDraw(printer, tonerCost))
            {
                ReleasePrinterReservation(printer, reservationPawn);
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, mealDef, tonerCost, "insufficient_toner", categorySelection);
                return false;
            }

            currentProcessingPawn = reservationPawn;
            processingMealDefName = mealDef.defName;
            processingTonerCost = tonerCost;
            processingTicksRemaining = FoodPrinterSystemUtility.PrintingDelayTicks;
            ClearPendingMealSelection();
            ApplyPowerSetting();
            LogProcessingEvent("printer_start_succeeded", printer, reservationPawn, eaterPawn, mealDef, tonerCost, "started", categorySelection);
            return true;
        }

        public void UpdateProcessingTicksRemaining(int ticksRemaining)
        {
            if (!IsProcessing)
            {
                return;
            }

            processingTicksRemaining = Mathf.Clamp(ticksRemaining, 0, FoodPrinterSystemUtility.PrintingDelayTicks);
            ApplyPowerSetting();
        }

        public Thing CompleteProcessing(Building printer, Pawn pawn)
        {
            return CompleteProcessing(printer, pawn, pawn);
        }

        public Thing CompleteProcessing(Building printer, Pawn reservationPawn, Pawn eaterPawn)
        {
            if (!HasCompletedProcessing)
            {
                LogProcessingEvent("printer_complete_failed", printer, reservationPawn, eaterPawn, ProcessingMealDef, processingTonerCost, "processing_not_complete");
                return null;
            }

            Building_FoodPrinter foodPrinter = printer as Building_FoodPrinter;
            if (foodPrinter != null && eaterPawn != null && !FoodPrinterPawnUtility.CanPawnConsumePrinterMeal(eaterPawn, foodPrinter))
            {
                ReleasePrinterReservation(printer, reservationPawn);
                ClearProcessingState();
                LogProcessingEvent("printer_complete_failed", printer, reservationPawn, eaterPawn, ProcessingMealDef, processingTonerCost, "policy_blocked");
                return null;
            }

            ThingDef mealDef = ProcessingMealDef;
            int tonerCost = processingTonerCost;
            Thing meal = mealDef == null ? null : ThingMaker.MakeThing(mealDef);
            if (meal == null || tonerCost <= 0 || !TonerNetworkUtility.TryConsumeToner(printer, tonerCost))
            {
                ReleasePrinterReservation(printer, reservationPawn);
                ClearProcessingState();
                if (meal != null && !meal.Destroyed)
                {
                    meal.Destroy(DestroyMode.Vanish);
                }

                string failureReason = meal == null
                    ? "meal_creation_failed"
                    : tonerCost <= 0
                        ? "invalid_toner_cost"
                        : "consume_toner_failed";
                LogProcessingEvent("printer_complete_failed", printer, reservationPawn, eaterPawn, mealDef, tonerCost, failureReason);
                return null;
            }

            if (meal != null)
            {
                System.Collections.Generic.List<ThingDef> ingredients = foodPrinter != null
                    ? FoodPrinterPawnUtility.GetPredictedFoodCharacteristics(foodPrinter).PredictedIngredientDefs
                    : TonerNetworkUtility.GetAllIngredients(printer);
                if (ingredients != null && ingredients.Count > 0)
                {
                    CompIngredients compIng = meal.TryGetComp<CompIngredients>();
                    if (compIng != null)
                    {
                        if (compIng.ingredients == null)
                        {
                            compIng.ingredients = new System.Collections.Generic.List<ThingDef>();
                        }
                        for (int i = 0; i < ingredients.Count; i++)
                        {
                            if (!compIng.ingredients.Contains(ingredients[i]))
                            {
                                compIng.ingredients.Add(ingredients[i]);
                            }
                        }
                    }
                }
            }

            ReleasePrinterReservation(printer, reservationPawn);
            ClearProcessingState();
            if (printer != null && printer.def.building != null && printer.def.building.soundDispense != null)
            {
                printer.def.building.soundDispense.PlayOneShot(new TargetInfo(printer.Position, printer.Map));
            }

            LogProcessingEvent("printer_complete_succeeded", printer, reservationPawn, eaterPawn, mealDef, tonerCost, "completed");
            return meal;
        }

        public void CancelProcessing(Building printer, Pawn pawn)
        {
            if (!IsProcessing)
            {
                return;
            }

            LogProcessingEvent("printer_processing_cancelled", printer, pawn, pawn, ProcessingMealDef, processingTonerCost, "cancelled");
            ReleasePrinterReservation(printer, pawn);
            ClearProcessingState();
        }

        public bool CanPrintMeal(Building printer, ThingDef mealDef)
        {
            if (printer == null || mealDef == null || !IsMealResearched(mealDef))
            {
                return false;
            }

            Building_FoodPrinter foodPrinter = printer as Building_FoodPrinter;
            if (foodPrinter != null && !foodPrinter.HasValidFeedSource())
            {
                return false;
            }

            if (IsPrinting)
            {
                return false;
            }

            CompPowerTrader powerComp = printer.TryGetComp<CompPowerTrader>();
            if (powerComp != null && !powerComp.PowerOn)
            {
                return false;
            }

            int tonerCost = FoodPrinterSystemUtility.GetPrintCost(mealDef);
            return tonerCost > 0 && TonerPipeNetManager.CanDraw(printer, tonerCost);
        }

        public void ApplyPowerSetting()
        {
            CompPowerTrader powerComp = parent == null ? null : parent.TryGetComp<CompPowerTrader>();
            if (powerComp != null)
            {
                powerComp.PowerOutput = HasActiveProcessing
                    ? -FoodPrinterSystemUtility.GetFoodPrinterActivePowerDraw()
                    : -FoodPrinterSystemUtility.GetFoodPrinterIdlePowerDraw();
            }
        }

        private static bool IsResearchFinished(ResearchProjectDef research)
        {
            return research != null && research.IsFinished;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            InvalidateMealCaches();
            if (disabledCategoryIds == null)
            {
                disabledCategoryIds = new List<string>();
            }

            MigrateLegacyEnabledCategories();
            EnsureManualMealSelection();
        }

        private void EnsureManualMealSelection()
        {
            FoodPreferability? manualCategory = GetCategoryForMealDef(ManualMealDef);
            if (manualCategory != null
                && IsCategoryEnabled(manualCategory.Value)
                && IsCategoryResearched(manualCategory.Value)
                && GetConfiguredMealsForCategory(manualCategory.Value).Count > 0)
            {
                return;
            }

            ThingDef fallbackMeal = GetRepresentativeMealForCategory(GetHighestEnabledResearchedCategory())
                ?? GetRepresentativeMealForCategory(GetHighestEnabledCategory())
                ?? GetRepresentativeMealForCategory(FoodPreferability.MealAwful)
                ?? GetRepresentativeMealForCategory(FoodPreferability.MealSimple);
            manualMealDefName = fallbackMeal == null ? null : fallbackMeal.defName;
        }

        private void MigrateLegacyAllowedMeals()
        {
            if (legacyAllowedMealDefNames == null || legacyAllowedMealDefNames.Count == 0)
            {
                return;
            }

            for (int i = 0; i < legacyAllowedMealDefNames.Count; i++)
            {
                ThingDef mealDef = DefDatabase<ThingDef>.GetNamedSilentFail(legacyAllowedMealDefNames[i]);
                FoodPreferability? category = GetCategoryForMealDef(mealDef);
                if (category != null)
                {
                    SetCategoryEnabled(category.Value, true);
                }
            }
        }

        private void MigrateLegacyEnabledCategories()
        {
            if (legacyEnabledCategoryIds == null)
            {
                return;
            }

            if (disabledCategoryIds == null)
            {
                disabledCategoryIds = new List<string>();
            }

            HashSet<string> legacyEnabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < legacyEnabledCategoryIds.Count; i++)
            {
                string categoryId = legacyEnabledCategoryIds[i];
                if (!categoryId.NullOrEmpty())
                {
                    legacyEnabled.Add(categoryId);
                }
            }

            for (int i = 0; i < CategoryOrder.Length; i++)
            {
                FoodPreferability category = CategoryOrder[i];
                if (!HasConfiguredMeals(category) || !IsCategoryResearched(category))
                {
                    continue;
                }

                if (!legacyEnabled.Contains(category.ToString()))
                {
                    AddDisabledCategory(category.ToString());
                }
            }

            legacyEnabledCategoryIds = null;
        }

        private FoodPreferability? GetBestAvailableCategory(Building printer)
        {
            for (int i = CategoryOrder.Length - 1; i >= 0; i--)
            {
                FoodPreferability category = CategoryOrder[i];
                if (!IsCategoryEnabled(category) || !IsCategoryResearched(category))
                {
                    continue;
                }

                if (GetAffordableMealsForCategory(printer, category).Count > 0)
                {
                    return category;
                }
            }

            return null;
        }

        private FoodPreferability? GetBestAvailableCategory(Building printer, PawnPrinterFoodPolicy policy, Pawn eater)
        {
            for (int i = CategoryOrder.Length - 1; i >= 0; i--)
            {
                FoodPreferability category = CategoryOrder[i];
                if (!IsCategoryEnabled(category) || !IsCategoryResearched(category))
                {
                    continue;
                }

                if (GetConsumableMealsForCategory(printer, category, policy, eater, false).Count > 0)
                {
                    return category;
                }
            }

            return null;
        }

        private FoodPreferability? GetHighestEnabledResearchedCategory()
        {
            for (int i = CategoryOrder.Length - 1; i >= 0; i--)
            {
                FoodPreferability category = CategoryOrder[i];
                if (IsCategoryEnabled(category)
                    && IsCategoryResearched(category)
                    && GetConfiguredMealsForCategory(category).Count > 0)
                {
                    return category;
                }
            }

            return null;
        }

        private FoodPreferability? GetHighestEnabledCategory()
        {
            for (int i = CategoryOrder.Length - 1; i >= 0; i--)
            {
                FoodPreferability category = CategoryOrder[i];
                if (IsCategoryEnabled(category) && GetConfiguredMealsForCategory(category).Count > 0)
                {
                    return category;
                }
            }

            return null;
        }

        private FoodPreferability? GetHighestPreferredCategory()
        {
            for (int i = CategoryOrder.Length - 1; i >= 0; i--)
            {
                FoodPreferability category = CategoryOrder[i];
                if (!IsCategoryExplicitlyDisabled(category) && HasConfiguredMeals(category))
                {
                    return category;
                }
            }

            return null;
        }

        private CategorySelectionContext ResolveCategorySelection(Building printer)
        {
            FoodPreferability? requestedCategory = autoMode ? GetHighestPreferredCategory() : GetCategoryForMealDef(ManualMealDef);
            FoodPreferability? resolvedCategory = autoMode ? GetBestAvailableCategory(printer) : GetSelectedManualCategory();
            return new CategorySelectionContext
            {
                RequestedCategory = requestedCategory,
                ResolvedCategory = resolvedCategory,
                FallbackReason = GetCategoryFallbackReason(requestedCategory, resolvedCategory, printer, null, null)
            };
        }

        private CategorySelectionContext ResolveCategorySelection(Building printer, PawnPrinterFoodPolicy policy, Pawn eater)
        {
            FoodPreferability? requestedCategory = autoMode ? GetHighestPreferredCategory() : GetCategoryForMealDef(ManualMealDef);
            FoodPreferability? resolvedCategory = autoMode ? GetBestAvailableCategory(printer, policy, eater) : GetSelectedManualCategory();
            return new CategorySelectionContext
            {
                RequestedCategory = requestedCategory,
                ResolvedCategory = resolvedCategory,
                FallbackReason = GetCategoryFallbackReason(requestedCategory, resolvedCategory, printer, policy, eater)
            };
        }

        private string GetCategoryFallbackReason(FoodPreferability? requestedCategory, FoodPreferability? resolvedCategory, Building printer, PawnPrinterFoodPolicy policy, Pawn eater)
        {
            if (requestedCategory == resolvedCategory)
            {
                return null;
            }

            if (requestedCategory == null)
            {
                return "target_category_unset";
            }

            FoodPreferability category = requestedCategory.Value;
            if (!HasConfiguredMeals(category))
            {
                return "target_category_no_configured_meals";
            }

            if (IsCategoryExplicitlyDisabled(category))
            {
                return "target_category_disabled";
            }

            if (!IsCategoryResearched(category))
            {
                return "target_category_unresearched";
            }

            if (printer == null)
            {
                return "target_category_unavailable";
            }

            List<ThingDef> affordableMeals = GetAffordableMealsForCategory(printer, category);
            if (affordableMeals.Count == 0)
            {
                return "target_category_no_affordable_meals";
            }

            if (eater != null)
            {
                List<ThingDef> consumableMeals = GetConsumableMealsForCategory(printer, category, policy, eater, false);
                if (consumableMeals.Count == 0)
                {
                    return "target_category_all_candidates_blocked";
                }
            }

            return "target_category_unavailable";
        }

        private ThingDef GetMealForCategory(CategorySelectionContext categorySelection, Building printer, bool randomize)
        {
            FoodPreferability? category = categorySelection == null ? null : categorySelection.ResolvedCategory;
            if (category == null || !IsCategoryResearched(category.Value))
            {
                return null;
            }

            List<ThingDef> candidates = printer == null
                ? GetConfiguredMealsForCategory(category.Value)
                : GetAffordableMealsForCategory(printer, category.Value);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (randomize && FoodPrinterSystemUtility.RandomMealSelectionEnabled)
            {
                ThingDef selectedMeal = GetWeightedRandomMeal(candidates);
                LogRandomMealSelection(printer, null, categorySelection, candidates, candidates, selectedMeal);
                return selectedMeal;
            }

            return GetRepresentativeMeal(candidates);
        }

        private ThingDef GetMealForCategory(CategorySelectionContext categorySelection, Building printer, PawnPrinterFoodPolicy policy, Pawn eater, bool randomize)
        {
            FoodPreferability? category = categorySelection == null ? null : categorySelection.ResolvedCategory;
            if (category == null || !IsCategoryResearched(category.Value))
            {
                return null;
            }

            if (randomize && FoodPrinterSystemUtility.RandomMealSelectionEnabled)
            {
                List<ThingDef> allCandidates = printer == null
                    ? GetConfiguredMealsForCategory(category.Value)
                    : GetAffordableMealsForCategory(printer, category.Value);
                if (allCandidates.Count == 0)
                {
                    return null;
                }

                List<ThingDef> finalCandidates = GetConsumableMealsForCategory(printer, category.Value, policy, eater, true);
                if (finalCandidates.Count == 0)
                {
                    return null;
                }

                ThingDef selectedMeal = GetPendingMealSelection(printer, policy, eater, finalCandidates);
                if (selectedMeal == null)
                {
                    selectedMeal = GetWeightedRandomMeal(finalCandidates);
                    CachePendingMealSelection(printer, policy, eater, selectedMeal);
                }

                LogRandomMealSelection(printer, eater, categorySelection, allCandidates, finalCandidates, selectedMeal);
                return selectedMeal;
            }

            List<ThingDef> candidates = printer == null
                ? GetConfiguredMealsForCategory(category.Value)
                : GetConsumableMealsForCategory(printer, category.Value, policy, eater, false);
            if (candidates.Count == 0)
            {
                return null;
            }

            return GetRepresentativeMeal(candidates);
        }

        private List<ThingDef> GetConfiguredMealsForCategory(FoodPreferability category)
        {
            if (configuredMealsByCategory.TryGetValue(category, out List<ThingDef> cachedMeals))
            {
                return cachedMeals;
            }

            List<ThingDef> meals = new List<ThingDef>();
            configuredMealsByCategory[category] = meals;
            if (Props.defaultMealDefs != null)
            {
                for (int i = 0; i < Props.defaultMealDefs.Count; i++)
                {
                    string mealDefName = Props.defaultMealDefs[i];
                    if (mealDefName.NullOrEmpty())
                    {
                        continue;
                    }

                    ThingDef mealDef = DefDatabase<ThingDef>.GetNamedSilentFail(mealDefName);
                    if (IsValidMealDef(mealDef) && GetCategoryForMealDef(mealDef) == category)
                    {
                        AddMealIfMissing(meals, mealDef);
                    }
                }
            }

            List<ThingDef> discoveredMeals = GetDiscoveredMealsForCategory(category);
            for (int i = 0; i < discoveredMeals.Count; i++)
            {
                ThingDef discoveredMeal = discoveredMeals[i];
                if (IsValidMealDef(discoveredMeal) && GetCategoryForMealDef(discoveredMeal) == category)
                {
                    AddMealIfMissing(meals, discoveredMeal);
                }
            }

            return meals;
        }

        private List<ThingDef> GetAffordableMealsForCategory(Building printer, FoodPreferability category)
        {
            if (printer == null)
            {
                return GetConfiguredMealsForCategory(category);
            }

            int settingsRevision = FoodPrinterSystemMod.SettingsRevision;
            int researchStateMask = GetResearchStateMask();
            int affordabilityRevision = GetAffordabilityRevision(printer);
            if (affordableMealsByCategory.TryGetValue(category, out CachedAffordableMeals cachedMeals)
                && cachedMeals != null
                && cachedMeals.SettingsRevision == settingsRevision
                && cachedMeals.ResearchStateMask == researchStateMask
                && cachedMeals.AffordabilityRevision == affordabilityRevision
                && cachedMeals.Meals != null)
            {
                return cachedMeals.Meals;
            }

            List<ThingDef> meals = GetConfiguredMealsForCategory(category);
            TonerNetworkSummary summary = TonerNetworkUtility.GetSummary(printer);
            List<ThingDef> affordable = new List<ThingDef>(meals.Count);
            for (int i = 0; i < meals.Count; i++)
            {
                if (summary.Stored >= FoodPrinterSystemUtility.GetPrintCost(meals[i]))
                {
                    affordable.Add(meals[i]);
                }
            }

            affordableMealsByCategory[category] = new CachedAffordableMeals
            {
                SettingsRevision = settingsRevision,
                ResearchStateMask = researchStateMask,
                AffordabilityRevision = affordabilityRevision,
                Meals = affordable
            };
            return affordable;
        }

        private List<ThingDef> GetConsumableMealsForCategory(Building printer, FoodPreferability category, PawnPrinterFoodPolicy policy, Pawn eater, bool logDiagnostics)
        {
            if (printer == null)
            {
                return GetConfiguredMealsForCategory(category);
            }

            List<ThingDef> meals = GetAffordableMealsForCategory(printer, category);
            Building_FoodPrinter foodPrinter = printer as Building_FoodPrinter;
            if (foodPrinter == null || eater == null)
            {
                return meals;
            }

            int currentTick = GetCurrentTick();
            PruneConsumableMealCacheIfNeeded(currentTick);
            ConsumableMealsCacheKey cacheKey = new ConsumableMealsCacheKey
            {
                PrinterId = printer.thingIDNumber,
                PawnId = eater.thingIDNumber,
                FoodPolicyId = GetFoodPolicyCacheId(eater),
                PolicyStateHash = GetResolvedPolicyStateHash(policy),
                Category = category,
                AffordabilityRevision = GetAffordabilityRevision(printer),
                CharacteristicsRevision = GetCharacteristicsRevision(printer),
                SettingsRevision = FoodPrinterSystemMod.SettingsRevision,
                ResearchStateMask = GetResearchStateMask(),
                HardFoodTypeCheckEnabled = IsHardFoodTypeCheckEnabledForCache()
            };
            if (consumableMealsByCategory.TryGetValue(cacheKey, out CachedConsumableMeals cachedMeals)
                && cachedMeals != null
                && cachedMeals.Meals != null)
            {
                cachedMeals.LastAccessTick = currentTick;
                return cachedMeals.Meals;
            }

            List<ThingDef> consumable = new List<ThingDef>(meals.Count);
            for (int i = 0; i < meals.Count; i++)
            {
                ThingDef mealDef = meals[i];
                if (!FoodPrinterPawnUtility.TryDiagnoseFoodPolicyAllowance(eater, mealDef, out string reason))
                {
                    if (logDiagnostics)
                    {
                        LogRandomCandidateRejected(printer, eater, mealDef, reason);
                    }

                    continue;
                }

                if (FoodPrinterPawnUtility.TryDiagnoseMealAllowance(policy, eater, foodPrinter, mealDef, out reason))
                {
                    consumable.Add(mealDef);
                }
                else if (logDiagnostics)
                {
                    LogRandomCandidateRejected(printer, eater, mealDef, reason);
                }
            }

            consumableMealsByCategory[cacheKey] = new CachedConsumableMeals
            {
                LastAccessTick = currentTick,
                Meals = consumable
            };
            return consumable;
        }

        private ThingDef GetRepresentativeMealForCategory(FoodPreferability? category)
        {
            return category == null ? null : GetRepresentativeMeal(GetConfiguredMealsForCategory(category.Value));
        }

        private static ThingDef GetRepresentativeMeal(List<ThingDef> meals)
        {
            ThingDef bestMeal = null;
            float bestWeight = -1f;
            for (int i = 0; i < meals.Count; i++)
            {
                ThingDef mealDef = meals[i];
                float weight = GetMealSelectionWeight(mealDef);
                if (bestMeal == null || weight > bestWeight)
                {
                    bestMeal = mealDef;
                    bestWeight = weight;
                }
            }

            return bestMeal;
        }

        private static ThingDef GetWeightedRandomMeal(List<ThingDef> meals)
        {
            if (meals.Count == 1)
            {
                return meals[0];
            }

            float totalWeight = 0f;
            for (int i = 0; i < meals.Count; i++)
            {
                totalWeight += GetMealSelectionWeight(meals[i]);
            }

            if (totalWeight <= 0f)
            {
                return meals[Rand.Range(0, meals.Count)];
            }

            float roll = Rand.Value * totalWeight;
            for (int i = 0; i < meals.Count; i++)
            {
                roll -= GetMealSelectionWeight(meals[i]);
                if (roll <= 0f)
                {
                    return meals[i];
                }
            }

            return meals[meals.Count - 1];
        }

        private static float GetMealSelectionWeight(ThingDef mealDef)
        {
            if (mealDef == null || mealDef.defName.NullOrEmpty())
            {
                return 0f;
            }

            return mealDef.defName.EndsWith("_Veg") || mealDef.defName.EndsWith("_Meat") ? 1f : 2f;
        }

        private static FoodPreferability? GetCategoryForMealDef(ThingDef mealDef)
        {
            if (!IsValidMealDef(mealDef) || mealDef.ingestible == null)
            {
                return null;
            }

            if (mealDef.defName == "MealNutrientPaste")
            {
                return FoodPreferability.MealAwful;
            }

            FoodPreferability preferability = mealDef.ingestible.preferability;
            if (preferability == FoodPreferability.MealSimple
                || preferability == FoodPreferability.MealFine
                || preferability == FoodPreferability.MealLavish)
            {
                return preferability;
            }

            return null;
        }

        private static bool IsValidMealDef(ThingDef mealDef)
        {
            return mealDef != null
                && mealDef.IsNutritionGivingIngestible
                && FoodPrinterSystemUtility.GetPrintCost(mealDef) > 0
                && mealDef.ingestible != null
                && (mealDef.defName == "MealNutrientPaste" || (IsPrinterMealFoodType(mealDef) && IsPrintableMealItem(mealDef)));
        }

        private static bool IsPrinterMealFoodType(ThingDef mealDef)
        {
            return mealDef != null
                && mealDef.ingestible != null
                && (mealDef.ingestible.foodType & FoodTypeFlags.Meal) != FoodTypeFlags.None;
        }

        private static bool IsPrintableMealItem(ThingDef mealDef)
        {
            if (mealDef == null || mealDef.thingCategories == null)
            {
                return false;
            }

            for (int i = 0; i < mealDef.thingCategories.Count; i++)
            {
                ThingCategoryDef category = mealDef.thingCategories[i];
                if (category != null && string.Equals(category.defName, "FoodMeals", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<ThingDef> GetDiscoveredMealsForCategory(FoodPreferability category)
        {
            EnsureDiscoveredMealsInitialized();
            return DiscoveredMealsByCategory.TryGetValue(category, out List<ThingDef> meals)
                ? meals
                : DiscoveredMealsByCategory[FoodPreferability.MealAwful];
        }

        private static void EnsureDiscoveredMealsInitialized()
        {
            if (discoveredMealsInitialized)
            {
                return;
            }

            discoveredMealsInitialized = true;
            DiscoveredMealsByCategory.Clear();
            DiscoveredMealsByCategory[FoodPreferability.MealAwful] = new List<ThingDef>();
            DiscoveredMealsByCategory[FoodPreferability.MealSimple] = new List<ThingDef>();
            DiscoveredMealsByCategory[FoodPreferability.MealFine] = new List<ThingDef>();
            DiscoveredMealsByCategory[FoodPreferability.MealLavish] = new List<ThingDef>();

            List<ThingDef> allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < allDefs.Count; i++)
            {
                ThingDef mealDef = allDefs[i];
                if (!IsValidMealDef(mealDef))
                {
                    continue;
                }

                FoodPreferability? category = GetCategoryForMealDef(mealDef);
                if (category == null || category == FoodPreferability.MealAwful)
                {
                    continue;
                }

                AddMealIfMissing(DiscoveredMealsByCategory[category.Value], mealDef);
            }

            foreach (KeyValuePair<FoodPreferability, List<ThingDef>> pair in DiscoveredMealsByCategory)
            {
                pair.Value.Sort(CompareMealDefsForDisplay);
            }
        }

        private static int CompareMealDefsForDisplay(ThingDef left, ThingDef right)
        {
            string leftLabel = left == null ? string.Empty : left.LabelCap.ToString();
            string rightLabel = right == null ? string.Empty : right.LabelCap.ToString();
            int labelComparison = string.Compare(leftLabel, rightLabel, StringComparison.CurrentCultureIgnoreCase);
            if (labelComparison != 0)
            {
                return labelComparison;
            }

            string leftDefName = left == null ? string.Empty : left.defName;
            string rightDefName = right == null ? string.Empty : right.defName;
            return string.Compare(leftDefName, rightDefName, StringComparison.OrdinalIgnoreCase);
        }

        private bool HasConfiguredMeals(FoodPreferability category)
        {
            return GetConfiguredMealsForCategory(category).Count > 0;
        }

        private void AddDisabledCategory(string categoryId)
        {
            if (categoryId.NullOrEmpty())
            {
                return;
            }

            if (disabledCategoryIds == null)
            {
                disabledCategoryIds = new List<string>();
            }

            for (int i = 0; i < disabledCategoryIds.Count; i++)
            {
                if (string.Equals(disabledCategoryIds[i], categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            disabledCategoryIds.Add(categoryId);
        }

        private void RemoveDisabledCategory(string categoryId)
        {
            if (categoryId.NullOrEmpty() || disabledCategoryIds == null)
            {
                return;
            }

            for (int i = disabledCategoryIds.Count - 1; i >= 0; i--)
            {
                if (string.Equals(disabledCategoryIds[i], categoryId, StringComparison.OrdinalIgnoreCase))
                {
                    disabledCategoryIds.RemoveAt(i);
                }
            }
        }

        private static void AddMealIfMissing(List<ThingDef> meals, ThingDef mealDef)
        {
            if (meals == null || mealDef == null)
            {
                return;
            }

            for (int i = 0; i < meals.Count; i++)
            {
                ThingDef existing = meals[i];
                if (existing == mealDef
                    || (existing != null
                        && !existing.defName.NullOrEmpty()
                        && string.Equals(existing.defName, mealDef.defName, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }
            }

            meals.Add(mealDef);
        }

        public bool IsBaseConfiguredMeal(ThingDef mealDef)
        {
            if (mealDef == null || Props == null || Props.defaultMealDefs == null)
            {
                return false;
            }

            for (int i = 0; i < Props.defaultMealDefs.Count; i++)
            {
                string mealDefName = Props.defaultMealDefs[i];
                if (!mealDefName.NullOrEmpty() && string.Equals(mealDefName, mealDef.defName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogRandomMealSelection(Building printer, Pawn eater, CategorySelectionContext categorySelection, List<ThingDef> allCandidates, List<ThingDef> finalCandidates, ThingDef selectedMeal)
        {
            if (!ShouldDebugLog() || allCandidates == null || allCandidates.Count == 0)
            {
                return;
            }

            bool hasDiscoveredCandidate = HasDiscoveredMeal(allCandidates) || HasDiscoveredMeal(finalCandidates) || IsDiscoveredMeal(selectedMeal);
            if (!hasDiscoveredCandidate)
            {
                return;
            }

            List<string> allCandidateDescriptions = BuildCandidateDebugDescriptions(allCandidates);
            List<string> finalCandidateDescriptions = BuildCandidateDebugDescriptions(finalCandidates);

            Log.Message("[FPS] printer_random_meal_pool"
                + ": printer=" + GetPrinterDebugLabel(printer)
                + ", eaterPawn=" + GetPawnDebugLabel(eater)
                + ", requestedCategory=" + GetCategoryDebugLabel(categorySelection == null ? null : categorySelection.RequestedCategory)
                + ", resolvedCategory=" + GetCategoryDebugLabel(categorySelection == null ? null : categorySelection.ResolvedCategory)
                + ", categoryFallbackReason=" + GetCategoryFallbackReasonDebug(categorySelection)
                + ", selected=" + GetMealDebugLabel(selectedMeal)
                + ", selectedLabel=" + GetMealLabelDebug(selectedMeal)
                + ", selectedOrigin=" + (IsDiscoveredMeal(selectedMeal) ? "discovered" : "base")
                + ", foodPolicy=" + GetFoodPolicyDebugLabel(eater)
                + ", allCandidates=" + string.Join(", ", allCandidateDescriptions.ToArray())
                + ", finalCandidates=" + string.Join(", ", finalCandidateDescriptions.ToArray()));

            if (IsDiscoveredMeal(selectedMeal))
            {
                Log.Message("[FPS] printer_random_selected_mod_meal"
                    + ": printer=" + GetPrinterDebugLabel(printer)
                    + ", eaterPawn=" + GetPawnDebugLabel(eater)
                    + ", requestedCategory=" + GetCategoryDebugLabel(categorySelection == null ? null : categorySelection.RequestedCategory)
                    + ", resolvedCategory=" + GetCategoryDebugLabel(categorySelection == null ? null : categorySelection.ResolvedCategory)
                    + ", categoryFallbackReason=" + GetCategoryFallbackReasonDebug(categorySelection)
                    + ", foodPolicy=" + GetFoodPolicyDebugLabel(eater)
                    + ", meal=" + GetMealDebugLabel(selectedMeal)
                    + ", mealLabel=" + GetMealLabelDebug(selectedMeal));
            }
        }

        private void LogRandomCandidateRejected(Building printer, Pawn eater, ThingDef mealDef, string reason)
        {
            if (!ShouldDebugLog() || mealDef == null)
            {
                return;
            }

            Log.Message("[FPS] printer_random_candidate_rejected"
                + ": printer=" + GetPrinterDebugLabel(printer)
                + ", eaterPawn=" + GetPawnDebugLabel(eater)
                + ", foodPolicy=" + GetFoodPolicyDebugLabel(eater)
                + ", meal=" + GetMealDebugLabel(mealDef)
                + ", mealLabel=" + GetMealLabelDebug(mealDef)
                + ", mealOrigin=" + (IsDiscoveredMeal(mealDef) ? "discovered" : "base")
                + ", mealCategory=" + GetMealCategoryDebugLabel(mealDef)
                + ", reason=" + reason);
        }

        private List<string> BuildCandidateDebugDescriptions(List<ThingDef> meals)
        {
            List<string> descriptions = new List<string>(meals == null ? 0 : meals.Count);
            if (meals == null)
            {
                return descriptions;
            }

            for (int i = 0; i < meals.Count; i++)
            {
                ThingDef mealDef = meals[i];
                descriptions.Add(GetMealDebugLabel(mealDef)
                    + "[" + (IsDiscoveredMeal(mealDef) ? "discovered" : "base")
                    + ",cat=" + GetMealCategoryDebugLabel(mealDef)
                    + ",w=" + GetMealSelectionWeight(mealDef).ToString("0.##") + "]");
            }

            return descriptions;
        }

        private bool HasDiscoveredMeal(List<ThingDef> meals)
        {
            if (meals == null)
            {
                return false;
            }

            for (int i = 0; i < meals.Count; i++)
            {
                if (IsDiscoveredMeal(meals[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsDiscoveredMeal(ThingDef mealDef)
        {
            return mealDef != null && !IsBaseConfiguredMeal(mealDef);
        }

        private static bool ShouldDebugLog()
        {
            return FoodPrinterSystemMod.Settings != null && FoodPrinterSystemMod.Settings.DebugLoggingEnabled;
        }

        public bool IsBusyFor(Pawn pawn)
        {
            return currentProcessingPawn != null && currentProcessingPawn != pawn;
        }

        public bool IsProcessingPawn(Pawn pawn)
        {
            return currentProcessingPawn != null && currentProcessingPawn == pawn;
        }

        private bool TryReservePrinterForProcessing(Building printer, Pawn pawn)
        {
            if (printer == null || pawn == null || pawn.CurJob == null || printer.Map == null)
            {
                return false;
            }

            Job job = pawn.CurJob;
            return printer.Map.reservationManager.ReservedBy(printer, pawn, job)
                || pawn.Reserve(printer, job, 1, -1, null, false);
        }

        private static void ReleasePrinterReservation(Building printer, Pawn pawn)
        {
            if (printer == null || pawn == null || printer.Map == null || pawn.CurJob == null)
            {
                return;
            }

            Job job = pawn.CurJob;
            if (printer.Map.reservationManager.ReservedBy(printer, pawn, job))
            {
                printer.Map.reservationManager.Release(printer, pawn, job);
            }
        }

        private void ClearProcessingState()
        {
            processingMealDefName = null;
            processingTicksRemaining = 0;
            processingTonerCost = 0;
            currentProcessingPawn = null;
            ClearPendingMealSelection();
            ApplyPowerSetting();
        }

        private static void LogProcessingEvent(string eventName, Building printer, Pawn reservationPawn, Pawn eaterPawn, ThingDef mealDef, int tonerCost, string reason, CategorySelectionContext categorySelection = null)
        {
            if (FoodPrinterSystemMod.Settings == null || !FoodPrinterSystemMod.Settings.DebugLoggingEnabled)
            {
                return;
            }

            TonerNetworkSummary summary = TonerPipeNetManager.GetSummary(printer);
            Log.Message("[FPS] " + eventName
                + ": printer=" + GetPrinterDebugLabel(printer)
                + ", reservationPawn=" + GetPawnDebugLabel(reservationPawn)
                + ", eaterPawn=" + GetPawnDebugLabel(eaterPawn)
                + ", requestedCategory=" + GetCategoryDebugLabel(categorySelection == null ? null : categorySelection.RequestedCategory)
                + ", resolvedCategory=" + GetCategoryDebugLabel(categorySelection == null ? null : categorySelection.ResolvedCategory)
                + ", categoryFallbackReason=" + GetCategoryFallbackReasonDebug(categorySelection)
                + ", meal=" + GetMealDebugLabel(mealDef)
                + ", tonerCost=" + tonerCost
                + ", networkStored=" + summary.Stored
                + ", networkCapacity=" + summary.Capacity
                + ", reason=" + reason);
        }

        private static void LogCategorySelection(Building printer, Pawn eaterPawn, CategorySelectionContext categorySelection)
        {
            if (!ShouldDebugLog()
                || categorySelection == null
                || (categorySelection.RequestedCategory == categorySelection.ResolvedCategory
                    && categorySelection.FallbackReason.NullOrEmpty()))
            {
                return;
            }

            Log.Message("[FPS] printer_category_resolution"
                + ": printer=" + GetPrinterDebugLabel(printer)
                + ", eaterPawn=" + GetPawnDebugLabel(eaterPawn)
                + ", foodPolicy=" + GetFoodPolicyDebugLabel(eaterPawn)
                + ", autoMode=" + (printer != null && printer.GetComp<CompFoodPrinter>() != null && printer.GetComp<CompFoodPrinter>().AutoMode)
                + ", requestedCategory=" + GetCategoryDebugLabel(categorySelection.RequestedCategory)
                + ", resolvedCategory=" + GetCategoryDebugLabel(categorySelection.ResolvedCategory)
                + ", categoryFallbackReason=" + GetCategoryFallbackReasonDebug(categorySelection));
        }

        private static string GetPrinterDebugLabel(Building printer)
        {
            return printer == null ? "null" : printer.LabelShortCap + " (" + printer.thingIDNumber + ")";
        }

        private static string GetPawnDebugLabel(Pawn pawn)
        {
            return pawn == null ? "null" : pawn.LabelShortCap + " (" + pawn.thingIDNumber + ")";
        }

        private static string GetMealDebugLabel(ThingDef mealDef)
        {
            return mealDef == null ? "null" : mealDef.defName;
        }

        private static string GetMealLabelDebug(ThingDef mealDef)
        {
            return mealDef == null ? "null" : mealDef.LabelCap.ToString();
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

        private static string GetFoodPolicyDebugLabel(Pawn pawn)
        {
            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;
            if (currentPolicy == null)
            {
                return "null";
            }

            return currentPolicy.label.NullOrEmpty() ? "unnamed_policy" : currentPolicy.label;
        }

        internal bool HasConsumableMealForPawn(Building printer, PawnPrinterFoodPolicy policy, Pawn eater)
        {
            if (printer == null)
            {
                return false;
            }

            CategorySelectionContext categorySelection = ResolveCategorySelection(printer, policy, eater);
            FoodPreferability? category = categorySelection == null ? null : categorySelection.ResolvedCategory;
            return category != null
                && GetConsumableMealsForCategory(printer, category.Value, policy, eater, false).Count > 0;
        }

        private static string GetCategoryDebugLabel(FoodPreferability? category)
        {
            return category == null ? "null" : category.Value.ToString();
        }

        private static string GetCategoryFallbackReasonDebug(CategorySelectionContext categorySelection)
        {
            return categorySelection == null || categorySelection.FallbackReason.NullOrEmpty()
                ? "none"
                : categorySelection.FallbackReason;
        }

        private void DrawProcessingBar()
        {
            if (!IsProcessing || parent == null || parent.Map == null)
            {
                return;
            }

            float fillPercent = ProcessingProgress;
            if (fillPercent < 0f)
            {
                fillPercent = 0f;
            }
            else if (fillPercent > 1f)
            {
                fillPercent = 1f;
            }

            Vector2 barSize = new Vector2(parent.def.size.x * 0.82f, 0.10f);
            Vector3 barCenter = parent.TrueCenter();
            barCenter.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
            barCenter.z += parent.def.size.z * 0.56f;

            DrawBar(barCenter, barSize, ProcessingBarBackgroundMat);
            if (fillPercent > 0f)
            {
                Vector2 fillSize = new Vector2(barSize.x * fillPercent, barSize.y * 0.72f);
                Vector3 fillCenter = barCenter;
                fillCenter.x -= (barSize.x - fillSize.x) * 0.5f;
                fillCenter.y += 0.002f;
                DrawBar(fillCenter, fillSize, ProcessingBarFillMat);
            }
        }

        private static void DrawBar(Vector3 center, Vector2 size, Material material)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size.x, 1f, size.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }

        private void InvalidateMealCaches()
        {
            configuredMealsByCategory.Clear();
            affordableMealsByCategory.Clear();
            consumableMealsByCategory.Clear();
            lastConsumableMealCachePruneTick = 0;
            cachedManualMealDefName = null;
            cachedManualMealDef = null;
            cachedProcessingMealDefName = null;
            cachedProcessingMealDef = null;
            ClearPendingMealSelection();
        }

        private ThingDef GetPendingMealSelection(Building printer, PawnPrinterFoodPolicy policy, Pawn eater, List<ThingDef> validCandidates)
        {
            if (printer == null
                || eater == null
                || validCandidates == null
                || validCandidates.Count == 0
                || pendingSelectedMealExpiresAtTick <= GetCurrentTick()
                || pendingSelectedMealPawnId != eater.thingIDNumber
                || pendingSelectedMealFoodPolicyId != GetFoodPolicyCacheId(eater)
                || pendingSelectedMealPolicyStateHash != GetResolvedPolicyStateHash(policy)
                || pendingSelectedMealAffordabilityRevision != GetAffordabilityRevision(printer)
                || pendingSelectedMealCharacteristicsRevision != GetCharacteristicsRevision(printer)
                || pendingSelectedMealResearchStateMask != GetResearchStateMask()
                || pendingSelectedMealSettingsRevision != FoodPrinterSystemMod.SettingsRevision)
            {
                return null;
            }

            ThingDef selectedMeal = DefDatabase<ThingDef>.GetNamedSilentFail(pendingSelectedMealDefName);
            if (selectedMeal == null || !validCandidates.Contains(selectedMeal))
            {
                return null;
            }

            return selectedMeal;
        }

        private void CachePendingMealSelection(Building printer, PawnPrinterFoodPolicy policy, Pawn eater, ThingDef mealDef)
        {
            if (printer == null || eater == null || mealDef == null)
            {
                return;
            }

            pendingSelectedMealDefName = mealDef.defName;
            pendingSelectedMealPawnId = eater.thingIDNumber;
            pendingSelectedMealFoodPolicyId = GetFoodPolicyCacheId(eater);
            pendingSelectedMealPolicyStateHash = GetResolvedPolicyStateHash(policy);
            pendingSelectedMealAffordabilityRevision = GetAffordabilityRevision(printer);
            pendingSelectedMealCharacteristicsRevision = GetCharacteristicsRevision(printer);
            pendingSelectedMealResearchStateMask = GetResearchStateMask();
            pendingSelectedMealSettingsRevision = FoodPrinterSystemMod.SettingsRevision;
            pendingSelectedMealExpiresAtTick = GetCurrentTick() + PendingMealSelectionRetentionTicks;
        }

        private void ClearPendingMealSelection()
        {
            pendingSelectedMealDefName = null;
            pendingSelectedMealPawnId = 0;
            pendingSelectedMealFoodPolicyId = 0;
            pendingSelectedMealPolicyStateHash = 0;
            pendingSelectedMealAffordabilityRevision = 0;
            pendingSelectedMealCharacteristicsRevision = 0;
            pendingSelectedMealResearchStateMask = 0;
            pendingSelectedMealSettingsRevision = 0;
            pendingSelectedMealExpiresAtTick = 0;
        }

        private static ThingDef ResolveCachedMealDef(string mealDefName, ref string cachedMealDefName, ref ThingDef cachedMealDef)
        {
            if (mealDefName.NullOrEmpty())
            {
                cachedMealDefName = null;
                cachedMealDef = null;
                return null;
            }

            if (cachedMealDefName == mealDefName)
            {
                return cachedMealDef;
            }

            cachedMealDefName = mealDefName;
            cachedMealDef = DefDatabase<ThingDef>.GetNamedSilentFail(mealDefName);
            return cachedMealDef;
        }

        private int GetResearchStateMask()
        {
            int mask = 0;
            for (int i = 0; i < CategoryOrder.Length; i++)
            {
                if (IsCategoryResearched(CategoryOrder[i]))
                {
                    mask |= 1 << i;
                }
            }

            return mask;
        }

        private static int GetAffordabilityRevision(Building printer)
        {
            if (printer == null || printer.Map == null)
            {
                return 0;
            }

            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(printer.Map);
            return networkComponent == null
                ? 0
                : unchecked((networkComponent.NetworkRevision * 397) ^ networkComponent.StorageRevision);
        }

        private static int GetCharacteristicsRevision(Building printer)
        {
            Building_FoodPrinter foodPrinter = printer as Building_FoodPrinter;
            return foodPrinter == null ? 0 : FoodPrinterPawnUtility.GetPrinterCharacteristicsRevisionForCaching(foodPrinter);
        }

        private static int GetCurrentTick()
        {
            return Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
        }

        private static int GetFoodPolicyCacheId(Pawn pawn)
        {
            FoodPolicy currentPolicy = pawn == null || pawn.foodRestriction == null ? null : pawn.foodRestriction.CurrentFoodPolicy;
            if (currentPolicy == null)
            {
                return 0;
            }

            string uniqueId = currentPolicy.GetUniqueLoadID();
            return uniqueId.NullOrEmpty() ? currentPolicy.label.GetHashCode() : uniqueId.GetHashCode();
        }

        private static int GetResolvedPolicyStateHash(PawnPrinterFoodPolicy policy)
        {
            return FoodPrinterPawnUtility.GetPolicyStateHash(policy);
        }

        private static bool IsHardFoodTypeCheckEnabledForCache()
        {
            return FoodPrinterSystemMod.Settings == null || FoodPrinterSystemMod.Settings.HardCheckFoodType;
        }

        private void PruneConsumableMealCacheIfNeeded(int currentTick)
        {
            if (consumableMealsByCategory.Count == 0)
            {
                lastConsumableMealCachePruneTick = currentTick;
                return;
            }

            if (consumableMealsByCategory.Count <= ConsumableMealCachePruneThreshold
                && currentTick - lastConsumableMealCachePruneTick < ConsumableMealCachePruneIntervalTicks)
            {
                return;
            }

            List<ConsumableMealsCacheKey> expiredKeys = null;
            foreach (KeyValuePair<ConsumableMealsCacheKey, CachedConsumableMeals> pair in consumableMealsByCategory)
            {
                CachedConsumableMeals cachedMeals = pair.Value;
                if (cachedMeals == null || cachedMeals.LastAccessTick + ConsumableMealCacheRetentionTicks <= currentTick)
                {
                    if (expiredKeys == null)
                    {
                        expiredKeys = new List<ConsumableMealsCacheKey>();
                    }

                    expiredKeys.Add(pair.Key);
                }
            }

            if (expiredKeys != null)
            {
                for (int i = 0; i < expiredKeys.Count; i++)
                {
                    consumableMealsByCategory.Remove(expiredKeys[i]);
                }
            }

            lastConsumableMealCachePruneTick = currentTick;
        }
    }
}



