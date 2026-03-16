using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_AnimalFeeder : Building_Storage
    {
        private const int FixedBatchSize = 10;

        private CompPowerTrader powerComp;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            ApplyPowerSetting();
        }

        public override void TickRare()
        {
            base.TickRare();
            ApplyPowerSetting();

            if (powerComp == null || !powerComp.PowerOn)
            {
                return;
            }

            int outputLimit = FoodPrinterSystemUtility.GetAnimalFeederOutputLimit();
            int storedKibble = CountStoredKibble();
            int remainingCapacity = outputLimit - storedKibble;
            if (remainingCapacity <= 0)
            {
                return;
            }

            int batchSize = Mathf.Min(remainingCapacity, FixedBatchSize);
            int tonerCostPerKibble = FoodPrinterSystemUtility.GetPrintCost(ThingDefOf.Kibble);
            int tonerCost = tonerCostPerKibble * batchSize;
            if (tonerCost <= 0 || !TonerNetworkUtility.TryConsumeToner(this, tonerCost))
            {
                return;
            }

            Thing kibble = ThingMaker.MakeThing(ThingDefOf.Kibble);
            kibble.stackCount = batchSize;

            if (!TryStoreKibble(kibble))
            {
                TonerNetworkUtility.TryAddToner(this, tonerCost);
                if (!kibble.Destroyed)
                {
                    kibble.Destroy(DestroyMode.Vanish);
                }
            }
        }

        public void ApplyPowerSetting()
        {
            if (powerComp == null)
            {
                powerComp = GetComp<CompPowerTrader>();
            }

            if (powerComp != null)
            {
                powerComp.PowerOutput = -FoodPrinterSystemUtility.GetConstantPowerDraw(def);
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            TonerNetworkSummary summary = TonerNetworkUtility.GetSummary(this);
            if (!text.NullOrEmpty())
            {
                text += "\n";
            }

            text += FoodPrinterSystemUtility.FormatSummary(summary);
            return text;
        }

        private int CountStoredKibble()
        {
            int total = 0;
            foreach (IntVec3 cell in this.OccupiedRect())
            {
                if (!cell.InBounds(Map))
                {
                    continue;
                }

                var things = cell.GetThingList(Map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i].def == ThingDefOf.Kibble)
                    {
                        total += things[i].stackCount;
                    }
                }
            }

            return total;
        }

        private bool TryStoreKibble(Thing kibble)
        {
            foreach (IntVec3 cell in this.OccupiedRect())
            {
                if (cell.InBounds(Map) && GenPlace.TryPlaceThing(kibble, cell, Map, ThingPlaceMode.Direct))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
