using System;
using FoodSystemPipe;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace FoodPrinterSystem
{
    [StaticConstructorOnStartup]
    public static class FoodPrinterHarmony
    {
        private static readonly AccessTools.FieldRef<Designator_Place, Rot4> DesignatorPlacePlacingRot = AccessTools.FieldRefAccess<Designator_Place, Rot4>("placingRot");
        private const int PrinterOnlyFallbackBlockDurationTicks = 150;

        private sealed class PrinterOnlyFallbackBlockState
        {
            public int ExpiresAtTick;
            public int SourcePrinterId;
        }

        private static readonly Dictionary<int, PrinterOnlyFallbackBlockState> PrinterOnlyFallbackBlockByPawnId = new Dictionary<int, PrinterOnlyFallbackBlockState>();
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

                bool printerOnlyFallbackBlocked = HasActivePrinterOnlyFallbackBlock(getter);
                Building_FoodPrinter selectedPrinter = foodSource as Building_FoodPrinter;
                if (__result && selectedPrinter != null)
                {
                    ClearPrinterOnlyFallbackBlock(getter, selectedPrinter, "printer_search_succeeded");
                    return;
                }

                if (__result && foodSource != null && selectedPrinter == null && !printerOnlyFallbackBlocked)
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
                    else if (printerOnlyFallbackBlocked)
                    {
                        if (__result && foodSource != null)
                        {
                            LogNonPrinterFallbackSuppressed(getter, eater, null, foodSource, foodDef, "printer_only_search_active");
                        }

                        foodSource = null;
                        foodDef = null;
                        __result = false;
                    }

                    return;
                }

                if (printerOnlyFallbackBlocked && __result && foodSource != null && selectedPrinter == null)
                {
                    LogNonPrinterFallbackSuppressed(getter, eater, replacementPrinter, foodSource, foodDef, "printer_only_search_active");
                }

                ClearPrinterOnlyFallbackBlock(getter, replacementPrinter, "printer_search_succeeded");
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
                string pendingFailureReason = null;
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
                        ArmPrinterOnlyFallbackBlock(actor, currentPrinter, "missing_printer_or_comp");
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

                    PawnPrinterFoodPolicy eaterPolicy = FoodPrinterPawnUtility.ResolvePawnFoodPolicy(eaterPawn);
                    if (!FoodPrinterPawnUtility.CanPawnConsumePrinterMeal(eaterPolicy, eaterPawn, currentPrinter))
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
                    pendingFailureReason = null;
                    ClearPrinterOnlyFallbackBlock(actor, currentPrinter, "processing_started");
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
                            PawnPrinterFoodPolicy eaterPolicy = FoodPrinterPawnUtility.ResolvePawnFoodPolicy(eaterPawn);
                            // Revalidate the hard allow/deny rules immediately before
                            // completion so we never generate a meal from a printer the
                            // pawn can no longer use.
                            if (!FoodPrinterPawnUtility.CanPawnConsumePrinterMeal(eaterPolicy, eaterPawn, currentPrinter))
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
                    if (!pendingFailureReason.NullOrEmpty())
                    {
                        TrySwitchToAlternativePrinter(actor, printerInd, currentPrinter, pendingFailureReason);
                    }

                    if (processingStarted && comp != null && comp.IsProcessingPawn(actor) && !comp.HasCompletedProcessing)
                    {
                        if (pendingFailureReason.NullOrEmpty())
                        {
                            ClearPrinterOnlyFallbackBlock(actor, currentPrinter, "processing_cancelled");
                        }

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
                        pendingFailureReason = "printer_lost_power";
                        return true;
                    }

                    CompFoodPrinter comp = currentPrinter.FoodPrinterComp;
                    if (comp == null || !comp.IsProcessingPawn(actor) || comp.IsBusyFor(actor))
                    {
                        pendingFailureReason = "printer_processing_invalid";
                        return true;
                    }

                    int tonerCost = comp.ProcessingTonerCost;
                    bool failed = tonerCost <= 0 || !TonerPipeNetManager.CanDraw(currentPrinter, tonerCost);
                    if (failed)
                    {
                        pendingFailureReason = tonerCost <= 0 ? "invalid_processing_toner_cost" : "processing_toner_unavailable";
                    }

                    return failed;
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

        [HarmonyPatch(typeof(Building_Bed), "set_ForPrisoners")]
        public static class Patch_BuildingBed_set_ForPrisoners
        {
            public static void Postfix(Building_Bed __instance)
            {
                NotifyBedPrintersMaskStateChanged(__instance);
            }
        }

        [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.SetBedOwnerTypeByInterface))]
        public static class Patch_BuildingBed_SetBedOwnerTypeByInterface
        {
            public static void Postfix(Building_Bed __instance)
            {
                NotifyBedPrintersMaskStateChanged(__instance);
            }
        }

        private static bool IsPrinterValidFoodSource(Building_FoodPrinter printer)
        {
            Pawn getter = currentFoodGetter;
            Pawn eater = currentFoodEater ?? getter;
            return FoodPrinterJobUtility.IsPrinterJobCandidate(printer, getter, eater, currentAllowDispenserFull, currentAllowForbidden, currentAllowSociallyImproper, false);
        }

        private static void NotifyRoomPrintersMaskStateChanged(Room room)
        {
            if (room == null || room.Map == null || FoodPrinterSystemDefOf.FPS_FoodPrinter == null)
            {
                return;
            }

            List<Thing> printers = room.Map.listerThings.ThingsOfDef(FoodPrinterSystemDefOf.FPS_FoodPrinter);
            for (int i = 0; i < printers.Count; i++)
            {
                Building_FoodPrinter printer = printers[i] as Building_FoodPrinter;
                if (printer == null || !printer.UsesRoom(room))
                {
                    continue;
                }

                printer.NotifyMaskStateChanged();
            }
        }

        private static void NotifyBedPrintersMaskStateChanged(Building_Bed bed)
        {
            if (bed == null || !bed.Spawned || bed.Map == null)
            {
                return;
            }

            Room room = bed.GetRoom();
            if (room != null)
            {
                NotifyRoomPrintersMaskStateChanged(room);
            }
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

        private static bool TrySwitchToAlternativePrinter(Pawn pawn, TargetIndex targetIndex, Building_FoodPrinter currentPrinter, string failureReason = null)
        {
            Pawn eater = GetMealConsumer(pawn);
            Building_FoodPrinter alternative = FoodPrinterJobUtility.FindClosestValidPrinter(pawn, eater, true, false, false, false, currentPrinter);
            if (alternative == null || pawn == null || pawn.CurJob == null)
            {
                LogPrinterJobEvent("printer_switch_failed", pawn, eater, currentPrinter, "no_alternative_printer");
                ArmPrinterOnlyFallbackBlock(pawn, currentPrinter, failureReason.NullOrEmpty() ? "no_alternative_printer" : failureReason);
                return false;
            }

            Job replacementJob = pawn.CurJob.Clone();
            replacementJob.SetTarget(targetIndex, alternative);
            bool switched = pawn.jobs.TryTakeOrderedJob(replacementJob, null, false);
            LogPrinterJobEvent(switched ? "printer_switch_succeeded" : "printer_switch_failed", pawn, eater, alternative, switched ? "switched" : "take_ordered_job_failed");
            if (switched)
            {
                ClearPrinterOnlyFallbackBlock(pawn, alternative, "switched_to_alternative_printer");
            }
            else
            {
                ArmPrinterOnlyFallbackBlock(pawn, currentPrinter, failureReason.NullOrEmpty() ? "take_ordered_job_failed" : failureReason);
            }

            return switched;
        }

        private static void LogPrinterJobEvent(string eventName, Pawn actor, Pawn eater, Building_FoodPrinter printer, string reason)
        {
            if (FoodPrinterSystemMod.Settings == null || !FoodPrinterSystemMod.Settings.DebugLoggingEnabled)
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

        private static bool HasActivePrinterOnlyFallbackBlock(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            if (!PrinterOnlyFallbackBlockByPawnId.TryGetValue(pawn.thingIDNumber, out PrinterOnlyFallbackBlockState state) || state == null)
            {
                return false;
            }

            if (state.ExpiresAtTick <= GetCurrentTick())
            {
                PrinterOnlyFallbackBlockByPawnId.Remove(pawn.thingIDNumber);
                LogPrinterOnlyFallbackCleared(pawn, null, "timeout");
                return false;
            }

            return true;
        }

        private static void ArmPrinterOnlyFallbackBlock(Pawn pawn, Building_FoodPrinter printer, string reason)
        {
            if (pawn == null)
            {
                return;
            }

            PrinterOnlyFallbackBlockByPawnId[pawn.thingIDNumber] = new PrinterOnlyFallbackBlockState
            {
                ExpiresAtTick = GetCurrentTick() + PrinterOnlyFallbackBlockDurationTicks,
                SourcePrinterId = printer == null ? -1 : printer.thingIDNumber
            };
            LogPrinterOnlyFallbackArmed(pawn, printer, reason);
        }

        private static void ClearPrinterOnlyFallbackBlock(Pawn pawn, Building_FoodPrinter printer, string reason)
        {
            if (pawn == null)
            {
                return;
            }

            if (PrinterOnlyFallbackBlockByPawnId.Remove(pawn.thingIDNumber))
            {
                LogPrinterOnlyFallbackCleared(pawn, printer, reason);
            }
        }

        private static void LogPrinterOnlyFallbackArmed(Pawn pawn, Building_FoodPrinter printer, string reason)
        {
            if (FoodPrinterSystemMod.Settings == null || !FoodPrinterSystemMod.Settings.DebugLoggingEnabled)
            {
                return;
            }

            Log.Message("[FPS] printer_only_search_armed"
                + ": actor=" + GetPawnDebugLabel(pawn)
                + ", printer=" + GetPrinterDebugLabel(printer)
                + ", expiresAtTick=" + (GetCurrentTick() + PrinterOnlyFallbackBlockDurationTicks)
                + ", reason=" + reason);
        }

        private static void LogPrinterOnlyFallbackCleared(Pawn pawn, Building_FoodPrinter printer, string reason)
        {
            if (FoodPrinterSystemMod.Settings == null || !FoodPrinterSystemMod.Settings.DebugLoggingEnabled)
            {
                return;
            }

            Log.Message("[FPS] printer_only_search_cleared"
                + ": actor=" + GetPawnDebugLabel(pawn)
                + ", printer=" + GetPrinterDebugLabel(printer)
                + ", reason=" + reason);
        }

        private static void LogNonPrinterFallbackSuppressed(Pawn getter, Pawn eater, Building_FoodPrinter replacementPrinter, Thing originalFoodSource, ThingDef originalFoodDef, string reason)
        {
            if (FoodPrinterSystemMod.Settings == null || !FoodPrinterSystemMod.Settings.DebugLoggingEnabled)
            {
                return;
            }

            Log.Message("[FPS] printer_non_printer_fallback_suppressed"
                + ": actor=" + GetPawnDebugLabel(getter)
                + ", eater=" + GetPawnDebugLabel(eater)
                + ", originalFoodSource=" + GetThingDebugLabel(originalFoodSource)
                + ", originalFoodDef=" + GetMealDebugLabel(originalFoodDef)
                + ", replacementPrinter=" + GetPrinterDebugLabel(replacementPrinter)
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

        private static string GetThingDebugLabel(Thing thing)
        {
            if (thing == null)
            {
                return "null";
            }

            return thing.LabelShortCap + " (" + thing.thingIDNumber + ")";
        }

        private static int GetCurrentTick()
        {
            return Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
        }
    }
}
