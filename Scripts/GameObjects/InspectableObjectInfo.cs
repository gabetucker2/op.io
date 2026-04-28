using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace op.io
{
    public readonly struct TerrainInspectionSnapshot
    {
        public const int SharedTerrainId = -9001;

        public TerrainInspectionSnapshot(
            Vector2 hoverWorldPosition,
            bool isBoundary,
            bool isLand,
            float fieldValue,
            Color terrainColor,
            int seed,
            int residentChunkCount,
            int residentComponentCount,
            int residentEdgeLoopCount,
            int residentColliderCount,
            int activeColliderCount,
            int visualTriangleCount,
            int pendingChunkCount,
            int colliderActivationCandidateCount,
            bool chunkBuildsInFlight,
            bool materializationInFlight,
            bool materializationRestartPending,
            double lastMaterializationMilliseconds,
            float chunkWorldSize,
            float featureWorldScaleMultiplier,
            float preloadMarginWorldUnits,
            float octogonalCornerCutCellRatio,
            string worldBounds,
            string seedAnchor,
            string centerChunk,
            string visibleChunkWindow,
            string colliderChunkWindow)
        {
            HoverWorldPosition = hoverWorldPosition;
            IsBoundary = isBoundary;
            IsLand = isLand;
            FieldValue = fieldValue;
            TerrainColor = terrainColor;
            Seed = seed;
            ResidentChunkCount = residentChunkCount;
            ResidentComponentCount = residentComponentCount;
            ResidentEdgeLoopCount = residentEdgeLoopCount;
            ResidentColliderCount = residentColliderCount;
            ActiveColliderCount = activeColliderCount;
            VisualTriangleCount = visualTriangleCount;
            PendingChunkCount = pendingChunkCount;
            ColliderActivationCandidateCount = colliderActivationCandidateCount;
            ChunkBuildsInFlight = chunkBuildsInFlight;
            MaterializationInFlight = materializationInFlight;
            MaterializationRestartPending = materializationRestartPending;
            LastMaterializationMilliseconds = lastMaterializationMilliseconds;
            ChunkWorldSize = chunkWorldSize;
            FeatureWorldScaleMultiplier = featureWorldScaleMultiplier;
            PreloadMarginWorldUnits = preloadMarginWorldUnits;
            OctogonalCornerCutCellRatio = octogonalCornerCutCellRatio;
            WorldBounds = worldBounds ?? string.Empty;
            SeedAnchor = seedAnchor ?? string.Empty;
            CenterChunk = centerChunk ?? string.Empty;
            VisibleChunkWindow = visibleChunkWindow ?? string.Empty;
            ColliderChunkWindow = colliderChunkWindow ?? string.Empty;
        }

        public Vector2 HoverWorldPosition { get; }
        public bool IsBoundary { get; }
        public bool IsLand { get; }
        public float FieldValue { get; }
        public Color TerrainColor { get; }
        public int Seed { get; }
        public int ResidentChunkCount { get; }
        public int ResidentComponentCount { get; }
        public int ResidentEdgeLoopCount { get; }
        public int ResidentColliderCount { get; }
        public int ActiveColliderCount { get; }
        public int VisualTriangleCount { get; }
        public int PendingChunkCount { get; }
        public int ColliderActivationCandidateCount { get; }
        public bool ChunkBuildsInFlight { get; }
        public bool MaterializationInFlight { get; }
        public bool MaterializationRestartPending { get; }
        public double LastMaterializationMilliseconds { get; }
        public float ChunkWorldSize { get; }
        public float FeatureWorldScaleMultiplier { get; }
        public float PreloadMarginWorldUnits { get; }
        public float OctogonalCornerCutCellRatio { get; }
        public string WorldBounds { get; }
        public string SeedAnchor { get; }
        public string CenterChunk { get; }
        public string VisibleChunkWindow { get; }
        public string ColliderChunkWindow { get; }
    }

    public sealed class InspectableObjectInfo
    {
        public InspectableObjectInfo(GameObject source)
        {
            Source = source;
            Refresh();
        }

        private InspectableObjectInfo(TerrainInspectionSnapshot terrain)
        {
            IsTerrain = true;
            Terrain = terrain;
            Position = terrain.HoverWorldPosition;
            Refresh();
        }

        public GameObject Source { get; }
        public bool IsTerrain { get; private set; }
        public TerrainInspectionSnapshot Terrain { get; private set; }
        public int Id { get; private set; }
        public string Name { get; private set; }
        public bool IsPrototype { get; private set; }
        public Vector2 Position { get; private set; }
        public Vector2 ParentPosition { get; private set; }
        public Vector2 ObjectPosition { get; private set; }
        public float Rotation { get; private set; }
        public float Mass { get; private set; }
        public bool IsCollidable { get; private set; }
        public bool IsDestructible { get; private set; }
        public bool DynamicPhysics { get; private set; }
        public bool IsPlayer { get; private set; }
        public bool IsInteract { get; private set; }
        public bool IsZoneBlock { get; private set; }
        public int DrawLayer { get; private set; }
        public Shape Shape { get; private set; }
        public Color FillColor { get; private set; }
        public Color OutlineColor { get; private set; }
        public int OutlineWidth { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Sides { get; private set; }
        public float CurrentXP           { get; private set; }
        public float MaxXP               { get; private set; }
        public float DeathPointReward    { get; private set; }
        public float CurrentHealth       { get; private set; }
        public float MaxHealth           { get; private set; }
        public float CurrentShield       { get; private set; }
        public float MaxShield           { get; private set; }
        public float LastHealthDamageTime { get; private set; }
        public float LastShieldDamageTime { get; private set; }
        public bool IsValid { get; private set; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"ID {Id}" : Name;

        public static InspectableObjectInfo CreateTerrain(TerrainInspectionSnapshot terrain)
        {
            return new InspectableObjectInfo(terrain);
        }

        public void Refresh()
        {
            if (IsTerrain)
            {
                if (!GameBlockTerrainBackground.TryBuildTerrainInspectionSnapshot(
                    Position,
                    requireTerrainHit: false,
                    out TerrainInspectionSnapshot terrain))
                {
                    IsValid = false;
                    return;
                }

                Terrain = terrain;
                Id = TerrainInspectionSnapshot.SharedTerrainId;
                Name = "Terrain";
                IsPrototype = false;
                Position = terrain.HoverWorldPosition;
                ParentPosition = Vector2.Zero;
                ObjectPosition = Position;
                Rotation = 0f;
                Mass = 0f;
                IsCollidable = true;
                IsDestructible = false;
                DynamicPhysics = false;
                IsInteract = false;
                IsZoneBlock = false;
                DrawLayer = GameBlockTerrainBackground.TerrainDrawLayerSetting;
                Shape = null;
                FillColor = terrain.TerrainColor;
                OutlineColor = Color.Transparent;
                OutlineWidth = 0;
                Width = 0;
                Height = 0;
                Sides = 0;
                IsPlayer = false;
                CurrentXP = 0f;
                MaxXP = 0f;
                DeathPointReward = 0f;
                CurrentHealth = 0f;
                MaxHealth = 0f;
                CurrentShield = 0f;
                MaxShield = 0f;
                LastHealthDamageTime = float.NegativeInfinity;
                LastShieldDamageTime = float.NegativeInfinity;
                IsValid = true;
                return;
            }

            if (Source == null || Source.Shape == null)
            {
                IsValid = false;
                return;
            }

            Id = Source.ID;
            Name = Source.Name;
            IsPrototype = Source.IsPrototype;
            Position = Source.Position;
            ParentPosition = Source.ParentPosition;
            ObjectPosition = Source.ObjectPosition;
            Rotation = Source.Rotation;
            Mass = Source.Mass;
            IsCollidable = Source.IsCollidable;
            IsDestructible = Source.IsDestructible;
            DynamicPhysics = Source.DynamicPhysics;
            IsInteract = Source.IsInteract;
            IsZoneBlock = Source.IsZoneBlock;
            DrawLayer = Source.DrawLayer;
            Shape = Source.Shape;
            FillColor = Source.FillColor;
            OutlineColor = Source.OutlineColor;
            OutlineWidth = Source.OutlineWidth;
            Width = Shape.Width;
            Height = Shape.Height;
            Sides = Shape.Sides;

            if (Source is Agent agent)
            {
                IsPlayer = agent.IsPlayer;
            }
            else
            {
                IsPlayer = false;
            }

            CurrentXP            = Source.CurrentXP;
            MaxXP                = Source.MaxXP;
            DeathPointReward     = Source.DeathPointReward;
            CurrentHealth        = Source.CurrentHealth;
            MaxHealth            = Source.MaxHealth;
            CurrentShield        = Source.CurrentShield;
            MaxShield            = Source.MaxShield;
            LastHealthDamageTime = Source.LastHealthDamageTime;
            LastShieldDamageTime = Source.LastShieldDamageTime;

            IsValid = true;
        }

        public InspectableObjectInfo Clone()
        {
            if (IsTerrain)
            {
                return CreateTerrain(Terrain);
            }

            InspectableObjectInfo copy = new(Source);
            copy.Refresh();
            return copy;
        }
    }

    public static class GameObjectInspector
    {
        public static InspectableObjectInfo FindHoveredObject(Vector2 gameCursorPosition)
        {
            List<GameObject> objects = GameObjectRegister.GetRegisteredGameObjects();
            if (objects == null || objects.Count == 0)
            {
                return FindHoveredTerrain(gameCursorPosition);
            }

            InspectableObjectInfo best = null;
            float bestScore = float.MaxValue;

            foreach (GameObject obj in objects)
            {
                if (!IsInspectable(obj))
                {
                    continue;
                }

                if (!IsPointInside(obj, gameCursorPosition, out float score))
                {
                    continue;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = new InspectableObjectInfo(obj);
                }
            }

            return best ?? FindHoveredTerrain(gameCursorPosition);
        }

        public static bool IsInspectableObject(GameObject gameObject)
        {
            return IsInspectable(gameObject);
        }

        public static bool IsInspectableTarget(InspectableObjectInfo target)
        {
            if (target == null)
            {
                return false;
            }

            return target.IsValid && (target.IsTerrain || IsInspectable(target.Source));
        }

        private static InspectableObjectInfo FindHoveredTerrain(Vector2 gameCursorPosition)
        {
            return GameBlockTerrainBackground.TryBuildTerrainInspectionSnapshot(
                gameCursorPosition,
                requireTerrainHit: true,
                out TerrainInspectionSnapshot terrain)
                ? InspectableObjectInfo.CreateTerrain(terrain)
                : null;
        }

        private static bool IsInspectable(GameObject gameObject)
        {
            return gameObject != null &&
                gameObject.Shape != null &&
                !gameObject.Shape.IsPrototype &&
                !gameObject.Shape.SkipHover &&
                FogOfWarManager.IsWorldPositionVisible(
                    gameObject.Position,
                    MathF.Max(gameObject.BoundingRadius, 2f));
        }

        private static bool IsPointInside(GameObject gameObject, Vector2 point, out float score)
        {
            score = float.MaxValue;

            Shape shape = gameObject.Shape;
            if (shape == null)
            {
                return false;
            }

            Vector2 delta = point - gameObject.Position;
            float radius = gameObject.BoundingRadius + shape.OutlineWidth + 2f;
            float radiusSq = radius * radius;
            float distanceSq = delta.LengthSquared();

            if (distanceSq > radiusSq)
            {
                return false;
            }

            bool inside = shape.ShapeType switch
            {
                "Rectangle" => PointInsideRectangle(delta, shape, gameObject.Rotation),
                "Circle" => PointInsideEllipse(delta, shape),
                "Polygon" => PointInsidePolygon(point, shape, gameObject),
                _ => PointInsideRectangle(delta, shape, gameObject.Rotation)
            };

            if (inside)
            {
                score = distanceSq;
            }

            return inside;
        }

        private static bool PointInsideRectangle(Vector2 localPoint, Shape shape, float rotation)
        {
            float cos = MathF.Cos(-rotation);
            float sin = MathF.Sin(-rotation);

            float x = localPoint.X * cos - localPoint.Y * sin;
            float y = localPoint.X * sin + localPoint.Y * cos;

            float halfWidth = (shape.Width / 2f) + shape.OutlineWidth;
            float halfHeight = (shape.Height / 2f) + shape.OutlineWidth;

            return MathF.Abs(x) <= halfWidth && MathF.Abs(y) <= halfHeight;
        }

        private static bool PointInsideEllipse(Vector2 localPoint, Shape shape)
        {
            float rx = (shape.Width / 2f) + shape.OutlineWidth;
            float ry = (shape.Height / 2f) + shape.OutlineWidth;

            if (rx <= 0 || ry <= 0)
            {
                return false;
            }

            float normalized = (localPoint.X * localPoint.X) / (rx * rx) +
                (localPoint.Y * localPoint.Y) / (ry * ry);

            return normalized <= 1.0f;
        }

        private static bool PointInsidePolygon(Vector2 point, Shape shape, GameObject gameObject)
        {
            Vector2[] vertices = shape.GetTransformedVertices(gameObject.Position, gameObject.Rotation);
            if (vertices == null || vertices.Length == 0)
            {
                return false;
            }

            bool inside = false;
            int count = vertices.Length;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 vi = vertices[i];
                Vector2 vj = vertices[j];

                bool intersects = ((vi.Y > point.Y) != (vj.Y > point.Y)) &&
                    (point.X < (vj.X - vi.X) * (point.Y - vi.Y) / (vj.Y - vi.Y + 0.0001f) + vi.X);

                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
