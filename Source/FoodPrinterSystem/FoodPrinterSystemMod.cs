using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class FoodPrinterSystemSettings : ModSettings
    {
        private const int DefaultPastePrintCost = 6;
        private const int DefaultSimpleMealPrintCost = 10;
        private const int DefaultFineMealPrintCost = 14;
        private const int DefaultLavishMealPrintCost = 20;

        public int pastePrintCost = DefaultPastePrintCost;
        public int simpleMealPrintCost = DefaultSimpleMealPrintCost;
        public int fineMealPrintCost = DefaultFineMealPrintCost;
        public int lavishMealPrintCost = DefaultLavishMealPrintCost;
        public int feederOutputLimit = 75;
        public int smallTankCapacity = 100;
        public int mediumTankCapacity = 500;
        public int largeTankCapacity = 1200;
        public int disintegratorIdlePower = 80;
        public int disintegratorActivePower = 300;
        public int smallTankPower = 250;
        public int mediumTankPower = 800;
        public int largeTankPower = 1600;
        public int foodPrinterIdlePower = 30;
        public int foodPrinterPower = 300;
        public int animalFeederIdlePower = 20;
        public int animalFeederPower = 150;
        public int nutrientFeederIdlePower = 20;
        public int nutrientFeederPower = 200;
        public float nutrientFeederTonerCost = 3.0f;
        public bool randomMealSelection = true;
        public bool debugLoggingEnabled = false;
        public bool hardCheckFoodType = true;
        public List<string> disabledModMealDefNames = new List<string>();

        public void ResetToDefaults()
        {
            pastePrintCost = DefaultPastePrintCost;
            simpleMealPrintCost = DefaultSimpleMealPrintCost;
            fineMealPrintCost = DefaultFineMealPrintCost;
            lavishMealPrintCost = DefaultLavishMealPrintCost;
            feederOutputLimit = 75;
            smallTankCapacity = 100;
            mediumTankCapacity = 500;
            largeTankCapacity = 1200;
            disintegratorIdlePower = 80;
            disintegratorActivePower = 300;
            smallTankPower = 250;
            mediumTankPower = 800;
            largeTankPower = 1600;
            foodPrinterIdlePower = 30;
            foodPrinterPower = 300;
            animalFeederIdlePower = 20;
            animalFeederPower = 150;
            nutrientFeederIdlePower = 20;
            nutrientFeederPower = 200;
            nutrientFeederTonerCost = 3.0f;
            randomMealSelection = true;
            debugLoggingEnabled = false;
            hardCheckFoodType = true;
            if (disabledModMealDefNames == null)
            {
                disabledModMealDefNames = new List<string>();
            }
            else
            {
                disabledModMealDefNames.Clear();
            }
            Sanitize();
        }

        public bool RandomMealSelection
        {
            get { return randomMealSelection; }
        }

        public bool HardCheckFoodType
        {
            get { return hardCheckFoodType; }
        }

        public bool DebugLoggingEnabled
        {
            get { return debugLoggingEnabled; }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            int legacyFeederBatchSize = 10;
            Scribe_Values.Look(ref pastePrintCost, "pastePrintCost", DefaultPastePrintCost);
            Scribe_Values.Look(ref simpleMealPrintCost, "simpleMealPrintCost", DefaultSimpleMealPrintCost);
            Scribe_Values.Look(ref fineMealPrintCost, "fineMealPrintCost", DefaultFineMealPrintCost);
            Scribe_Values.Look(ref lavishMealPrintCost, "lavishMealPrintCost", DefaultLavishMealPrintCost);
            Scribe_Values.Look(ref feederOutputLimit, "feederOutputLimit", 75);
            Scribe_Values.Look(ref legacyFeederBatchSize, "feederBatchSize", 10);
            Scribe_Values.Look(ref smallTankCapacity, "smallTankCapacity", 100);
            Scribe_Values.Look(ref mediumTankCapacity, "mediumTankCapacity", 500);
            Scribe_Values.Look(ref largeTankCapacity, "largeTankCapacity", 1200);
            Scribe_Values.Look(ref disintegratorIdlePower, "disintegratorIdlePower", 80);
            Scribe_Values.Look(ref disintegratorActivePower, "disintegratorActivePower", 300);
            Scribe_Values.Look(ref smallTankPower, "smallTankPower", 250);
            Scribe_Values.Look(ref mediumTankPower, "mediumTankPower", 800);
            Scribe_Values.Look(ref largeTankPower, "largeTankPower", 1600);
            Scribe_Values.Look(ref foodPrinterIdlePower, "foodPrinterIdlePower", 30);
            Scribe_Values.Look(ref foodPrinterPower, "foodPrinterPower", 300);
            Scribe_Values.Look(ref animalFeederIdlePower, "animalFeederIdlePower", 20);
            Scribe_Values.Look(ref animalFeederPower, "animalFeederPower", 150);
            Scribe_Values.Look(ref nutrientFeederIdlePower, "nutrientFeederIdlePower", 20);
            Scribe_Values.Look(ref nutrientFeederPower, "nutrientFeederPower", 200);
            Scribe_Values.Look(ref nutrientFeederTonerCost, "nutrientFeederTonerCost", 3.0f);
            Scribe_Values.Look(ref randomMealSelection, "randomMealSelection", true);
            Scribe_Values.Look(ref debugLoggingEnabled, "debugLoggingEnabled", false);
            Scribe_Values.Look(ref hardCheckFoodType, "hardCheckFoodType", true);
            Scribe_Collections.Look(ref disabledModMealDefNames, "disabledModMealDefNames", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (feederOutputLimit == 75 && legacyFeederBatchSize != 10)
                {
                    feederOutputLimit = legacyFeederBatchSize;
                }

                Sanitize();
            }
        }

        public void Sanitize()
        {
            pastePrintCost = Mathf.Clamp(pastePrintCost, 1, 50);
            simpleMealPrintCost = Mathf.Clamp(simpleMealPrintCost, 1, 50);
            fineMealPrintCost = Mathf.Clamp(fineMealPrintCost, 1, 50);
            lavishMealPrintCost = Mathf.Clamp(lavishMealPrintCost, 1, 50);
            feederOutputLimit = Mathf.Clamp(feederOutputLimit, 0, 10000);
            smallTankCapacity = Mathf.Clamp(smallTankCapacity, 0, 100000);
            mediumTankCapacity = Mathf.Clamp(mediumTankCapacity, 0, 100000);
            largeTankCapacity = Mathf.Clamp(largeTankCapacity, 0, 100000);
            disintegratorIdlePower = Mathf.Clamp(disintegratorIdlePower, 0, 10000);
            disintegratorActivePower = Mathf.Clamp(disintegratorActivePower, 0, 10000);
            smallTankPower = Mathf.Clamp(smallTankPower, 0, 10000);
            mediumTankPower = Mathf.Clamp(mediumTankPower, 0, 10000);
            largeTankPower = Mathf.Clamp(largeTankPower, 0, 10000);
            foodPrinterIdlePower = Mathf.Clamp(foodPrinterIdlePower, 0, 10000);
            foodPrinterPower = Mathf.Clamp(foodPrinterPower, 0, 10000);
            animalFeederIdlePower = Mathf.Clamp(animalFeederIdlePower, 0, 10000);
            animalFeederPower = Mathf.Clamp(animalFeederPower, 0, 10000);
            nutrientFeederIdlePower = Mathf.Clamp(nutrientFeederIdlePower, 0, 10000);
            nutrientFeederPower = Mathf.Clamp(nutrientFeederPower, 0, 10000);
            nutrientFeederTonerCost = Mathf.Clamp(nutrientFeederTonerCost, 0.1f, 50f);
            if (disabledModMealDefNames == null)
            {
                disabledModMealDefNames = new List<string>();
                return;
            }

            HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = disabledModMealDefNames.Count - 1; i >= 0; i--)
            {
                string defName = disabledModMealDefNames[i];
                if (defName.NullOrEmpty() || !seen.Add(defName))
                {
                    disabledModMealDefNames.RemoveAt(i);
                }
            }
        }

        public bool IsExternalModMealEnabled(ThingDef mealDef)
        {
            if (mealDef == null || mealDef.defName.NullOrEmpty())
            {
                return false;
            }

            return disabledModMealDefNames == null
                || !disabledModMealDefNames.Contains(mealDef.defName);
        }

        public void SetExternalModMealEnabled(ThingDef mealDef, bool enabled)
        {
            if (mealDef == null || mealDef.defName.NullOrEmpty())
            {
                return;
            }

            if (disabledModMealDefNames == null)
            {
                disabledModMealDefNames = new List<string>();
            }

            for (int i = disabledModMealDefNames.Count - 1; i >= 0; i--)
            {
                if (string.Equals(disabledModMealDefNames[i], mealDef.defName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (enabled)
                    {
                        disabledModMealDefNames.RemoveAt(i);
                    }

                    return;
                }
            }

            if (!enabled)
            {
                disabledModMealDefNames.Add(mealDef.defName);
            }
        }

        public void PruneMissingExternalModMealDefs()
        {
            if (disabledModMealDefNames == null || disabledModMealDefNames.Count == 0)
            {
                return;
            }

            HashSet<string> availableMealDefNames = CompFoodPrinter.GetAvailableExternalModMealDefNames();
            for (int i = disabledModMealDefNames.Count - 1; i >= 0; i--)
            {
                if (!availableMealDefNames.Contains(disabledModMealDefNames[i]))
                {
                    disabledModMealDefNames.RemoveAt(i);
                }
            }
        }
    }

    public class FoodPrinterSystemMod : Mod
    {
        private sealed class IntSliderSetting
        {
            public readonly string LabelKey;
            public readonly string TooltipKey;
            public readonly int Min;
            public readonly int Max;
            public readonly Func<int> Getter;
            public readonly Action<int> Setter;

            public IntSliderSetting(string labelKey, string tooltipKey, int min, int max, Func<int> getter, Action<int> setter)
            {
                LabelKey = labelKey;
                TooltipKey = tooltipKey;
                Min = min;
                Max = max;
                Getter = getter;
                Setter = setter;
            }
        }

        private sealed class IntFieldSetting
        {
            public readonly string LabelKey;
            public readonly string TooltipKey;
            public readonly int Min;
            public readonly int Max;
            public readonly Func<int> Getter;
            public readonly Action<int> Setter;
            public readonly Func<string> BufferGetter;
            public readonly Action<string> BufferSetter;

            public IntFieldSetting(string labelKey, string tooltipKey, int min, int max, Func<int> getter, Action<int> setter, Func<string> bufferGetter, Action<string> bufferSetter)
            {
                LabelKey = labelKey;
                TooltipKey = tooltipKey;
                Min = min;
                Max = max;
                Getter = getter;
                Setter = setter;
                BufferGetter = bufferGetter;
                BufferSetter = bufferSetter;
            }
        }

        private const string HarmonyId = "Codex.FoodPrinterSystem";
        private Vector2 settingsScrollPosition;
        private string feederOutputLimitBuffer;
        private string smallTankCapacityBuffer;
        private string mediumTankCapacityBuffer;
        private string largeTankCapacityBuffer;
        private string disintegratorIdlePowerBuffer;
        private string disintegratorActivePowerBuffer;
        private string smallTankPowerBuffer;
        private string mediumTankPowerBuffer;
        private string largeTankPowerBuffer;
        private string foodPrinterIdlePowerBuffer;
        private string foodPrinterPowerBuffer;
        private string animalFeederIdlePowerBuffer;
        private string animalFeederPowerBuffer;
        private string nutrientFeederIdlePowerBuffer;
        private string nutrientFeederPowerBuffer;
        private bool showDebugSettings;
        private int lastSettingsDrawFrame = -1;
        private static int settingsRevision = 1;
        private IntSliderSetting[] mealCostSliderSettings;
        private IntFieldSetting[] feederFieldSettings;
        private IntFieldSetting[] storageFieldSettings;
        private IntFieldSetting[] powerFieldSettings;

        public static FoodPrinterSystemSettings Settings { get; private set; }
        public static int SettingsRevision
        {
            get { return settingsRevision; }
        }

        public FoodPrinterSystemMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FoodPrinterSystemSettings>();
            InitializeSettingDefinitions();
            Settings.Sanitize();
            BumpSettingsRevision();
            new Harmony(HarmonyId).PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Food Process";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.Sanitize();

            if (Time.frameCount > lastSettingsDrawFrame + 1)
            {
                // Reset foldout state whenever the settings window is opened again.
                showDebugSettings = false;
                Settings.PruneMissingExternalModMealDefs();
            }

            lastSettingsDrawFrame = Time.frameCount;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 1160f);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("FPS_SettingsSectionConsumption".Translate());
            DrawIntSliderSettings(listing, mealCostSliderSettings);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionFeeder".Translate());
            DrawIntFieldSettings(listing, feederFieldSettings);
            Settings.nutrientFeederTonerCost = DrawFloatSlider(listing, "FPS_SettingsNutrientFeederCost".Translate().ToString(), "FPS_SettingsNutrientFeederCostDesc".Translate().ToString(), Settings.nutrientFeederTonerCost, 0.1f, 20f);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionStorage".Translate());
            DrawIntFieldSettings(listing, storageFieldSettings);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionPower".Translate());
            DrawIntFieldSettings(listing, powerFieldSettings);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionPrinter".Translate());
            listing.CheckboxLabeled(
                "FPS_SettingsRandomization".Translate().ToString(),
                ref Settings.randomMealSelection,
                "FPS_SettingsRandomizationDesc".Translate().ToString());
            DrawModMealSelectionButton(listing);

            listing.GapLine();
            DrawDebugSection(listing);

            listing.Gap(6f);
            DrawResetButton(listing);

            listing.End();
            Widgets.EndScrollView();
            Settings.Sanitize();
        }

        public override void WriteSettings()
        {
            Settings.Sanitize();
            base.WriteSettings();
            ApplyLiveSettings();
        }

        public static void ApplyLiveSettings()
        {
            if (Settings == null)
            {
                return;
            }

            Settings.Sanitize();
            Settings.PruneMissingExternalModMealDefs();
            BumpSettingsRevision();
            if (Current.Game == null || Find.Maps == null)
            {
                return;
            }

            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                ApplyToThingsOfDef<Building_FoodDisintegrator>(map, FoodPrinterSystemDefOf.FPS_FoodDisintegrator, disintegrator => disintegrator.ApplyPowerSetting());
                ApplyToThingsOfDef<Building_AnimalFeeder>(map, FoodPrinterSystemDefOf.FPS_AnimalFeeder, feeder => feeder.ApplyPowerSetting());
                ApplyToThingsOfDef<Building_NutrientFeeder>(map, FoodPrinterSystemDefOf.FPS_NutrientFeeder, nutrientFeeder => nutrientFeeder.ApplyPowerSetting());
                ApplyToThingsOfDef<Building_FoodPrinter>(map, FoodPrinterSystemDefOf.FPS_FoodPrinter, printer => printer.TryGetComp<CompFoodPrinter>()?.NotifySettingsChanged());
                NotifyTankSettingsChanged(map);
            }
        }

        private static void ApplyToThingsOfDef<TThing>(Map map, ThingDef def, Action<TThing> applyAction) where TThing : Thing
        {
            if (map?.listerThings == null || def == null || applyAction == null)
            {
                return;
            }

            List<Thing> things = map.listerThings.ThingsOfDef(def);
            if (things == null)
            {
                return;
            }

            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is TThing thing)
                {
                    applyAction(thing);
                }
            }
        }

        private static void NotifyTankSettingsChanged(Map map)
        {
            MapComponent_TonerNetwork networkComponent = FoodPrinterSystemUtility.GetNetworkComponent(map);
            if (networkComponent?.AllTanks == null || networkComponent.AllTanks.Count == 0)
            {
                return;
            }

            foreach (CompTonerTank tank in networkComponent.AllTanks)
            {
                if (tank?.parent != null && tank.parent.Spawned && tank.parent.Map == map)
                {
                    tank.NotifySettingsChanged();
                }
            }
        }

        private void DrawDebugSection(Listing_Standard listing)
        {
            Rect debugRect = listing.GetRect(34f);
            string debugLabel = (showDebugSettings ? "\u25BC " : "\u25B6 ") + "FPS_SettingsSectionDebug".Translate();
            if (Widgets.ButtonText(debugRect, debugLabel))
            {
                showDebugSettings = !showDebugSettings;
            }

            if (!showDebugSettings)
            {
                return;
            }

            listing.CheckboxLabeled(
                "FPS_SettingsDebugLog".Translate().ToString(),
                ref Settings.debugLoggingEnabled,
                "FPS_SettingsDebugLogDesc".Translate().ToString());
            listing.CheckboxLabeled(
                "FPS_SettingsHardCheckFoodType".Translate().ToString(),
                ref Settings.hardCheckFoodType,
                "FPS_SettingsHardCheckFoodTypeDesc".Translate().ToString());
        }

        private void DrawModMealSelectionButton(Listing_Standard listing)
        {
            Rect rect = listing.GetRect(34f);
            string label = "FPS_SettingsModMealSelectionButton".Translate();
            string tooltip = "FPS_SettingsModMealSelectionButtonDesc".Translate();
            AddTooltip(rect, tooltip);
            if (Widgets.ButtonText(rect, label))
            {
                Find.WindowStack.Add(new Dialog_ModMealSelection(Settings));
            }
        }

        private void DrawResetButton(Listing_Standard listing)
        {
            Rect buttonRect = listing.GetRect(34f);
            string label = "FPS_SettingsResetToDefaults".Translate();
            string tooltip = "FPS_SettingsResetToDefaultsDesc".Translate();
            AddTooltip(buttonRect, tooltip);
            if (Widgets.ButtonText(buttonRect, label))
            {
                Settings.ResetToDefaults();
                ClearSettingBuffers();
            }
        }

        private void ClearSettingBuffers()
        {
            ClearBuffers(feederFieldSettings);
            ClearBuffers(storageFieldSettings);
            ClearBuffers(powerFieldSettings);
        }

        private void InitializeSettingDefinitions()
        {
            mealCostSliderSettings = new[]
            {
                new IntSliderSetting("FPS_SettingsPasteCost", "FPS_SettingsPasteCostDesc", 1, 50, () => Settings.pastePrintCost, value => Settings.pastePrintCost = value),
                new IntSliderSetting("FPS_SettingsSimpleCost", "FPS_SettingsSimpleCostDesc", 1, 50, () => Settings.simpleMealPrintCost, value => Settings.simpleMealPrintCost = value),
                new IntSliderSetting("FPS_SettingsFineCost", "FPS_SettingsFineCostDesc", 1, 50, () => Settings.fineMealPrintCost, value => Settings.fineMealPrintCost = value),
                new IntSliderSetting("FPS_SettingsLavishCost", "FPS_SettingsLavishCostDesc", 1, 50, () => Settings.lavishMealPrintCost, value => Settings.lavishMealPrintCost = value)
            };

            feederFieldSettings = new[]
            {
                new IntFieldSetting("FPS_SettingsFeederOutput", "FPS_SettingsFeederOutputDesc", 0, 10000, () => Settings.feederOutputLimit, value => Settings.feederOutputLimit = value, () => feederOutputLimitBuffer, value => feederOutputLimitBuffer = value)
            };

            storageFieldSettings = new[]
            {
                new IntFieldSetting("FPS_SettingsSmallTankCapacity", "FPS_SettingsSmallTankCapacityDesc", 0, 100000, () => Settings.smallTankCapacity, value => Settings.smallTankCapacity = value, () => smallTankCapacityBuffer, value => smallTankCapacityBuffer = value),
                new IntFieldSetting("FPS_SettingsMediumTankCapacity", "FPS_SettingsMediumTankCapacityDesc", 0, 100000, () => Settings.mediumTankCapacity, value => Settings.mediumTankCapacity = value, () => mediumTankCapacityBuffer, value => mediumTankCapacityBuffer = value),
                new IntFieldSetting("FPS_SettingsLargeTankCapacity", "FPS_SettingsLargeTankCapacityDesc", 0, 100000, () => Settings.largeTankCapacity, value => Settings.largeTankCapacity = value, () => largeTankCapacityBuffer, value => largeTankCapacityBuffer = value)
            };

            powerFieldSettings = new[]
            {
                new IntFieldSetting("FPS_SettingsDisintegratorIdlePower", "FPS_SettingsDisintegratorIdlePowerDesc", 0, 10000, () => Settings.disintegratorIdlePower, value => Settings.disintegratorIdlePower = value, () => disintegratorIdlePowerBuffer, value => disintegratorIdlePowerBuffer = value),
                new IntFieldSetting("FPS_SettingsDisintegratorActivePower", "FPS_SettingsDisintegratorActivePowerDesc", 0, 10000, () => Settings.disintegratorActivePower, value => Settings.disintegratorActivePower = value, () => disintegratorActivePowerBuffer, value => disintegratorActivePowerBuffer = value),
                new IntFieldSetting("FPS_SettingsSmallTankPower", "FPS_SettingsSmallTankPowerDesc", 0, 10000, () => Settings.smallTankPower, value => Settings.smallTankPower = value, () => smallTankPowerBuffer, value => smallTankPowerBuffer = value),
                new IntFieldSetting("FPS_SettingsMediumTankPower", "FPS_SettingsMediumTankPowerDesc", 0, 10000, () => Settings.mediumTankPower, value => Settings.mediumTankPower = value, () => mediumTankPowerBuffer, value => mediumTankPowerBuffer = value),
                new IntFieldSetting("FPS_SettingsLargeTankPower", "FPS_SettingsLargeTankPowerDesc", 0, 10000, () => Settings.largeTankPower, value => Settings.largeTankPower = value, () => largeTankPowerBuffer, value => largeTankPowerBuffer = value),
                new IntFieldSetting("FPS_SettingsPrinterIdlePower", "FPS_SettingsPrinterIdlePowerDesc", 0, 10000, () => Settings.foodPrinterIdlePower, value => Settings.foodPrinterIdlePower = value, () => foodPrinterIdlePowerBuffer, value => foodPrinterIdlePowerBuffer = value),
                new IntFieldSetting("FPS_SettingsPrinterPower", "FPS_SettingsPrinterPowerDesc", 0, 10000, () => Settings.foodPrinterPower, value => Settings.foodPrinterPower = value, () => foodPrinterPowerBuffer, value => foodPrinterPowerBuffer = value),
                new IntFieldSetting("FPS_SettingsFeederIdlePower", "FPS_SettingsFeederIdlePowerDesc", 0, 10000, () => Settings.animalFeederIdlePower, value => Settings.animalFeederIdlePower = value, () => animalFeederIdlePowerBuffer, value => animalFeederIdlePowerBuffer = value),
                new IntFieldSetting("FPS_SettingsFeederPower", "FPS_SettingsFeederPowerDesc", 0, 10000, () => Settings.animalFeederPower, value => Settings.animalFeederPower = value, () => animalFeederPowerBuffer, value => animalFeederPowerBuffer = value),
                new IntFieldSetting("FPS_SettingsNutrientFeederIdlePower", "FPS_SettingsNutrientFeederIdlePowerDesc", 0, 10000, () => Settings.nutrientFeederIdlePower, value => Settings.nutrientFeederIdlePower = value, () => nutrientFeederIdlePowerBuffer, value => nutrientFeederIdlePowerBuffer = value),
                new IntFieldSetting("FPS_SettingsNutrientFeederPower", "FPS_SettingsNutrientFeederPowerDesc", 0, 10000, () => Settings.nutrientFeederPower, value => Settings.nutrientFeederPower = value, () => nutrientFeederPowerBuffer, value => nutrientFeederPowerBuffer = value)
            };
        }

        private void DrawIntSliderSettings(Listing_Standard listing, IntSliderSetting[] settings)
        {
            for (int i = 0; i < settings.Length; i++)
            {
                IntSliderSetting setting = settings[i];
                int value = DrawIntSlider(listing, setting.LabelKey.Translate().ToString(), setting.TooltipKey.Translate().ToString(), setting.Getter(), setting.Min, setting.Max);
                setting.Setter(value);
            }
        }

        private void DrawIntFieldSettings(Listing_Standard listing, IntFieldSetting[] settings)
        {
            for (int i = 0; i < settings.Length; i++)
            {
                IntFieldSetting setting = settings[i];
                string buffer = setting.BufferGetter();
                int value = DrawIntField(listing, setting.LabelKey.Translate().ToString(), setting.TooltipKey.Translate().ToString(), setting.Getter(), ref buffer, setting.Min, setting.Max);
                setting.BufferSetter(buffer);
                setting.Setter(value);
            }
        }

        private static void ClearBuffers(IntFieldSetting[] settings)
        {
            for (int i = 0; i < settings.Length; i++)
            {
                settings[i].BufferSetter(null);
            }
        }

        private static int DrawIntSlider(Listing_Standard listing, string label, string tooltip, int value, int min, int max)
        {
            Rect rect = listing.GetRect(34f);
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.42f, rect.height);
            Rect sliderRect = new Rect(rect.x + rect.width * 0.44f, rect.y, rect.width * 0.56f, rect.height);
            Widgets.Label(labelRect, label);
            AddTooltip(rect, tooltip);
            float sliderValue = Widgets.HorizontalSlider(sliderRect, value, min, max, true, value.ToString(), min.ToString(), max.ToString(), 1f);
            return Mathf.RoundToInt(sliderValue);
        }

        private static float DrawFloatSlider(Listing_Standard listing, string label, string tooltip, float value, float min, float max)
        {
            Rect rect = listing.GetRect(34f);
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.42f, rect.height);
            Rect sliderRect = new Rect(rect.x + rect.width * 0.44f, rect.y, rect.width * 0.56f, rect.height);
            Widgets.Label(labelRect, label);
            AddTooltip(rect, tooltip);
            float sliderValue = Widgets.HorizontalSlider(sliderRect, value, min, max, true, value.ToString("0.0"), min.ToString("0.0"), max.ToString("0.0"), 0.1f);
            return sliderValue;
        }

        private static int DrawIntField(Listing_Standard listing, string label, string tooltip, int value, ref string buffer, int min, int max)
        {
            Rect rect = listing.GetRect(34f);
            Rect labelRect = new Rect(rect.x, rect.y + 5f, rect.width * 0.62f, 24f);
            Rect fieldRect = new Rect(rect.x + rect.width * 0.66f, rect.y + 2f, rect.width * 0.34f, 24f);
            Widgets.Label(labelRect, label);
            AddTooltip(rect, tooltip);
            if (buffer.NullOrEmpty())
            {
                buffer = value.ToString();
            }

            Widgets.TextFieldNumeric<int>(fieldRect, ref value, ref buffer, min, max);
            value = Mathf.Clamp(value, min, max);
            buffer = value.ToString();
            return value;
        }

        private static void AddTooltip(Rect rect, string tooltip)
        {
            if (!tooltip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
        }

        private static void BumpSettingsRevision()
        {
            if (settingsRevision == int.MaxValue)
            {
                settingsRevision = 1;
            }
            else
            {
                settingsRevision++;
            }
        }
    }
}
