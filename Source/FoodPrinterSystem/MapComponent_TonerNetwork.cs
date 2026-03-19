using Verse;

namespace FoodPrinterSystem
{
    public class MapComponent_TonerNetwork : PipeMapComponent
    {
        public System.Collections.Generic.HashSet<CompTonerTank> AllTanks = new System.Collections.Generic.HashSet<CompTonerTank>();
        private int contentsRevision = 1;
        private int cachedNutritionRevision = -1;
        private float cachedTotalTonerNutrition;

        public MapComponent_TonerNetwork(Map map) : base(map)
        {
        }

        public int ContentsRevision
        {
            get { return contentsRevision; }
        }

        public void RegisterTank(CompTonerTank tank)
        {
            if (tank != null && AllTanks.Add(tank))
            {
                NotifyContentsChanged();
            }
        }

        public void DeregisterTank(CompTonerTank tank)
        {
            if (tank != null && AllTanks.Remove(tank))
            {
                NotifyContentsChanged();
            }
        }

        public void NotifyContentsChanged()
        {
            if (contentsRevision == int.MaxValue)
            {
                contentsRevision = 1;
            }
            else
            {
                contentsRevision++;
            }

            cachedNutritionRevision = -1;
        }

        public float GetTotalTonerNutrition()
        {
            if (cachedNutritionRevision == contentsRevision)
            {
                return cachedTotalTonerNutrition;
            }

            float total = 0f;
            foreach (CompTonerTank tank in AllTanks)
            {
                if (tank != null && tank.parent != null && tank.parent.Spawned)
                {
                    total += tank.StoredToner * FoodPrinterSystemUtility.NutritionPerUnit;
                }
            }

            cachedTotalTonerNutrition = total;
            cachedNutritionRevision = contentsRevision;
            return total;
        }
    }

    public static class TonerNetworkUtility
    {
        public static TonerNetworkSummary GetSummary(Thing node)
        {
            return TonerPipeNetManager.GetSummary(node);
        }

        public static bool TryAddToner(Thing node, int amount)
        {
            return TonerPipeNetManager.TryAddToner(node, amount);
        }

        public static bool TryConsumeToner(Thing node, int amount)
        {
            return TonerPipeNetManager.TryDrawToner(node, amount);
        }

        public static void DistributeIngredients(Thing node, System.Collections.Generic.List<ThingDef> ingredients)
        {
            TonerPipeNetManager.DistributeIngredients(node, ingredients);
        }

        public static System.Collections.Generic.List<ThingDef> GetAllIngredients(Thing node)
        {
            return TonerPipeNetManager.GetAllIngredients(node);
        }

        public static void NotifyContentsChanged(Map map)
        {
            TonerPipeNetManager.NotifyContentsChanged(map);
        }
    }
}
