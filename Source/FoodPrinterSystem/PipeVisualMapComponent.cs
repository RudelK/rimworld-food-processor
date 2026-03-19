using System;
using System.Collections.Generic;
using Verse;

namespace FoodSystemPipe
{
    internal struct PipeVisualMaskCacheKey : IEquatable<PipeVisualMaskCacheKey>
    {
        public PipeVisualMaskCacheKey(int ownerThingId, IntVec3 cell)
        {
            OwnerThingId = ownerThingId;
            Cell = cell;
        }

        public int OwnerThingId { get; private set; }
        public IntVec3 Cell { get; private set; }

        public bool Equals(PipeVisualMaskCacheKey other)
        {
            return OwnerThingId == other.OwnerThingId && Cell == other.Cell;
        }

        public override bool Equals(object obj)
        {
            return obj is PipeVisualMaskCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (OwnerThingId * 397) ^ Cell.GetHashCode();
            }
        }
    }

    internal struct PipeVisualMaskCacheEntry
    {
        public int Revision;
        public PipeDirectionMask Mask;
    }

    public class PipeVisualMapComponent : MapComponent
    {
        private readonly Dictionary<PipeVisualMaskCacheKey, PipeVisualMaskCacheEntry> maskCache = new Dictionary<PipeVisualMaskCacheKey, PipeVisualMaskCacheEntry>();
        private int visualRevision = 1;

        public PipeVisualMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref visualRevision, "visualRevision", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InvalidateAll();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            InvalidateAll();
        }

        public PipeDirectionMask GetOrBuildMask(Thing ownerThing, IntVec3 cell, Func<PipeDirectionMask> maskBuilder)
        {
            PipeVisualMaskCacheKey key = new PipeVisualMaskCacheKey(ownerThing == null ? 0 : ownerThing.thingIDNumber, cell);
            if (maskCache.TryGetValue(key, out PipeVisualMaskCacheEntry entry) && entry.Revision == visualRevision)
            {
                return entry.Mask;
            }

            PipeDirectionMask mask = maskBuilder == null ? PipeDirectionMask.None : maskBuilder();
            maskCache[key] = new PipeVisualMaskCacheEntry
            {
                Revision = visualRevision,
                Mask = mask
            };
            return mask;
        }

        public void MarkThingDirty(IntVec3 position, Rot4 rotation, ThingDef def)
        {
            if (def == null)
            {
                return;
            }

            InvalidateAll();
        }

        private void InvalidateAll()
        {
            maskCache.Clear();
            if (visualRevision == int.MaxValue)
            {
                visualRevision = 1;
            }
            else
            {
                visualRevision++;
            }
        }
    }

    public static class PipeVisualMapUtility
    {
        public static PipeVisualMapComponent Get(Map map)
        {
            return map == null ? null : map.GetComponent<PipeVisualMapComponent>();
        }

        public static void NotifyThingChanged(Thing thing, Map mapOverride = null)
        {
            if (thing == null)
            {
                return;
            }

            Map map = mapOverride ?? thing.Map;
            PipeVisualMapComponent component = Get(map);
            if (component != null)
            {
                component.MarkThingDirty(thing.Position, thing.Rotation, thing.def);
            }
        }
    }
}
