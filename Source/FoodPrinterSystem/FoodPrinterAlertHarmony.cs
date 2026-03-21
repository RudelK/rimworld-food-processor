using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    public sealed class Alert_TonerConsumerNeedsTank : Alert
    {
        private sealed class CachedMapReport
        {
            public int NetworkRevision;
            public int SettingsRevision;
            public int ConsumerRegistryRevision;
            public List<Thing> Culprits = new List<Thing>();
        }

        private readonly Dictionary<int, CachedMapReport> cachedReportsByMapId = new Dictionary<int, CachedMapReport>();

        public Alert_TonerConsumerNeedsTank()
        {
            defaultPriority = AlertPriority.High;
            defaultLabel = "FPS_PrinterNeedsTankAlert".Translate();
            defaultExplanation = "FPS_PrinterNeedsTankAlertDesc".Translate();
        }

        public override AlertReport GetReport()
        {
            List<Thing> culprits = GetCulprits();
            return culprits.Count == 0 ? false : AlertReport.CulpritsAre(culprits);
        }

        public override TaggedString GetExplanation()
        {
            return "FPS_PrinterNeedsTankAlertDesc".Translate();
        }

        private List<Thing> GetCulprits()
        {
            List<Thing> culprits = new List<Thing>();
            if (Find.Maps == null)
            {
                return culprits;
            }

            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                if (map == null)
                {
                    continue;
                }

                List<Thing> cachedCulprits = GetCachedMapCulprits(map);
                for (int j = 0; j < cachedCulprits.Count; j++)
                {
                    culprits.Add(cachedCulprits[j]);
                }
            }

            return culprits;
        }

        private List<Thing> GetCachedMapCulprits(Map map)
        {
            MapComponent_TonerNetwork networkComponent = map.GetComponent<MapComponent_TonerNetwork>();
            int networkRevision = networkComponent == null ? 0 : networkComponent.NetworkRevision;
            int settingsRevision = FoodPrinterSystemMod.SettingsRevision;
            int consumerRegistryRevision = FoodPrinterAlertHarmony.ConsumerRegistryRevision;

            if (cachedReportsByMapId.TryGetValue(map.uniqueID, out CachedMapReport cachedReport)
                && cachedReport.NetworkRevision == networkRevision
                && cachedReport.SettingsRevision == settingsRevision
                && cachedReport.ConsumerRegistryRevision == consumerRegistryRevision)
            {
                return cachedReport.Culprits;
            }

            List<Thing> culprits = new List<Thing>();
            AddDisconnectedConsumers(culprits, map, FoodPrinterSystemDefOf.FPS_FoodPrinter);
            AddDisconnectedConsumers(culprits, map, FoodPrinterSystemDefOf.FPS_AnimalFeeder);
            AddDisconnectedConsumers(culprits, map, FoodPrinterSystemDefOf.FPS_NutrientFeeder);

            cachedReportsByMapId[map.uniqueID] = new CachedMapReport
            {
                NetworkRevision = networkRevision,
                SettingsRevision = settingsRevision,
                ConsumerRegistryRevision = consumerRegistryRevision,
                Culprits = culprits
            };
            return culprits;
        }

        private static void AddDisconnectedConsumers(List<Thing> culprits, Map map, ThingDef consumerDef)
        {
            if (culprits == null || map == null || consumerDef == null || map.listerThings == null)
            {
                return;
            }

            List<Thing> consumerThings = map.listerThings.ThingsOfDef(consumerDef);
            if (consumerThings == null)
            {
                return;
            }

            for (int i = 0; i < consumerThings.Count; i++)
            {
                Thing consumer = consumerThings[i];
                if (consumer != null
                    && consumer.Spawned
                    && consumer.Faction == Faction.OfPlayer
                    && !FoodPrinterAlertHarmony.HasValidConsumerFeedSource(consumer))
                {
                    culprits.Add(consumer);
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class FoodPrinterAlertHarmony
    {
        private static readonly System.Reflection.FieldInfo AllAlertsFieldInfo = AccessTools.Field(typeof(AlertsReadout), "AllAlerts");
        private static readonly System.Reflection.MethodInfo AlertRecalculateMethodInfo = AccessTools.Method(typeof(Alert), "Recalculate");
        private static readonly System.Reflection.FieldInfo ResourceCounterMapFieldInfo = AccessTools.Field(typeof(ResourceCounter), "map");
        private static int lastInvalidationTick = -1;
        private static int consumerRegistryRevision = 1;

        public static int ConsumerRegistryRevision
        {
            get { return consumerRegistryRevision; }
        }

        public static bool HasValidConsumerFeedSource(Thing thing)
        {
            return TonerPipeNetManager.HasConnectedStorage(thing);
        }

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
                        if (alert is Alert_LowFood || alert is Alert_TonerConsumerNeedsTank)
                        {
                            AlertRecalculateMethodInfo.Invoke(alert, null);
                        }
                    }
                }
            }
        }

        public static void NotifyConsumerRegistryChanged()
        {
            if (consumerRegistryRevision == int.MaxValue)
            {
                consumerRegistryRevision = 1;
            }
            else
            {
                consumerRegistryRevision++;
            }

            InvalidateAlertCache();
        }

        public static void NotifyPrinterRegistryChanged()
        {
            NotifyConsumerRegistryChanged();
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

        [HarmonyPatch(typeof(Alert_PasteDispenserNeedsHopper), "get_BadDispensers")]
        public static class Patch_Alert_PasteDispenserNeedsHopper_BadDispensers
        {
            public static void Postfix(ref List<Thing> __result)
            {
                if (__result == null || __result.Count == 0)
                {
                    return;
                }

                __result.RemoveAll(thing => thing is Building_FoodPrinter);
            }
        }
    }
}
