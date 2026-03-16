using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoodPrinterSystem
{
    public class Dialog_FoodPrinterSettings : Window
    {
        private readonly CompFoodPrinter printerComp;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize
        {
            get { return new Vector2(760f, 360f); }
        }

        public Dialog_FoodPrinterSettings(CompFoodPrinter printerComp)
        {
            this.printerComp = printerComp;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            Rect autoRect = new Rect(inRect.x, inRect.y, 180f, 30f);
            Rect manualRect = new Rect(inRect.x + 190f, inRect.y, 180f, 30f);
            Rect modeLabelRect = new Rect(inRect.x, inRect.y + 35f, inRect.width, 24f);
            if (Widgets.ButtonText(autoRect, "FPS_PrinterModeAuto".Translate()))
            {
                printerComp.AutoMode = true;
            }

            if (Widgets.ButtonText(manualRect, "FPS_PrinterModeManual".Translate()))
            {
                printerComp.AutoMode = false;
            }

            string modeText = printerComp.AutoMode
                ? "FPS_PrinterModeAuto".Translate().ToString()
                : "FPS_PrinterModeManual".Translate().ToString();
            Widgets.Label(modeLabelRect, "FPS_PrinterMode".Translate(modeText));

            Rect headerRect = new Rect(inRect.x, inRect.y + 70f, inRect.width, 24f);
            Widgets.Label(new Rect(headerRect.x, headerRect.y, 250f, 24f), "FPS_PrinterColumnMeal".Translate());
            Widgets.Label(new Rect(headerRect.x + 255f, headerRect.y, 100f, 24f), "FPS_PrinterColumnNutrition".Translate());
            Widgets.Label(new Rect(headerRect.x + 360f, headerRect.y, 240f, 24f), "FPS_PrinterColumnStatus".Translate());

            List<FoodPreferability> categories = printerComp.GetKnownCategories();
            Rect scrollOutRect = new Rect(inRect.x, inRect.y + 98f, inRect.width, inRect.height - 130f);
            Rect viewRect = new Rect(0f, 0f, scrollOutRect.width - 16f, Mathf.Max(scrollOutRect.height, categories.Count * 36f));
            Widgets.BeginScrollView(scrollOutRect, ref scrollPosition, viewRect);

            float y = 0f;
            FoodPreferability? selectedManualCategory = printerComp.GetSelectedManualCategory();
            for (int i = 0; i < categories.Count; i++)
            {
                FoodPreferability category = categories[i];
                bool researched = printerComp.IsCategoryResearched(category);
                bool enabled = printerComp.IsCategoryEnabled(category);
                bool manualSelected = !printerComp.AutoMode && selectedManualCategory == category;

                Rect rowRect = new Rect(0f, y, viewRect.width, 32f);
                Rect categoryRect = new Rect(rowRect.x, rowRect.y, 250f, rowRect.height);
                Rect costRect = new Rect(rowRect.x + 255f, rowRect.y + 4f, 100f, rowRect.height);
                Rect statusRect = new Rect(rowRect.x + 360f, rowRect.y + 4f, 240f, rowRect.height);
                Rect manualButtonRect = new Rect(rowRect.x + 605f, rowRect.y, 95f, rowRect.height);

                TooltipHandler.TipRegion(categoryRect, printerComp.GetCategoryTooltip(category));
                GUI.enabled = researched;
                Widgets.CheckboxLabeled(categoryRect, printerComp.GetCategoryLabel(category), ref enabled);
                GUI.enabled = true;
                if (researched)
                {
                    printerComp.SetCategoryEnabled(category, enabled);
                }

                Widgets.Label(costRect, printerComp.GetCategoryCostLabel(category));
                Widgets.Label(statusRect, printerComp.GetCategoryStatusLabel(category));

                GUI.enabled = researched;
                string manualLabel = manualSelected
                    ? "FPS_PrinterManualSelected".Translate().ToString()
                    : "FPS_PrinterManualButton".Translate().ToString();
                if (Widgets.ButtonText(manualButtonRect, manualLabel))
                {
                    printerComp.SetManualCategory(category);
                }
                GUI.enabled = true;

                y += 36f;
            }

            Widgets.EndScrollView();
        }
    }
}
