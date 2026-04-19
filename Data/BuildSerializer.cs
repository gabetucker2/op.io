using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace op.io
{
    /// <summary>
    /// JSON-serializable snapshot of a unit's full loadout: every barrel's
    /// attributes plus the body and unit stat blocks.  Designed to be packed
    /// and unpacked so players can save, share, and restore builds.
    ///
    /// Example round-trip:
    ///     string json = BuildSerializer.ToJson(BuildSerializer.Snapshot(player));
    ///     BuildSerializer.Apply(player, BuildSerializer.FromJson(json));
    /// </summary>
    public sealed class UnitBuild
    {
        /// <summary>Ordered list of barrel attribute sets (index 0 = first active barrel).</summary>
        [JsonPropertyName("barrels")]
        public List<BarrelBuildData> Barrels { get; set; } = [];

        /// <summary>Body stat block (health, speed, armor, etc.). Legacy single-body field.</summary>
        [JsonPropertyName("body")]
        public BodyBuildData Body { get; set; } = new();

        /// <summary>Ordered list of body attribute sets (index 0 = first active body).</summary>
        [JsonPropertyName("bodies")]
        public List<BodyBuildData> Bodies { get; set; } = [];

        /// <summary>Unit-level metadata (name, rewards, switch speeds, etc.).</summary>
        [JsonPropertyName("unit")]
        public UnitBuildData Unit { get; set; } = new();
    }

    /// <summary>JSON-serializable mirror of <see cref="Attributes_Barrel"/>.</summary>
    public sealed class BarrelBuildData
    {
        [JsonPropertyName("name")]             public string Name             { get; set; }
        [JsonPropertyName("bulletDamage")]      public float BulletDamage      { get; set; }
        [JsonPropertyName("bulletPenetration")] public float BulletPenetration { get; set; }
        [JsonPropertyName("bulletSpeed")]       public float BulletSpeed       { get; set; }
        [JsonPropertyName("reloadSpeed")]       public float ReloadSpeed       { get; set; }
        [JsonPropertyName("bulletMaxLifespan")] public float BulletMaxLifespan { get; set; }
        [JsonPropertyName("bulletMass")]        public float BulletMass        { get; set; }
        [JsonPropertyName("bulletHealth")]      public float BulletHealth      { get; set; }
        [JsonPropertyName("bulletFillR")]       public byte  BulletFillR       { get; set; }
        [JsonPropertyName("bulletFillG")]       public byte  BulletFillG       { get; set; }
        [JsonPropertyName("bulletFillB")]       public byte  BulletFillB       { get; set; }
        [JsonPropertyName("bulletFillA")]       public byte  BulletFillA       { get; set; }
        [JsonPropertyName("bulletOutlineR")]    public byte  BulletOutlineR    { get; set; }
        [JsonPropertyName("bulletOutlineG")]    public byte  BulletOutlineG    { get; set; }
        [JsonPropertyName("bulletOutlineB")]    public byte  BulletOutlineB    { get; set; }
        [JsonPropertyName("bulletOutlineA")]    public byte  BulletOutlineA    { get; set; }
        [JsonPropertyName("bulletOutlineWidth")]public int   BulletOutlineWidth { get; set; }
        // Bullet effectors (only non-hidden ones serialized)
        [JsonPropertyName("bulletControl")]                  public float BulletControl                  { get; set; }

        public Attributes_Barrel ToAttributes() => new()
        {
            BulletDamage      = BulletDamage,
            BulletPenetration = BulletPenetration,
            BulletSpeed       = BulletSpeed,
            ReloadSpeed       = ReloadSpeed,
            BulletMaxLifespan = BulletMaxLifespan,
            BulletMass        = BulletMass,
            BulletHealth      = BulletHealth,
            BulletControl                  = BulletControl,
            BulletFillColor    = new Color(BulletFillR, BulletFillG, BulletFillB, BulletFillA),
            BulletOutlineColor = new Color(BulletOutlineR, BulletOutlineG, BulletOutlineB, BulletOutlineA),
            BulletOutlineWidth = BulletOutlineWidth,
        };

        public static BarrelBuildData FromAttributes(Attributes_Barrel a, string name = null) => new()
        {
            Name              = name,
            BulletDamage      = a.BulletDamage,
            BulletPenetration = a.BulletPenetration,
            BulletSpeed       = a.BulletSpeed,
            ReloadSpeed       = a.ReloadSpeed,
            BulletMaxLifespan = a.BulletMaxLifespan,
            BulletMass        = a.BulletMass,
            BulletHealth      = a.BulletHealth,
            BulletControl                  = a.BulletControl,
            BulletFillR       = a.BulletFillColor.R,
            BulletFillG       = a.BulletFillColor.G,
            BulletFillB       = a.BulletFillColor.B,
            BulletFillA       = a.BulletFillColor.A,
            BulletOutlineR    = a.BulletOutlineColor.R,
            BulletOutlineG    = a.BulletOutlineColor.G,
            BulletOutlineB    = a.BulletOutlineColor.B,
            BulletOutlineA    = a.BulletOutlineColor.A,
            BulletOutlineWidth = a.BulletOutlineWidth,
        };
    }

    /// <summary>JSON-serializable mirror of <see cref="Attributes_Body"/>.</summary>
    public sealed class BodyBuildData
    {
        [JsonPropertyName("name")]                     public string Name                    { get; set; }
        [JsonPropertyName("mass")]                     public float Mass                     { get; set; }
        [JsonPropertyName("healthRegen")]              public float HealthRegen              { get; set; }
        [JsonPropertyName("healthRegenDelay")]         public float HealthRegenDelay         { get; set; }
        [JsonPropertyName("healthArmor")]              public float HealthArmor              { get; set; }
        [JsonPropertyName("maxShield")]                public float MaxShield                { get; set; }
        [JsonPropertyName("shieldRegen")]              public float ShieldRegen              { get; set; }
        [JsonPropertyName("shieldRegenDelay")]         public float ShieldRegenDelay         { get; set; }
        [JsonPropertyName("shieldArmor")]              public float ShieldArmor              { get; set; }
        [JsonPropertyName("bodyCollisionDamage")]      public float BodyCollisionDamage      { get; set; }
        [JsonPropertyName("bodyPenetration")]          public float BodyPenetration          { get; set; }
        [JsonPropertyName("collisionDamageResistance")]public float CollisionDamageResistance{ get; set; }
        [JsonPropertyName("bulletDamageResistance")]   public float BulletDamageResistance   { get; set; }
        [JsonPropertyName("speed")]                    public float Speed                    { get; set; }
        [JsonPropertyName("control")]                  public float Control                  { get; set; }
        [JsonPropertyName("sight")]                    public float Sight                    { get; set; }
        [JsonPropertyName("bodyActionBuff")]           public float BodyActionBuff           { get; set; }

        public Attributes_Body ToAttributes() => new()
        {
            Mass                      = Mass,
            HealthRegen               = HealthRegen,
            HealthRegenDelay          = HealthRegenDelay,
            HealthArmor               = HealthArmor,
            MaxShield                 = MaxShield,
            ShieldRegen               = ShieldRegen,
            ShieldRegenDelay          = ShieldRegenDelay,
            ShieldArmor               = ShieldArmor,
            BodyCollisionDamage       = BodyCollisionDamage,
            BodyPenetration           = BodyPenetration,
            CollisionDamageResistance = CollisionDamageResistance,
            BulletDamageResistance    = BulletDamageResistance,
            Speed                     = Speed,
            Control                   = Control,
            Sight                     = Sight,
            BodyActionBuff            = BodyActionBuff,
        };

        public static BodyBuildData FromAttributes(Attributes_Body a, string name = null) => new()
        {
            Name                      = name,
            Mass                      = a.Mass,
            HealthRegen               = a.HealthRegen,
            HealthRegenDelay          = a.HealthRegenDelay,
            HealthArmor               = a.HealthArmor,
            MaxShield                 = a.MaxShield,
            ShieldRegen               = a.ShieldRegen,
            ShieldRegenDelay          = a.ShieldRegenDelay,
            ShieldArmor               = a.ShieldArmor,
            BodyCollisionDamage       = a.BodyCollisionDamage,
            BodyPenetration           = a.BodyPenetration,
            CollisionDamageResistance = a.CollisionDamageResistance,
            BulletDamageResistance    = a.BulletDamageResistance,
            Speed                     = a.Speed,
            Control                   = a.Control,
            Sight                     = a.Sight,
            BodyActionBuff            = a.BodyActionBuff,
        };
    }

    /// <summary>JSON-serializable mirror of <see cref="Attributes_Unit"/>.</summary>
    public sealed class UnitBuildData
    {
        [JsonPropertyName("name")]             public string Name            { get; set; } = string.Empty;
        [JsonPropertyName("deathPointReward")] public float  DeathPointReward{ get; set; }
        [JsonPropertyName("bodySwitchSpeed")]  public float  BodySwitchSpeed { get; set; }
        [JsonPropertyName("barrelSwitchSpeed")]public float  BarrelSwitchSpeed{ get; set; }

        public Attributes_Unit ToAttributes() => new()
        {
            Name             = Name,
            DeathPointReward = DeathPointReward,
            BodySwitchSpeed  = BodySwitchSpeed,
            BarrelSwitchSpeed= BarrelSwitchSpeed,
        };

        public static UnitBuildData FromAttributes(Attributes_Unit a) => new()
        {
            Name             = a.Name ?? string.Empty,
            DeathPointReward = a.DeathPointReward,
            BodySwitchSpeed  = a.BodySwitchSpeed,
            BarrelSwitchSpeed= a.BarrelSwitchSpeed,
        };
    }

    /// <summary>
    /// Packs and unpacks <see cref="UnitBuild"/> to/from JSON, and provides
    /// helpers for snapshotting an <see cref="Agent"/> and applying a build back.
    /// </summary>
    public static class BuildSerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        // ── Encode / decode ──────────────────────────────────────────────────────

        /// <summary>Serializes a <see cref="UnitBuild"/> to an indented JSON string.</summary>
        public static string ToJson(UnitBuild build)
            => JsonSerializer.Serialize(build, _options);

        /// <summary>Deserializes a JSON string produced by <see cref="ToJson"/>.</summary>
        public static UnitBuild FromJson(string json)
            => JsonSerializer.Deserialize<UnitBuild>(json, _options) ?? new UnitBuild();

        // ── Agent helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Captures the agent's current barrel list plus body and unit attributes
        /// into a <see cref="UnitBuild"/> that can be serialized.
        /// </summary>
        public static UnitBuild Snapshot(Agent agent)
        {
            Attributes_Unit unitAttrs = agent.UnitAttributes;
            unitAttrs.Name = agent.Name; // always snapshot from the authoritative field
            var build = new UnitBuild
            {
                Body = BodyBuildData.FromAttributes(agent.BodyAttributes),
                Unit = UnitBuildData.FromAttributes(unitAttrs),
            };
            foreach (var slot in agent.Barrels)
                build.Barrels.Add(BarrelBuildData.FromAttributes(slot.Attrs, slot.Name));
            foreach (var slot in agent.Bodies)
                build.Bodies.Add(BodyBuildData.FromAttributes(slot.Attrs, slot.Name));
            return build;
        }

        /// <summary>
        /// Replaces the agent's barrel loadout and stat blocks with those stored
        /// in <paramref name="build"/>.  Barrel shapes are loaded immediately if
        /// the graphics device is available (safe to call at runtime).
        /// </summary>
        public static void Apply(Agent agent, UnitBuild build)
        {
            agent.ClearBarrels();
            for (int i = 0; i < build.Barrels.Count; i++)
            {
                var barrelData = build.Barrels[i];
                agent.AddBarrel(barrelData.ToAttributes());
                if (!string.IsNullOrEmpty(barrelData.Name))
                    agent.Barrels[i].Name = barrelData.Name;
            }

            // Restore bodies — prefer the Bodies list; fall back to legacy single Body field.
            if (build.Bodies != null && build.Bodies.Count > 0)
            {
                agent.ClearBodies();
                for (int i = 0; i < build.Bodies.Count; i++)
                {
                    var bodyData = build.Bodies[i];
                    agent.AddBody(bodyData.ToAttributes());
                    if (!string.IsNullOrEmpty(bodyData.Name))
                        agent.Bodies[i].Name = bodyData.Name;
                }
            }
            else
            {
                agent.BodyAttributes = build.Body.ToAttributes();
            }

            agent.UnitAttributes   = build.Unit.ToAttributes(); // setter syncs Name → GameObject.Name
            agent.DeathPointReward = agent.UnitAttributes.DeathPointReward;
            if (!string.IsNullOrWhiteSpace(build.Unit.Name))
                agent.Name = build.Unit.Name;
        }
    }
}
