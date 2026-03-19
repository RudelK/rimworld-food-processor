using System.Collections.Generic;
using FoodPrinterSystem;
using Verse;

namespace FoodSystemPipe
{
    public class CompProperties_Pipe : CompProperties
    {
        public bool transmitsResource = true;
        public bool drawPipeOverlay = true;

        public CompProperties_Pipe()
        {
            compClass = typeof(CompPipe);
        }
    }

    public class CompProperties_PipeUser : CompProperties_Pipe
    {
        public float consumptionRate;

        public CompProperties_PipeUser()
        {
            compClass = typeof(CompPipeUser);
        }
    }

    public class CompPipe : ThingComp, ITonerNetworkUser, IEmbeddedPipeNode
    {
        private PipeNet pipeNet;

        public CompProperties_Pipe PipeProps
        {
            get { return (CompProperties_Pipe)props; }
        }

        public virtual bool TransmitsResource
        {
            get { return PipeProps == null || PipeProps.transmitsResource; }
        }

        public PipeNet PipeNet
        {
            get { return pipeNet; }
            internal set { pipeNet = value; }
        }

        public Thing TonerNetworkThing
        {
            get { return parent; }
        }

        public CellRect OccupiedRect
        {
            get { return GenAdj.OccupiedRect(parent.Position, parent.Rotation, parent.def.size); }
        }

        public IEnumerable<IntVec3> NetworkCells
        {
            get
            {
                foreach (IntVec3 cell in OccupiedRect)
                {
                    yield return cell;
                }
            }
        }

        public IEnumerable<IntVec3> VisualPipeCells
        {
            get { return PipeVisualCellProvider.GetVisualCells(parent); }
        }

        public bool ConnectsPipeAt(IntVec3 cell)
        {
            return PipeVisualCellProvider.ContainsCell(parent, cell);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && PipePlacementValidationUtility.TryRejectDuplicateSpawn(parent))
            {
                return;
            }

            NotifyPipeVisuals();

            PipeMapComponent component = FoodPrinterSystemUtility.GetNetworkComponent(parent.Map);
            if (component != null)
            {
                component.Notify_PipeSpawned(this);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            NotifyPipeVisuals(map);
            base.PostDeSpawn(map, mode);

            PipeMapComponent component = FoodPrinterSystemUtility.GetNetworkComponent(map);
            if (component != null)
            {
                component.Notify_PipeDespawned(this);
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            NotifyPipeVisuals(previousMap);
            base.PostDestroy(mode, previousMap);

            PipeMapComponent component = FoodPrinterSystemUtility.GetNetworkComponent(previousMap);
            if (component != null)
            {
                component.Notify_PipeDespawned(this);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                pipeNet = null;
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (ShouldDrawPipeOverlay())
            {
                PipeOverlayDrawer.DrawBuiltOverlay(parent, this);
            }
        }

        protected virtual bool ShouldDrawPipeOverlay()
        {
            return TransmitsResource
                && PipeProps != null
                && PipeProps.drawPipeOverlay
                && parent.Spawned
                && parent.def.drawerType != DrawerType.MapMeshOnly
                && !(parent is Building_Pipe)
                && PipeNet != null;
        }

        private void NotifyPipeVisuals(Map mapOverride = null)
        {
            PipeRenderMapUtility.NotifyVisualNodeChanged(parent, mapOverride);
            PipeVisualMapUtility.NotifyThingChanged(parent, mapOverride);
            PipeOverlayMapUtility.NotifyThingChanged(parent, mapOverride);
        }
    }

    public class CompPipeUser : CompPipe
    {
        public CompProperties_PipeUser PipeUserProps
        {
            get { return (CompProperties_PipeUser)props; }
        }

        public virtual float ConsumptionRate
        {
            get { return PipeUserProps == null ? 0f : PipeUserProps.consumptionRate; }
        }
    }
}
