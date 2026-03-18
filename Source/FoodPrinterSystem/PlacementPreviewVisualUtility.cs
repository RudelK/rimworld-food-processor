using System;
using System.Reflection;
using FoodPrinterSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public enum PlacementPreviewVisualState
    {
        Normal,
        Warning,
        Invalid
    }

    public static class PlacementPreviewVisualUtility
    {
        private static readonly BindingFlags PublicStaticFlags = BindingFlags.Public | BindingFlags.Static;
        private static readonly MethodInfo CanPlaceBlueprintAtMethod = ResolveCanPlaceBlueprintAtMethod();

        public static PlacementPreviewVisualState GetVisualState(BuildableDef buildableDef, IntVec3 center, Rot4 rotation, Map map, Thing thingToIgnore = null, Thing placingThing = null)
        {
            if (IsInvalidPlacement(buildableDef, center, rotation, map, thingToIgnore, placingThing))
            {
                return PlacementPreviewVisualState.Invalid;
            }

            return TouchesPrisonRoom(buildableDef, center, rotation, map)
                ? PlacementPreviewVisualState.Warning
                : PlacementPreviewVisualState.Normal;
        }

        public static Color GetColorForState(PlacementPreviewVisualState state, bool buildingCell = false)
        {
            switch (state)
            {
                case PlacementPreviewVisualState.Warning:
                    return buildingCell
                        ? new Color(1f, 0.82f, 0.32f, 0.54f)
                        : new Color(1f, 0.78f, 0.24f, 0.78f);
                case PlacementPreviewVisualState.Invalid:
                    return buildingCell
                        ? new Color(1f, 0.42f, 0.38f, 0.56f)
                        : new Color(1f, 0.36f, 0.30f, 0.82f);
                default:
                    return buildingCell
                        ? new Color(0.42f, 1f, 0.84f, 0.48f)
                        : new Color(0.40f, 0.96f, 0.78f, 0.70f);
            }
        }

        public static bool ShouldOverrideBuildingGhostColor(ThingDef thingDef)
        {
            if (thingDef?.thingClass == null || typeof(Building_Pipe).IsAssignableFrom(thingDef.thingClass))
            {
                return false;
            }

            if (thingDef.placeWorkers == null)
            {
                return false;
            }

            for (int i = 0; i < thingDef.placeWorkers.Count; i++)
            {
                if (thingDef.placeWorkers[i] == typeof(PlaceWorker_EmbeddedPipePreview))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetBuildingGhostColor(ThingDef thingDef, IntVec3 center, Rot4 rotation, Map map, out Color color, Thing thingToIgnore = null, Thing placingThing = null)
        {
            color = default(Color);
            if (thingDef == null || map == null || !ShouldOverrideBuildingGhostColor(thingDef))
            {
                return false;
            }

            PlacementPreviewVisualState state = GetVisualState(thingDef, center, rotation, map, thingToIgnore, placingThing);
            color = GetColorForState(state, true);
            return true;
        }

        private static bool IsInvalidPlacement(BuildableDef buildableDef, IntVec3 center, Rot4 rotation, Map map, Thing thingToIgnore, Thing placingThing)
        {
            if (buildableDef == null || map == null)
            {
                return true;
            }

            if (TryGetPlacementReport(buildableDef, center, rotation, map, thingToIgnore, placingThing, out AcceptanceReport report) && !report.Accepted)
            {
                return true;
            }

            return !PipePlacementValidationUtility
                .ValidateNoDuplicatePipeInfrastructure(buildableDef, center, rotation, map, thingToIgnore, placingThing)
                .Accepted;
        }

        private static bool TouchesPrisonRoom(BuildableDef buildableDef, IntVec3 center, Rot4 rotation, Map map)
        {
            if (!(buildableDef is ThingDef thingDef) || map == null)
            {
                return false;
            }

            foreach (IntVec3 cell in PipeVisualCellProvider.GetVisualCells(thingDef, center, rotation))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                Room room = cell.GetRoom(map);
                if (room != null && room.IsPrisonCell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPlacementReport(BuildableDef buildableDef, IntVec3 center, Rot4 rotation, Map map, Thing thingToIgnore, Thing placingThing, out AcceptanceReport report)
        {
            report = AcceptanceReport.WasAccepted;
            if (CanPlaceBlueprintAtMethod == null)
            {
                return false;
            }

            try
            {
                object result = CanPlaceBlueprintAtMethod.Invoke(null, BuildPlacementArguments(CanPlaceBlueprintAtMethod.GetParameters(), buildableDef, center, rotation, map, thingToIgnore, placingThing));
                if (result is AcceptanceReport acceptanceReport)
                {
                    report = acceptanceReport;
                    return true;
                }

                if (result is bool accepted)
                {
                    report = accepted ? AcceptanceReport.WasAccepted : new AcceptanceReport("Placement invalid");
                    return true;
                }
            }
            catch (TargetInvocationException)
            {
            }
            catch (ArgumentException)
            {
            }

            return false;
        }

        private static MethodInfo ResolveCanPlaceBlueprintAtMethod()
        {
            foreach (MethodInfo method in typeof(GenConstruct).GetMethods(PublicStaticFlags))
            {
                if (!string.Equals(method.Name, "CanPlaceBlueprintAt", StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 4)
                {
                    continue;
                }

                if (parameters[0].ParameterType == typeof(BuildableDef)
                    && parameters[1].ParameterType == typeof(IntVec3)
                    && parameters[2].ParameterType == typeof(Rot4)
                    && parameters[3].ParameterType == typeof(Map))
                {
                    return method;
                }
            }

            return null;
        }

        private static object[] BuildPlacementArguments(ParameterInfo[] parameters, BuildableDef buildableDef, IntVec3 center, Rot4 rotation, Map map, Thing thingToIgnore, Thing placingThing)
        {
            object[] arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                Type parameterType = parameter.ParameterType;
                string parameterName = parameter.Name ?? string.Empty;

                if (parameterType == typeof(BuildableDef))
                {
                    arguments[i] = buildableDef;
                }
                else if (parameterType == typeof(IntVec3))
                {
                    arguments[i] = center;
                }
                else if (parameterType == typeof(Rot4))
                {
                    arguments[i] = rotation;
                }
                else if (parameterType == typeof(Map))
                {
                    arguments[i] = map;
                }
                else if (typeof(Thing).IsAssignableFrom(parameterType))
                {
                    arguments[i] = parameterName.IndexOf("ignore", StringComparison.OrdinalIgnoreCase) >= 0 ? thingToIgnore : placingThing;
                }
                else if (parameterType == typeof(bool))
                {
                    arguments[i] = false;
                }
                else if (parameter.HasDefaultValue)
                {
                    object defaultValue = parameter.DefaultValue;
                    arguments[i] = defaultValue == DBNull.Value ? null : defaultValue;
                }
                else if (parameterType.IsValueType)
                {
                    arguments[i] = Activator.CreateInstance(parameterType);
                }
                else
                {
                    arguments[i] = null;
                }
            }

            return arguments;
        }
    }
}