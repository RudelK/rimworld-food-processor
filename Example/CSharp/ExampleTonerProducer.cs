using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FoodPrinterSystem.Examples
{
    public class ExampleTonerProducer : Building
    {
        private const int TonerAddedPerCycle = 10;

        private CompPowerTrader powerComp;
        private int successfulCycles;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        public override void TickRare()
        {
            base.TickRare();

            if (powerComp == null)
            {
                powerComp = GetComp<CompPowerTrader>();
            }

            if (powerComp != null && !powerComp.PowerOn)
            {
                return;
            }

            if (!TonerNetworkUtility.TryAddToner(this, TonerAddedPerCycle))
            {
                return;
            }

            // Producers that care about downstream food-type logic should
            // contribute final ingestible ingredient defs here.
            TonerNetworkUtility.DistributeIngredients(this, new List<ThingDef>
            {
                ThingDefOf.RawPotatoes
            });

            successfulCycles++;
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            TonerNetworkSummary summary = TonerNetworkUtility.GetSummary(this);
            if (summary.HasNetwork)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text += FoodPrinterSystemUtility.FormatSummary(summary);
            }

            if (successfulCycles > 0)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text += "Example cycles completed: " + successfulCycles.ToString();
            }

            return text;
        }
    }
}
