using System;
using System.Reflection;
using FoodPrinterSystem;
using RimWorld;
using Verse;

namespace FoodSystemPipe
{
    public enum PipeOverlayVisibilityMode
    {
        Hidden,
        FoodProcess
    }

    public static class PipeOverlayVisibilityUtility
    {
        private const string FoodProcessCategoryDefName = "FPS_FoodProcessingCategory";
        private static readonly bool DebugLoggingEnabled = false;
        private static readonly BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static PipeOverlayVisibilityMode GetVisibilityMode(Map map)
        {
            if (map == null || Find.CurrentMap != map)
            {
                return PipeOverlayVisibilityMode.Hidden;
            }

            return IsFoodProcessCategoryActive(GetSelectedDesignator())
                ? PipeOverlayVisibilityMode.FoodProcess
                : PipeOverlayVisibilityMode.Hidden;
        }

        public static string GetSelectedDesignatorDebugLabel()
        {
            Designator selectedDesignator = GetSelectedDesignator();
            if (selectedDesignator != null)
            {
                BuildableDef placingDef = GetSelectedBuildableDef(selectedDesignator);
                return placingDef == null
                    ? selectedDesignator.GetType().FullName
                    : selectedDesignator.GetType().FullName + "(" + placingDef.defName + ")";
            }

            DesignationCategoryDef architectCategory = GetSelectedArchitectCategory();
            return architectCategory == null
                ? "<none>"
                : "ArchitectCategory(" + architectCategory.defName + ")";
        }

        public static void DebugLog(string message)
        {
            if (DebugLoggingEnabled)
            {
                Log.Message("[PipeOverlay] " + message);
            }
        }

        private static Designator GetSelectedDesignator()
        {
            return Find.DesignatorManager == null ? null : Find.DesignatorManager.SelectedDesignator;
        }

        private static bool IsFoodProcessCategoryActive(Designator selectedDesignator)
        {
            DesignationCategoryDef foodProcessCategory = FoodPrinterSystemDefOf.FPS_FoodProcessingCategory
                ?? DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(FoodProcessCategoryDefName);
            if (foodProcessCategory == null)
            {
                return false;
            }

            BuildableDef placingDef = GetSelectedBuildableDef(selectedDesignator);
            if (placingDef != null)
            {
                return placingDef.designationCategory == foodProcessCategory;
            }

            DesignationCategoryDef activeCategory = ResolveDesignationCategory(selectedDesignator);
            return activeCategory == foodProcessCategory || GetSelectedArchitectCategory() == foodProcessCategory;
        }

        private static BuildableDef GetSelectedBuildableDef(Designator selectedDesignator)
        {
            Designator_Build buildDesignator = selectedDesignator as Designator_Build;
            return buildDesignator == null ? null : buildDesignator.PlacingDef;
        }

        private static DesignationCategoryDef ResolveDesignationCategory(Designator designator)
        {
            object category = GetMemberValue(designator, "DesigCategory")
                ?? GetMemberValue(designator, "desigCategory")
                ?? GetMemberValue(designator, "DesignationCategory")
                ?? GetMemberValue(designator, "designationCategory");
            return category as DesignationCategoryDef;
        }

        private static DesignationCategoryDef GetSelectedArchitectCategory()
        {
            MainButtonDef openTab = Find.MainTabsRoot == null ? null : Find.MainTabsRoot.OpenTab;
            MainTabWindow_Architect architectWindow = openTab == null ? null : openTab.TabWindow as MainTabWindow_Architect;
            if (architectWindow == null || !architectWindow.IsOpen)
            {
                return null;
            }

            object selectedTab = GetMemberValue(architectWindow, "selectedDesPanel");
            return GetMemberValue(selectedTab, "def") as DesignationCategoryDef;
        }

        private static object GetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            Type type = target.GetType();
            PropertyInfo property = type.GetProperty(memberName, MemberFlags);
            if (property != null && property.CanRead)
            {
                return property.GetValue(target, null);
            }

            FieldInfo field = type.GetField(memberName, MemberFlags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            return null;
        }
    }
}
