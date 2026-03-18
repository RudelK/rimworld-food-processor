using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    [StaticConstructorOnStartup]
    public static class FoodPrinterAlertHarmony
    {
        private static readonly float LowFoodNutritionThresholdPerColonist = ResolveLowFoodThreshold();

        private static readonly System.Reflection.FieldInfo AllAlertsFieldInfo = AccessTools.Field(typeof(AlertsReadout), "AllAlerts");
        private static readonly System.Reflection.MethodInfo AlertRecalculateMethodInfo = AccessTools.Method(typeof(Alert), "Recalculate");

        public static void InvalidateAlertCache()
        {
            UIRoot_Play uiRoot = Find.UIRoot as UIRoot_Play;
            if (uiRoot != null && uiRoot.alerts != null && AllAlertsFieldInfo != null && AlertRecalculateMethodInfo != null)
            {
                var allAlertsList = AllAlertsFieldInfo.GetValue(uiRoot.alerts) as System.Collections.IEnumerable;
                if (allAlertsList != null)
                {
                    foreach (Alert alert in allAlertsList)
                    {
                        if (alert is Alert_LowFood)
                        {
                            AlertRecalculateMethodInfo.Invoke(alert, null);
                            break;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Alert_LowFood), "MapWithLowFood")]
        public static class Patch_AlertLowFood_MapWithLowFood
        {
            public static void Postfix(ref Map __result)
            {
                __result = GetMapWithLowFoodIncludingToner();
            }
        }

        private static Map GetMapWithLowFoodIncludingToner()
        {
            if (Find.Maps == null)
            {
                return null;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (!IsColonyFoodMap(map))
                {
                    continue;
                }

                if (GetTotalAvailableNutrition(map) < map.mapPawns.FreeColonistsSpawnedCount * LowFoodNutritionThresholdPerColonist)
                {
                    return map;
                }
            }

            return null;
        }

        private static bool IsColonyFoodMap(Map map)
        {
            return map != null
                && map.IsPlayerHome
                && map.mapPawns != null
                && map.mapPawns.FreeColonistsSpawnedCount > 0;
        }

        private static float GetTotalAvailableNutrition(Map map)
        {
            return GetEdibleFoodNutrition(map) + GetStoredTonerNutrition(map);
        }

        private static float GetEdibleFoodNutrition(Map map)
        {
            if (map != null && map.resourceCounter != null)
            {
                return map.resourceCounter.TotalHumanEdibleNutrition;
            }
            return 0f;
        }

        private static float GetStoredTonerNutrition(Map map)
        {
            if (map == null || map.listerThings == null || map.listerThings.AllThings == null)
            {
                return 0f;
            }

            float totalNutrition = 0f;
            List<Thing> allThings = map.listerThings.AllThings;
            for (int i = 0; i < allThings.Count; i++)
            {
                CompTonerTank tank = allThings[i].TryGetComp<CompTonerTank>();
                if (tank != null)
                {
                    totalNutrition += tank.StoredToner * FoodPrinterSystemUtility.NutritionPerUnit;
                }
            }

            return totalNutrition;
        }

        private static float ResolveLowFoodThreshold()
        {
            object thresholdValue = AccessTools.Field(typeof(Alert_LowFood), "NutritionThresholdPerColonist")?.GetValue(null);
            return thresholdValue is float nutritionThreshold ? nutritionThreshold : 4f;
        }
    }
}
