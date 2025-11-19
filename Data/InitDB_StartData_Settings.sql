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

INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('MoveUp', 'W', 'Hold', 0, 1, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('MoveDown', 'S', 'Hold', 0, 2, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('MoveLeft', 'A', 'Hold', 0, 3, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('MoveRight', 'D', 'Hold', 0, 4, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('MoveTowardsCursor', 'LeftClick', 'Hold', 0, 5, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('MoveAwayFromCursor', 'RightClick', 'Hold', 0, 6, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('Sprint', 'LeftShift', 'Hold', 0, 7, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('Crouch', 'LeftControl', 'Switch', 0, 8, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('ReturnCursorToPlayer', 'Space', 'Trigger', 0, 9, 0);

INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('Exit', 'Escape', 'Trigger', 1, 10, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('PanelMenu', 'Shift + X', 'Switch', 0, 11, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('DockingMode', 'V', 'Switch', 0, 12, 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('DebugMode', 'B', 'Switch', 1, 13, 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState) VALUES ('AllowGameInputFreeze', 'Shift + C', 'Switch', 1, 14, 1);

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.2');

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '10');

---------------------------------------------------------------------------------------------------------------------------
