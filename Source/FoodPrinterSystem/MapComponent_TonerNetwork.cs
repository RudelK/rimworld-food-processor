using Verse;

namespace FoodPrinterSystem
{
    public class MapComponent_TonerNetwork : PipeMapComponent
    {
        public System.Collections.Generic.HashSet<CompTonerTank> AllTanks = new System.Collections.Generic.HashSet<CompTonerTank>();
        private int ingredientRevision = 1;
        private int storageRevision = 1;
        private int cachedNutritionRevision = -1;
        private float cachedTotalTonerNutrition;

        public MapComponent_TonerNetwork(Map map) : base(map)
        {
        }

        public int ContentsRevision
        {
            get { return unchecked((ingredientRevision * 397) ^ storageRevision); }
        }

        public int IngredientRevision
        {
            get { return ingredientRevision; }
        }

        public int StorageRevision
        {
            get { return storageRevision; }
        }

        public void RegisterTank(CompTonerTank tank)
        {
            if (tank != null && AllTanks.Add(tank))
            {
                NotifyTankConfigurationChanged();
            }
        }

        public void DeregisterTank(CompTonerTank tank)
        {
            if (tank != null && AllTanks.Remove(tank))
            {
                NotifyTankConfigurationChanged();
            }
        }

        public void NotifyContentsChanged()
        {
            NotifyTankConfigurationChanged();
        }

        public void NotifyIngredientStateChanged()
        {
            ingredientRevision = BumpRevision(ingredientRevision);
        }

        public void NotifyStorageStateChanged()
        {
            storageRevision = BumpRevision(storageRevision);
            cachedNutritionRevision = -1;
        }

        public void NotifyTankConfigurationChanged()
        {
            ingredientRevision = BumpRevision(ingredientRevision);
            storageRevision = BumpRevision(storageRevision);
            cachedNutritionRevision = -1;
        }

        public float GetTotalTonerNutrition()
        {
            if (cachedNutritionRevision == storageRevision)
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
            cachedNutritionRevision = storageRevision;
            return total;
        }

        private static int BumpRevision(int revision)
        {
            return revision == int.MaxValue ? 1 : revision + 1;
        }
    }
}
