using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_FoodPrinter : Building_NutrientPasteDispenser
    {
        public CompFoodPrinter FoodPrinterComp
        {
            get { return GetComp<CompFoodPrinter>(); }
        }

        public TonerPipeNet TonerNet
        {
            get { return TonerPipeNetManager.GetConnectedTonerNet(this); }
        }

        public bool IsPrinting
        {
            get { return FoodPrinterComp != null && FoodPrinterComp.IsPrinting; }
        }

        public Pawn CurrentProcessingPawn
        {
            get { return FoodPrinterComp == null ? null : FoodPrinterComp.CurrentProcessingPawn; }
        }

        public ThingDef AvailableMealDef
        {
            get
            {
                CompFoodPrinter comp = FoodPrinterComp;
                if (comp == null)
                {
                    return null;
                }

                ThingDef mealDef = comp.GetMealToPrint(this, false);
                return comp.IsMealResearched(mealDef) ? mealDef : null;
            }
        }

        public int CurrentPrintCost
        {
            get
            {
                ThingDef mealDef = AvailableMealDef;
                return mealDef == null ? 0 : FoodPrinterSystemUtility.GetPrintCost(mealDef);
            }
        }

        public bool IsPowered
        {
            get
            {
                CompPowerTrader power = GetComp<CompPowerTrader>();
                return power == null || power.PowerOn;
            }
        }

        public bool CanPrintNow
        {
            get
            {
                CompFoodPrinter comp = FoodPrinterComp;
                ThingDef mealDef = AvailableMealDef;
                return comp != null && comp.CanPrintMeal(this, mealDef);
            }
        }

        public override ThingDef DispensableDef
        {
            get
            {
                CompFoodPrinter comp = FoodPrinterComp;
                ThingDef previewMeal = comp == null ? null : comp.GetPreviewMealDef(this);
                return previewMeal ?? ThingDefOf.MealNutrientPaste;
            }
        }

        public override bool HasEnoughFeedstockInHoppers()
        {
            return CanPrintNow;
        }

        public override Thing TryDispenseFood()
        {
            CompFoodPrinter comp = FoodPrinterComp;
            if (comp == null || !comp.HasCompletedProcessing)
            {
                return null;
            }

            return comp.CompleteProcessing(this, comp.CurrentProcessingPawn);
        }

        public bool CanPawnPrint(Pawn eater)
        {
            CompFoodPrinter comp = FoodPrinterComp;
            ThingDef mealDef = AvailableMealDef;
            if (comp == null || mealDef == null || !comp.IsMealResearched(mealDef))
            {
                return false;
            }

            FoodPolicy currentPolicy = eater == null ? null : eater.foodRestriction?.CurrentFoodPolicy;
            return currentPolicy == null || currentPolicy.Allows(mealDef);
        }
    }
}
