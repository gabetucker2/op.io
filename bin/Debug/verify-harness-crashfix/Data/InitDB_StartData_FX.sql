-- InitDB_StartData_FX.sql
-- Visual effects: hit flash, despawn, and damage number animations.

---------------------------------------------------------------------------------------------------------------------------
-- Animation timings — stored as "fadeIn|hold|fadeOut" in seconds

INSERT INTO FXSettings (SettingKey, Value) VALUES ('HitFlashAnim',  '0.05|0.0|0.2');  -- hit-flash:  fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DespawnAnim',   '0.0|0.0|0.2');   -- despawn:    fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumAnim', '0.12|0.8|0.8');  -- damage num: fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DeathFadeAnim',  '0.0|0.0|0.5');   -- GO death:   fadeIn | hold | fadeOut
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DeathFadeScaleMultiplier',     '1.5');   -- final scale multiplier reached at the end of death fade (1.5 = +50%)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DeathFadeSpinMinDegPerSecond', '90');    -- minimum random death spin speed (degrees/second)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DeathFadeSpinMaxDegPerSecond', '240');   -- maximum random death spin speed (degrees/second)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('BodyTransitionDurationSeconds', '0.3');  -- duration of a body-to-body lerp
INSERT INTO FXSettings (SettingKey, Value) VALUES ('BodyTransitionBufferSeconds',   '0.5');  -- extra lockout after the transition completes

---------------------------------------------------------------------------------------------------------------------------
-- Damage number behavior

INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumDriftSpeed',        '28');   -- upward drift speed (px/s)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumDriftSpread',       '22');   -- horizontal random spread (px/s)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumScaleStart',        '0.5');  -- initial scale at spawn
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumScalePeak',         '1.4');  -- peak scale at end of fade-in
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumSpawnCooldown',     '0.15'); -- min time between damage numbers per object (seconds)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('DamageNumLifetimeExtension', '0.15'); -- lifetime added per stacked hit (seconds)

---------------------------------------------------------------------------------------------------------------------------
-- XP drops and clumps (Nova Drift-like pickup behavior)

INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpPickupPerSecond', '12');                 -- per-unit absorption cap each second
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadZoneRadius', '240');                 -- outside this, clumps count as unattended
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpPullZoneRadius', '240');                 -- inside this, clumps begin pull-zone velocity steering
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbZoneRadius', '85');                -- inside this, clumps orbit-lock and try to absorb
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadZoneStartSeconds', '20');            -- unattended duration before warning pulse starts
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadZoneDespawnSeconds', '10');          -- warning pulse duration before despawn
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpPullSpeedMin', '0');                     -- minimum pull-zone target speed
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpPullSpeedMax', '250');                   -- maximum pull-zone target speed
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpPullVelocityLerpPerSecond', '4');        -- per-second velocity blend toward pull target
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpRadius', '6');                           -- visual orb radius (pixels)
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpSpawnSpreadRadius', '18');               -- random spawn spread from farm center
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpSpawnInitialSpeed', '24');               -- initial launch speed at spawn
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpMaxSpeed', '320');                       -- movement speed clamp
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpVelocityDampingPerSecond', '1.8');       -- baseline velocity damping
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpClusterRadius', '110');                  -- neighbor range for clump-to-clump attraction
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpClusterAttractForce', '45');             -- attraction force toward local clump clusters
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpClusterHomeostasisDistance', '8');       -- preferred local spacing where forces balance
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpClusterRepelForce', '95');               -- close-range repulsion to prevent singular collapse
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpVisualMergeRadius', '24');               -- neighbor radius used for visual clump body convergence
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpVisualMergeGrowth', '0.19');             -- scale growth per nearby free clump
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpVisualMergeMaxScale', '4.5');            -- cap for convergence scale multiplier
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpVisualMergeScaleLerpPerSecond', '8');    -- smoothing speed for visual cluster scale changes
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbConsumeDistance', '10');           -- distance to unit center required to consume
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbFadeSeconds', '0.42');             -- fade-out duration when consumed
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbConsumeGrowMaxScale', '2.6');      -- consumed free clumps grow toward this scale while fading out
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpConsumeGrowthExponent', '2.2');          -- curve exponent controlling how quickly consumed clumps expand while fading
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitMaxAngularSpeedDeg', '170');  -- max lock-orbit angular speed
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitAngularBlendPerSecond', '3.2');-- angular velocity blend while locking
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitAngularDampingPerSecond', '3.6');-- angular damping while locking
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitCollapseSpeed', '72');        -- inward collapse speed for locked clumps
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbConsumeCollapseSpeed', '120');     -- inward collapse speed while consuming
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitFollowGain', '6.2');          -- radial follow gain while locked
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbVelocityLerpPerSecond', '8');      -- velocity blend rate for locked clumps
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitTangentialVelocityWeight', '0.35'); -- tangential velocity contribution while locked
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitInitialRadiusFactor', '0.35');-- initial lock radius as factor of absorb consume distance
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitMinRadiusFactor', '0.05');    -- minimum lock radius as factor of absorb consume distance
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitMinRadiusAbsolute', '0.35');  -- absolute minimum lock radius
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitCollapseBoostLow', '1.15');   -- collapse multiplier when angular velocity is low
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitCollapseBoostHigh', '1.0');   -- collapse multiplier when angular velocity is high
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitInwardCollapseFactor', '0.12');-- added inward speed factor from collapse speed
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitMaxInwardSpeedMin', '40');    -- minimum inward speed cap baseline
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbOrbitMaxInwardSpeedPullScale', '1.5'); -- inward cap multiplier from pull max speed
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpAbsorbConsumeExtraInwardFactor', '0.35');-- extra inward pull applied during consume phase
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpCoreHighlightScale', '0.45');             -- inner-core highlight size multiplier
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpCoreHighlightAlphaScale', '0.24');        -- inner-core highlight alpha multiplier
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpGlowScale', '1.9');                       -- glow scale multiplier
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpGlowAlphaScale', '0.58');                 -- glow alpha multiplier
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpShadowScale', '1.55');                    -- dark underlay scale multiplier drawn behind clumps
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpShadowAlphaScale', '0.4');                -- dark underlay alpha multiplier drawn behind clumps
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadPulseSpeedMin', '3.8');               -- dead-clump flicker pulse speed at start
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadPulseSpeedMax', '12.0');              -- dead-clump flicker pulse speed near despawn
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadPulseLowAlphaStart', '0.55');         -- dead-clump minimum pulse alpha at start
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPClumpDeadPulseLowAlphaEnd', '0.03');           -- dead-clump minimum pulse alpha near despawn
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableHealthThresholdRatio', '0.3'); -- destructible HP ratio where unstable clumps start rendering
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableJitterAccel', '1500');            -- random acceleration intensity of unstable clumps
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableCenterPullAccel', '300');         -- center-restoring acceleration keeping unstable clumps contained
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableVelocityDampingPerSecond', '2.7');-- damping for unstable clump velocity
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableMaxSpeed', '360');                -- max unstable clump speed inside the source radius
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaMin', '0.2');                -- minimum unstable clump alpha
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaMax', '0.95');               -- maximum unstable clump alpha
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaPulseHz', '10.5');           -- base flicker frequency for unstable clump alpha
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableRadiusScale', '0.49');            -- fraction of source radius usable by unstable clump motion
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableRenderScale', '1.0');             -- unstable clump render size relative to free clump radius
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableFadeOutSeconds', '0.55');         -- fade-out time when a source heals above unstable threshold
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableShowLerpPerSecond', '12');        -- fade-in lerp speed when source enters unstable threshold
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableVisibilityEpsilon', '0.001');     -- unstable preview visibility cutoff before removal
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableBurstRatePerSecond', '2.6');      -- burst-event rate for unstable clump velocity spikes
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableRandomKickFactorMin', '1.0');     -- minimum random kick factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableRandomKickFactorMax', '2.15');    -- maximum random kick factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableTangentialKickBaseFactor', '0.34'); -- base tangential kick factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableTangentialKickPressureFactor', '0.62'); -- tangential kick pressure scaling
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableCenterPullBaseFactor', '0.18');   -- base center pull factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableCenterPullPressureFactor', '0.5');-- center pull pressure scaling
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableBurstFactorMin', '1.3');          -- minimum burst magnitude factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableBurstFactorMax', '2.35');         -- maximum burst magnitude factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableBoundaryBounceFactor', '2.2');    -- boundary reflection strength for unstable clumps
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableBoundaryTangentialFactor', '0.35'); -- boundary tangential boost factor
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaNoiseAmplitude', '0.9');     -- random alpha noise amplitude
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaPulseWeight', '0.62');       -- sinusoidal alpha contribution
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaPressureWeight', '0.28');    -- pressure alpha contribution
INSERT INTO FXSettings (SettingKey, Value) VALUES ('XPDropUnstableAlphaLerpPerSecond', '14');       -- alpha smoothing speed for unstable clumps
