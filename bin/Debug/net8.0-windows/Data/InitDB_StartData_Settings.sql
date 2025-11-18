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
INSERT INTO GeneralSettings (SettingKey, Value) VALUES ('NumLogFiles', '5');

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('MoveUp', 'W', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('MoveDown', 'S', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('MoveLeft', 'A', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('MoveRight', 'D', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('MoveTowardsCursor', 'LeftClick', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('MoveAwayFromCursor', 'RightClick', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('Sprint', 'LeftShift', 'Hold', 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl) VALUES ('Crouch', 'LeftControl', 'Switch', 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('ReturnCursorToPlayer', 'Space', 'Trigger', 0);

INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl) VALUES ('Exit', 'Escape', 'Trigger', 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl) VALUES ('PanelMenu', 'Shift + X', 'Switch', 0, 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl) VALUES ('DockingMode', 'V', 'Switch', 0, 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl) VALUES ('DebugMode', 'B', 'Switch', 1, 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, SwitchStartState, MetaControl) VALUES ('AllowGameInputFreeze', 'Shift + C', 'Switch', 1, 1);

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.2');

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '10');

---------------------------------------------------------------------------------------------------------------------------
