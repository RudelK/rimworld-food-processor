using RimWorld;
using Verse;

namespace FoodPrinterSystem.Examples
{
    public class ExampleTonerConsumer : Building
    {
        private const int TonerCostPerCycle = 5;

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
