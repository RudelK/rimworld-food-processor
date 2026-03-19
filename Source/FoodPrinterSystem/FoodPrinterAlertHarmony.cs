using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    [StaticConstructorOnStartup]
    public static class FoodPrinterAlertHarmony
    {
        private static readonly System.Reflection.FieldInfo AllAlertsFieldInfo = AccessTools.Field(typeof(AlertsReadout), "AllAlerts");
        private static readonly System.Reflection.MethodInfo AlertRecalculateMethodInfo = AccessTools.Method(typeof(Alert), "Recalculate");
        private static readonly System.Reflection.FieldInfo ResourceCounterMapFieldInfo = AccessTools.Field(typeof(ResourceCounter), "map");
        private static int lastInvalidationTick = -1;

        public static void InvalidateAlertCache()
        {
            int currentTick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            if (currentTick >= 0 && lastInvalidationTick == currentTick)
            {
                return;
            }

            lastInvalidationTick = currentTick;
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

        [HarmonyPatch(typeof(ResourceCounter), "get_TotalHumanEdibleNutrition")]
        public static class Patch_ResourceCounter_TotalHumanEdibleNutrition
        {
            public static void Postfix(ResourceCounter __instance, ref float __result)
            {
                Map map = ResourceCounterMapFieldInfo == null ? null : ResourceCounterMapFieldInfo.GetValue(__instance) as Map;
                if (map != null)
                {
                    MapComponent_TonerNetwork netComp = map.GetComponent<MapComponent_TonerNetwork>();
                    if (netComp != null)
                    {
                        __result += netComp.GetTotalTonerNutrition();
                    }
                }
            }
        }
    }
}
