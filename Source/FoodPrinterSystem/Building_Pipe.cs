using Verse;

namespace FoodPrinterSystem
{
    public class Building_Pipe : Building, ITonerNetworkUser
    {
        public Thing TonerNetworkThing
        {
            get { return this; }
        }

        public TonerPipeNet ConnectedTonerNet
        {
            get { return GetConnectedTonerNet(); }
        }

        public TonerPipeNet GetConnectedTonerNet()
        {
            return TonerPipeNetManager.GetConnectedTonerNet((Thing)this);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            TonerPipeNetManager.MarkDirty(map);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map previousMap = Map;
            base.DeSpawn(mode);
            TonerPipeNetManager.MarkDirty(previousMap);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map previousMap = Map;
            base.Destroy(mode);
            TonerPipeNetManager.MarkDirty(previousMap);
        }
    }
}
