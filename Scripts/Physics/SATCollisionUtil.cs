using System;
using Microsoft.Xna.Framework;

namespace op.io
{
    public static class SATCollisionUtil
    {
        public static bool SATCollision(Vector2[] polyA, Vector2[] polyB)
        {
            return !HasSeparatingAxis(polyA, polyB) && !HasSeparatingAxis(polyB, polyA);
        }

        private static bool HasSeparatingAxis(Vector2[] verticesA, Vector2[] verticesB)
        {
            int countA = verticesA.Length;

            for (int i = 0; i < countA; i++)
            {
                Vector2 edge = verticesA[(i + 1) % countA] - verticesA[i];
                Vector2 axis = new Vector2(-edge.Y, edge.X);
                axis.Normalize();

                ProjectVertices(axis, verticesA, out float minA, out float maxA);
                ProjectVertices(axis, verticesB, out float minB, out float maxB);

                if (maxA < minB || maxB < minA)
                    return true; // Separating axis found
            }

            return false; // No separating axis found
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
    }
}