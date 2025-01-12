using Microsoft.Xna.Framework;
using op_io;
using System;
using System.Collections.Generic;

namespace op.io.Scripts
{
    public class PhysicsManager
    {
        public void ResolveCollisions(List<FarmShape> shapes, Player player, bool destroyOnCollision)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    ApplyForces(shapes[i], shapes[j]);
                }

                if (CheckCollision(player.Position, player.Radius, shapes[i].Position, shapes[i].Size / 2))
                {
                    ApplyForces(player, shapes[i]);

                    if (destroyOnCollision)
                    {
                        shapes.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
        }

        private bool CheckCollision(Vector2 posA, float radiusA, Vector2 posB, float radiusB)
        {
            // Distance between the centers of two objects
            float distance = Vector2.Distance(posA, posB);

            // Check if distance is less than the sum of their radii
            return distance < (radiusA + radiusB);
        }

        private void ApplyForces(FarmShape a, FarmShape b)
        {
            Vector2 direction = b.Position - a.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (a.Weight + b.Weight) / Math.Max(distance, 0.1f); // Avoid division by zero

            a.Position -= direction * force * (b.Weight / (a.Weight + b.Weight));
            b.Position += direction * force * (a.Weight / (a.Weight + b.Weight));
        }

        private void ApplyForces(Player player, FarmShape shape)
        {
            Vector2 direction = shape.Position - player.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (player.Weight + shape.Weight) / Math.Max(distance, 0.1f); // Avoid division by zero

            player.Position -= direction * force * (shape.Weight / (player.Weight + shape.Weight));
            shape.Position += direction * force * (player.Weight / (player.Weight + shape.Weight));
        }
    }
}
