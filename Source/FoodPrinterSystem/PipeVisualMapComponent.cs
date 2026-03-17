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
        private readonly HashSet<IntVec3> dirtyCells = new HashSet<IntVec3>();
        private int visualRevision = 1;

        public PipeVisualMapComponent(Map map) : base(map)
        {
        }

        public int VisualRevision
        {
            get { return visualRevision; }
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

            CellRect occupiedRect = GenAdj.OccupiedRect(position, rotation, def.size);
            for (int x = occupiedRect.minX - 1; x <= occupiedRect.maxX + 1; x++)
            {
                for (int z = occupiedRect.minZ - 1; z <= occupiedRect.maxZ + 1; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (cell.InBounds(map))
                    {
                        dirtyCells.Add(cell);
                    }
                }
            }

            InvalidateAll();
        }

        public bool IsCellMarkedDirty(IntVec3 cell)
        {
            return dirtyCells.Contains(cell);
        }

        public void AcknowledgeCell(IntVec3 cell)
        {
            dirtyCells.Remove(cell);
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