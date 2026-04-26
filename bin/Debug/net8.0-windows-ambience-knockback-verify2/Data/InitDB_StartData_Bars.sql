-- InitDB_StartData_Bars.sql
-- In-game bar layout dimensions, animation timing, and fill colors.

---------------------------------------------------------------------------------------------------------------------------
-- Bar layout and animation

INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarHeight',     '4');            -- bar height in pixels
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarOffsetY',    '8');            -- pixels below object edge
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarSegmentSize','10');           -- HP per segment tick (LoL-style)
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarAnim',       '0.15|0.0|0.40'); -- fadeIn | hold | fadeOut (seconds)

---------------------------------------------------------------------------------------------------------------------------
-- In-game health bar fill gradient (low health → high health)

INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillLowR',  '220');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillLowG',  '50');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillLowB',  '50');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillLowA',  '255');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillHighR', '60');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillHighG', '200');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillHighB', '60');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarFillHighA', '255');

-- In-game health bar background
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarBgR', '64');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarBgG', '64');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarBgB', '64');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('HealthBarBgA', '255');

---------------------------------------------------------------------------------------------------------------------------
-- Shield bar fill (sky blue) — shared between in-game bar and properties block

INSERT INTO BarSettings (SettingKey, Value) VALUES ('ShieldBarFillR', '0');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('ShieldBarFillG', '180');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('ShieldBarFillB', '255');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('ShieldBarFillA', '255');

---------------------------------------------------------------------------------------------------------------------------
-- Properties block bar colors

-- Health gradient
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthLowR',  '200');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthLowG',  '50');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthLowB',  '50');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthLowA',  '255');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthHighR', '50');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthHighG', '200');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthHighB', '80');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarHealthHighA', '255');

-- Empty segment background
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarEmptyR', '35');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarEmptyG', '35');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarEmptyB', '35');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('PropBarEmptyA', '210');

---------------------------------------------------------------------------------------------------------------------------
-- XP bar fill color

INSERT INTO BarSettings (SettingKey, Value) VALUES ('XPBarFillR', '50');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('XPBarFillG', '220');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('XPBarFillB', '80');
INSERT INTO BarSettings (SettingKey, Value) VALUES ('XPBarFillA', '255');
