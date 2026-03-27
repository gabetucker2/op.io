namespace op.io
{
    public readonly struct Player
    {
        public Player(long playerId)
        {
            PlayerID = playerId;
        }

        public long PlayerID { get; }
    }
}
