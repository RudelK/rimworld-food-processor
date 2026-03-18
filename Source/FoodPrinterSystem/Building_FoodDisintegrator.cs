using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_FoodDisintegrator : Building
    {
        private CompPowerTrader powerComp;
        private int activeTicksRemaining;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
            ApplyPowerSetting();
        }

        public override void TickRare()
        {
            base.TickRare();

            if (activeTicksRemaining > 0)
            {
                activeTicksRemaining -= GenTicks.TickRareInterval;
                if (activeTicksRemaining < 0)
                {
                    activeTicksRemaining = 0;
                }
            }

            ApplyPowerSetting();
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

            List<ThingDef> ingredients = new List<ThingDef>();
            CompIngredients compIng = food.TryGetComp<CompIngredients>();
            if (compIng != null && compIng.ingredients != null)
            {
                ingredients.AddRange(compIng.ingredients);
            }
            else if (food.def.ingestible != null)
            {
                // If it's a raw ingredient without a comp, save itself or its source
                if (food.def.ingestible.sourceDef != null)
                {
                    ingredients.Add(food.def.ingestible.sourceDef);
                }
                else
                {
                    ingredients.Add(food.def);
                }
            }

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
                        return food;
                    }
                }
            }

            return null;
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
