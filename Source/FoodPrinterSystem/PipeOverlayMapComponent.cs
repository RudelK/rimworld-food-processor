using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public class PipeOverlayMapComponent : MapComponent
    {
        private PipeOverlayState visibleState;
        private PipeOverlayVisibilityMode currentMode = PipeOverlayVisibilityMode.Hidden;
        private PipeOverlayVisibilityMode lastLoggedMode = (PipeOverlayVisibilityMode)(-1);
        private string lastLoggedDesignator = string.Empty;
        private bool dirty = true;
        private int lastActiveDrawFrame = -1;

        public PipeOverlayMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Invalidate();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Invalidate();
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (Find.CurrentMap != map)
            {
                return;
            }

            PipeOverlayVisibilityMode mode = PipeOverlayVisibilityUtility.GetVisibilityMode(map);
            LogVisibilityChange(mode);

            if (mode == PipeOverlayVisibilityMode.Hidden)
            {
                ClearVisibleState(true);
                return;
            }
        }

        public PipeOverlayState GetVisibleState()
        {
            PipeOverlayVisibilityMode mode = PipeOverlayVisibilityUtility.GetVisibilityMode(map);
            if (mode == PipeOverlayVisibilityMode.Hidden)
            {
                ClearVisibleState(true);
                return null;
            }

            if (!dirty && visibleState != null && currentMode == mode)
            {
                return visibleState;
            }

            currentMode = mode;
            visibleState = PipeOverlaySelectionUtility.BuildMapWideState(map);
            dirty = false;
            PipeOverlayVisibilityUtility.DebugLog("Mode=" + mode + " (map-wide)");
            return visibleState;
        }

        public bool BeginActiveDrawFrame()
        {
            int frame = Time.frameCount;
            if (lastActiveDrawFrame == frame)
            {
                return false;
            }

            lastActiveDrawFrame = frame;
            return true;
        }

        public void Invalidate()
        {
            dirty = true;
            visibleState = null;
        }

        private void ClearVisibleState(bool hiddenByVisibilityRule)
        {
            bool hadState = visibleState != null || currentMode != PipeOverlayVisibilityMode.Hidden;
            visibleState = null;
            currentMode = PipeOverlayVisibilityMode.Hidden;
            dirty = true;

            if (hadState)
            {
                PipeOverlayVisibilityUtility.DebugLog(hiddenByVisibilityRule
                    ? "Hidden: no valid designator active"
                    : "Overlay state cleared");
            }
        }

        private void LogVisibilityChange(PipeOverlayVisibilityMode mode)
        {
            string selectedDesignator = PipeOverlayVisibilityUtility.GetSelectedDesignatorDebugLabel();
            if (mode == lastLoggedMode && string.Equals(selectedDesignator, lastLoggedDesignator, System.StringComparison.Ordinal))
            {
                return;
            }

            lastLoggedMode = mode;
            lastLoggedDesignator = selectedDesignator;
            PipeOverlayVisibilityUtility.DebugLog("SelectedDesignator=" + selectedDesignator);
            PipeOverlayVisibilityUtility.DebugLog(mode == PipeOverlayVisibilityMode.Hidden ? "Hidden: no valid designator active" : "Mode=" + mode);
        }
    }

    public static class PipeOverlayMapUtility
    {
        public static PipeOverlayMapComponent Get(Map map)
        {
            return map == null ? null : map.GetComponent<PipeOverlayMapComponent>();
        }

        public static void NotifyThingChanged(Thing thing, Map mapOverride = null)
        {
            if (thing == null)
            {
                return;
            }

            Map map = mapOverride ?? thing.MapHeld;
            PipeOverlayMapComponent component = Get(map);
            if (component != null)
            {
                component.Invalidate();
            }
        }
    }
}
