using System.Collections.Generic;
using FoodSystemPipe;
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
        // These must remain the final ingestible ingredient defs actually used by food
        // (for example muffalo meat, not muffalo) because printer food-type prediction
        // depends on ingredientDef.ingestible.foodType staying accurate downstream.
        private List<ThingDef> storedIngredients = new List<ThingDef>();
        private FoodTypeFlags cachedIngredientFoodTypes;
        private FoodKind cachedIngredientFoodKind = FoodKind.Any;
        private bool cachedContainsHumanMeatIngredient;
        private bool cachedContainsVegetarianForbiddenIngredient;

        public List<ThingDef> StoredIngredients => storedIngredients;
        public FoodTypeFlags CachedIngredientFoodTypes => cachedIngredientFoodTypes;
        public FoodKind CachedIngredientFoodKind => cachedIngredientFoodKind;
        public bool CachedContainsHumanMeatIngredient => cachedContainsHumanMeatIngredient;
        public bool CachedContainsVegetarianForbiddenIngredient => cachedContainsVegetarianForbiddenIngredient;

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


        public bool PowerOn
        {
            get { return powerComp == null || powerComp.PowerOn; }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.TryGetComp<CompPowerTrader>();
            NotifySettingsChanged();
            RebuildIngredientCharacteristics();

            MapComponent_TonerNetwork netComp = parent.Map.GetComponent<MapComponent_TonerNetwork>();
            if (netComp != null)
            {
                netComp.RegisterTank(this);
            }

            TonerPipeNetManager.MarkDirty(parent.MapHeld);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            MapComponent_TonerNetwork netComp = map.GetComponent<MapComponent_TonerNetwork>();
            if (netComp != null)
            {
                netComp.DeregisterTank(this);
            }

            TonerPipeNetManager.MarkDirty(map);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

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

            SetStoredToner(0);
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

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            if (parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Action
                {
                    defaultLabel = "FPS_EmptyTank".Translate(),
                    defaultDesc = "FPS_EmptyTankDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true),
                    action = delegate
                    {
                        SetStoredToner(0);
                    }
                };
            }
        }

        public void AddIngredients(List<ThingDef> ingredients)
        {
            if (ingredients == null || ingredients.Count == 0) return;
            bool changed = false;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (!storedIngredients.Contains(ingredients[i]))
                {
                    storedIngredients.Add(ingredients[i]);
                    changed = true;
                }
            }

            if (changed)
            {
                RebuildIngredientCharacteristics();
                NotifyIngredientStateChanged();
            }
        }

        public void ClearIngredients()
        {
            if (storedIngredients.Count == 0)
            {
                return;
            }

            storedIngredients.Clear();
            RebuildIngredientCharacteristics();
            NotifyIngredientStateChanged();
        }

        public void SetStoredToner(int amount)
        {
            SetStoredTonerInternal(amount, true);
        }

        internal void SetStoredTonerFromNetwork(int amount)
        {
            SetStoredTonerInternal(amount, false);
        }

        public void NotifySettingsChanged()
        {
            int previousStored = storedToner;
            ClampStoredTonerToCapacity();
            ApplyPowerSetting();
            if (parent != null && parent.Spawned)
            {
                NotifyStoredTonerChanged();
            }

            if (storedToner != previousStored)
            {
                FoodPrinterAlertHarmony.InvalidateAlertCache();
            }
        }

        private void SetStoredTonerInternal(int amount, bool notifyNetwork)
        {
            int previousStored = storedToner;
            storedToner = amount < 0 ? 0 : amount;
            ClampStoredTonerToCapacity();
            if (storedToner == 0 && previousStored > 0)
            {
                ClearIngredients();
            }

            if (storedToner != previousStored)
            {
                if (notifyNetwork)
                {
                    NotifyStoredTonerChanged();
                }

                FoodPrinterAlertHarmony.InvalidateAlertCache();
            }
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

            TonerNetworkSummary summary = TonerNetworkUtility.GetSummary(parent);
            if (summary.HasNetwork)
            {
                text += "\n" + FoodPrinterSystemUtility.FormatSummary(summary);
            }

            if (!PowerOn && storedToner > 0)
            {
                int remainingTicks = PowerLossSpoilTicks - unpoweredTicks;
                if (remainingTicks < 0)
                {
                    remainingTicks = 0;
                }

                text += "\n" + "FPS_TonerRemainingPowerLoss".Translate(FoodPrinterSystemUtility.FormatHoursRemaining(remainingTicks));
            }

            if (storedToner > 0 && storedIngredients.Count > 0)
            {
                List<string> ingredientLabels = new List<string>();
                for (int i = 0; i < storedIngredients.Count; i++)
                {
                    ingredientLabels.Add(storedIngredients[i].LabelCap);
                }
                text += "\n" + "FPS_TankContains".Translate(string.Join(", ", ingredientLabels.ToArray()));
            }

            return text;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref storedToner, "storedTonerUnitsV2", 0);
            Scribe_Collections.Look(ref storedIngredients, "storedIngredients", LookMode.Def);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (storedIngredients == null)
                {
                    storedIngredients = new List<ThingDef>();
                }
                storedIngredients.RemoveAll(x => x == null);
                RebuildIngredientCharacteristics();
            }

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

        private void NotifyIngredientStateChanged()
        {
            // Printer food-character prediction is cached by toner network content
            // revision, so ingredient provenance changes must invalidate that cache
            // without forcing a full pipe topology rebuild.
            if (parent != null && parent.MapHeld != null)
            {
                TonerNetworkUtility.NotifyContentsChanged(parent.MapHeld);
            }
        }

        private void NotifyStoredTonerChanged()
        {
            if (parent == null)
            {
                return;
            }

            CompPipe pipeComp = parent.TryGetComp<CompPipe>();
            if (pipeComp != null && pipeComp.PipeNet != null)
            {
                pipeComp.PipeNet.NotifyStorageChanged();
            }

            if (parent.MapHeld != null)
            {
                TonerNetworkUtility.NotifyContentsChanged(parent.MapHeld);
            }
        }

        private void RebuildIngredientCharacteristics()
        {
            FoodTypeFlags foodTypes = FoodTypeFlags.None;
            bool hasMeat = false;
            bool hasNonMeat = false;
            bool containsHumanMeat = false;
            bool containsVegetarianForbidden = false;

            for (int i = 0; i < storedIngredients.Count; i++)
            {
                ThingDef ingredientDef = storedIngredients[i];
                if (ingredientDef == null || ingredientDef.ingestible == null)
                {
                    continue;
                }

                FoodTypeFlags ingredientFoodTypes = ingredientDef.ingestible.foodType;
                foodTypes |= ingredientFoodTypes;
                if ((ingredientFoodTypes & FoodTypeFlags.Meat) != FoodTypeFlags.None)
                {
                    hasMeat = true;
                }

                if ((ingredientFoodTypes & FoodTypeFlags.Meat) != ingredientFoodTypes)
                {
                    hasNonMeat = true;
                }

                if (!containsHumanMeat && FoodUtility.IsHumanFood(ingredientDef))
                {
                    containsHumanMeat = true;
                }

                if (!containsVegetarianForbidden && FoodUtility.UnacceptableVegetarian(ingredientDef))
                {
                    containsVegetarianForbidden = true;
                }
            }

            cachedIngredientFoodTypes = foodTypes;
            cachedIngredientFoodKind = DetermineFoodKind(hasMeat, hasNonMeat);
            cachedContainsHumanMeatIngredient = containsHumanMeat;
            cachedContainsVegetarianForbiddenIngredient = containsVegetarianForbidden;
        }

        private static FoodKind DetermineFoodKind(bool hasMeat, bool hasNonMeat)
        {
            if (hasMeat && hasNonMeat)
            {
                return FoodKind.Any;
            }

            if (hasMeat)
            {
                return FoodKind.Meat;
            }

            if (hasNonMeat)
            {
                return FoodKind.NonMeat;
            }

            return FoodKind.Any;
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
