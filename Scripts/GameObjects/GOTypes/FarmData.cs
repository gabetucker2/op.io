namespace op.io
{
    public class FarmData
    {
        public int ID { get; set; }
        public int Count { get; set; }

        // Body Attributes (Attributes_Body)
        public float MaxHealth                 { get; set; }
        public float HealthRegen               { get; set; }
        public float HealthRegenDelay          { get; set; }
        public float HealthArmor               { get; set; }
        public float MaxShield                 { get; set; }
        public float ShieldRegen               { get; set; }
        public float ShieldRegenDelay          { get; set; }
        public float ShieldArmor               { get; set; }
        public float BodyPenetration           { get; set; }
        public float BodyCollisionDamage       { get; set; }
        public float CollisionDamageResistance { get; set; }
        public float BulletDamageResistance    { get; set; }
        public float Speed                     { get; set; }
        public float RotationSpeed             { get; set; }
        public float DeathPointReward          { get; set; }

        // Manual placement: spawn at a fixed position instead of random generation
        public bool  IsManual  { get; set; }
        public float ManualX   { get; set; }
        public float ManualY   { get; set; }

        // FarmAttributes: back-and-forth sine-wave float animation
        public float FloatAmplitude { get; set; }
        public float FloatSpeed     { get; set; }
    }
}
