using System.Collections.Generic;
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
        private bool productionEnabled = true;
        private int activeTicksRemaining;
        private int cachedStoredKibble = -1;
        private int nextKibbleRecountTick;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref productionEnabled, "productionEnabled", true);
        }

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

            if (BuildingActivityUtility.TickDownActiveWindow(ref activeTicksRemaining))
            {
                ApplyPowerSetting();
            }

            if (powerComp == null || !powerComp.PowerOn)
            {
                return;
            }

            if (!productionEnabled)
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

                BuildingActivityUtility.MarkActiveNow(ref activeTicksRemaining);
                ApplyPowerSetting();
            }
        }

        public void ApplyPowerSetting()
        {
            BuildingActivityUtility.ApplyIdleActivePower(
                this,
                ref powerComp,
                activeTicksRemaining,
                FoodPrinterSystemUtility.GetAnimalFeederIdlePowerDraw(),
                FoodPrinterSystemUtility.GetAnimalFeederActivePowerDraw());
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
            if (!productionEnabled)
            {
                text += "\n" + "FPS_AnimalFeederPaused".Translate();
            }

            return text;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            yield return new Command_Toggle
            {
                defaultLabel = "FPS_AnimalFeederToggleLabel".Translate(),
                defaultDesc = "FPS_AnimalFeederToggleDesc".Translate(),
                icon = TexCommand.ForbidOff,
                isActive = () => productionEnabled,
                toggleAction = delegate
                {
                    productionEnabled = !productionEnabled;
                    if (!productionEnabled && activeTicksRemaining > 0)
                    {
                        activeTicksRemaining = 0;
                    }

                    ApplyPowerSetting();
                }
            };
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
