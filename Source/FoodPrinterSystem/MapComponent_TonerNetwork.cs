using System.Collections.Generic;
using Verse;

namespace FoodPrinterSystem
{
    public class MapComponent_TonerNetwork : MapComponent
    {
        private sealed class TonerNetwork
        {
            public readonly List<Thing> Nodes = new List<Thing>();
            public readonly List<CompTonerTank> Tanks = new List<CompTonerTank>();
        }

        private readonly Dictionary<int, TonerNetwork> networkByThingId = new Dictionary<int, TonerNetwork>();
        private readonly object syncRoot = new object();
        private bool dirty = true;

        public MapComponent_TonerNetwork(Map map) : base(map)
        {
        }

        public void MarkDirty()
        {
            lock (syncRoot)
            {
                dirty = true;
            }
        }

        public bool HasConnectedNet(Thing node)
        {
            lock (syncRoot)
            {
                return GetNetworkLocked(node) != null;
            }
        }

        public TonerPipeNet GetConnectedTonerNet(Thing node)
        {
            lock (syncRoot)
            {
                return GetNetworkLocked(node) == null ? null : new TonerPipeNet(this, node);
            }
        }

        public TonerNetworkSummary GetSummary(Thing node)
        {
            lock (syncRoot)
            {
                TonerNetwork network = GetNetworkLocked(node);
                BalanceNetworkLocked(network);
                return GetSummaryLocked(network);
            }
        }

        public List<Thing> GetConnectedNodes(Thing node)
        {
            lock (syncRoot)
            {
                TonerNetwork network = GetNetworkLocked(node);
                return network == null ? new List<Thing>() : new List<Thing>(network.Nodes);
            }
        }

        public bool CanDraw(Thing node, int amount)
        {
            lock (syncRoot)
            {
                return CanDrawLocked(node, amount);
            }
        }

        public bool CanDraw(Thing node, float amount)
        {
            return CanDraw(node, FoodPrinterSystemUtility.NormalizeTonerAmount(amount));
        }

        public bool Draw(Thing node, int amount)
        {
            return TryDrawToner(node, amount);
        }

        public bool Draw(Thing node, float amount)
        {
            return TryDrawToner(node, amount);
        }

        public bool TryDrawToner(Thing node, int amount)
        {
            lock (syncRoot)
            {
                return TryConsumeTonerLocked(node, amount);
            }
        }

        public bool TryDrawToner(Thing node, float amount)
        {
            return TryDrawToner(node, FoodPrinterSystemUtility.NormalizeTonerAmount(amount));
        }

        public bool TryAddToner(Thing node, int amount)
        {
            lock (syncRoot)
            {
                if (amount <= 0)
                {
                    return true;
                }

                TonerNetwork network = GetNetworkLocked(node);
                return TryAddTonerLocked(network, amount);
            }
        }

        public bool TryConsumeToner(Thing node, int amount)
        {
            lock (syncRoot)
            {
                return TryConsumeTonerLocked(node, amount);
            }
        }

        private TonerNetwork GetNetworkLocked(Thing node)
        {
            if (node == null || !node.Spawned || node.Map != map)
            {
                return null;
            }

            EnsureBuiltLocked();
            TonerNetwork network;
            if (networkByThingId.TryGetValue(node.thingIDNumber, out network))
            {
                return network;
            }

            return null;
        }

        private void EnsureBuiltLocked()
        {
            if (!dirty)
            {
                return;
            }

            RebuildNetworksLocked();
            dirty = false;
        }

        private void RebuildNetworksLocked()
        {
            networkByThingId.Clear();

            List<Thing> candidates = new List<Thing>();
            for (int i = 0; i < map.listerThings.AllThings.Count; i++)
            {
                Thing thing = map.listerThings.AllThings[i];
                if (IsNetworkThing(thing))
                {
                    candidates.Add(thing);
                }
            }

            Dictionary<IntVec3, List<Thing>> occupancy = new Dictionary<IntVec3, List<Thing>>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Building building = candidates[i] as Building;
                if (building == null)
                {
                    continue;
                }

                foreach (IntVec3 cell in building.OccupiedRect())
                {
                    List<Thing> thingsAtCell;
                    if (!occupancy.TryGetValue(cell, out thingsAtCell))
                    {
                        thingsAtCell = new List<Thing>();
                        occupancy[cell] = thingsAtCell;
                    }

                    thingsAtCell.Add(building);
                }
            }

