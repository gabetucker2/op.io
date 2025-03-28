using Microsoft.Xna.Framework;

namespace op.io
{
    public static class PhysicsResolver
    {
        public static void HandleCollision(GameObject objA, GameObject objB)
        {
            Vector2 direction = objB.Position - objA.Position;
            float distance = direction.Length();

            if (distance == 0)
                return;

            direction.Normalize();

            float totalMass = objA.Mass + objB.Mass;
            float force = (objA.Mass * objB.Mass) / distance;

            objA.Position -= direction * force * (objB.Mass / totalMass);
            objB.Position += direction * force * (objA.Mass / totalMass);
        }
    }
}
