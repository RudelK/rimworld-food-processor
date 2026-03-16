using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    [DefOf]
    public static class FoodPrinterSystemDefOf
    {
        static FoodPrinterSystemDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FoodPrinterSystemDefOf));
        }

        public static ThingDef FPS_FoodPrinter;
        public static ThingDef FPS_AnimalFeeder;
        public static ThingDef FPS_Pipe;
        public static ThingDef FPS_HiddenPipe;

        public static ResearchProjectDef FPS_FoodProcessing;
        public static ResearchProjectDef FPS_SimpleMealPrinting;
        public static ResearchProjectDef FPS_FineMealPrinting;
        public static ResearchProjectDef FPS_LavishMealPrinting;
    }
}
