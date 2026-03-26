using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class Dialog_ModMealSelection : Window
    {
        private const float SelectionColumnX = 822f;
        private const float SelectionColumnWidth = 36f;

        private sealed class MealRow
        {
            public ThingDef MealDef;
            public string Label;
            public string DefName;
            public string ModName;
            public string MealType;
            public string SearchText;
        }

        private readonly FoodPrinterSystemSettings settings;
        private readonly List<MealRow> rows = new List<MealRow>();
        private Vector2 scrollPosition;
        private string searchText = string.Empty;

        public override Vector2 InitialSize
        {
            get { return new Vector2(900f, 700f); }
        }

        public Dialog_ModMealSelection(FoodPrinterSystemSettings settings)
        {
            this.settings = settings;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            doCloseButton = true;
            closeOnClickedOutside = false;
            optionalTitle = "FPS_SettingsModMealSelectionTitle".Translate();
            BuildRows();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            settings.PruneMissingExternalModMealDefs();
            BuildRows();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            Rect summaryRect = new Rect(inRect.x, inRect.y, inRect.width, 24f);
            Widgets.Label(summaryRect, "FPS_SettingsModMealSelectionSummary".Translate(rows.Count.ToString()));

            Rect buttonRowRect = new Rect(inRect.x, summaryRect.yMax + 8f, inRect.width, 32f);
            float buttonWidth = 170f;
            Rect enableAllRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            Rect disableAllRect = new Rect(enableAllRect.xMax + 10f, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            Rect searchLabelRect = new Rect(buttonRowRect.xMax - 370f, buttonRowRect.y + 5f, 60f, 24f);
            Rect searchRect = new Rect(buttonRowRect.xMax - 305f, buttonRowRect.y, 305f, buttonRowRect.height);

            if (Widgets.ButtonText(enableAllRect, "FPS_SettingsModMealSelectionEnableAll".Translate()))
            {
                SetVisibleRowsEnabled(true);
            }

            if (Widgets.ButtonText(disableAllRect, "FPS_SettingsModMealSelectionDisableAll".Translate()))
            {
                SetVisibleRowsEnabled(false);
            }

            Widgets.Label(searchLabelRect, "FPS_SettingsModMealSelectionSearch".Translate());
            searchText = Widgets.TextField(searchRect, searchText ?? string.Empty);

            Rect headerRect = new Rect(inRect.x, buttonRowRect.yMax + 12f, inRect.width - 16f, 24f);
            Widgets.Label(new Rect(headerRect.x, headerRect.y, 64f, headerRect.height), "FPS_SettingsModMealSelectionColumnImage".Translate());
            Widgets.Label(new Rect(headerRect.x + 72f, headerRect.y, 220f, headerRect.height), "FPS_SettingsModMealSelectionColumnLabel".Translate());
            Widgets.Label(new Rect(headerRect.x + 300f, headerRect.y, 220f, headerRect.height), "FPS_SettingsModMealSelectionColumnDefName".Translate());
            Widgets.Label(new Rect(headerRect.x + 528f, headerRect.y, 150f, headerRect.height), "FPS_SettingsModMealSelectionColumnMod".Translate());
            Widgets.Label(new Rect(headerRect.x + 686f, headerRect.y, 120f, headerRect.height), "FPS_SettingsModMealSelectionColumnMealType".Translate());
            DrawCenteredLabel(new Rect(headerRect.x + SelectionColumnX, headerRect.y, SelectionColumnWidth, headerRect.height), "FPS_SettingsModMealSelectionColumnEnabled".Translate());

            List<MealRow> visibleRows = GetVisibleRows();
            Rect outRect = new Rect(inRect.x, headerRect.yMax + 4f, inRect.width, inRect.height - (headerRect.yMax - inRect.y) - 40f);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, visibleRows.Count * 42f));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float y = 0f;
            for (int i = 0; i < visibleRows.Count; i++)
            {
                DrawRow(new Rect(0f, y, viewRect.width, 38f), visibleRows[i], i);
                y += 42f;
            }

            Widgets.EndScrollView();
        }

        private void BuildRows()
        {
            rows.Clear();
            List<ThingDef> meals = CompFoodPrinter.GetExternalModMealsForSettings();
            for (int i = 0; i < meals.Count; i++)
            {
                ThingDef mealDef = meals[i];
                string label = mealDef == null ? string.Empty : mealDef.LabelCap.ToString();
                string defName = mealDef == null ? string.Empty : mealDef.defName;
                string modName = mealDef?.modContentPack?.Name ?? "?";
                string mealType = GetMealTypeLabel(mealDef);
                rows.Add(new MealRow
                {
                    MealDef = mealDef,
                    Label = label,
                    DefName = defName,
                    ModName = modName,
                    MealType = mealType,
                    SearchText = (label + "\n" + defName + "\n" + modName + "\n" + mealType).ToLowerInvariant()
                });
            }

            rows.Sort(CompareRows);
        }

        private static int CompareRows(MealRow left, MealRow right)
        {
            int result = GetMealTypeSortOrder(left?.MealDef).CompareTo(GetMealTypeSortOrder(right?.MealDef));
            if (result != 0)
            {
                return result;
            }

            result = string.Compare(left?.Label ?? string.Empty, right?.Label ?? string.Empty, System.StringComparison.CurrentCultureIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            return string.Compare(left?.DefName ?? string.Empty, right?.DefName ?? string.Empty, System.StringComparison.CurrentCultureIgnoreCase);
        }

        private static int GetMealTypeSortOrder(ThingDef mealDef)
        {
            if (mealDef?.ingestible == null)
            {
                return int.MaxValue;
            }

            switch (mealDef.ingestible.preferability)
            {
                case FoodPreferability.MealSimple:
                    return 0;
                case FoodPreferability.MealFine:
                    return 1;
                case FoodPreferability.MealLavish:
                    return 2;
                default:
                    return 3;
            }
        }

        private void DrawRow(Rect rowRect, MealRow row, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawLightHighlight(rowRect);
            }

            Rect iconRect = new Rect(rowRect.x + 4f, rowRect.y + 3f, 32f, 32f);
            Rect labelRect = new Rect(rowRect.x + 72f, rowRect.y + 7f, 220f, 24f);
            Rect defNameRect = new Rect(rowRect.x + 300f, rowRect.y + 7f, 220f, 24f);
            Rect modRect = new Rect(rowRect.x + 528f, rowRect.y + 7f, 150f, 24f);
            Rect mealTypeRect = new Rect(rowRect.x + 686f, rowRect.y + 7f, 120f, 24f);
            Rect enabledRect = new Rect(rowRect.x + SelectionColumnX + ((SelectionColumnWidth - 24f) * 0.5f), rowRect.y + 7f, 24f, 24f);

            DrawMealIcon(iconRect, row.MealDef);
            TooltipHandler.TipRegion(iconRect, row.Label);
            Widgets.Label(labelRect, row.Label);
            Widgets.Label(defNameRect, row.DefName);
            Widgets.Label(modRect, row.ModName);
            Widgets.Label(mealTypeRect, row.MealType);

            bool enabledBefore = settings.IsExternalModMealEnabled(row.MealDef);
            bool enabled = enabledBefore;
            Widgets.Checkbox(enabledRect.position, ref enabled, 24f, false, true);
            if (enabled != enabledBefore)
            {
                settings.SetExternalModMealEnabled(row.MealDef, enabled);
                FoodPrinterSystemMod.ApplyLiveSettings();
            }
        }

        private static void DrawMealIcon(Rect rect, ThingDef mealDef)
        {
            Widgets.DrawMenuSection(rect.ContractedBy(1f));
            Texture texture = mealDef == null ? BaseContent.BadTex : mealDef.uiIcon;
            Color color = mealDef == null ? Color.white : mealDef.uiIconColor;
            GUI.color = color;
            Widgets.DrawTextureFitted(rect.ContractedBy(3f), texture ?? BaseContent.BadTex, 1f);
            GUI.color = Color.white;
        }

        private static void DrawCenteredLabel(Rect rect, string label)
        {
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = oldAnchor;
        }

        private static string GetMealTypeLabel(ThingDef mealDef)
        {
            if (mealDef?.ingestible == null)
            {
                return string.Empty;
            }

            switch (mealDef.ingestible.preferability)
            {
                case FoodPreferability.MealSimple:
                    return "FPS_PrinterCategorySimple".Translate();
                case FoodPreferability.MealFine:
                    return "FPS_PrinterCategoryFine".Translate();
                case FoodPreferability.MealLavish:
                    return "FPS_PrinterCategoryLavish".Translate();
                default:
                    return mealDef.ingestible.preferability.ToString();
            }
        }

        private List<MealRow> GetVisibleRows()
        {
            if (searchText.NullOrEmpty())
            {
                return rows;
            }

            string filter = searchText.ToLowerInvariant().Trim();
            List<MealRow> visibleRows = new List<MealRow>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].SearchText.Contains(filter))
                {
                    visibleRows.Add(rows[i]);
                }
            }

            return visibleRows;
        }

        private void SetVisibleRowsEnabled(bool enabled)
        {
            List<MealRow> visibleRows = GetVisibleRows();
            bool changed = false;
            for (int i = 0; i < visibleRows.Count; i++)
            {
                MealRow row = visibleRows[i];
                bool currentlyEnabled = settings.IsExternalModMealEnabled(row.MealDef);
                if (currentlyEnabled == enabled)
                {
                    continue;
                }

                settings.SetExternalModMealEnabled(row.MealDef, enabled);
                changed = true;
            }

            if (changed)
            {
                FoodPrinterSystemMod.ApplyLiveSettings();
            }
        }
    }
}
