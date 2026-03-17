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
            return TryResolveGraphicKey(thing == null ? null : thing.def, mask, out key);
        }

        public static bool TryResolveGraphicKey(ThingDef def, PipeDirectionMask mask, out PipeGraphicKey key)
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

            key = new PipeGraphicKey(graphicData.texPath, mask, shader, (Color32)graphicData.color);
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
    }
}
