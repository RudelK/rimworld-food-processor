using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class CompProperties_TonerTank : CompProperties
    {
        public int capacity;

        public CompProperties_TonerTank()
        {
            compClass = typeof(CompTonerTank);
        }
    }

    [StaticConstructorOnStartup]
    public class CompTonerTank : ThingComp
    {
        private const int PowerLossSpoilTicks = 80000;
        private static readonly Material TankBarBackgroundMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.08f, 0.08f, 0.08f, 0.85f));
        private static readonly Material TankBarFillMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.24f, 0.77f, 0.36f, 0.95f));

        private int storedToner;
        private int unpoweredTicks;
        private CompPowerTrader powerComp;

        public CompProperties_TonerTank Props
        {
            get { return (CompProperties_TonerTank)props; }
        }

        public int StoredToner
        {
            get
            {
                ClampStoredTonerToCapacity();
                return storedToner;
            }
        }

        public int Capacity
        {
            get
            {
                int configuredCapacity = FoodPrinterSystemUtility.GetTankCapacity(parent.def);
                return configuredCapacity > 0 ? configuredCapacity : Props.capacity;
            }
        }

        public int SpaceLeft
        {
            get
            {
                ClampStoredTonerToCapacity();
                return storedToner >= Capacity ? 0 : Capacity - storedToner;
            }
        }

        public bool PowerOn
        {
            get { return powerComp == null || powerComp.PowerOn; }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.TryGetComp<CompPowerTrader>();
            NotifySettingsChanged();
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            NotifySettingsChanged();

            if (storedToner <= 0)
            {
                unpoweredTicks = 0;
                return;
            }

            if (PowerOn)
            {
                unpoweredTicks = 0;
                return;
            }

            unpoweredTicks += GenTicks.TickRareInterval;
            if (unpoweredTicks < PowerLossSpoilTicks)
            {
                return;
            }

            storedToner = 0;
            unpoweredTicks = 0;
            if (parent.MapHeld != null)
            {
                Messages.Message("FPS_TankSpoiled".Translate(parent.LabelShortCap), parent, MessageTypeDefOf.NegativeEvent, false);
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            DrawStorageBar();
        }

        public int AddToner(int amount)
        {
            ClampStoredTonerToCapacity();
            if (amount <= 0 || SpaceLeft <= 0)
            {
                return 0;
            }

            int added = amount > SpaceLeft ? SpaceLeft : amount;
            storedToner += added;
            return added;
        }

        public int DrawToner(int amount)
        {
            ClampStoredTonerToCapacity();
            if (amount <= 0 || storedToner <= 0)
            {
                return 0;
            }

            int drawn = amount > storedToner ? storedToner : amount;
            storedToner -= drawn;
            return drawn;
        }

        public void SetStoredToner(int amount)
        {
            storedToner = amount < 0 ? 0 : amount;
            ClampStoredTonerToCapacity();
        }

        public void NotifySettingsChanged()
        {
            ClampStoredTonerToCapacity();
            ApplyPowerSetting();
        }

        public void ApplyPowerSetting()
        {
            if (powerComp == null)
            {
                powerComp = parent.TryGetComp<CompPowerTrader>();
            }

            if (powerComp != null)
            {
                powerComp.PowerOutput = -FoodPrinterSystemUtility.GetConstantPowerDraw(parent.def);
            }
        }

        public override string CompInspectStringExtra()
        {
            ClampStoredTonerToCapacity();
            string text = "FPS_TonerStored".Translate(FoodPrinterSystemUtility.FormatToner(storedToner))
                + "\n"
                + "FPS_TonerCapacity".Translate(FoodPrinterSystemUtility.FormatToner(Capacity));

            if (!PowerOn && storedToner > 0)
            {
                int remainingTicks = PowerLossSpoilTicks - unpoweredTicks;
                if (remainingTicks < 0)
                {
                    remainingTicks = 0;
                }

                text += "\n" + "FPS_TonerRemainingPowerLoss".Translate(FoodPrinterSystemUtility.FormatHoursRemaining(remainingTicks));
            }

            return text;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref storedToner, "storedTonerUnitsV2", 0);
            if (Scribe.mode == LoadSaveMode.LoadingVars && storedToner == 0)
            {
                int legacyStoredTonerUnits = 0;
                Scribe_Values.Look(ref legacyStoredTonerUnits, "storedTonerUnits", 0);
                if (legacyStoredTonerUnits > 0)
                {
                    storedToner = FoodPrinterSystemUtility.ConvertLegacyStoredTonerUnits(legacyStoredTonerUnits);
                }

                if (storedToner == 0)
                {
                    float legacyStoredToner = 0f;
                    Scribe_Values.Look(ref legacyStoredToner, "storedToner", 0f);
                    if (legacyStoredToner > 0f)
                    {
                        storedToner = FoodPrinterSystemUtility.ToTonerUnits(legacyStoredToner);
                    }
                }
            }

            Scribe_Values.Look(ref unpoweredTicks, "unpoweredTicks", 0);
        }

        private void ClampStoredTonerToCapacity()
        {
            int capacity = Capacity;
            if (capacity < 0)
            {
                capacity = 0;
            }

            if (storedToner > capacity)
            {
                storedToner = capacity;
            }
            else if (storedToner < 0)
            {
                storedToner = 0;
            }
        }

        private void DrawStorageBar()
        {
            if (parent.Map == null || Capacity <= 0)
            {
                return;
            }

            float fillPercent = storedToner / (float)Capacity;
            if (fillPercent < 0f)
            {
                fillPercent = 0f;
            }
            else if (fillPercent > 1f)
            {
                fillPercent = 1f;
            }

            Vector2 barSize = new Vector2(parent.def.size.x * 0.78f, 0.12f);
            Vector3 barCenter = parent.TrueCenter();
            barCenter.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
            barCenter.z -= parent.def.size.z * 0.44f;

            DrawBar(barCenter, barSize, TankBarBackgroundMat);
            if (fillPercent > 0f)
            {
                Vector2 fillSize = new Vector2(barSize.x * fillPercent, barSize.y * 0.72f);
                Vector3 fillCenter = barCenter;
                fillCenter.x -= (barSize.x - fillSize.x) * 0.5f;
                fillCenter.y += 0.002f;
                DrawBar(fillCenter, fillSize, TankBarFillMat);
            }
        }

        private static void DrawBar(Vector3 center, Vector2 size, Material material)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(size.x, 1f, size.y));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }
    }
}
