using System.Collections.Generic;
using Verse;

namespace FoodPrinterSystem
{
    public interface ITonerNetworkUser
    {
        Thing TonerNetworkThing { get; }
    }

    public sealed class TonerPipeNet
    {
        private readonly MapComponent_TonerNetwork owner;
        private readonly Thing anchorThing;

        internal TonerPipeNet(MapComponent_TonerNetwork owner, Thing anchorThing)
        {
            this.owner = owner;
            this.anchorThing = anchorThing;
        }

        public Thing AnchorThing
        {
            get { return anchorThing; }
        }

        public Map Map
        {
            get { return anchorThing == null ? null : anchorThing.Map; }
        }

        public bool IsConnected
        {
            get { return owner != null && owner.HasConnectedNet(anchorThing); }
        }

        public TonerNetworkSummary Summary
        {
            get { return owner == null ? default(TonerNetworkSummary) : owner.GetSummary(anchorThing); }
        }

        public int TotalStored
        {
            get { return Summary.Stored; }
        }

        public int TotalCapacity
        {
            get { return Summary.Capacity; }
        }

        public bool HasStorage
        {
            get { return TotalCapacity > 0; }
        }

        public bool CanDraw(int amount)
        {
            return owner != null && owner.CanDraw(anchorThing, amount);
        }

        public bool CanDraw(float amount)
        {
            return owner != null && owner.CanDraw(anchorThing, amount);
        }

        public bool Draw(int amount)
        {
            return owner != null && owner.Draw(anchorThing, amount);
        }

        public bool Draw(float amount)
        {
            return owner != null && owner.Draw(anchorThing, amount);
        }

        public bool TryDrawToner(int amount)
        {
            return owner != null && owner.TryDrawToner(anchorThing, amount);
        }

        public bool TryDrawToner(float amount)
        {
            return owner != null && owner.TryDrawToner(anchorThing, amount);
        }

        public bool TryAddToner(int amount)
        {
            return owner != null && owner.TryAddToner(anchorThing, amount);
        }

        public bool TryAddToner(float amount)
        {
            return owner != null && owner.TryAddToner(anchorThing, FoodPrinterSystemUtility.NormalizeTonerAmount(amount));
        }
    }

    public static class TonerPipeNetManager
    {
        public static TonerPipeNet GetConnectedTonerNet(Thing thing)
        {
            MapComponent_TonerNetwork component = FoodPrinterSystemUtility.GetNetworkComponent(thing == null ? null : thing.Map);
            return component == null ? null : component.GetConnectedTonerNet(thing);
        }

        public static TonerPipeNet GetConnectedTonerNet(ThingComp comp)
        {
            return GetConnectedTonerNet(comp == null ? null : comp.parent);
        }

        public static TonerPipeNet GetConnectedTonerNet(ITonerNetworkUser user)
        {
            return user == null ? null : GetConnectedTonerNet(user.TonerNetworkThing);
        }

        public static bool HasConnectedTonerNet(Thing thing)
        {
            return GetConnectedTonerNet(thing) != null;
        }

        public static bool HasConnectedTonerNet(ThingComp comp)
        {
            return GetConnectedTonerNet(comp) != null;
        }

        public static bool HasConnectedTonerNet(ITonerNetworkUser user)
        {
            return GetConnectedTonerNet(user) != null;
        }

        public static TonerNetworkSummary GetSummary(Thing thing)
        {
            MapComponent_TonerNetwork component = FoodPrinterSystemUtility.GetNetworkComponent(thing == null ? null : thing.Map);
            return component == null ? default(TonerNetworkSummary) : component.GetSummary(thing);
        }

        public static TonerNetworkSummary GetSummary(ThingComp comp)
        {
            return GetSummary(comp == null ? null : comp.parent);
        }

        public static TonerNetworkSummary GetSummary(ITonerNetworkUser user)
        {
            return user == null ? default(TonerNetworkSummary) : GetSummary(user.TonerNetworkThing);
        }

