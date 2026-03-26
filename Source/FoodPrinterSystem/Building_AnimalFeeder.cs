using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_AnimalFeeder : Building_Storage
    {
        private const int FixedBatchSize = 10;
        private const int KibbleRecountIntervalTicks = GenTicks.TickRareInterval * 4;

        private CompPowerTrader powerComp;
        private int activeTicksRemaining;
        private int cachedStoredKibble = -1;
        private int nextKibbleRecountTick;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            ApplyPowerSetting();
            FoodPrinterAlertHarmony.NotifyConsumerRegistryChanged();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            FoodPrinterAlertHarmony.NotifyConsumerRegistryChanged();
        }

        public override void TickRare()
        {
            base.TickRare();

            bool wasActive = activeTicksRemaining > 0;
            if (activeTicksRemaining > 0)
            {
                activeTicksRemaining -= GenTicks.TickRareInterval;
                if (activeTicksRemaining < 0)
                {
                    activeTicksRemaining = 0;
                }
            }

            if (wasActive != (activeTicksRemaining > 0))
            {
                ApplyPowerSetting();
            }

            if (powerComp == null || !powerComp.PowerOn)
            {
                return;
            }

            int outputLimit = FoodPrinterSystemUtility.GetAnimalFeederOutputLimit();
            int storedKibble = GetStoredKibbleCount();
            int remainingCapacity = outputLimit - storedKibble;
            if (remainingCapacity <= 0)
            {
                return;
            }

            int batchSize = Mathf.Min(remainingCapacity, FixedBatchSize);
            int tonerCostPerKibble = FoodPrinterSystemUtility.GetPrintCost(ThingDefOf.Kibble);
            int tonerCost = tonerCostPerKibble * batchSize;
            if (tonerCost <= 0 || !TonerPipeNetManager.TryDrawToner(this, tonerCost))
            {
                return;
            }

            Thing kibble = ThingMaker.MakeThing(ThingDefOf.Kibble);
            kibble.stackCount = batchSize;

            if (!TryStoreKibble(kibble))
            {
                TonerPipeNetManager.TryAddToner(this, tonerCost);
                if (!kibble.Destroyed)
                {
                    kibble.Destroy(DestroyMode.Vanish);
                }

                cachedStoredKibble = -1;
            }
            else
            {
                if (cachedStoredKibble >= 0)
                {
                    cachedStoredKibble += batchSize;
                }

                activeTicksRemaining = GenTicks.TickRareInterval;
                ApplyPowerSetting();
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
                powerComp.PowerOutput = activeTicksRemaining > 0
                    ? -FoodPrinterSystemUtility.GetAnimalFeederActivePowerDraw()
                    : -FoodPrinterSystemUtility.GetAnimalFeederIdlePowerDraw();
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            TonerNetworkSummary summary = TonerPipeNetManager.GetSummary(this);
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

        private int GetStoredKibbleCount()
        {
            int currentTick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            if (cachedStoredKibble >= 0 && currentTick < nextKibbleRecountTick)
            {
                return cachedStoredKibble;
            }

            cachedStoredKibble = CountStoredKibble();
            nextKibbleRecountTick = currentTick + KibbleRecountIntervalTicks;
            return cachedStoredKibble;
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
