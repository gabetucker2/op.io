-- InitDB_StartData_FX.sql
-- Visual effects: hit flash, despawn, and damage number animations.

---------------------------------------------------------------------------------------------------------------------------
-- Animation timings — stored as "fadeIn|hold|fadeOut" in seconds

INSERT INTO FXSettings (SettingKey, Value) VALUES ('HitFlashAnim',  '0.05|0.0|0.2');  -- hit-flash:  fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DespawnAnim',   '0.0|0.0|0.2');   -- despawn:    fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumAnim', '0.12|0.8|0.8');  -- damage num: fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DeathFadeAnim',  '0.0|0.0|0.5');   -- GO death:   fadeIn | hold | fadeOut

---------------------------------------------------------------------------------------------------------------------------
-- Damage number behavior

INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumDriftSpeed',        '28');   -- upward drift speed (px/s)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumDriftSpread',       '22');   -- horizontal random spread (px/s)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumScaleStart',        '0.5');  -- initial scale at spawn
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumScalePeak',         '1.4');  -- peak scale at end of fade-in
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumSpawnCooldown',     '0.15'); -- min time between damage numbers per object (seconds)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumLifetimeExtension', '0.15'); -- lifetime added per stacked hit (seconds)
