using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_FoodDisintegrator : Building
    {
        private CompPowerTrader powerComp;
        private int activeTicksRemaining;
        private Thing cachedAdjacentFood;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            ApplyPowerSetting();
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

            Thing food = FindAdjacentFood();
            if (food == null)
            {
                return;
            }

            int tonerValue = FoodPrinterSystemUtility.GetStoredTonerValue(food);
            if (tonerValue <= 0 || !TonerNetworkUtility.TryAddToner(this, tonerValue))
            {
                return;
            }

            ConsumeOne(food);

            List<ThingDef> ingredients = ExtractIngredientProvenanceDefs(food);
            TonerNetworkUtility.DistributeIngredients(this, ingredients);
            activeTicksRemaining = GenTicks.TickRareInterval;
            ApplyPowerSetting();
        }

        public void ApplyPowerSetting()
        {
            SetPowerDraw(activeTicksRemaining > 0);
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

            if (summary.Capacity <= 0)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }

                text += "FPS_DisintegratorNoStorage".Translate();
            }

            return text;
        }

        private Thing FindAdjacentFood()
        {
            if (IsCachedAdjacentFoodValid())
            {
                return cachedAdjacentFood;
            }

            cachedAdjacentFood = null;
            foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(this))
            {
                if (!cell.InBounds(Map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(Map);
                for (int j = 0; j < things.Count; j++)
                {
                    Thing food = FindFoodInThing(things[j]);
                    if (food != null)
                    {
                        if (food.Spawned)
                        {
                            cachedAdjacentFood = food;
                        }

                        return food;
                    }
                }
            }

            return null;
        }

        private bool IsCachedAdjacentFoodValid()
        {
            return cachedAdjacentFood != null
                && cachedAdjacentFood.Spawned
                && cachedAdjacentFood.Map == Map
                && FoodPrinterSystemUtility.IsProcessableFood(cachedAdjacentFood)
                && IsAdjacentCardinalCell(cachedAdjacentFood.Position);
        }

        private bool IsAdjacentCardinalCell(IntVec3 cell)
        {
            foreach (IntVec3 adjacent in GenAdj.CellsAdjacentCardinal(this))
            {
                if (adjacent == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static Thing FindFoodInThing(Thing thing)
        {
            if (FoodPrinterSystemUtility.IsProcessableFood(thing))
            {
                return thing;
            }

            Building_Storage storage = thing as Building_Storage;
            if (storage != null)
            {
                SlotGroup slotGroup = storage.GetSlotGroup();
                if (slotGroup != null)
                {
                    foreach (Thing storedThing in slotGroup.HeldThings)
                    {
                        if (FoodPrinterSystemUtility.IsProcessableFood(storedThing))
                        {
                            return storedThing;
                        }
                    }
                }

                return null;
            }

            // Only inspect building-held containers. Looking inside arbitrary
            // thing holders lets the disintegrator consume food from pawns and
            // other unintended holders standing next to it.
            if (!(thing is Building))
            {
                return null;
            }

            IThingHolder thingHolder = thing as IThingHolder;
            if (thingHolder != null)
            {
                ThingOwner directlyHeldThings = thingHolder.GetDirectlyHeldThings();
                if (directlyHeldThings != null)
                {
                    for (int i = 0; i < directlyHeldThings.Count; i++)
                    {
                        if (FoodPrinterSystemUtility.IsProcessableFood(directlyHeldThings[i]))
                        {
                            return directlyHeldThings[i];
                        }
                    }
                }
            }

            return null;
        }

        private static void ConsumeOne(Thing food)
        {
            Thing oneUnit = food.stackCount > 1 ? food.SplitOff(1) : food;
            oneUnit.Destroy(DestroyMode.Vanish);
        }

        private static List<ThingDef> ExtractIngredientProvenanceDefs(Thing food)
        {
            List<ThingDef> ingredients = new List<ThingDef>();
            if (food == null)
            {
                return ingredients;
            }

            CompIngredients compIng = food.TryGetComp<CompIngredients>();
            if (compIng != null && compIng.ingredients != null)
            {
                // Toner provenance must preserve the final ingestible ingredient defs
                // exactly as used by the food. Downstream printer food-type prediction
                // reads ingredientDef.ingestible.foodType, so muffalo meat must stay
                // muffalo meat instead of being rewritten to muffalo or another source def.
                for (int i = 0; i < compIng.ingredients.Count; i++)
                {
                    ThingDef ingredientDef = compIng.ingredients[i];
                    if (ingredientDef != null)
                    {
                        ingredients.Add(ingredientDef);
                    }
                }

                return ingredients;
            }

            if (food.def != null && food.def.ingestible != null)
            {
                ingredients.Add(food.def);
            }

            return ingredients;
        }

        private void SetPowerDraw(bool active)
        {
            if (powerComp == null)
            {
                return;
            }

            powerComp.PowerOutput = active
                ? -FoodPrinterSystemUtility.GetDisintegratorActivePowerDraw()
                : -FoodPrinterSystemUtility.GetDisintegratorIdlePowerDraw();
        }
    }
}
