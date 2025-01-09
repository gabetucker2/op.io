using Microsoft.Xna.Framework;

namespace op.io.Scripts
{
    public static class ActionHandler
    {
        public static void Move(GameObject gameObject, Vector2 direction, float speed, float deltaTime)
        {
            gameObject.Position += direction * speed * deltaTime;
        }
    }
}
