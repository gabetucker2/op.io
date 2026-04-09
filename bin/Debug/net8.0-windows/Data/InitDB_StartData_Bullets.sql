-- InitDB_StartData_Bullets.sql
-- Bullet physics simulation parameters and default bullet attributes.

---------------------------------------------------------------------------------------------------------------------------
-- BulletPhysics — simulation constants that govern how all bullets behave in the physics engine.

INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('AirResistanceScalar',   '2.12'); -- drag scalar: drag = AirResistanceScalar * bulletVolume / bulletDragFactor
INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('BounceVelocityLoss',    '0.2');  -- fraction of bullet speed lost per bounce off static objects (scaled by object mass ratio)
INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('HitVelocityLoss',       '0.2');  -- fraction of bullet speed lost per hit through non-static objects (scaled by object mass ratio)
INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('PenetrationSpringCoeff','500');  -- spring stiffness: higher = shallower embed; needs >= mv²/R² to stop bullet in-object
INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('PenetrationDamping',    '10');   -- damping: 0 = elastic bounce; ~89 = critical (no overshoot); higher = inelastic/sticky
INSERT INTO BulletPhysics (SettingKey, Value) VALUES ('BulletRadiusScalar',    '6');    -- bullet radius scalar: radius = sqrt(mass) * BulletRadiusScalar

---------------------------------------------------------------------------------------------------------------------------
-- BulletDefaults — fallback bullet attributes used when a barrel does not explicitly set them.

INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletSpeed',       '400'); -- fallback bullet speed (px/s)
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletLifespan',    '3');   -- fallback bullet lifespan (seconds)
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletDragFactor',  '800'); -- fallback bullet drag factor
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletMass',        '0.5');   -- fallback bullet mass
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletDamage',      '10');  -- fallback bullet damage
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletHealth',      '10');  -- fallback bullet penetration HP
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletPenetration', '0');   -- fallback bullet penetration (armor bypass; 0 = no bypass)

-- Default bullet fill color
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletFillR',      '255');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletFillG',      '0');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletFillB',      '0');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletFillA',      '255');

-- Default bullet outline color
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletOutlineR',   '139');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletOutlineG',   '0');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletOutlineB',   '0');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletOutlineA',   '255');
INSERT INTO BulletDefaults (SettingKey, Value) VALUES ('DefaultBulletOutlineWidth','2');
