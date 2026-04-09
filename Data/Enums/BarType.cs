namespace op.io
{
    public enum BarType
    {
        Health,
        Shield,
        XP,
        HealthRegen,   // countdown bar: fills from 0→1 over HealthRegenDelay seconds after last damage
        ShieldRegen    // countdown bar: fills from 0→1 over ShieldRegenDelay seconds after last damage
    }
}
