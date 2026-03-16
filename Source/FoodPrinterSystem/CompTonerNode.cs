using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class CompProperties_TonerUser : CompProperties
    {
        public CompProperties_TonerUser()
        {
            compClass = typeof(CompTonerUser);
        }
    }

    public class CompProperties_TonerNode : CompProperties_TonerUser
    {
        public CompProperties_TonerNode()
        {
            compClass = typeof(CompTonerNode);
        }
    }

    [StaticConstructorOnStartup]
    public class CompTonerUser : ThingComp, ITonerNetworkUser
    {
        private static readonly Material ConnectionMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.71f, 0.90f, 0.76f, 0.92f));
        private static readonly Material NodeMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.58f, 0.82f, 0.68f, 0.96f));
        private static readonly Vector2 ConnectionSizeX = new Vector2(0.70f, 0.16f);
        private static readonly Vector2 ConnectionSizeZ = new Vector2(0.16f, 0.70f);
        private static readonly Vector2 NodeSize = new Vector2(0.24f, 0.24f);

        public Thing TonerNetworkThing
        {
            get { return parent; }
        }

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

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            TonerPipeNetManager.MarkDirty(parent.Map);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            TonerPipeNetManager.MarkDirty(map);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            TonerPipeNetManager.MarkDirty(previousMap);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            DrawTonerConnections();
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

        private void DrawTonerConnections()
        {
            Building building = parent as Building;
            if (building == null || parent.Map == null || parent is Building_Pipe)
            {
                return;
            }

            List<Thing> connectedNodes = TonerPipeNetManager.GetConnectedNodes(parent);
            if (connectedNodes == null || connectedNodes.Count <= 1)
            {
                return;
            }

            HashSet<int> connectedIds = new HashSet<int>();
            for (int i = 0; i < connectedNodes.Count; i++)
            {
                Thing thing = connectedNodes[i];
                if (thing != null && thing != parent)
                {
                    connectedIds.Add(thing.thingIDNumber);
                }
            }

            if (connectedIds.Count == 0)
            {
                return;
            }

            HashSet<IntVec3> nodeCells = new HashSet<IntVec3>();
            CellRect occupiedRect = building.OccupiedRect();
            foreach (IntVec3 cell in occupiedRect)
            {
                for (int j = 0; j < 4; j++)
                {
                    IntVec3 direction = GenAdj.CardinalDirections[j];
                    IntVec3 adjacent = cell + direction;
                    if (occupiedRect.Contains(adjacent) || !adjacent.InBounds(parent.Map) || !HasConnectedNodeAt(adjacent, connectedIds))
                    {
                        continue;
                    }

                    DrawConnection(cell, direction);
                    nodeCells.Add(cell);
                }
            }

            foreach (IntVec3 nodeCell in nodeCells)
            {
                DrawNode(nodeCell);
            }
        }

        private bool HasConnectedNodeAt(IntVec3 cell, HashSet<int> connectedIds)
        {
            List<Thing> things = cell.GetThingList(parent.Map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && connectedIds.Contains(thing.thingIDNumber) && IsVisualConnectionThing(thing))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVisualConnectionThing(Thing thing)
        {
            return thing is Building_Pipe || thing.TryGetComp<CompTonerUser>() != null || thing is ITonerNetworkUser;
        }

        private static void DrawConnection(IntVec3 cell, IntVec3 direction)
        {
            Vector3 center = cell.ToVector3Shifted();
            center.x += direction.x * 0.34f;
            center.z += direction.z * 0.34f;
            center.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays) - 0.02f;
            DrawQuad(center, direction.x != 0 ? ConnectionSizeX : ConnectionSizeZ, ConnectionMat);
        }

        private static void DrawNode(IntVec3 cell)
        {
            Vector3 center = cell.ToVector3Shifted();
            center.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays) - 0.021f;
            DrawQuad(center, NodeSize, NodeMat);
        }

        private static void DrawQuad(Vector3 center, Vector2 size, Material material)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size.x, 1f, size.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }
    }

    public class CompTonerNode : CompTonerUser
    {
    }
}
