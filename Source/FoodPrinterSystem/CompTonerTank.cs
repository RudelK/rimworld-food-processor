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
        private const string TankFillPath = "Things/Building/foodsystem/toner/toner_tank_fill";
        private const float TankFillTextureSize = 256f;
        private const int FillMeshSteps = 256;
        private const float TankFillAltitudeOffset = 0.001f;
        // The fill sprite only occupies the tank's internal window, not the whole texture sheet.
        // Keep the visible fill mapped to that opaque region so 100% lines up with the tank body.
        private static readonly Rect TankFillVisibleUvRect = new Rect(52f / TankFillTextureSize, 47f / TankFillTextureSize, 156f / TankFillTextureSize, 120f / TankFillTextureSize);
        private static readonly Color TankFillColor = new Color(0.92f, 0.43f, 0.08f, 0.5f);
        private static readonly Material TankFillMaterial = MaterialPool.MatFrom(TankFillPath, ShaderDatabase.Transparent, TankFillColor);
        private static readonly Mesh[] TankFillMeshes = new Mesh[FillMeshSteps + 1];

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

        public override void PostPrintOnto(SectionLayer layer)
        {
            base.PostPrintOnto(layer);
            if (!(layer is SectionLayer_Things) && !(layer is SectionLayer_ThingsGeneral))
            {
                return;
            }

            PrintTankFillOverlay(layer);
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

        internal bool SetStoredTonerFromNetwork(int amount, bool deferVisualRefresh = false)
        {
            return SetStoredTonerInternal(amount, false, !deferVisualRefresh);
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

        private bool SetStoredTonerInternal(int amount, bool notifyNetwork, bool notifyVisualRefresh = true)
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
                else if (notifyVisualRefresh)
                {
                    NotifyStoredTonerVisualChanged(true);
                }

                FoodPrinterAlertHarmony.InvalidateAlertCache();
                return true;
            }

            return false;
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

            TonerNetworkSummary summary = TonerPipeNetManager.GetSummary(parent);
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
                TonerPipeNetManager.NotifyIngredientStateChanged(parent.MapHeld);
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
                NotifyStoredTonerVisualChanged(true);
            }
        }

        private void NotifyStoredTonerVisualChanged(bool notifyStorageRevision)
        {
            if (parent == null || parent.MapHeld == null)
            {
                return;
            }

            if (notifyStorageRevision)
            {
                TonerPipeNetManager.NotifyStorageStateChanged(parent.MapHeld);
            }
            MarkTankMeshDirty();
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
            cachedIngredientFoodKind = FoodPrinterSystemUtility.DetermineFoodKind(hasMeat, hasNonMeat);
            cachedContainsHumanMeatIngredient = containsHumanMeat;
            cachedContainsVegetarianForbiddenIngredient = containsVegetarianForbidden;
        }

        private void PrintTankFillOverlay(SectionLayer layer)
        {
            if (layer == null || parent.Map == null || Capacity <= 0)
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

            if (fillPercent <= 0f)
            {
                return;
            }

            Vector2 drawSize = GetTankDrawSize();
            if (drawSize.x <= 0f || drawSize.y <= 0f)
            {
                return;
            }

            int fillStep = Mathf.Clamp(Mathf.CeilToInt(fillPercent * FillMeshSteps), 1, FillMeshSteps);
            float quantizedFillPercent = fillStep / (float)FillMeshSteps;
            Rect fillUvRect = new Rect(
                TankFillVisibleUvRect.x,
                TankFillVisibleUvRect.y,
                TankFillVisibleUvRect.width,
                TankFillVisibleUvRect.height * quantizedFillPercent);
            Vector2[] uvs = GetUvCorners(fillUvRect);
            if (uvs == null || TankFillMaterial == null)
            {
                return;
            }

            float fillWorldWidth = drawSize.x * TankFillVisibleUvRect.width;
            float fillWorldHeight = drawSize.y * TankFillVisibleUvRect.height * quantizedFillPercent;
            if (fillWorldWidth <= 0f || fillWorldHeight <= 0f)
            {
                return;
            }

            Vector3 drawPos = parent.TrueCenter();
            drawPos.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays) + TankFillAltitudeOffset;
            drawPos.x += ((TankFillVisibleUvRect.x + (TankFillVisibleUvRect.width * 0.5f)) - 0.5f) * drawSize.x;
            drawPos.z += ((TankFillVisibleUvRect.y + (TankFillVisibleUvRect.height * quantizedFillPercent * 0.5f)) - 0.5f) * drawSize.y;

            layer.GetSubMesh(TankFillMaterial);
            Printer_Plane.PrintPlane(layer, drawPos, new Vector2(fillWorldWidth, fillWorldHeight), TankFillMaterial, 0f, false, uvs, null, 0f, 0f);
        }

        private Vector2 GetTankDrawSize()
        {
            GraphicData graphicData = parent == null || parent.def == null ? null : parent.def.graphicData;
            if (graphicData != null && graphicData.drawSize.x > 0f && graphicData.drawSize.y > 0f)
            {
                return graphicData.drawSize;
            }

            IntVec2 size = parent == null || parent.def == null ? IntVec2.Zero : parent.def.size;
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.z));
        }

        private void MarkTankMeshDirty()
        {
            if (parent == null || !parent.Spawned || parent.Map == null || parent.Map.mapDrawer == null)
            {
                return;
            }

            CellRect occupiedRect = GenAdj.OccupiedRect(parent.Position, parent.Rotation, parent.def.size);
            foreach (IntVec3 cell in occupiedRect)
            {
                if (!cell.InBounds(parent.Map))
                {
                    continue;
                }

                parent.Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Things);
                parent.Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Buildings);
            }
        }

        private static Vector2[] GetUvCorners(Rect rect)
        {
            int fillStep = Mathf.Clamp(Mathf.RoundToInt(rect.height * FillMeshSteps), 0, FillMeshSteps);
            if (fillStep <= 0)
            {
                return null;
            }

            Vector2[] corners = TankFillMeshes[fillStep] == null ? null : TankFillMeshes[fillStep].uv;
            if (corners != null && corners.Length == 4)
            {
                return corners;
            }

            corners = new[]
            {
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMax, rect.yMin)
            };

            Mesh uvCacheMesh = TankFillMeshes[fillStep];
            if (uvCacheMesh == null)
            {
                uvCacheMesh = Object.Instantiate(MeshPool.plane10);
                uvCacheMesh.name = "TonerTankFillMaskUvs_" + fillStep;
                TankFillMeshes[fillStep] = uvCacheMesh;
            }

            uvCacheMesh.uv = corners;
            return corners;
        }
    }
}
