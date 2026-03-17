using FoodSystemPipe;
using Verse;

namespace FoodPrinterSystem
{
    public class CompProperties_TonerNode : CompProperties_Pipe
    {
        public CompProperties_TonerNode()
        {
            drawPipeOverlay = true;
            transmitsResource = true;
            compClass = typeof(CompTonerNode);
        }
    }

    public class CompProperties_TonerUser : CompProperties_TonerNode
    {
        public CompProperties_TonerUser()
        {
            compClass = typeof(CompTonerUser);
        }
    }

    public class CompTonerNode : CompPipe
    {
        public TonerPipeNet ConnectedTonerNet
        {
            get { return GetConnectedTonerNet(); }
        }

        public bool IsConnectedToTonerNet
        {
            get { return ConnectedTonerNet != null; }
        }

        public TonerPipeNet GetConnectedTonerNet()
        {
            return TonerPipeNetManager.GetConnectedTonerNet(parent);
        }

        public TonerNetworkSummary GetTonerNetworkSummary()
        {
            return TonerPipeNetManager.GetSummary(parent);
        }

        public bool CanDrawToner(int amount)
        {
            return TonerPipeNetManager.CanDraw(parent, amount);
        }

        public bool CanDrawToner(float amount)
        {
            return TonerPipeNetManager.CanDraw(parent, amount);
        }

        public bool TryDrawToner(int amount)
        {
            return TonerPipeNetManager.TryDrawToner(parent, amount);
        }

        public bool TryDrawToner(float amount)
        {
            return TonerPipeNetManager.TryDrawToner(parent, amount);
        }

        public bool TryAddToner(int amount)
        {
            return TonerPipeNetManager.TryAddToner(parent, amount);
        }

        public bool TryAddToner(float amount)
        {
            return TonerPipeNetManager.TryAddToner(parent, amount);
        }

        public override string CompInspectStringExtra()
        {
            if (parent.Map == null || parent.TryGetComp<CompTonerTank>() != null)
            {
                return null;
            }

            TonerNetworkSummary summary = GetTonerNetworkSummary();
            return summary.HasNetwork ? FoodPrinterSystemUtility.FormatSummary(summary) : null;
        }
    }

    public class CompTonerUser : CompTonerNode
    {
    }
}

