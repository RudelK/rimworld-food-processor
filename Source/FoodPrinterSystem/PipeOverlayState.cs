using System.Collections.Generic;
using Verse;

namespace FoodSystemPipe
{
    public sealed class PipeOverlayState
    {
        private readonly HashSet<IntVec3> pipeCells;
        private readonly HashSet<IntVec3> buildingCells;
        private readonly Dictionary<IntVec3, PipeDirectionMask> cellMasks;
        private readonly Dictionary<PipeDirectionMask, PipeGraphicKey> baseGraphicKeysByMask;

        public PipeOverlayState(
            Map map,
            int selectedThingId,
            int networkKey,
            HashSet<IntVec3> pipeCells,
            HashSet<IntVec3> buildingCells,
            Dictionary<IntVec3, PipeDirectionMask> cellMasks,
            Dictionary<PipeDirectionMask, PipeGraphicKey> baseGraphicKeysByMask,
            Thing overlaySourceThing,
            ThingDef overlaySourceDef)
        {
            Map = map;
            SelectedThingId = selectedThingId;
            NetworkKey = networkKey;
            this.pipeCells = pipeCells ?? new HashSet<IntVec3>();
            this.buildingCells = buildingCells ?? new HashSet<IntVec3>();
            this.cellMasks = cellMasks ?? new Dictionary<IntVec3, PipeDirectionMask>();
            this.baseGraphicKeysByMask = baseGraphicKeysByMask ?? new Dictionary<PipeDirectionMask, PipeGraphicKey>();
            OverlaySourceThing = overlaySourceThing;
            OverlaySourceDef = overlaySourceDef;
        }

        public Map Map { get; private set; }
        public int SelectedThingId { get; private set; }
        public int NetworkKey { get; private set; }
        public Thing OverlaySourceThing { get; private set; }
        public ThingDef OverlaySourceDef { get; private set; }

        public ISet<IntVec3> PipeCells
        {
            get { return pipeCells; }
        }

        public ISet<IntVec3> BuildingCells
        {
            get { return buildingCells; }
        }

        public IDictionary<IntVec3, PipeDirectionMask> CellMasks
        {
            get { return cellMasks; }
        }

        public IDictionary<PipeDirectionMask, PipeGraphicKey> BaseGraphicKeysByMask
        {
            get { return baseGraphicKeysByMask; }
        }

        public bool IsEmpty
        {
            get { return pipeCells.Count == 0 && buildingCells.Count == 0; }
        }

        public bool IsBuildingCell(IntVec3 cell)
        {
            return buildingCells.Contains(cell);
        }

        public static PipeOverlayState Empty(Thing selectedThing)
        {
            return new PipeOverlayState(
                selectedThing == null ? null : selectedThing.MapHeld,
                selectedThing == null ? -1 : selectedThing.thingIDNumber,
                -1,
                new HashSet<IntVec3>(),
                new HashSet<IntVec3>(),
                new Dictionary<IntVec3, PipeDirectionMask>(),
                new Dictionary<PipeDirectionMask, PipeGraphicKey>(),
                null,
                null);
        }
    }
}
