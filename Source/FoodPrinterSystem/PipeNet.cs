using System.Collections.Generic;
using FoodSystemPipe;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class PipeNet : IExposable
    {
        private readonly List<CompPipe> pipes = new List<CompPipe>();
        private readonly List<CompPipeUser> users = new List<CompPipeUser>();
        private readonly List<CompTonerTank> tanks = new List<CompTonerTank>();

        private float resourceBuffer;
        private float maxCapacity;
        private int netId = -1;

        public List<CompPipe> Pipes
        {
            get { return pipes; }
        }

        public List<CompPipeUser> Users
        {
            get { return users; }
        }

        public float ResourceBuffer
        {
            get
            {
                RefreshStorageState();
                return resourceBuffer;
            }
        }

        public float MaxCapacity
        {
            get
            {
                RefreshStorageState();
                return maxCapacity;
            }
        }

        internal int NetId
        {
            get { return netId; }
            set { netId = value; }
        }

        internal void AddPipe(CompPipe pipe)
        {
            if (pipe == null || pipes.Contains(pipe))
            {
                return;
            }

            pipes.Add(pipe);
            pipe.PipeNet = this;

            CompPipeUser pipeUser = pipe as CompPipeUser;
            if (pipeUser != null && !users.Contains(pipeUser))
            {
                users.Add(pipeUser);
            }

            CompTonerTank tank = pipe.parent.TryGetComp<CompTonerTank>();
            if (tank != null && !tanks.Contains(tank))
            {
                tanks.Add(tank);
            }
        }

        internal void AddUser(CompPipeUser user)
        {
            if (user == null || users.Contains(user))
            {
                return;
            }

            users.Add(user);
            user.PipeNet = this;

            CompTonerTank tank = user.parent.TryGetComp<CompTonerTank>();
            if (tank != null && !tanks.Contains(tank))
            {
                tanks.Add(tank);
            }
        }

        public void NetTick()
        {
            RefreshStorageState();

            float netConsumptionRate = 0f;
            for (int i = 0; i < users.Count; i++)
            {
                CompPipeUser user = users[i];
                if (user != null && user.parent.Spawned && user.PipeNet == this)
                {
                    netConsumptionRate += user.ConsumptionRate;
                }
            }

            if (Mathf.Approximately(netConsumptionRate, 0f))
            {
                return;
            }

            if (netConsumptionRate > 0f)
            {
                TryDraw(netConsumptionRate);
            }
            else
            {
                TryAdd(-netConsumptionRate);
            }
        }

        public bool CanDraw(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            RefreshStorageState();
            return resourceBuffer + 0.0001f >= amount;
        }

        public bool TryDraw(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            RefreshStorageState();
            if (resourceBuffer + 0.0001f < amount)
            {
                return false;
            }

            resourceBuffer = Mathf.Max(0f, resourceBuffer - amount);
            SyncBufferToTanks();
            return true;
        }

        public bool TryAdd(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            RefreshStorageState();
            if (maxCapacity <= 0f || resourceBuffer + amount > maxCapacity + 0.0001f)
            {
                return false;
            }

            resourceBuffer = Mathf.Min(maxCapacity, resourceBuffer + amount);
            SyncBufferToTanks();
            return true;
        }

        public TonerNetworkSummary GetSummary()
        {
            RefreshStorageState();
            TonerNetworkSummary summary = new TonerNetworkSummary();
            summary.HasNetwork = pipes.Count > 0;
            summary.Stored = Mathf.RoundToInt(resourceBuffer);
            summary.Capacity = Mathf.RoundToInt(maxCapacity);
            return summary;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref resourceBuffer, "resourceBuffer", 0f);
            Scribe_Values.Look(ref maxCapacity, "maxCapacity", 0f);
            Scribe_Values.Look(ref netId, "netId", -1);
        }

        internal void ClearAssignments()
        {
            for (int i = 0; i < pipes.Count; i++)
            {
                if (pipes[i] != null)
                {
                    pipes[i].PipeNet = null;
                }
            }

            for (int i = 0; i < users.Count; i++)
            {
                if (users[i] != null)
                {
                    users[i].PipeNet = null;
                }
            }

            pipes.Clear();
            users.Clear();
            tanks.Clear();
            resourceBuffer = 0f;
            maxCapacity = 0f;
        }

        private void RefreshStorageState()
        {
            int totalCapacity = 0;
            int totalStored = 0;

            for (int i = 0; i < tanks.Count; i++)
            {
                CompTonerTank tank = tanks[i];
                if (tank == null || tank.parent == null)
                {
                    continue;
                }

                totalCapacity += tank.Capacity;
                totalStored += tank.StoredToner;
            }

            maxCapacity = totalCapacity;
            if (totalCapacity <= 0)
            {
                resourceBuffer = 0f;
                ZeroAllTanks();
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

            resourceBuffer = totalStored;
            RedistributeAcrossTanks(totalStored, totalCapacity);
        }

        private void SyncBufferToTanks()
        {
            int totalCapacity = 0;
            for (int i = 0; i < tanks.Count; i++)
            {
                CompTonerTank tank = tanks[i];
                if (tank != null && tank.parent != null)
                {
                    totalCapacity += tank.Capacity;
                }
            }

            maxCapacity = totalCapacity;
            if (totalCapacity <= 0)
            {
                resourceBuffer = 0f;
                ZeroAllTanks();
                return;
            }

            int totalStored = Mathf.Clamp(Mathf.RoundToInt(resourceBuffer), 0, totalCapacity);
            resourceBuffer = totalStored;
            RedistributeAcrossTanks(totalStored, totalCapacity);
        }

        private void ZeroAllTanks()
        {
            for (int i = 0; i < tanks.Count; i++)
            {
                CompTonerTank tank = tanks[i];
                if (tank != null)
                {
                    tank.SetStoredToner(0);
                }
            }
        }

        private void RedistributeAcrossTanks(int totalStored, int totalCapacity)
        {
            if (tanks.Count == 0)
            {
                return;
            }

            int[] targets = new int[tanks.Count];
            int[] remainders = new int[tanks.Count];
            int assigned = 0;

            for (int i = 0; i < tanks.Count; i++)
            {
                CompTonerTank tank = tanks[i];
                int capacity = tank == null ? 0 : tank.Capacity;
                long scaled = (long)totalStored * capacity;
                targets[i] = totalCapacity <= 0 ? 0 : (int)(scaled / totalCapacity);
                remainders[i] = totalCapacity <= 0 ? 0 : (int)(scaled % totalCapacity);
                assigned += targets[i];
            }

            int leftover = totalStored - assigned;
            while (leftover > 0)
            {
                int bestIndex = -1;
                int bestRemainder = -1;
                int bestCapacity = -1;
                for (int i = 0; i < tanks.Count; i++)
                {
                    CompTonerTank tank = tanks[i];
                    if (tank == null || targets[i] >= tank.Capacity)
                    {
                        continue;
                    }

                    if (remainders[i] > bestRemainder || remainders[i] == bestRemainder && tank.Capacity > bestCapacity)
                    {
                        bestIndex = i;
                        bestRemainder = remainders[i];
                        bestCapacity = tank.Capacity;
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

            for (int i = 0; i < tanks.Count; i++)
            {
                CompTonerTank tank = tanks[i];
                if (tank != null && tank.StoredToner != targets[i])
                {
                    tank.SetStoredToner(targets[i]);
                }
            }
        }
    }
}

