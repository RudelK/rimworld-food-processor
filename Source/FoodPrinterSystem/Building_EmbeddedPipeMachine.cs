using System.Collections.Generic;
using FoodSystemPipe;
using Verse;

namespace FoodPrinterSystem
{
    public class Building_EmbeddedPipeMachine : Building, IEmbeddedPipeNode
    {
        private bool HandlesPipeLifecycleLocally
        {
            get { return GetComp<CompPipe>() == null; }
        }

        public IEnumerable<IntVec3> VisualPipeCells
        {
            get
            {
                foreach (IntVec3 cell in this.OccupiedRect())
                {
                    yield return cell;
                }
            }
        }

        public bool ConnectsPipeAt(IntVec3 cell)
        {
            return this.OccupiedRect().Contains(cell);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!HandlesPipeLifecycleLocally)
            {
                return;
            }

            if (!respawningAfterLoad && PipePlacementValidationUtility.TryRejectDuplicateSpawn(this))
            {
                return;
            }

            NotifyLocalPipeVisuals();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (HandlesPipeLifecycleLocally)
            {
                NotifyLocalPipeVisuals();
            }

            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (HandlesPipeLifecycleLocally)
            {
                NotifyLocalPipeVisuals(MapHeld);
            }

            base.Destroy(mode);
        }

        private void NotifyLocalPipeVisuals(Map mapOverride = null)
        {
            PipeVisualNotifyUtility.NotifyThingChanged(this, mapOverride);
        }
    }
}
