-- InitDB_StartData_Settings.sql

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_R', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_G', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_B', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_A', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleRadius', '3');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_R', '20');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_G', '20');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_B', '25');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BackgroundColor_A', '255');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportWidth', '1200');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportHeight', '1200');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('WindowMode', 'BorderedWindowed');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('VSync', 'false'); -- G-Sync will override this
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('FixedTimeStep', 'false');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('TargetFrameRate', '240');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('NumLogFiles', '5');

---------------------------------------------------------------------------------------------------------------------------

-- ControlKey defaults are seeded via control configuration saves (see ControlConfigurationManager).

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.1');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CursorFollowDeadzonePixels', '10');

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '25');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('AirResistance', '2.12');     -- global scalar: drag = AirResistance * bulletVolume / bulletRange
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('BounceVelocityLoss', '0.2'); -- base fraction of bullet speed lost per bounce (scaled by object mass ratio)
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletSpeed', '400');    -- fallback bullet speed (px/s) when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletLifespan', '3');   -- fallback bullet lifespan (seconds) when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletRange', '800');    -- fallback bullet range (px) when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('DefaultBulletMass', '1');       -- fallback bullet mass when barrel attrs are unset
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('BulletRadius', '6');            -- bullet collision/render radius (px)

---------------------------------------------------------------------------------------------------------------------------
