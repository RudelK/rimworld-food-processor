using HarmonyLib;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class FoodPrinterSystemSettings : ModSettings
    {
        public int pastePrintCost = 3;
        public int simpleMealPrintCost = 5;
        public int fineMealPrintCost = 7;
        public int lavishMealPrintCost = 10;
        public int feederOutputLimit = 75;
        public int smallTankCapacity = 500;
        public int mediumTankCapacity = 1900;
        public int largeTankCapacity = 4300;
        public int disintegratorIdlePower = 50;
        public int disintegratorActivePower = 500;
        public int smallTankPower = 200;
        public int mediumTankPower = 700;
        public int largeTankPower = 1500;
        public int foodPrinterPower = 200;
        public int animalFeederPower = 100;
        public float bedsideFeederTonerCost = 3.0f;
        public bool randomMealSelection = true;
        public bool hardCheckFoodType = true;

        public bool RandomMealSelection
        {
            get { return randomMealSelection; }
        }

        public bool HardCheckFoodType
        {
            get { return hardCheckFoodType; }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            int legacyFeederBatchSize = 10;
            Scribe_Values.Look(ref pastePrintCost, "pastePrintCost", 3);
            Scribe_Values.Look(ref simpleMealPrintCost, "simpleMealPrintCost", 5);
            Scribe_Values.Look(ref fineMealPrintCost, "fineMealPrintCost", 7);
            Scribe_Values.Look(ref lavishMealPrintCost, "lavishMealPrintCost", 10);
            Scribe_Values.Look(ref feederOutputLimit, "feederOutputLimit", 75);
            Scribe_Values.Look(ref legacyFeederBatchSize, "feederBatchSize", 10);
            Scribe_Values.Look(ref smallTankCapacity, "smallTankCapacity", 500);
            Scribe_Values.Look(ref mediumTankCapacity, "mediumTankCapacity", 1900);
            Scribe_Values.Look(ref largeTankCapacity, "largeTankCapacity", 4300);
            Scribe_Values.Look(ref disintegratorIdlePower, "disintegratorIdlePower", 50);
            Scribe_Values.Look(ref disintegratorActivePower, "disintegratorActivePower", 500);
            Scribe_Values.Look(ref smallTankPower, "smallTankPower", 200);
            Scribe_Values.Look(ref mediumTankPower, "mediumTankPower", 700);
            Scribe_Values.Look(ref largeTankPower, "largeTankPower", 1500);
            Scribe_Values.Look(ref foodPrinterPower, "foodPrinterPower", 200);
            Scribe_Values.Look(ref animalFeederPower, "animalFeederPower", 100);
            Scribe_Values.Look(ref bedsideFeederTonerCost, "bedsideFeederTonerCost", 3.0f);
            Scribe_Values.Look(ref randomMealSelection, "randomMealSelection", true);
            Scribe_Values.Look(ref hardCheckFoodType, "hardCheckFoodType", true);

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
            foodPrinterPower = Mathf.Clamp(foodPrinterPower, 0, 10000);
            animalFeederPower = Mathf.Clamp(animalFeederPower, 0, 10000);
            bedsideFeederTonerCost = Mathf.Clamp(bedsideFeederTonerCost, 0.1f, 50f);
        }
    }

    public class FoodPrinterSystemMod : Mod
    {
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
        private string foodPrinterPowerBuffer;
        private string animalFeederPowerBuffer;

        public static FoodPrinterSystemSettings Settings { get; private set; }

        public FoodPrinterSystemMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FoodPrinterSystemSettings>();
            Settings.Sanitize();
            new Harmony(HarmonyId).PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Food Process";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.Sanitize();

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 800f);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("FPS_SettingsSectionConsumption".Translate());
            Settings.pastePrintCost = DrawIntSlider(listing, "FPS_SettingsPasteCost".Translate().ToString(), Settings.pastePrintCost, 1, 50);
            Settings.simpleMealPrintCost = DrawIntSlider(listing, "FPS_SettingsSimpleCost".Translate().ToString(), Settings.simpleMealPrintCost, 1, 50);
            Settings.fineMealPrintCost = DrawIntSlider(listing, "FPS_SettingsFineCost".Translate().ToString(), Settings.fineMealPrintCost, 1, 50);
            Settings.lavishMealPrintCost = DrawIntSlider(listing, "FPS_SettingsLavishCost".Translate().ToString(), Settings.lavishMealPrintCost, 1, 50);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionFeeder".Translate());
            Settings.feederOutputLimit = DrawIntField(listing, "FPS_SettingsFeederOutput".Translate().ToString(), Settings.feederOutputLimit, ref feederOutputLimitBuffer, 0, 10000);
            Settings.bedsideFeederTonerCost = DrawFloatSlider(listing, "FPS_SettingsBedsideFeederCost".Translate().ToString(), Settings.bedsideFeederTonerCost, 0.1f, 20f);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionStorage".Translate());
            Settings.smallTankCapacity = DrawIntField(listing, "FPS_SettingsSmallTankCapacity".Translate().ToString(), Settings.smallTankCapacity, ref smallTankCapacityBuffer, 0, 100000);
            Settings.mediumTankCapacity = DrawIntField(listing, "FPS_SettingsMediumTankCapacity".Translate().ToString(), Settings.mediumTankCapacity, ref mediumTankCapacityBuffer, 0, 100000);
            Settings.largeTankCapacity = DrawIntField(listing, "FPS_SettingsLargeTankCapacity".Translate().ToString(), Settings.largeTankCapacity, ref largeTankCapacityBuffer, 0, 100000);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionPower".Translate());
            Settings.disintegratorIdlePower = DrawIntField(listing, "FPS_SettingsDisintegratorIdlePower".Translate().ToString(), Settings.disintegratorIdlePower, ref disintegratorIdlePowerBuffer, 0, 10000);
            Settings.disintegratorActivePower = DrawIntField(listing, "FPS_SettingsDisintegratorActivePower".Translate().ToString(), Settings.disintegratorActivePower, ref disintegratorActivePowerBuffer, 0, 10000);
            Settings.smallTankPower = DrawIntField(listing, "FPS_SettingsSmallTankPower".Translate().ToString(), Settings.smallTankPower, ref smallTankPowerBuffer, 0, 10000);
            Settings.mediumTankPower = DrawIntField(listing, "FPS_SettingsMediumTankPower".Translate().ToString(), Settings.mediumTankPower, ref mediumTankPowerBuffer, 0, 10000);
            Settings.largeTankPower = DrawIntField(listing, "FPS_SettingsLargeTankPower".Translate().ToString(), Settings.largeTankPower, ref largeTankPowerBuffer, 0, 10000);
            Settings.foodPrinterPower = DrawIntField(listing, "FPS_SettingsPrinterPower".Translate().ToString(), Settings.foodPrinterPower, ref foodPrinterPowerBuffer, 0, 10000);
            Settings.animalFeederPower = DrawIntField(listing, "FPS_SettingsFeederPower".Translate().ToString(), Settings.animalFeederPower, ref animalFeederPowerBuffer, 0, 10000);

            listing.GapLine();
            listing.Label("FPS_SettingsSectionPrinter".Translate());
            listing.CheckboxLabeled("FPS_SettingsRandomization".Translate().ToString(), ref Settings.randomMealSelection);
            listing.CheckboxLabeled(
                "FPS_SettingsHardCheckFoodType".Translate().ToString(),
                ref Settings.hardCheckFoodType,
                "FPS_SettingsHardCheckFoodTypeDesc".Translate().ToString());

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
            if (Current.Game == null || Find.Maps == null)
            {
                return;
            }

            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                Map map = Find.Maps[mapIndex];
                MapComponent_TonerNetwork network = FoodPrinterSystemUtility.GetNetworkComponent(map);
                if (network != null)
                {
                    network.MarkDirty();
                }

                for (int i = 0; i < map.listerThings.AllThings.Count; i++)
                {
                    Thing thing = map.listerThings.AllThings[i];

                    Building_FoodDisintegrator disintegrator = thing as Building_FoodDisintegrator;
                    if (disintegrator != null)
                    {
                        disintegrator.ApplyPowerSetting();
                    }

                    Building_AnimalFeeder feeder = thing as Building_AnimalFeeder;
                    if (feeder != null)
                    {
                        feeder.ApplyPowerSetting();
                    }

                    CompFoodPrinter printer = thing.TryGetComp<CompFoodPrinter>();
                    if (printer != null)
                    {
                        printer.ApplyPowerSetting();
                    }

                    CompTonerTank tank = thing.TryGetComp<CompTonerTank>();
                    if (tank != null)
                    {
                        tank.NotifySettingsChanged();
                    }
                }
            }
        }

        private static int DrawIntSlider(Listing_Standard listing, string label, int value, int min, int max)
        {
            Rect rect = listing.GetRect(34f);
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.42f, rect.height);
            Rect sliderRect = new Rect(rect.x + rect.width * 0.44f, rect.y, rect.width * 0.56f, rect.height);
            Widgets.Label(labelRect, label);
            float sliderValue = Widgets.HorizontalSlider(sliderRect, value, min, max, true, value.ToString(), min.ToString(), max.ToString(), 1f);
            return Mathf.RoundToInt(sliderValue);
        }

        private static float DrawFloatSlider(Listing_Standard listing, string label, float value, float min, float max)
        {
            Rect rect = listing.GetRect(34f);
            Rect labelRect = new Rect(rect.x, rect.y, rect.width * 0.42f, rect.height);
            Rect sliderRect = new Rect(rect.x + rect.width * 0.44f, rect.y, rect.width * 0.56f, rect.height);
            Widgets.Label(labelRect, label);
            float sliderValue = Widgets.HorizontalSlider(sliderRect, value, min, max, true, value.ToString("0.0"), min.ToString("0.0"), max.ToString("0.0"), 0.1f);
            return sliderValue;
        }

        private static int DrawIntField(Listing_Standard listing, string label, int value, ref string buffer, int min, int max)
        {
            Rect rect = listing.GetRect(34f);
            Rect labelRect = new Rect(rect.x, rect.y + 5f, rect.width * 0.62f, 24f);
            Rect fieldRect = new Rect(rect.x + rect.width * 0.66f, rect.y + 2f, rect.width * 0.34f, 24f);
            Widgets.Label(labelRect, label);
            if (buffer.NullOrEmpty())
            {
                buffer = value.ToString();
            }

            Widgets.TextFieldNumeric<int>(fieldRect, ref value, ref buffer, min, max);
            value = Mathf.Clamp(value, min, max);
            buffer = value.ToString();
            return value;
        }
    }
}
