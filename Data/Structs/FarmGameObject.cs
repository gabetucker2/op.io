using Microsoft.Xna.Framework;

namespace op.io
{
    public readonly struct FarmGameObject
    {
        public FarmGameObject(SimpleGameObject baseObject, FarmData farmData)
        {
            BaseObject = baseObject;
            FarmData = farmData;
        }

        public SimpleGameObject BaseObject { get; }
        public FarmData FarmData { get; }
        public GameObjectStructSet StructSet => BaseObject.StructSet | GameObjectStructSet.FarmData;

        public SimpleGameObject CreateInstance(int id, Vector2 position, float rotation)
        {
            return BaseObject.WithTransform(id, position, rotation);
        }

        public GameObject ToGameObject(bool isPrototype = false)
        {
            return BaseObject.ToGameObject(isPrototype);
        }
    }
}
