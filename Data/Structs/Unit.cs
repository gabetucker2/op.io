namespace op.io
{
    public readonly struct Unit
    {
        public Unit(Player player, Barrel barrel)
        {
            Player = player;
            Barrel = barrel;
        }

        public Player Player { get; }
        public Barrel Barrel { get; }
    }
}
