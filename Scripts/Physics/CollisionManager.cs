using Microsoft.Xna.Framework;

namespace op.io
{
    public static class CollisionManager
    {
        public static bool CheckCollision(GameObject objA, GameObject objB)
        {
            return TryGetCollision(objA, objB, out _);
        }

        public static bool TryGetCollision(GameObject objA, GameObject objB, out Vector2 minimumTranslationVector)
        {
            minimumTranslationVector = Vector2.Zero;

            if (objA?.Shape == null || objB?.Shape == null)
            {
                return false;
            }

            Vector2[] verticesA = objA.Shape.GetTransformedVertices(objA.Position, objA.Rotation);
            Vector2[] verticesB = objB.Shape.GetTransformedVertices(objB.Position, objB.Rotation);

            if (verticesA.Length == 0 || verticesB.Length == 0)
            {
                DebugLogger.PrintWarning($"Missing vertices for SAT: ID={objA.ID}, ID={objB.ID}");
                return false;
            }

            return SATCollisionUtil.TryGetCollision(verticesA, verticesB, out minimumTranslationVector);
        }
    }
}
