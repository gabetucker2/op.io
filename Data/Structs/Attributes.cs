namespace op.io
{
    public struct Attributes_Barrel
    {
        public float BulletDamage { get; set; }
        public float BulletPenetration { get; set; }
        public float BulletSpeed { get; set; }
        public float BulletRange { get; set; }
        public float ReloadSpeed { get; set; }
        public float BulletHealth { get; set; }
        public float BulletMaxLifespan { get; set; }
        public string BulletSpecialBuff { get; set; }
    }

    public struct Attributes_Body
    {
        public float MaxHealth { get; set; }
        public float HealthRegen { get; set; }
        public float HealthArmor { get; set; }

        public float MaxShield { get; set; }
        public float ShieldRegen { get; set; }
        public float ShieldArmor { get; set; }
        
        public float BodyPenetration { get; set; }
        public float BodyCollisionDamage { get; set; }
        public float BodyKnockback { get; set; }
        
        public float CollisionDamageResistance { get; set; }
        public float BulletDamageResistance { get; set; }
        
        public float Speed { get; set; }
        public float RotationSpeed { get; set; }
        public float BodySpecialBuff { get; set; }
    }

    public struct Attributes_Meta
    {
        public float BodySwitchSpeed { get; set; }
        public float BarrelSwitchSpeed { get; set; }
        public float DeathPointReward { get; set; }
    }
}
