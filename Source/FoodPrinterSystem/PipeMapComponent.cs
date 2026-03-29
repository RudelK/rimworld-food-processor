using System;
using System.Collections.Generic;
using FoodSystemPipe;
using Verse;

namespace FoodPrinterSystem
{
    public class PipeMapComponent : MapComponent
    {
        private readonly List<CompPipe> registeredPipes = new List<CompPipe>();
        private readonly List<CompPipeUser> registeredUsers = new List<CompPipeUser>();
        private readonly List<PipeNet> pipeNets = new List<PipeNet>();

        protected CompPipe[] pipeGrid;
        private bool dirty = true;
        private int nextNetId = 1;
        private int networkRevision = 1;

        public PipeMapComponent(Map map) : base(map)
        {
        }

        public int NetworkRevision
        {
            get
            {
                EnsureBuilt();
                return networkRevision;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref dirty, "dirty", true);
            Scribe_Values.Look(ref networkRevision, "networkRevision", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                dirty = true;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            EnsureGrid();
            dirty = true;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            EnsureBuilt();
            for (int i = 0; i < pipeNets.Count; i++)
            {
                pipeNets[i].NetTick();
            }
        }

        public PipeNet GetNetAt(IntVec3 pos)
        {
            EnsureBuilt();
            return GetPipeAt(pos)?.PipeNet;
        }

        public PipeNet GetNetForThing(Thing thing)
        {
            return GetNetForThingInternal(thing);
        }

        public CompPipe GetPipeAt(IntVec3 pos)
        {
            EnsureBuilt();
            return GetPipeAtInternal(pos);
        }

        public void Notify_PipeSpawned(CompPipe pipe)
        {
            if (pipe == null || pipe.parent == null || pipe.parent.Map != map)
            {
                return;
            }

            RegisterPipe(pipe);
            dirty = true;
        }

        public void Notify_PipeDespawned(CompPipe pipe)
        {
            if (pipe == null)
            {
                return;
            }

            UnregisterPipe(pipe);
            pipe.PipeNet = null;
            dirty = true;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        public bool HasConnectedNet(Thing node)
        {
            return GetNetForThingInternal(node) != null;
        }

        public TonerPipeNet GetConnectedTonerNet(Thing node)
        {
            return GetNetForThingInternal(node) == null ? null : new TonerPipeNet((MapComponent_TonerNetwork)this, node);
        }

        public TonerNetworkSummary GetSummary(Thing node)
        {
            PipeNet net = GetNetForThingInternal(node);
            return net == null ? default(TonerNetworkSummary) : net.GetSummary();
        }

        public bool HasConnectedStorage(Thing node)
        {
            PipeNet net = GetNetForThingInternal(node);
            return net != null && net.HasConnectedStorage;
        }

        public List<Thing> GetConnectedNodes(Thing node)
        {
            PipeNet net = GetNetForThingInternal(node);
            List<Thing> result = new List<Thing>();
            if (net == null)
            {
                return result;
            }

            HashSet<int> seenThings = new HashSet<int>();
            List<CompPipe> pipes = net.Pipes;
            for (int i = 0; i < pipes.Count; i++)
            {
                CompPipe pipe = pipes[i];
                if (pipe != null && pipe.parent != null && seenThings.Add(pipe.parent.thingIDNumber))
                {
                    result.Add(pipe.parent);
                }
            }

            return result;
        }

        public bool CanDraw(Thing node, int amount)
        {
            return CanDraw(node, (float)amount);
        }

        public bool CanDraw(Thing node, float amount)
        {
            PipeNet net = GetNetForThingInternal(node);
            return net != null && net.CanDraw(amount);
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
            return TryDrawToner(node, (float)amount);
        }

        public bool TryDrawToner(Thing node, float amount)
        {
            PipeNet net = GetNetForThingInternal(node);
            bool success = net != null && net.TryDraw(amount);
            if (success)
            {
                NotifyStorageStateChangedInternal();
            }

            return success;
        }

        public bool TryAddToner(Thing node, int amount)
        {
            return TryAddToner(node, (float)amount);
        }

        public bool TryAddToner(Thing node, float amount)
        {
            PipeNet net = GetNetForThingInternal(node);
            bool success = net != null && net.TryAdd(amount);
            if (success)
            {
                NotifyStorageStateChangedInternal();
            }

            return success;
        }

        public void GetActivePipes(List<CompPipe> buffer)
        {
            if (buffer == null)
            {
                return;
            }

            for (int i = 0; i < registeredPipes.Count; i++)
            {
                CompPipe pipe = registeredPipes[i];
                if (IsActivePipe(pipe))
                {
                    buffer.Add(pipe);
                }
            }
        }

        private PipeNet GetNetForThingInternal(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map != map)
            {
                return null;
            }

            EnsureBuilt();

            CompPipe directPipe = thing.TryGetComp<CompPipe>();
            if (directPipe != null)
            {
                return directPipe.PipeNet;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(thing.Position, thing.Rotation, thing.def.size);
            foreach (IntVec3 cell in occupiedRect)
            {
                CompPipe pipe = GetPipeAtInternal(cell);
                if (pipe != null)
                {
                    return pipe.PipeNet;
                }
            }

            return null;
        }

        private void EnsureBuilt()
        {
            if (!dirty)
            {
                return;
            }

            RebuildNets();
            dirty = false;
        }

        private void RebuildNets()
        {
            EnsureGrid();
            Array.Clear(pipeGrid, 0, pipeGrid.Length);

            for (int i = registeredPipes.Count - 1; i >= 0; i--)
            {
                if (!IsActivePipe(registeredPipes[i]))
                {
                    registeredPipes.RemoveAt(i);
                }
            }

            for (int i = registeredUsers.Count - 1; i >= 0; i--)
            {
                if (!IsActiveUser(registeredUsers[i]))
                {
                    registeredUsers.RemoveAt(i);
                }
            }

            for (int i = 0; i < pipeNets.Count; i++)
            {
                pipeNets[i].ClearAssignments();
            }

            pipeNets.Clear();

            for (int i = 0; i < registeredPipes.Count; i++)
            {
                CompPipe pipe = registeredPipes[i];
                pipe.PipeNet = null;

                if (!pipe.TransmitsResource)
                {
                    continue;
                }

                foreach (IntVec3 cell in pipe.NetworkCells)
                {
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    int index = map.cellIndices.CellToIndex(cell);
                    pipeGrid[index] = SelectGridPipe(pipeGrid[index], pipe);
                }
            }

            HashSet<int> visited = new HashSet<int>();
            Queue<CompPipe> open = new Queue<CompPipe>();

            for (int i = 0; i < registeredPipes.Count; i++)
            {
                CompPipe root = registeredPipes[i];
                if (root == null || !root.TransmitsResource || !visited.Add(root.parent.thingIDNumber))
                {
                    continue;
                }

                PipeNet net = new PipeNet();
                net.NetId = nextNetId++;
                open.Enqueue(root);

                while (open.Count > 0)
                {
                    CompPipe current = open.Dequeue();
                    net.AddPipe(current);

                    HashSet<int> yielded = new HashSet<int>();
                    foreach (CompPipe neighbor in EnumerateNeighborPipes(current, yielded))
                    {
                        if (neighbor != null && visited.Add(neighbor.parent.thingIDNumber))
                        {
                            open.Enqueue(neighbor);
                        }
                    }
                }

                pipeNets.Add(net);
            }

            for (int i = 0; i < registeredUsers.Count; i++)
            {
                CompPipeUser user = registeredUsers[i];
                if (user == null || user.PipeNet != null)
                {
                    continue;
                }

                PipeNet net = FindNetForUser(user);
                if (net != null)
                {
                    net.AddUser(user);
                }
            }

            BumpNetworkRevision();
        }

        private IEnumerable<CompPipe> EnumerateNeighborPipes(CompPipe pipe, HashSet<int> yielded)
        {
            foreach (IntVec3 cell in pipe.NetworkCells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> thingsAtCell = cell.GetThingList(map);
                for (int i = 0; i < thingsAtCell.Count; i++)
                {
                    CompPipe sameCellPipe = thingsAtCell[i].TryGetComp<CompPipe>();
                    if (sameCellPipe != null && sameCellPipe != pipe && sameCellPipe.TransmitsResource && yielded.Add(sameCellPipe.parent.thingIDNumber))
                    {
                        yield return sameCellPipe;
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.CardinalDirections[i];
                    if (!adjacent.InBounds(map))
                    {
                        continue;
                    }

                    CompPipe neighbor = GetPipeAtInternal(adjacent);
                    if (neighbor != null && neighbor != pipe && yielded.Add(neighbor.parent.thingIDNumber))
                    {
                        yield return neighbor;
                    }
                }
            }
        }

        private PipeNet FindNetForUser(CompPipeUser user)
        {
            foreach (IntVec3 cell in user.NetworkCells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                CompPipe pipe = GetPipeAtInternal(cell);
                if (pipe != null && pipe.PipeNet != null)
                {
                    return pipe.PipeNet;
                }
            }

            return null;
        }

        private CompPipe GetPipeAtInternal(IntVec3 pos)
        {
            if (pipeGrid == null || !pos.InBounds(map))
            {
                return null;
            }

            return pipeGrid[map.cellIndices.CellToIndex(pos)];
        }

        private void EnsureGrid()
        {
            if (pipeGrid == null || pipeGrid.Length != map.cellIndices.NumGridCells)
            {
                pipeGrid = new CompPipe[map.cellIndices.NumGridCells];
            }
        }

        private void RegisterPipe(CompPipe pipe)
        {
            if (!registeredPipes.Contains(pipe))
            {
                registeredPipes.Add(pipe);
            }

            CompPipeUser user = pipe as CompPipeUser;
            if (user != null && !registeredUsers.Contains(user))
            {
                registeredUsers.Add(user);
            }
        }

        private void UnregisterPipe(CompPipe pipe)
        {
            registeredPipes.Remove(pipe);

            CompPipeUser user = pipe as CompPipeUser;
            if (user != null)
            {
                registeredUsers.Remove(user);
            }
        }

        private bool IsActivePipe(CompPipe pipe)
        {
            return pipe != null && pipe.parent != null && pipe.parent.Spawned && pipe.parent.Map == map;
        }

        private bool IsActiveUser(CompPipeUser user)
        {
            return user != null && user.parent != null && user.parent.Spawned && user.parent.Map == map;
        }

        private void BumpNetworkRevision()
        {
            if (networkRevision == int.MaxValue)
            {
                networkRevision = 1;
            }
            else
            {
                networkRevision++;
            }
        }

        private static CompPipe SelectGridPipe(CompPipe current, CompPipe candidate)
        {
            if (current == null)
            {
                return candidate;
            }

            bool currentIsStandalonePipe = current.parent is Building_Pipe;
            bool candidateIsStandalonePipe = candidate.parent is Building_Pipe;
            if (currentIsStandalonePipe && !candidateIsStandalonePipe)
            {
                return candidate;
            }

            return current;
        }

        private void NotifyStorageStateChangedInternal()
        {
            MapComponent_TonerNetwork tonerNetwork = this as MapComponent_TonerNetwork;
            if (tonerNetwork != null)
            {
                tonerNetwork.NotifyStorageStateChanged();
            }
        }
    }
}