            HashSet<int> visited = new HashSet<int>();
            Queue<Thing> open = new Queue<Thing>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Thing root = candidates[i];
                if (!visited.Add(root.thingIDNumber))
                {
                    continue;
                }

                TonerNetwork network = new TonerNetwork();
                open.Enqueue(root);
                while (open.Count > 0)
                {
                    Thing current = open.Dequeue();
                    network.Nodes.Add(current);

                    CompTonerTank tank = current.TryGetComp<CompTonerTank>();
                    if (tank != null)
                    {
                        tank.NotifySettingsChanged();
                        network.Tanks.Add(tank);
                    }

                    Building currentBuilding = current as Building;
                    if (currentBuilding == null)
                    {
                        continue;
                    }

                    foreach (Thing neighbor in EnumerateNeighbors(currentBuilding, occupancy))
                    {
                        if (visited.Add(neighbor.thingIDNumber))
                        {
                            open.Enqueue(neighbor);
                        }
                    }
                }

                BalanceNetworkLocked(network);
                for (int j = 0; j < network.Nodes.Count; j++)
                {
                    networkByThingId[network.Nodes[j].thingIDNumber] = network;
                }
            }
        }

        private bool CanDrawLocked(Thing node, int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            TonerNetwork network = GetNetworkLocked(node);
            if (network == null || network.Tanks.Count == 0)
            {
                return false;
            }

            BalanceNetworkLocked(network);
            return GetTotalStoredLocked(network) >= amount;
        }

        private bool TryAddTonerLocked(TonerNetwork network, int amount)
        {
            if (network == null || network.Tanks.Count == 0)
            {
                return false;
            }

            BalanceNetworkLocked(network);
            int totalStored = GetTotalStoredLocked(network);
            int totalCapacity = GetTotalCapacityLocked(network);
            if (totalStored + amount > totalCapacity)
            {
                return false;
            }

            RedistributeStoredTonerLocked(network, totalStored + amount);
            return true;
        }

        private bool TryConsumeTonerLocked(Thing node, int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            TonerNetwork network = GetNetworkLocked(node);
            if (network == null || network.Tanks.Count == 0)
            {
                return false;
            }

            BalanceNetworkLocked(network);
            int totalStored = GetTotalStoredLocked(network);
            if (totalStored < amount)
            {
                return false;
            }

            RedistributeStoredTonerLocked(network, totalStored - amount);
            return true;
        }

        private static TonerNetworkSummary GetSummaryLocked(TonerNetwork network)
        {
            TonerNetworkSummary summary = new TonerNetworkSummary();
            summary.HasNetwork = network != null;
            if (network == null)
            {
                return summary;
            }

            summary.Stored = GetTotalStoredLocked(network);
            summary.Capacity = GetTotalCapacityLocked(network);
            return summary;
        }

        private static void BalanceNetworkLocked(TonerNetwork network)
        {
            if (network == null || network.Tanks.Count == 0)
            {
                return;
            }

            int totalCapacity = GetTotalCapacityLocked(network);
            if (totalCapacity <= 0)
            {
                for (int i = 0; i < network.Tanks.Count; i++)
                {
                    network.Tanks[i].SetStoredToner(0);
                }

                return;
            }

            int totalStored = GetTotalStoredLocked(network);
            if (totalStored > totalCapacity)
            {
                totalStored = totalCapacity;
            }

            RedistributeStoredTonerLocked(network, totalStored);
        }

        private static int GetTotalStoredLocked(TonerNetwork network)
        {
            int total = 0;
            for (int i = 0; i < network.Tanks.Count; i++)
            {
                total += network.Tanks[i].StoredToner;
            }

            return total;
        }

        private static int GetTotalCapacityLocked(TonerNetwork network)
        {
            int total = 0;
            for (int i = 0; i < network.Tanks.Count; i++)
            {
                total += network.Tanks[i].Capacity;
            }

            return total;
        }

        private static void RedistributeStoredTonerLocked(TonerNetwork network, int totalStored)
        {
            if (network == null || network.Tanks.Count == 0)
            {
                return;
            }

            int totalCapacity = GetTotalCapacityLocked(network);
            if (totalCapacity <= 0)
            {
                for (int i = 0; i < network.Tanks.Count; i++)
                {
                    network.Tanks[i].SetStoredToner(0);
                }

                return;
            }

            if (totalStored < 0)
            {
                totalStored = 0;
            }
            else if (totalStored > totalCapacity)
            {
                totalStored = totalCapacity;
            }

            int[] targets = new int[network.Tanks.Count];
            int[] remainders = new int[network.Tanks.Count];
            int assigned = 0;
            for (int i = 0; i < network.Tanks.Count; i++)
            {
                int capacity = network.Tanks[i].Capacity;
                long scaled = (long)totalStored * capacity;
                targets[i] = (int)(scaled / totalCapacity);
                remainders[i] = (int)(scaled % totalCapacity);
                assigned += targets[i];
            }

            int leftover = totalStored - assigned;
            while (leftover > 0)
            {
                int bestIndex = -1;
                int bestRemainder = -1;
                int bestCapacity = -1;
                for (int i = 0; i < network.Tanks.Count; i++)
                {
                    if (targets[i] >= network.Tanks[i].Capacity)
                    {
                        continue;
                    }

                    if (remainders[i] > bestRemainder || remainders[i] == bestRemainder && network.Tanks[i].Capacity > bestCapacity)
                    {
                        bestIndex = i;
                        bestRemainder = remainders[i];
                        bestCapacity = network.Tanks[i].Capacity;
                    }
                }

                if (bestIndex < 0)
                {
                    break;
                }

                targets[bestIndex]++;
                remainders[bestIndex] = 0;
                leftover--;
            }

            for (int i = 0; i < network.Tanks.Count; i++)
            {
                network.Tanks[i].SetStoredToner(targets[i]);
            }
        }

        private static bool IsNetworkThing(Thing thing)
        {
            if (!(thing is Building))
            {
                return false;
            }

            return thing is Building_Pipe
                || thing.TryGetComp<CompTonerUser>() != null
                || thing is ITonerNetworkUser;
        }

        private static IEnumerable<Thing> EnumerateNeighbors(Building building, Dictionary<IntVec3, List<Thing>> occupancy)
        {
            HashSet<int> yielded = new HashSet<int>();
            foreach (IntVec3 cell in building.OccupiedRect())
            {
                foreach (Thing thing in ThingsAtCell(cell, occupancy))
                {
                    if (thing != building && yielded.Add(thing.thingIDNumber))
                    {
                        yield return thing;
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                    foreach (Thing thing in ThingsAtCell(adjacent, occupancy))
                    {
                        if (thing != building && yielded.Add(thing.thingIDNumber))
                        {
                            yield return thing;
                        }
                    }
                }
            }
        }

        private static IEnumerable<Thing> ThingsAtCell(IntVec3 cell, Dictionary<IntVec3, List<Thing>> occupancy)
        {
            List<Thing> thingsAtCell;
            if (!occupancy.TryGetValue(cell, out thingsAtCell))
            {
                yield break;
            }

            for (int i = 0; i < thingsAtCell.Count; i++)
            {
                yield return thingsAtCell[i];
            }
        }
    }

    public static class TonerNetworkUtility
    {
        public static TonerNetworkSummary GetSummary(Thing node)
        {
            return TonerPipeNetManager.GetSummary(node);
        }

        public static bool TryAddToner(Thing node, int amount)
        {
            return TonerPipeNetManager.TryAddToner(node, amount);
        }

        public static bool TryConsumeToner(Thing node, int amount)
        {
            return TonerPipeNetManager.TryDrawToner(node, amount);
        }
    }
}
