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
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0)
            {
                return 0f;
            }

            List<Thing> allThings = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Everything), allThings, false, null, true);
            float totalNutrition = 0f;
            for (int i = 0; i < allThings.Count; i++)
            {
                Thing thing = allThings[i];
                if (!IsCountedFoodThing(thing))
                {
                    continue;
                }

                if (!CanAnyColonistEat(thing, colonists))
                {
                    continue;
                }

                totalNutrition += thing.GetStatValue(StatDefOf.Nutrition) * thing.stackCount;
            }

            return totalNutrition;
        }

        private static bool IsCountedFoodThing(Thing thing)
        {
            return thing != null
                && thing.def != null
                && thing.def.IsNutritionGivingIngestible
                && !(thing is Corpse);
        }

        private static bool CanAnyColonistEat(Thing thing, List<Pawn> colonists)
        {
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn colonist = colonists[i];
                if (colonist != null && FoodUtility.WillEat(colonist, thing, colonist, false, false))
                {
                    return true;
                }
            }

            return false;
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
