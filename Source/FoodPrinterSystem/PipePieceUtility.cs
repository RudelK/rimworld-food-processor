using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FoodSystemPipe
{
    public enum PipePieceKind
    {
        Hub,
        NorthArm,
        EastArm,
        SouthArm,
        WestArm,
        NorthBridge,
        EastBridge,
        SouthBridge,
        WestBridge
    }

    public struct PipeRenderPiece
    {
        public PipeRenderPiece(PipePieceKind kind, Vector3 offset, Vector2 size)
        {
            Kind = kind;
            Offset = offset;
            Size = size;
        }

        public PipePieceKind Kind { get; private set; }
        public Vector3 Offset { get; private set; }
        public Vector2 Size { get; private set; }
    }

    [StaticConstructorOnStartup]
    public static class PipePieceUtility
    {
        private static readonly PipeDirectionMask[] CanonicalBridgeDirections =
        {
            PipeDirectionMask.North,
            PipeDirectionMask.East
        };

        private const float StrokeWorldSize = 0.56f;
        private const float HubWorldSize = 0.36f;
        private const float JoinWorldOverlap = 0.04f;
        private const float BridgeWorldWidth = 0.52f;
        private const float BridgeWorldLength = 0.18f;
        private const float BridgeWorldOffset = 0.5f;

        private const float StrokeUvFraction = 0.56f;
        private const float HubUvFraction = 0.36f;
        private const float JoinUvOverlapFraction = 0.04f;
        private const float BridgeUvFraction = 0.18f;

        private const int VerticalSourceAtlasIndex = 5;
        private const int HorizontalSourceAtlasIndex = 10;
        private const int HubSourceAtlasIndex = 15;

        private const float HubHalfWorldSize = HubWorldSize * 0.5f;
        private const float VerticalArmWorldStart = HubHalfWorldSize - JoinWorldOverlap;
        private const float VerticalArmWorldEnd = 0.5f;
        private const float VerticalArmWorldLength = VerticalArmWorldEnd - VerticalArmWorldStart;
        private const float VerticalArmWorldOffset = (VerticalArmWorldStart + VerticalArmWorldEnd) * 0.5f;
        private const float HorizontalArmWorldStart = HubHalfWorldSize - JoinWorldOverlap;
        private const float HorizontalArmWorldEnd = 0.5f;
        private const float HorizontalArmWorldLength = HorizontalArmWorldEnd - HorizontalArmWorldStart;
        private const float HorizontalArmWorldOffset = (HorizontalArmWorldStart + HorizontalArmWorldEnd) * 0.5f;

        private static readonly PipeRenderPiece HubPiece = new PipeRenderPiece(PipePieceKind.Hub, Vector3.zero, new Vector2(HubWorldSize, HubWorldSize));
        private static readonly PipeRenderPiece NorthArmPiece = new PipeRenderPiece(PipePieceKind.NorthArm, new Vector3(0f, 0f, VerticalArmWorldOffset), new Vector2(StrokeWorldSize, VerticalArmWorldLength));
        private static readonly PipeRenderPiece EastArmPiece = new PipeRenderPiece(PipePieceKind.EastArm, new Vector3(HorizontalArmWorldOffset, 0f, 0f), new Vector2(HorizontalArmWorldLength, StrokeWorldSize));
        private static readonly PipeRenderPiece SouthArmPiece = new PipeRenderPiece(PipePieceKind.SouthArm, new Vector3(0f, 0f, -VerticalArmWorldOffset), new Vector2(StrokeWorldSize, VerticalArmWorldLength));
        private static readonly PipeRenderPiece WestArmPiece = new PipeRenderPiece(PipePieceKind.WestArm, new Vector3(-HorizontalArmWorldOffset, 0f, 0f), new Vector2(HorizontalArmWorldLength, StrokeWorldSize));
        private static readonly PipeRenderPiece NorthBridgePiece = new PipeRenderPiece(PipePieceKind.NorthBridge, new Vector3(0f, 0f, BridgeWorldOffset), new Vector2(BridgeWorldWidth, BridgeWorldLength));
        private static readonly PipeRenderPiece EastBridgePiece = new PipeRenderPiece(PipePieceKind.EastBridge, new Vector3(BridgeWorldOffset, 0f, 0f), new Vector2(BridgeWorldLength, BridgeWorldWidth));
        private static readonly PipeRenderPiece SouthBridgePiece = new PipeRenderPiece(PipePieceKind.SouthBridge, new Vector3(0f, 0f, -BridgeWorldOffset), new Vector2(BridgeWorldWidth, BridgeWorldLength));
        private static readonly PipeRenderPiece WestBridgePiece = new PipeRenderPiece(PipePieceKind.WestBridge, new Vector3(-BridgeWorldOffset, 0f, 0f), new Vector2(BridgeWorldLength, BridgeWorldWidth));
        private static readonly PipeRenderPiece[][] RenderPiecesByMask = BuildRenderPiecesByMask();
        private static readonly Dictionary<PipePieceKind, Vector2[]> UvCornersByKind = new Dictionary<PipePieceKind, Vector2[]>();
        private static readonly Dictionary<PipePieceKind, Mesh> MeshesByKind = new Dictionary<PipePieceKind, Mesh>();

        public static IList<PipeRenderPiece> GetRenderPieces(PipeDirectionMask mask)
        {
            return RenderPiecesByMask[Mathf.Clamp((int)mask, 0, 15)];
        }

        public static bool IsBridgePiece(PipePieceKind kind)
        {
            switch (kind)
            {
                case PipePieceKind.NorthBridge:
                case PipePieceKind.EastBridge:
                case PipePieceKind.SouthBridge:
                case PipePieceKind.WestBridge:
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetBridgePiece(PipeDirectionMask direction, out PipeRenderPiece piece)
        {
            switch (direction)
            {
                case PipeDirectionMask.North:
                    piece = NorthBridgePiece;
                    return true;
                case PipeDirectionMask.East:
                    piece = EastBridgePiece;
                    return true;
                case PipeDirectionMask.South:
                    piece = SouthBridgePiece;
                    return true;
                case PipeDirectionMask.West:
                    piece = WestBridgePiece;
                    return true;
                default:
                    piece = default(PipeRenderPiece);
                    return false;
            }
        }

        public static Vector2[] GetUvCorners(PipePieceKind kind)
        {
            if (!UvCornersByKind.TryGetValue(kind, out Vector2[] corners))
            {
                Rect rect = GetPieceUvRect(kind);
                corners = new[]
                {
                    new Vector2(rect.xMin, rect.yMin),
                    new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax),
                    new Vector2(rect.xMax, rect.yMin)
                };
                UvCornersByKind[kind] = corners;
            }

            return corners;
        }

        public static Mesh GetMesh(PipePieceKind kind)
        {
            if (!MeshesByKind.TryGetValue(kind, out Mesh mesh) || mesh == null)
            {
                mesh = Object.Instantiate(MeshPool.plane10);
                mesh.name = "PipePiece_" + kind;
                mesh.uv = GetUvCorners(kind);
                MeshesByKind[kind] = mesh;
            }

            return mesh;
        }

        private static PipeRenderPiece[][] BuildRenderPiecesByMask()
        {
            PipeRenderPiece[][] piecesByMask = new PipeRenderPiece[16][];
            for (int i = 0; i < piecesByMask.Length; i++)
            {
                PipeDirectionMask mask = (PipeDirectionMask)i;
                List<PipeRenderPiece> pieces = new List<PipeRenderPiece>(7)
                {
                    HubPiece
                };

                if ((mask & PipeDirectionMask.North) != 0)
                {
                    pieces.Add(NorthArmPiece);
                }

                if ((mask & PipeDirectionMask.East) != 0)
                {
                    pieces.Add(EastArmPiece);
                }

                if ((mask & PipeDirectionMask.South) != 0)
                {
                    pieces.Add(SouthArmPiece);
                }

                if ((mask & PipeDirectionMask.West) != 0)
                {
                    pieces.Add(WestArmPiece);
                }

                AddCanonicalBridgePieces(pieces, mask);
                piecesByMask[i] = pieces.ToArray();
            }

            return piecesByMask;
        }

        private static void AddCanonicalBridgePieces(List<PipeRenderPiece> pieces, PipeDirectionMask mask)
        {
            for (int i = 0; i < CanonicalBridgeDirections.Length; i++)
            {
                PipeDirectionMask direction = CanonicalBridgeDirections[i];
                if ((mask & direction) == 0 || !TryGetBridgePiece(direction, out PipeRenderPiece bridgePiece))
                {
                    continue;
                }

                pieces.Add(bridgePiece);
            }
        }

        private static Rect GetPieceUvRect(PipePieceKind kind)
        {
            switch (kind)
            {
                case PipePieceKind.NorthArm:
                    return GetNorthArmUvRect();
                case PipePieceKind.EastArm:
                    return GetEastArmUvRect();
                case PipePieceKind.SouthArm:
                    return GetSouthArmUvRect();
                case PipePieceKind.WestArm:
                    return GetWestArmUvRect();
                case PipePieceKind.NorthBridge:
                    return GetNorthBridgeUvRect();
                case PipePieceKind.EastBridge:
                    return GetEastBridgeUvRect();
                case PipePieceKind.SouthBridge:
                    return GetSouthBridgeUvRect();
                case PipePieceKind.WestBridge:
                    return GetWestBridgeUvRect();
                default:
                    return GetHubUvRect();
            }
        }

        private static Rect GetHubUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(HubSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildCenteredSubRect(cellRect, HubUvFraction, HubUvFraction);
        }

        private static Rect GetNorthArmUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(VerticalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildVerticalArmSubRect(cellRect, true);
        }

        private static Rect GetSouthArmUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(VerticalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildVerticalArmSubRect(cellRect, false);
        }

        private static Rect GetEastArmUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(HorizontalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildHorizontalArmSubRect(cellRect, true);
        }

        private static Rect GetWestArmUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(HorizontalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildHorizontalArmSubRect(cellRect, false);
        }

        private static Rect GetNorthBridgeUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(VerticalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildCenteredSubRect(cellRect, StrokeUvFraction, BridgeUvFraction);
        }

        private static Rect GetSouthBridgeUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(VerticalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildCenteredSubRect(cellRect, StrokeUvFraction, BridgeUvFraction);
        }

        private static Rect GetEastBridgeUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(HorizontalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildCenteredSubRect(cellRect, BridgeUvFraction, StrokeUvFraction);
        }

        private static Rect GetWestBridgeUvRect()
        {
            Rect cellRect = PipeAtlasUtility.GetAtlasUvRect(HorizontalSourceAtlasIndex, PipeAtlasUtility.UsesFlippedRows);
            return BuildCenteredSubRect(cellRect, BridgeUvFraction, StrokeUvFraction);
        }

        private static Rect BuildCenteredSubRect(Rect cellRect, float widthFraction, float heightFraction)
        {
            float width = cellRect.width * widthFraction;
            float height = cellRect.height * heightFraction;
            float x = cellRect.xMin + ((cellRect.width - width) * 0.5f);
            float y = cellRect.yMin + ((cellRect.height - height) * 0.5f);
            return new Rect(x, y, width, height);
        }

        private static Rect BuildVerticalArmSubRect(Rect cellRect, bool upperHalf)
        {
            float width = cellRect.width * StrokeUvFraction;
            float height = cellRect.height * (0.5f + JoinUvOverlapFraction);
            float x = cellRect.xMin + ((cellRect.width - width) * 0.5f);
            float y = upperHalf
                ? cellRect.yMin + ((0.5f - JoinUvOverlapFraction) * cellRect.height)
                : cellRect.yMin;
            return new Rect(x, y, width, height);
        }

        private static Rect BuildHorizontalArmSubRect(Rect cellRect, bool rightHalf)
        {
            float width = cellRect.width * (0.5f + JoinUvOverlapFraction);
            float height = cellRect.height * StrokeUvFraction;
            float x = rightHalf
                ? cellRect.xMin + ((0.5f - JoinUvOverlapFraction) * cellRect.width)
                : cellRect.xMin;
            float y = cellRect.yMin + ((cellRect.height - height) * 0.5f);
            return new Rect(x, y, width, height);
        }
    }
}
