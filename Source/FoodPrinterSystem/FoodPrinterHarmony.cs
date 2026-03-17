using System;
using FoodSystemPipe;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace FoodPrinterSystem
{
    [StaticConstructorOnStartup]
    public static class FoodPrinterHarmony
    {
        private static bool bestFoodSourceOnMap;
        private static Pawn currentFoodGetter;
        private static Pawn currentFoodEater;
        private static bool currentAllowDispenserFull;
        private static bool currentAllowForbidden;
        private static bool currentAllowSociallyImproper;

        [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
        public static class Patch_BestFoodSourceOnMap
        {
            public static void Prefix(Pawn getter, Pawn eater, bool allowDispenserFull, bool allowForbidden, bool allowSociallyImproper)
            {
                bestFoodSourceOnMap = true;
                currentFoodGetter = getter;
                currentFoodEater = eater;
                currentAllowDispenserFull = allowDispenserFull;
                currentAllowForbidden = allowForbidden;
                currentAllowSociallyImproper = allowSociallyImproper;
            }

            public static void Postfix()
            {
                bestFoodSourceOnMap = false;
                currentFoodGetter = null;
                currentFoodEater = null;
                currentAllowDispenserFull = false;
                currentAllowForbidden = false;
                currentAllowSociallyImproper = false;
            }
        }

        [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.TryFindBestFoodSourceFor))]
        public static class Patch_TryFindBestFoodSourceFor
        {
            public static void Postfix(Pawn getter, Pawn eater, bool canRefillDispenser, bool allowForbidden, bool allowSociallyImproper, bool ignoreReservations, ref Thing foodSource, ref ThingDef foodDef, ref bool __result)
            {
                if (!canRefillDispenser || getter == null || eater == null || getter.Map == null)
                {
                    return;
                }

                Building_FoodPrinter selectedPrinter = foodSource as Building_FoodPrinter;
                if (__result && foodSource != null && selectedPrinter == null)
                {
                    return;
                }

                if (__result
                    && selectedPrinter != null
                    && FoodPrinterJobUtility.IsPrinterJobCandidate(selectedPrinter, getter, eater, canRefillDispenser, allowForbidden, allowSociallyImproper, ignoreReservations))
                {
                    foodDef = selectedPrinter.AvailableMealDef ?? foodDef;
                    return;
                }

                Building_FoodPrinter replacementPrinter = FoodPrinterJobUtility.FindClosestValidPrinter(getter, eater, canRefillDispenser, allowForbidden, allowSociallyImproper, ignoreReservations, selectedPrinter);
                if (replacementPrinter == null)
                {
                    if (selectedPrinter != null)
                    {
                        foodSource = null;
                        foodDef = null;
                        __result = false;
                    }

                    return;
                }

                foodSource = replacementPrinter;
                foodDef = replacementPrinter.AvailableMealDef;
                __result = foodDef != null;
            }
        }

        [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.GetFinalIngestibleDef))]
        public static class Patch_GetFinalIngestibleDef
        {
            public static bool Prefix(Thing foodSource, ref ThingDef __result)
            {
                Building_FoodPrinter printer = foodSource as Building_FoodPrinter;
                if (printer == null || !bestFoodSourceOnMap)
                {
                    return true;
                }

                ThingDef mealDef = printer.AvailableMealDef;
                if (mealDef == null)
                {
                    return true;
                }

                __result = mealDef;
                return false;
            }
        }

        [HarmonyPatch(typeof(FoodUtility), "SpawnedFoodSearchInnerScan", null)]
        public static class Patch_SpawnedFoodSearchInnerScan
        {
            public static void Prefix(ref Predicate<Thing> validator)
            {
                Predicate<Thing> originalValidator = validator;
                validator = delegate(Thing thing)
                {
                    Building_FoodPrinter printer = thing as Building_FoodPrinter;
                    if (printer != null)
                    {
                        return IsPrinterValidFoodSource(printer);
                    }

                    return originalValidator == null || originalValidator(thing);
                };
            }
        }

        [HarmonyPatch(typeof(ThingListGroupHelper), nameof(ThingListGroupHelper.Includes))]
        public static class Patch_ThingListGroupHelper_Includes
        {
            public static bool Prefix(ThingRequestGroup group, ThingDef def, ref bool __result)
            {
                if ((group == ThingRequestGroup.FoodSource || group == ThingRequestGroup.FoodSourceNotPlantOrTree)
                    && def != null
                    && def.thingClass != null
                    && typeof(Building_FoodPrinter).IsAssignableFrom(def.thingClass))
                {
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Building_NutrientPasteDispenser), "get_CanDispenseNow")]
        public static class Patch_CanDispenseNow
        {
            public static void Postfix(Building_NutrientPasteDispenser __instance, ref bool __result)
            {
                Building_FoodPrinter printer = __instance as Building_FoodPrinter;
                if (printer != null)
                {
                    __result = printer.CanPrintNow;
                }
            }
        }

        [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.TakeMealFromDispenser))]
        public static class Patch_TakeMealFromDispenser
        {
            public static bool Prefix(ref TargetIndex ind, ref Pawn eater, ref Toil __result)
            {
                Building_FoodPrinter printer = eater.jobs.curJob.GetTarget(ind).Thing as Building_FoodPrinter;
                if (printer == null)
                {
                    return true;
                }

                TargetIndex printerInd = ind;
                bool processingStarted = false;
                Toil toil = new Toil();
                toil.defaultCompleteMode = ToilCompleteMode.Delay;
                toil.defaultDuration = FoodPrinterSystemUtility.PrintingDelayTicks;
                toil.initAction = delegate
                {
                    Pawn actor = toil.actor;
                    Building_FoodPrinter currentPrinter = GetPrinter(actor, printerInd);
                    CompFoodPrinter comp = currentPrinter == null ? null : currentPrinter.FoodPrinterComp;
                    if (currentPrinter == null || comp == null)
                    {
                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (currentPrinter.IsPrinting || comp.IsBusyFor(actor) || !currentPrinter.CanPrintNow)
                    {
                        if (!TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter))
                        {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        }

                        return;
                    }

                    if (!comp.TryStartProcessing(currentPrinter, actor))
                    {
                        if (!TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter))
                        {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        }

                        return;
                    }

                    processingStarted = true;
                    comp.UpdateProcessingTicksRemaining(FoodPrinterSystemUtility.PrintingDelayTicks);
                };
                toil.tickAction = delegate
                {
                    Pawn actor = toil.actor;
                    Building_FoodPrinter currentPrinter = GetPrinter(actor, printerInd);
                    if (currentPrinter != null)
                    {
                        actor.rotationTracker.FaceTarget(currentPrinter);
                    }

                    CompFoodPrinter comp = currentPrinter == null ? null : currentPrinter.FoodPrinterComp;
                    if (processingStarted && comp != null && comp.IsProcessingPawn(actor) && actor.jobs != null && actor.jobs.curDriver != null)
                    {
                        comp.UpdateProcessingTicksRemaining(Math.Max(0, actor.jobs.curDriver.ticksLeftThisToil));
                    }
                };
                toil.AddFinishAction(delegate
                {
                    Pawn actor = toil.actor;
                    Building_FoodPrinter currentPrinter = GetPrinter(actor, printerInd);
                    CompFoodPrinter comp = currentPrinter == null ? null : currentPrinter.FoodPrinterComp;
                    if (!processingStarted || comp == null || !comp.IsProcessingPawn(actor))
                    {
                        return;
                    }

                    bool completedNaturally = actor.jobs != null
                        && actor.jobs.curDriver != null
                        && actor.jobs.curDriver.ticksLeftThisToil <= 0;
                    if (!completedNaturally)
                    {
                        comp.CancelProcessing(currentPrinter, actor);
                        return;
                    }

                    comp.UpdateProcessingTicksRemaining(0);
                    Thing meal = comp.CompleteProcessing(currentPrinter, actor);
                    if (meal == null)
                    {
                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (actor.Map == null)
                    {
                        if (!meal.Destroyed)
                        {
                            meal.Destroy(DestroyMode.Vanish);
                        }

                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (meal.Spawned)
                    {
                        meal.DeSpawn();
                    }

                    if (!actor.carryTracker.TryStartCarry(meal))
                    {
                        if (!meal.Destroyed && !meal.Spawned)
                        {
                            GenSpawn.Spawn(meal, actor.Position, actor.Map);
                        }

                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    actor.CurJob.SetTarget(printerInd, actor.carryTracker.CarriedThing);
                });
                toil.handlingFacing = true;
                toil.FailOnDespawnedNullOrForbidden(printerInd);
                toil.FailOnCannotTouch(printerInd, PathEndMode.InteractionCell);
                toil.FailOn(delegate(Toil t)
                {
                    if (!processingStarted)
                    {
                        return false;
                    }

                    Pawn actor = t.actor;
                    Building_FoodPrinter currentPrinter = GetPrinter(actor, printerInd);
                    if (currentPrinter == null || !currentPrinter.IsPowered)
                    {
                        return true;
                    }

                    CompFoodPrinter comp = currentPrinter.FoodPrinterComp;
                    if (comp == null || !comp.IsProcessingPawn(actor) || comp.IsBusyFor(actor))
                    {
                        return true;
                    }

                    int tonerCost = comp.ProcessingTonerCost;
                    return tonerCost <= 0 || !TonerPipeNetManager.CanDraw(currentPrinter, tonerCost);
                });
                toil.WithProgressBar(printerInd, delegate
                {
                    return GetProcessingProgress(toil.actor, printerInd);
                }, false, -0.5f);
                __result = toil;
                return false;
            }
        }

        [HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
        public static class Patch_DrawSelectionOverlays
        {
            public static void Postfix()
            {
                Map map = Find.CurrentMap;
                if (map != null)
                {
                    PipeOverlayDrawer.DrawActiveOverlay(map);
                }
            }
        }

        private static bool IsPrinterValidFoodSource(Building_FoodPrinter printer)
        {
            Pawn getter = currentFoodGetter;
            Pawn eater = currentFoodEater ?? getter;
            return FoodPrinterJobUtility.IsPrinterJobCandidate(printer, getter, eater, currentAllowDispenserFull, currentAllowForbidden, currentAllowSociallyImproper, false);
        }

        private static Building_FoodPrinter GetPrinter(Pawn pawn, TargetIndex targetIndex)
        {
            if (pawn == null || pawn.CurJob == null)
            {
                return null;
            }

            return pawn.CurJob.GetTarget(targetIndex).Thing as Building_FoodPrinter;
        }

        private static Pawn GetMealConsumer(Pawn actor)
        {
            if (actor == null || actor.CurJob == null)
            {
                return actor;
            }

            Pawn targetPawn = actor.CurJob.GetTarget(TargetIndex.B).Thing as Pawn;
            return targetPawn ?? actor;
        }

        private static float GetProcessingProgress(Pawn pawn, TargetIndex targetIndex)
        {
            Building_FoodPrinter printer = GetPrinter(pawn, targetIndex);
            CompFoodPrinter comp = printer == null ? null : printer.FoodPrinterComp;
            return comp == null ? 0f : comp.ProcessingProgress;
        }

        private static bool TrySwitchToAlternativePrinter(Pawn pawn, TargetIndex targetIndex, Building_FoodPrinter currentPrinter)
        {
            Pawn eater = GetMealConsumer(pawn);
            Building_FoodPrinter alternative = FoodPrinterJobUtility.FindClosestValidPrinter(pawn, eater, true, false, false, false, currentPrinter);
            if (alternative == null || pawn == null || pawn.CurJob == null)
            {
                return false;
            }

            Job replacementJob = pawn.CurJob.Clone();
            replacementJob.SetTarget(targetIndex, alternative);
            return pawn.jobs.TryTakeOrderedJob(replacementJob, null, false);
        }
    }
}

