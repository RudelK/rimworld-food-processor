using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace FoodSystemPipe
{
    public class PipeRenderMapComponent : MapComponent
    {
        private static readonly FieldInfo SectionLayersField = typeof(Section).GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly HashSet<IntVec3> dirtyCells = new HashSet<IntVec3>();
        private bool wholeMapDirty = true;
        private bool sectionsRegistered;

        public PipeRenderMapComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                sectionsRegistered = false;
                wholeMapDirty = true;
                dirtyCells.Clear();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            sectionsRegistered = false;
            wholeMapDirty = true;
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            EnsureSectionLayersRegistered();
            FlushDirtyRequests();
        }

        public void NotifyVisualNodeChanged(Thing thing)
        {
            if (thing == null || map == null)
            {
                return;
            }

            CellRect dirtyRect = GenAdj.OccupiedRect(thing.PositionHeld, thing.Rotation, thing.def.size).ExpandedBy(1);
            foreach (IntVec3 cell in dirtyRect)
            {
                MarkDirty(cell);
            }

            EnsureSectionLayersRegistered();
        }

        private void EnsureSectionLayersRegistered()
        {
            if (sectionsRegistered || SectionLayersField == null || map == null || map.mapDrawer == null)
            {
                return;
            }

            bool missingAnySection = false;
            for (int x = 0; x < map.Size.x; x += Section.Size)
            {
                for (int z = 0; z < map.Size.z; z += Section.Size)
                {
                    if (!TryGetSection(new IntVec3(x, 0, z), out Section section))
                    {
                        missingAnySection = true;
                        continue;
                    }

                    if (section.GetLayer(typeof(SectionLayer_Pipes)) != null)
                    {
                        continue;
                    }

                    List<SectionLayer> layers = SectionLayersField.GetValue(section) as List<SectionLayer>;
                    if (layers == null)
                    {
                        missingAnySection = true;
                        continue;
                    }

                    SectionLayer_Pipes layer = new SectionLayer_Pipes(section)
                    {
                        Dirty = true
                    };
                    layers.Add(layer);
                    wholeMapDirty = true;
                }
            }

            sectionsRegistered = !missingAnySection;
        }

        private bool TryGetSection(IntVec3 cell, out Section section)
        {
            section = null;
            if (map == null || map.mapDrawer == null)
            {
                return false;
            }

            try
            {
                section = map.mapDrawer.SectionAt(cell);
                return section != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }

        private void FlushDirtyRequests()
        {
            if (!sectionsRegistered || map == null || map.mapDrawer == null)
            {
                return;
            }

            if (wholeMapDirty)
            {
                map.mapDrawer.RegenerateLayerNow(typeof(SectionLayer_Pipes));
                dirtyCells.Clear();
                wholeMapDirty = false;
                return;
            }

            foreach (IntVec3 cell in dirtyCells)
            {
                map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.PowerGrid);
            }

            dirtyCells.Clear();
        }

        private void MarkDirty(IntVec3 cell)
        {
            if (cell.InBounds(map))
            {
                dirtyCells.Add(cell);
            }
        }
    }

    public static class PipeRenderMapUtility
    {
        public static PipeRenderMapComponent Get(Map map)
        {
            return map == null ? null : map.GetComponent<PipeRenderMapComponent>();
        }

        public static void NotifyVisualNodeChanged(Thing thing, Map mapOverride = null)
        {
            if (thing == null)
            {
                return;
            }

            Map map = mapOverride ?? thing.MapHeld;
            PipeRenderMapComponent component = Get(map);
            if (component != null)
            {
                component.NotifyVisualNodeChanged(thing);
            }
        }
    }
}
