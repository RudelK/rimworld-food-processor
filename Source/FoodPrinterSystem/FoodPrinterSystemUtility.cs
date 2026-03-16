using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public struct TonerNetworkSummary
    {
        public bool HasNetwork;
        public int Stored;
        public int Capacity;
    }

    public static class FoodPrinterSystemUtility
    {
        public const float NutritionPerUnit = 0.05f;
        private const int TicksPerHour = 2500;
        public const int PrintingDelayTicks = 250;
        private static readonly FoodPrinterSystemSettings DefaultSettings = new FoodPrinterSystemSettings();

        private static FoodPrinterSystemSettings CurrentSettings
        {
            get { return FoodPrinterSystemMod.Settings ?? DefaultSettings; }
        }

        public static int GetStoredTonerValue(Thing thing)
        {
            return thing == null ? 0 : GetStoredTonerValue(thing.def);
        }

        public static int GetStoredTonerValue(ThingDef thingDef)
        {
            if (thingDef == null || !thingDef.IsNutritionGivingIngestible)
            {
                return 0;
            }

            return ToTonerUnits(thingDef.GetStatValueAbstract(StatDefOf.Nutrition));
        }

        public static int GetPrintCost(ThingDef thingDef)
        {
            if (thingDef == null || !thingDef.IsNutritionGivingIngestible)
            {
                return 0;
            }

            if (thingDef.defName == "MealNutrientPaste")
            {
                return CurrentSettings.pastePrintCost;
            }

            if (thingDef.ingestible == null)
            {
                return GetStoredTonerValue(thingDef);
            }

            switch (thingDef.ingestible.preferability)
            {
                case FoodPreferability.MealSimple:
                    return CurrentSettings.simpleMealPrintCost;
                case FoodPreferability.MealFine:
                    return CurrentSettings.fineMealPrintCost;
                case FoodPreferability.MealLavish:
                    return CurrentSettings.lavishMealPrintCost;
                default:
                    return GetStoredTonerValue(thingDef);
            }
        }

        public static int GetCategoryPrintCost(FoodPreferability category)
        {
            switch (category)
            {
                case FoodPreferability.MealAwful:
                    return CurrentSettings.pastePrintCost;
                case FoodPreferability.MealSimple:
                    return CurrentSettings.simpleMealPrintCost;
                case FoodPreferability.MealFine:
                    return CurrentSettings.fineMealPrintCost;
                case FoodPreferability.MealLavish:
                    return CurrentSettings.lavishMealPrintCost;
                default:
                    return 0;
            }
        }

        public static int ToTonerUnits(float nutrition)
        {
            if (nutrition <= 0f)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(nutrition / NutritionPerUnit));
        }

        public static int NormalizeTonerAmount(float amount)
        {
            if (amount <= 0f)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(amount));
        }

        public static int ConvertLegacyStoredTonerUnits(int legacyUnits)
        {
            if (legacyUnits <= 0)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(legacyUnits / 5f));
        }

        public static int GetTankCapacity(ThingDef thingDef)
        {
            if (thingDef == null)
            {
                return 0;
            }

            switch (thingDef.defName)
            {
                case "FPS_TonerTankSmall":
                    return CurrentSettings.smallTankCapacity;
                case "FPS_TonerTankMedium":
                    return CurrentSettings.mediumTankCapacity;
                case "FPS_TonerTankLarge":
                    return CurrentSettings.largeTankCapacity;
                default:
                    return 0;
            }
        }

        public static float GetConstantPowerDraw(ThingDef thingDef)
        {
            if (thingDef == null)
            {
                return 0f;
            }

            switch (thingDef.defName)
            {
                case "FPS_TonerTankSmall":
                    return CurrentSettings.smallTankPower;
                case "FPS_TonerTankMedium":
                    return CurrentSettings.mediumTankPower;
                case "FPS_TonerTankLarge":
                    return CurrentSettings.largeTankPower;
                case "FPS_FoodPrinter":
                    return CurrentSettings.foodPrinterPower;
                case "FPS_AnimalFeeder":
                    return CurrentSettings.animalFeederPower;
                default:
                    return 0f;
            }
        }

        public static float GetDisintegratorIdlePowerDraw()
        {
            return CurrentSettings.disintegratorIdlePower;
        }

        public static float GetDisintegratorActivePowerDraw()
        {
            return CurrentSettings.disintegratorActivePower;
        }

        public static int GetAnimalFeederOutputLimit()
        {
            return CurrentSettings.feederOutputLimit;
        }

        public static bool RandomMealSelectionEnabled
        {
            get { return CurrentSettings.RandomMealSelection; }
        }

        public static bool IsProcessableFood(Thing thing)
        {
            return thing != null
                && thing.def.category == ThingCategory.Item
                && thing.def.IsNutritionGivingIngestible
                && GetStoredTonerValue(thing) > 0
                && !(thing is Corpse);
        }

        public static string FormatToner(int amount)
        {
            return amount.ToString();
        }

        public static string FormatHoursRemaining(int ticks)
        {
            float hours = ticks / (float)TicksPerHour;
            return hours.ToString("0.0");
        }

        public static string FormatSummary(TonerNetworkSummary summary)
        {
            return "FPS_TonerNetwork".Translate(FormatToner(summary.Stored), FormatToner(summary.Capacity));
        }

        public static MapComponent_TonerNetwork GetNetworkComponent(Map map)
        {
            return map == null ? null : map.GetComponent<MapComponent_TonerNetwork>();
        }
    }
}
