using System;
using FoodSystemPipe;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FoodPrinterSystem
{
    [StaticConstructorOnStartup]
    public static class FoodPrinterHarmony
    {
        private static readonly AccessTools.FieldRef<Designator_Place, Rot4> DesignatorPlacePlacingRot = AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");
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

                Building_FoodPrinter replacementPrinter = FoodPrinterJobUtility.FindClosestValidPrinter(getter, eater, canRefillDispenser, allowForbidden, allowSociallyImproper, ignoreReservations);
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
                foodDef = FoodPrinterPawnUtility.GetResolvedMealDefForPawn(eater, replacementPrinter);
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

                Pawn eater = currentFoodEater ?? currentFoodGetter;
                ThingDef mealDef = FoodPrinterPawnUtility.GetResolvedMealDefForPawn(eater, printer);
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
                    Pawn eaterPawn = GetMealConsumer(actor);
                    if (currentPrinter == null || comp == null)
                    {
                        LogPrinterJobEvent("printer_toil_init_failed", actor, eaterPawn, currentPrinter, "missing_printer_or_comp");
                        actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (currentPrinter.IsPrinting || comp.IsBusyFor(actor) || !currentPrinter.CanPrintNow)
                    {
                        LogPrinterJobEvent("printer_toil_init_failed", actor, eaterPawn, currentPrinter, "printer_unavailable");
                        if (!TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter))
                        {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        }

                        return;
                    }

                    if (!FoodPrinterPawnUtility.CanPawnConsumePrinterMeal(eaterPawn, currentPrinter))
                    {
                        LogPrinterJobEvent("printer_toil_init_failed", actor, eaterPawn, currentPrinter, "policy_blocked");
                        if (!TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter))
                        {
                            actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                        }

                        return;
                    }

                    if (!comp.TryStartProcessing(currentPrinter, actor, eaterPawn))
                    {
                        LogPrinterJobEvent("printer_toil_init_failed", actor, eaterPawn, currentPrinter, "start_processing_failed");
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
                        int ticksLeft = Math.Max(0, actor.jobs.curDriver.ticksLeftThisToil);
                        comp.UpdateProcessingTicksRemaining(ticksLeft);

                        if (ticksLeft <= 1)
                        {
                            Pawn eaterPawn = GetMealConsumer(actor);
                            // Revalidate the hard allow/deny rules immediately before
                            // completion so we never generate a meal from a printer the
                            // pawn can no longer use.
                            if (!FoodPrinterPawnUtility.CanPawnConsumePrinterMeal(eaterPawn, currentPrinter))
                            {
                                LogPrinterJobEvent("printer_toil_completion_failed", actor, eaterPawn, currentPrinter, "policy_blocked_before_completion");
                                if (!TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter))
                                {
                                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                }

                                return;
                            }

                            comp.UpdateProcessingTicksRemaining(0);
                            Thing meal = comp.CompleteProcessing(currentPrinter, actor, eaterPawn);
                            if (meal != null)
                            {
                                if (actor.carryTracker.TryStartCarry(meal))
                                {
                                    actor.CurJob.SetTarget(printerInd, actor.carryTracker.CarriedThing);
                                }
                                else
                                {
                                    LogPrinterJobEvent("printer_toil_completion_failed", actor, eaterPawn, currentPrinter, "carry_failed");
                                    if (!meal.Destroyed && !meal.Spawned && actor.Map != null)
                                    {
                                        GenSpawn.Spawn(meal, actor.Position, actor.Map);
                                    }
                                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                }
                            }
                            else
                            {
                                LogPrinterJobEvent("printer_toil_completion_failed", actor, eaterPawn, currentPrinter, "complete_processing_returned_null");
                                if (!TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter))
                                {
                                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                                }
                            }
                        }
                    }
                };
                toil.AddFinishAction(delegate
                {
                    Pawn actor = toil.actor;
                    Building_FoodPrinter currentPrinter = GetPrinter(actor, printerInd);
                    CompFoodPrinter comp = currentPrinter == null ? null : currentPrinter.FoodPrinterComp;
                    if (processingStarted && comp != null && comp.IsProcessingPawn(actor) && !comp.HasCompletedProcessing)
                    {
                        comp.CancelProcessing(currentPrinter, actor);
                    }
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

        [HarmonyPatch(typeof(Designator_Place), "DrawGhost")]
        public static class Patch_DesignatorBuild_DrawGhost
        {
            public static void Prefix(Designator_Place __instance, ref Color ghostCol)
            {
                ThingDef thingDef = __instance.PlacingDef as ThingDef;
                Map map = Find.CurrentMap;
                if (thingDef == null || map == null)
                {
                    return;
                }

                IntVec3 center = UI.MouseCell();
                if (!center.IsValid)
                {
                    return;
                }

                if (PlacementPreviewVisualUtility.TryGetBuildingGhostColor(thingDef, center, DesignatorPlacePlacingRot(__instance), map, out Color previewColor))
                {
                    ghostCol = previewColor;
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
                LogPrinterJobEvent("printer_switch_failed", pawn, eater, currentPrinter, "no_alternative_printer");
                return false;
            }

            Job replacementJob = pawn.CurJob.Clone();
            replacementJob.SetTarget(targetIndex, alternative);
            bool switched = pawn.jobs.TryTakeOrderedJob(replacementJob, null, false);
            LogPrinterJobEvent(switched ? "printer_switch_succeeded" : "printer_switch_failed", pawn, eater, alternative, switched ? "switched" : "take_ordered_job_failed");
            return switched;
        }

        private static void LogPrinterJobEvent(string eventName, Pawn actor, Pawn eater, Building_FoodPrinter printer, string reason)
        {
            if (!Prefs.DevMode)
            {
                return;
            }

            CompFoodPrinter comp = printer == null ? null : printer.FoodPrinterComp;
            Log.Message("[FPS] " + eventName
                + ": actor=" + GetPawnDebugLabel(actor)
                + ", eater=" + GetPawnDebugLabel(eater)
                + ", printer=" + GetPrinterDebugLabel(printer)
                + ", canPrintNow=" + (printer != null && printer.CanPrintNow)
                + ", isPrinting=" + (printer != null && printer.IsPrinting)
                + ", processingMeal=" + GetMealDebugLabel(comp == null ? null : comp.ProcessingMealDef)
                + ", processingCost=" + (comp == null ? 0 : comp.ProcessingTonerCost)
                + ", reason=" + reason);
        }

        private static string GetPawnDebugLabel(Pawn pawn)
        {
            return pawn == null ? "null" : pawn.LabelShortCap + " (" + pawn.thingIDNumber + ")";
        }

        private static string GetPrinterDebugLabel(Building_FoodPrinter printer)
        {
            return printer == null ? "null" : printer.LabelShortCap + " (" + printer.thingIDNumber + ")";
        }

        private static string GetMealDebugLabel(ThingDef mealDef)
        {
            return mealDef == null ? "null" : mealDef.defName;
        }
    }
}
