using FoodPrinterSystem;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public static class PipeGraphicResolver
    {
        private const string DefaultPipeDefName = "FPS_Pipe";
        private const string HiddenPipeDefName = "FPS_HiddenPipe";

        public static bool TryResolveGraphicKey(Thing thing, PipeDirectionMask mask, out PipeGraphicKey key)
        {
            return TryResolveGraphicKey(thing == null ? null : thing.def, thing, mask, out key);
        }

        public static bool TryResolveGraphicKey(ThingDef def, PipeDirectionMask mask, out PipeGraphicKey key)
        {
            return TryResolveGraphicKey(def, null, mask, out key);
        }

        public static bool TryResolveGraphicKey(ThingDef def, Thing stuffSourceThing, PipeDirectionMask mask, out PipeGraphicKey key)
        {
            key = default(PipeGraphicKey);
            GraphicData graphicData = def == null ? null : def.graphicData;
            if (graphicData == null || string.IsNullOrEmpty(graphicData.texPath))
            {
                return false;
            }

            Shader shader = graphicData.shaderType == null ? ShaderDatabase.CutoutComplex : graphicData.shaderType.Shader;
            if (shader == null)
            {
                shader = ShaderDatabase.CutoutComplex;
            }

            key = new PipeGraphicKey(graphicData.texPath, mask, shader, ResolveGraphicColor(graphicData, stuffSourceThing));
            return key.IsValid;
        }

        public static bool TryResolveGraphicKey(Map map, IntVec3 cell, PipeDirectionMask mask, out PipeGraphicKey key)
        {
            key = default(PipeGraphicKey);
            return TryGetPreferredPipeThingAt(map, cell, out Thing pipeThing) && TryResolveGraphicKey(pipeThing, mask, out key);
        }

        public static PipeGraphicKey ResolveBestGraphicKey(PipeOverlayState state, IntVec3 cell, PipeDirectionMask mask)
        {
            if (state != null)
            {
                if (TryResolveGraphicKey(state.Map, cell, mask, out PipeGraphicKey liveKey))
                {
                    return liveKey;
                }

                if (TryResolveGraphicKey(state.OverlaySourceThing, mask, out PipeGraphicKey sourceKey))
                {
                    return sourceKey;
                }

                if (state.BaseGraphicKeysByMask.TryGetValue(mask, out PipeGraphicKey cachedKey) && cachedKey.IsValid)
                {
                    return cachedKey;
                }

                if (TryResolveGraphicKey(state.OverlaySourceDef, mask, out PipeGraphicKey defKey))
                {
                    return defKey;
                }
            }

            TryResolveGraphicKey(ResolveDefaultPipeDef(), mask, out PipeGraphicKey fallbackKey);
            return fallbackKey;
        }

        public static PipeGraphicKey ResolveBestGraphicKey(Map map, Thing overlaySourceThing, ThingDef overlaySourceDef, IntVec3 cell, PipeDirectionMask mask)
        {
            if (TryResolveGraphicKey(map, cell, mask, out PipeGraphicKey liveKey))
            {
                return liveKey;
            }

            if (TryResolveGraphicKey(overlaySourceThing, mask, out PipeGraphicKey sourceKey))
            {
                return sourceKey;
            }

            if (TryResolveGraphicKey(overlaySourceDef, mask, out PipeGraphicKey defKey))
            {
                return defKey;
            }

            TryResolveGraphicKey(ResolveDefaultPipeDef(), mask, out PipeGraphicKey fallbackKey);
            return fallbackKey;
        }

        public static ThingDef ResolveDefaultPipeDef()
        {
            ThingDef pipeDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultPipeDefName);
            return pipeDef ?? DefDatabase<ThingDef>.GetNamedSilentFail(HiddenPipeDefName);
        }

        public static bool TryGetPreferredPipeThingAt(Map map, IntVec3 cell, out Thing pipeThing)
        {
            return PipeCellQueryUtility.TryGetPreferredPipeThingAt(map, cell, out pipeThing);
        }

        private static Color32 ResolveGraphicColor(GraphicData graphicData, Thing stuffSourceThing)
        {
            Color baseColor = graphicData.color;
            if (baseColor.a <= 0f)
            {
                return (Color32)baseColor;
            }

            ThingDef stuffDef = stuffSourceThing == null ? null : stuffSourceThing.Stuff;
            if (stuffDef?.stuffProps == null)
            {
                return (Color32)baseColor;
            }

            Color stuffColor = stuffDef.stuffProps.color;
            Color resolvedColor = new Color(
                baseColor.r * stuffColor.r,
                baseColor.g * stuffColor.g,
                baseColor.b * stuffColor.b,
                baseColor.a);
            return (Color32)resolvedColor;
        }
    }
}
