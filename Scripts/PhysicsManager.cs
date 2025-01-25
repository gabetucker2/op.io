using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace op.io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(List<Shape> shapes, Player player, bool destroyOnCollision)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                Shape shape = shapes[i];

                // Check for collision using radius-based or polygon detection
                if (CheckCollision(player, shape))
                {
                    ApplyForces(player, shape);

                    if (destroyOnCollision)
                    {
                        shapes.RemoveAt(i);
                        i--;
                        break;
                    }
                }
            }
        }

        private bool CheckCollision(Player player, Shape shape)
        {
            // Step 1: Check circular collision (player's area vs. shape's bounding circle)
            float distanceSquared = Vector2.DistanceSquared(player.Position, shape.Position);
            float combinedRadius = player.Radius + (shape.Size / 2);
            if (distanceSquared <= combinedRadius * combinedRadius)
            {
                return true; // Circular overlap detected
            }

            // Step 2: Check if the player's center is inside the polygon (if applicable)
            if (shape.Type == "Polygon")
            {
                return shape.IsPointInsidePolygon(player.Position);
            }

            return false;
        }

        private void ApplyForces(Player player, Shape shape)
        {
            // Calculate direction of force
            Vector2 direction = shape.Position - player.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (player.Weight + shape.Weight) / (distance > 0.1f ? distance : 0.1f);

            // Apply forces to the player and shape
            player.Position -= direction * force * (shape.Weight / (player.Weight + shape.Weight));
            shape.Position += direction * force * (player.Weight / (player.Weight + shape.Weight));
        }
    }
}
