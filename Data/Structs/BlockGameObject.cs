namespace op.io
{
    public readonly struct BlockGameObject
    {
        public BlockGameObject(SimpleGameObject baseObject, BlockType blockType)
        {
            BaseObject = baseObject;
            BlockType = blockType;
        }

        public SimpleGameObject BaseObject { get; }
        public BlockType BlockType { get; }
        public GameObjectStructSet StructSet => BaseObject.StructSet | GameObjectStructSet.BlockType;

        public GameObject ToGameObject(bool isPrototype = false)
        {
            return BaseObject.ToGameObject(isPrototype);
        }
    }
}
