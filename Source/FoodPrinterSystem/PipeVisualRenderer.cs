using RimWorld;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public static class PipeVisualRenderer
    {
        public static void PrintSectionCell(SectionLayer layer, IntVec3 cell, PipeDirectionMask mask, Material material, float baseAltitude, float bridgeAltitude)
        {
            if (layer == null || material == null)
            {
                return;
            }

            Vector3 cellCenter = cell.ToVector3Shifted();
            var pieces = PipePieceUtility.GetRenderPieces(mask);
            for (int i = 0; i < pieces.Count; i++)
            {
                PipeRenderPiece piece = pieces[i];
                Vector2[] uvs = PipePieceUtility.GetUvCorners(piece.Kind);
                if (uvs == null)
                {
                    continue;
                }

                Vector3 center = cellCenter + piece.Offset;
                center.y = PipePieceUtility.IsBridgePiece(piece.Kind) ? bridgeAltitude : baseAltitude;

                layer.GetSubMesh(material);
                Printer_Plane.PrintPlane(layer, center, piece.Size, material, 0f, false, uvs, null, 0f, 0f);
            }
        }

        public static void DrawDynamicCell(IntVec3 cell, PipeDirectionMask mask, Material material, float baseAltitude, float bridgeAltitude)
        {
            if (material == null)
            {
                return;
            }

            Vector3 cellCenter = cell.ToVector3Shifted();
            var pieces = PipePieceUtility.GetRenderPieces(mask);
            for (int i = 0; i < pieces.Count; i++)
            {
                PipeRenderPiece piece = pieces[i];
                Mesh mesh = PipePieceUtility.GetMesh(piece.Kind);
                if (mesh == null)
                {
                    continue;
                }

                Vector3 drawPos = cellCenter + piece.Offset;
                drawPos.y = PipePieceUtility.IsBridgePiece(piece.Kind) ? bridgeAltitude : baseAltitude;

                Matrix4x4 matrix = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(piece.Size.x, 1f, piece.Size.y));
                Graphics.DrawMesh(mesh, matrix, material, 0);
            }
        }
    }
}
