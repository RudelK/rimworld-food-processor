using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    [StaticConstructorOnStartup]
    public static class PipeAtlasUtility
    {
        private const int AtlasColumns = 4;
        private const int AtlasRows = 4;
        private const bool FlipAtlasRowsForUnityUv = false;

        // Atlas order matches PowerConduit_Atlas; only UV interpretation is being fixed.
        private static readonly Dictionary<int, Rect> UvRectCache = new Dictionary<int, Rect>();

        public static bool UsesFlippedRows
        {
            get { return FlipAtlasRowsForUnityUv; }
        }

        public static int GetAtlasIndex(int mask)
        {
            return Mathf.Clamp(mask, 0, 15);
        }

        public static int GetAtlasColumn(int atlasIndex)
        {
            atlasIndex = Mathf.Clamp(atlasIndex, 0, 15);
            return atlasIndex % AtlasColumns;
        }

        public static int GetAtlasRowFromTop(int atlasIndex)
        {
            atlasIndex = Mathf.Clamp(atlasIndex, 0, 15);
            return atlasIndex / AtlasColumns;
        }

        public static int GetAtlasRowUsed(int rowFromTop, bool flipRows)
        {
            return flipRows ? (AtlasRows - 1 - rowFromTop) : rowFromTop;
        }

        public static Rect GetAtlasUvRect(int atlasIndex, bool flipRows)
        {
            atlasIndex = Mathf.Clamp(atlasIndex, 0, 15);
            int cacheKey = atlasIndex | (flipRows ? 1 << 8 : 0);
            if (!UvRectCache.TryGetValue(cacheKey, out Rect rect))
            {
                rect = BuildAtlasUvRect(atlasIndex, flipRows);
                UvRectCache[cacheKey] = rect;
            }

            return rect;
        }

        private static Rect BuildAtlasUvRect(int atlasIndex, bool flipRows)
        {
            float cellWidth = 1f / AtlasColumns;
            float cellHeight = 1f / AtlasRows;
            int col = GetAtlasColumn(atlasIndex);
            int rowFromTop = GetAtlasRowFromTop(atlasIndex);
            int rowUsed = GetAtlasRowUsed(rowFromTop, flipRows);
            float x = col * cellWidth;
            float y = rowUsed * cellHeight;
            return new Rect(x, y, cellWidth, cellHeight);
        }
    }
}
