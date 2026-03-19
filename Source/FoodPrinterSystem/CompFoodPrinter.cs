using System.Collections.Generic;
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
        private sealed class CachedAffordableMeals
        {
            public int SettingsRevision;
            public int ResearchStateMask;
            public int NetworkRevision;
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

        private bool initialized;
        private bool autoMode = true;
        private string manualMealDefName;
        private List<string> enabledCategoryIds = new List<string>();
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
            Scribe_Collections.Look(ref enabledCategoryIds, "enabledCategoryIds", LookMode.Value);
            Scribe_Values.Look(ref processingMealDefName, "processingMealDefName");
            Scribe_Values.Look(ref processingTicksRemaining, "processingTicksRemaining", 0);
            Scribe_Values.Look(ref processingTonerCost, "processingTonerCost", 0);
            Scribe_References.Look(ref currentProcessingPawn, "currentProcessingPawn");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
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
            return enabledCategoryIds.Contains(category.ToString());
        }

        public void SetCategoryEnabled(FoodPreferability category, bool enabled)
        {
            EnsureInitialized();
            string categoryId = category.ToString();
            if (enabled)
            {
                if (!enabledCategoryIds.Contains(categoryId))
                {
                    enabledCategoryIds.Add(categoryId);
                }
            }
            else
            {
                enabledCategoryIds.Remove(categoryId);
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

            FoodPreferability? category = autoMode ? GetBestAvailableCategory(printer) : GetSelectedManualCategory();
            return GetMealForCategory(category, printer, false);
        }

        public string GetPreviewOutputLabel(Building printer)
        {
            EnsureInitialized();
            if (IsProcessing)
            {
                return ProcessingMealDef == null ? null : ProcessingMealDef.LabelCap.ToString();
            }

            FoodPreferability? category = autoMode ? GetBestAvailableCategory(printer) : GetSelectedManualCategory();
            if (category == null)
            {
                return null;
            }

            List<ThingDef> meals = GetConfiguredMealsForCategory(category.Value);
            if (meals.Count > 1)
            {
                return GetCategoryLabel(category.Value);
            }

            ThingDef previewMeal = GetMealForCategory(category, printer, false);
            return previewMeal == null ? GetCategoryLabel(category.Value) : previewMeal.LabelCap.ToString();
        }

        public ThingDef GetMealToPrint(Building printer, bool randomize)
        {
            return GetMealToPrint(printer, null, null, randomize);
        }

        public ThingDef GetMealToPrint(Building printer, PawnPrinterFoodPolicy policy, Pawn eater, bool randomize)
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

            FoodPreferability? category = autoMode
                ? (eater == null ? GetBestAvailableCategory(printer) : GetBestAvailableCategory(printer, policy, eater))
                : GetSelectedManualCategory();
            return eater == null
                ? GetMealForCategory(category, printer, randomize)
                : GetMealForCategory(category, printer, policy, eater, randomize);
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

            ThingDef mealDef = GetMealToPrint(printer, eaterPolicy, eaterPawn, true);
            if (mealDef == null || !IsMealResearched(mealDef))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, mealDef, 0, "no_valid_meal_selected");
                return false;
            }

            if (foodPrinter != null && !FoodPrinterPawnUtility.CanPawnConsumeMeal(eaterPolicy, eaterPawn, foodPrinter, mealDef))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, mealDef, 0, "selected_meal_policy_blocked");
                return false;
            }

            if (!TryReservePrinterForProcessing(printer, reservationPawn))
            {
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, null, 0, "reservation_failed");
                return false;
            }

            int tonerCost = FoodPrinterSystemUtility.GetPrintCost(mealDef);
            if (tonerCost <= 0 || !TonerPipeNetManager.CanDraw(printer, tonerCost))
            {
                ReleasePrinterReservation(printer, reservationPawn);
                LogProcessingEvent("printer_start_failed", printer, reservationPawn, eaterPawn, mealDef, tonerCost, "insufficient_toner");
                return false;
            }

            currentProcessingPawn = reservationPawn;
            processingMealDefName = mealDef.defName;
            processingTonerCost = tonerCost;
            processingTicksRemaining = FoodPrinterSystemUtility.PrintingDelayTicks;
            LogProcessingEvent("printer_start_succeeded", printer, reservationPawn, eaterPawn, mealDef, tonerCost, "started");
            return true;
        }

        public void UpdateProcessingTicksRemaining(int ticksRemaining)
        {
            if (!IsProcessing)
            {
                return;
            }

            processingTicksRemaining = Mathf.Clamp(ticksRemaining, 0, FoodPrinterSystemUtility.PrintingDelayTicks);
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
                powerComp.PowerOutput = -FoodPrinterSystemUtility.GetConstantPowerDraw(parent.def);
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
            if (enabledCategoryIds == null)
            {
                enabledCategoryIds = new List<string>();
            }

            if (enabledCategoryIds.Count == 0)
            {
                List<FoodPreferability> categories = GetKnownCategories();
                for (int i = 0; i < categories.Count; i++)
                {
                    enabledCategoryIds.Add(categories[i].ToString());
                }
            }

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

                if (GetAffordableMealsForCategory(printer, category, policy, eater).Count > 0)
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

        private ThingDef GetMealForCategory(FoodPreferability? category, Building printer, bool randomize)
        {
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

            return randomize && FoodPrinterSystemUtility.RandomMealSelectionEnabled ? GetWeightedRandomMeal(candidates) : GetRepresentativeMeal(candidates);
        }

        private ThingDef GetMealForCategory(FoodPreferability? category, Building printer, PawnPrinterFoodPolicy policy, Pawn eater, bool randomize)
        {
            if (category == null || !IsCategoryResearched(category.Value))
            {
                return null;
            }

            List<ThingDef> candidates = printer == null
                ? GetConfiguredMealsForCategory(category.Value)
                : GetAffordableMealsForCategory(printer, category.Value, policy, eater);
            if (candidates.Count == 0)
            {
                return null;
            }

            return randomize && FoodPrinterSystemUtility.RandomMealSelectionEnabled ? GetWeightedRandomMeal(candidates) : GetRepresentativeMeal(candidates);
        }

        private List<ThingDef> GetConfiguredMealsForCategory(FoodPreferability category)
        {
            if (configuredMealsByCategory.TryGetValue(category, out List<ThingDef> cachedMeals))
            {
                return cachedMeals;
            }

            List<ThingDef> meals = new List<ThingDef>();
            configuredMealsByCategory[category] = meals;
            if (Props.defaultMealDefs == null)
            {
                return meals;
            }

            for (int i = 0; i < Props.defaultMealDefs.Count; i++)
            {
                string mealDefName = Props.defaultMealDefs[i];
                if (mealDefName.NullOrEmpty())
                {
                    continue;
                }

                ThingDef mealDef = DefDatabase<ThingDef>.GetNamedSilentFail(mealDefName);
                if (IsValidMealDef(mealDef) && GetCategoryForMealDef(mealDef) == category && !meals.Contains(mealDef))
                {
                    meals.Add(mealDef);
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
            int networkRevision = GetNetworkRevision(printer);
            if (affordableMealsByCategory.TryGetValue(category, out CachedAffordableMeals cachedMeals)
                && cachedMeals != null
                && cachedMeals.SettingsRevision == settingsRevision
                && cachedMeals.ResearchStateMask == researchStateMask
                && cachedMeals.NetworkRevision == networkRevision
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
                NetworkRevision = networkRevision,
                Meals = affordable
            };
            return affordable;
        }

        private List<ThingDef> GetAffordableMealsForCategory(Building printer, FoodPreferability category, PawnPrinterFoodPolicy policy, Pawn eater)
        {
            List<ThingDef> meals = GetAffordableMealsForCategory(printer, category);
            List<ThingDef> affordable = new List<ThingDef>(meals.Count);
            Building_FoodPrinter foodPrinter = printer as Building_FoodPrinter;
            for (int i = 0; i < meals.Count; i++)
            {
                ThingDef mealDef = meals[i];
                if (foodPrinter != null && eater != null && !FoodPrinterPawnUtility.IsMealAllowedForPawn(policy, eater, foodPrinter, mealDef))
                {
                    continue;
                }

                affordable.Add(mealDef);
            }

            return affordable;
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
                && mealDef.ingestible != null;
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
        }

        private static void LogProcessingEvent(string eventName, Building printer, Pawn reservationPawn, Pawn eaterPawn, ThingDef mealDef, int tonerCost, string reason)
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
                + ", meal=" + GetMealDebugLabel(mealDef)
                + ", tonerCost=" + tonerCost
                + ", networkStored=" + summary.Stored
                + ", networkCapacity=" + summary.Capacity
                + ", reason=" + reason);
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
            cachedManualMealDefName = null;
            cachedManualMealDef = null;
            cachedProcessingMealDefName = null;
            cachedProcessingMealDef = null;
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

        private static int GetNetworkRevision(Building printer)
        {
            if (printer == null || printer.Map == null)
            {
                return 0;
            }

            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(printer.Map);
            return networkComponent == null
                ? 0
                : unchecked((networkComponent.NetworkRevision * 397) ^ networkComponent.ContentsRevision);
        }
    }
}