        public static List<Thing> GetConnectedNodes(Thing thing)
        {
            MapComponent_TonerNetwork component = FoodPrinterSystemUtility.GetNetworkComponent(thing == null ? null : thing.Map);
            return component == null ? new List<Thing>() : component.GetConnectedNodes(thing);
        }

        public static List<Thing> GetConnectedNodes(ThingComp comp)
        {
            return GetConnectedNodes(comp == null ? null : comp.parent);
        }

        public static List<Thing> GetConnectedNodes(ITonerNetworkUser user)
        {
            return user == null ? new List<Thing>() : GetConnectedNodes(user.TonerNetworkThing);
        }

        public static bool CanDraw(Thing thing, int amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.CanDraw(amount);
        }

        public static bool CanDraw(Thing thing, float amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.CanDraw(amount);
        }

        public static bool CanDraw(ThingComp comp, int amount)
        {
            return CanDraw(comp == null ? null : comp.parent, amount);
        }

        public static bool CanDraw(ThingComp comp, float amount)
        {
            return CanDraw(comp == null ? null : comp.parent, amount);
        }

        public static bool Draw(Thing thing, int amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.Draw(amount);
        }

        public static bool Draw(Thing thing, float amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.Draw(amount);
        }

        public static bool Draw(ThingComp comp, int amount)
        {
            return Draw(comp == null ? null : comp.parent, amount);
        }

        public static bool Draw(ThingComp comp, float amount)
        {
            return Draw(comp == null ? null : comp.parent, amount);
        }

        public static bool Draw(ITonerNetworkUser user, int amount)
        {
            return Draw(user == null ? null : user.TonerNetworkThing, amount);
        }

        public static bool Draw(ITonerNetworkUser user, float amount)
        {
            return Draw(user == null ? null : user.TonerNetworkThing, amount);
        }

        public static bool TryDrawToner(Thing thing, int amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.TryDrawToner(amount);
        }

        public static bool TryDrawToner(Thing thing, float amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.TryDrawToner(amount);
        }

        public static bool TryDrawToner(ThingComp comp, int amount)
        {
            return TryDrawToner(comp == null ? null : comp.parent, amount);
        }

        public static bool TryDrawToner(ThingComp comp, float amount)
        {
            return TryDrawToner(comp == null ? null : comp.parent, amount);
        }

        public static bool TryDrawToner(ITonerNetworkUser user, int amount)
        {
            return TryDrawToner(user == null ? null : user.TonerNetworkThing, amount);
        }

        public static bool TryDrawToner(ITonerNetworkUser user, float amount)
        {
            return TryDrawToner(user == null ? null : user.TonerNetworkThing, amount);
        }

        public static bool TryAddToner(Thing thing, int amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.TryAddToner(amount);
        }

        public static bool TryAddToner(Thing thing, float amount)
        {
            TonerPipeNet net = GetConnectedTonerNet(thing);
            return net != null && net.TryAddToner(amount);
        }

        public static bool TryAddToner(ThingComp comp, int amount)
        {
            return TryAddToner(comp == null ? null : comp.parent, amount);
        }

        public static bool TryAddToner(ThingComp comp, float amount)
        {
            return TryAddToner(comp == null ? null : comp.parent, amount);
        }

        public static bool TryAddToner(ITonerNetworkUser user, int amount)
        {
            return TryAddToner(user == null ? null : user.TonerNetworkThing, amount);
        }

        public static bool TryAddToner(ITonerNetworkUser user, float amount)
        {
            return TryAddToner(user == null ? null : user.TonerNetworkThing, amount);
        }

        public static void MarkDirty(Map map)
        {
            MapComponent_TonerNetwork component = FoodPrinterSystemUtility.GetNetworkComponent(map);
            if (component != null)
            {
                component.MarkDirty();
            }
        }
    }
}
