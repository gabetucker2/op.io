-- InitDB_StartData_Settings.sql
-- Debug and general game settings.
-- Controls  → InitDB_StartData_Controls.sql
-- Bullets   → InitDB_StartData_Bullets.sql
-- Bar UI    → InitDB_StartData_Bars.sql
-- FX / anims→ InitDB_StartData_FX.sql

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_R', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_G', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_B', '0');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleColor_A', '255');
INSERT INTO DebugVisuals (SettingKey, Value) VALUES ('DebugCircleRadius', '3');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('BarsVisible', 'true');
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
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('CameraSnapRange', '150');

INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('KnockbackMassScale', '4.0');
INSERT INTO PhysicsSettings (SettingKey, Value) VALUES ('RecoilMassScale', '50.0');


