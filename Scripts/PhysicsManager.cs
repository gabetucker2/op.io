using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace op_io
{
    public class PhysicsManager
    {
        public void ResolveCollisions(List<FarmManager.FarmShape> shapes, Player player, bool destroyOnCollision)
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

        public void ApplyPlayerInput(Player player, Vector2 input, float deltaTime)
        {
            // Normalize input vector
            if (input.LengthSquared() > 1)
                input.Normalize();

            // Calculate force based on input and player speed
            Vector2 force = input * player.Speed * deltaTime;

            // Apply force to player position
            player.Position += force;
        }

        private void ApplyForces(FarmManager.FarmShape a, FarmManager.FarmShape b)
        {
            Vector2 direction = b.Position - a.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (a.Weight + b.Weight) / distance;

            a.Position -= direction * force * (b.Weight / (a.Weight + b.Weight));
            b.Position += direction * force * (a.Weight / (a.Weight + b.Weight));
        }

        private void ApplyForces(Player player, FarmManager.FarmShape shape)
        {
            Vector2 direction = shape.Position - player.Position;
            float distance = direction.Length();
            if (distance == 0) return;

            direction.Normalize();
            float force = (player.Weight + shape.Weight) / distance;

            player.Position -= direction * force * (shape.Weight / (player.Weight + shape.Weight));
            shape.Position += direction * force * (player.Weight / (player.Weight + shape.Weight));
        }

        private bool CheckCollision(Vector2 posA, float radiusA, Vector2 posB, float radiusB)
        {
            float distance = Vector2.Distance(posA, posB);
            return distance < radiusA + radiusB;
        }
    }
}
