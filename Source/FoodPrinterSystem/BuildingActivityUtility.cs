using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    internal static class BuildingActivityUtility
    {
        public static bool TickDownActiveWindow(ref int activeTicksRemaining)
        {
            bool wasActive = activeTicksRemaining > 0;
            if (wasActive)
            {
                activeTicksRemaining -= GenTicks.TickRareInterval;
                if (activeTicksRemaining < 0)
                {
                    activeTicksRemaining = 0;
                }
            }

            return wasActive != (activeTicksRemaining > 0);
        }

        public static void MarkActiveNow(ref int activeTicksRemaining)
        {
            activeTicksRemaining = GenTicks.TickRareInterval;
        }

        public static void ApplyIdleActivePower(ThingWithComps owner, ref CompPowerTrader powerComp, int activeTicksRemaining, float idlePowerDraw, float activePowerDraw)
        {
            if (powerComp == null && owner != null)
            {
                powerComp = owner.GetComp<CompPowerTrader>();
            }

            if (powerComp != null)
            {
                powerComp.PowerOutput = activeTicksRemaining > 0
                    ? -activePowerDraw
                    : -idlePowerDraw;
            }
        }
    }
}
