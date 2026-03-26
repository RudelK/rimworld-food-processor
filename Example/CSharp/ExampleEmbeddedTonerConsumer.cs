using RimWorld;
using Verse;

namespace FoodPrinterSystem.Examples
{
    // This example stays a normal Building. The "embedded pipe" behavior comes
    // from XML comps and place workers, not from a special runtime base class
    // in the standard integration pattern used by the shipped defs.
    public class ExampleEmbeddedTonerConsumer : Building
    {
        private const int TonerCostPerCycle = 8;

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

            if (TonerPipeNetManager.TryDrawToner(this, TonerCostPerCycle))
            {
                successfulCycles++;
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            TonerNetworkSummary summary = TonerPipeNetManager.GetSummary(this);
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
