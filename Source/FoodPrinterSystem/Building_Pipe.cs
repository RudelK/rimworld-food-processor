using FoodSystemPipe;
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

        public override void Print(SectionLayer layer)
        {
            if (layer is SectionLayer_Pipes || layer is SectionLayer_Things || layer is SectionLayer_ThingsGeneral)
            {
                return;
            }

            base.Print(layer);
        }
    }
}
