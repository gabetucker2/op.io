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
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportWidth', '1400');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('ViewportHeight', '1400');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('WindowMode', 'BorderedWindowed');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('VSync', 'false'); -- G-Sync will override this
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('FixedTimeStep', 'false');
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('TargetFrameRate', '240');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveUp', 'W', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveDown', 'S', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveLeft', 'A', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveRight', 'D', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveTowardsCursor', 'LeftClick', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('MoveAwayFromCursor', 'RightClick', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('Sprint', 'LeftShift', 'Hold');
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState) VALUES ('Crouch', 'LeftControl', 'Switch', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('ReturnCursorToPlayer', 'Space', 'Trigger');
INSERT INTO ControlKey (SettingKey, InputKey, InputType) VALUES ('Exit', 'Escape', 'Trigger');
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState) VALUES ('DockingMode', 'V', 'Switch', 0);

INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState) VALUES ('DebugMode', 'B', 'Switch', 1);

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.5');

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '3');

---------------------------------------------------------------------------------------------------------------------------
