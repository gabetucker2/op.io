using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class SATCollisionUtil
    {
        private const float Epsilon = 1e-5f;

        public static bool TryGetCollision(Vector2[] polyA, Vector2[] polyB, out Vector2 minimumTranslationVector)
        {
            minimumTranslationVector = Vector2.Zero;

            float smallestOverlap = float.MaxValue;
            Vector2 smallestAxis = Vector2.Zero;

            if (polyA == null || polyB == null || polyA.Length == 0 || polyB.Length == 0)
            {
                return false;
            }

            Vector2 centerA = ComputeCentroid(polyA);
            Vector2 centerB = ComputeCentroid(polyB);

            if (!EvaluateAxes(polyA, polyB, ref smallestOverlap, ref smallestAxis))
            {
                return false;
            }

            if (!EvaluateAxes(polyB, polyA, ref smallestOverlap, ref smallestAxis))
            {
                return false;
            }

            if (smallestAxis == Vector2.Zero || smallestOverlap == float.MaxValue)
            {
                return false;
            }

            Vector2 direction = centerB - centerA;
            if (Vector2.Dot(direction, smallestAxis) < 0f)
            {
                smallestAxis = -smallestAxis;
            }

            minimumTranslationVector = smallestAxis * smallestOverlap;
            return true;
        }

        private static bool EvaluateAxes(Vector2[] verticesA, Vector2[] verticesB, ref float smallestOverlap, ref Vector2 smallestAxis)
        {
            int countA = verticesA.Length;

            for (int i = 0; i < countA; i++)
            {
                Vector2 edge = verticesA[(i + 1) % countA] - verticesA[i];
                Vector2 axis = new Vector2(-edge.Y, edge.X);
                float lengthSq = axis.LengthSquared();
                if (lengthSq < Epsilon)
                {
                    continue;
                }

                axis /= MathF.Sqrt(lengthSq);

                ProjectVertices(axis, verticesA, out float minA, out float maxA);
                ProjectVertices(axis, verticesB, out float minB, out float maxB);

                float overlap = Math.Min(maxA, maxB) - Math.Max(minA, minB);
                if (overlap <= 0f)
                {
                    return false; // Separating axis found
                }

                if (overlap < smallestOverlap)
                {
                    smallestOverlap = overlap;
                    smallestAxis = axis;
                }
            }

            return true; // No separating axis found
        }

        private static void ProjectVertices(Vector2 axis, Vector2[] vertices, out float min, out float max)
        {
            min = max = Vector2.Dot(axis, vertices[0]);

            for (int i = 1; i < vertices.Length; i++)
            {
                float projection = Vector2.Dot(axis, vertices[i]);
                if (projection < min) min = projection;
                if (projection > max) max = projection;
            }
        }

        private static Vector2 ComputeCentroid(Vector2[] vertices)
        {
            Vector2 sum = Vector2.Zero;
            for (int i = 0; i < vertices.Length; i++)
            {
                sum += vertices[i];
            }

            return sum / Math.Max(1, vertices.Length);
        }
    }
}
