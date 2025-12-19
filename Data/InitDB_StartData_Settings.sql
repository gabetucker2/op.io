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

INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('MoveUp', 'W', 'Hold', 0, 1, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('MoveDown', 'S', 'Hold', 0, 2, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('MoveLeft', 'A', 'Hold', 0, 3, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('MoveRight', 'D', 'Hold', 0, 4, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('MoveTowardsCursor', 'LeftClick', 'Hold', 0, 5, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('MoveAwayFromCursor', 'RightClick', 'Hold', 0, 6, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('Sprint', 'LeftShift', 'Hold', 0, 7, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('Crouch', 'LeftControl', 'NoSaveSwitch', 0, 8, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('ReturnCursorToPlayer', 'Space', 'Trigger', 0, 9, 0, 0);

INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('Exit', 'Escape', 'Trigger', 1, 10, 0, 1);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('BlockMenu', 'Shift + X', 'SaveSwitch', 1, 11, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('DockingMode', 'V', 'SaveSwitch', 1, 12, 1, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('DebugMode', 'Shift + B', 'SaveSwitch', 1, 13, 1, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('AllowGameInputFreeze', 'Shift + C', 'SaveSwitch', 1, 14, 1, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('TransparentTabBlocking', 'Shift + V', 'SaveSwitch', 1, 15, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('HoldInputs', 'M', 'NoSaveSwitch', 1, 16, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('UsePreviousConfiguration', '[', 'Trigger', 1, 17, 0, 0);
INSERT INTO ControlKey (SettingKey, InputKey, InputType, MetaControl, RenderOrder, SwitchStartState, LockMode) VALUES ('UseNextConfiguration', ']', 'Trigger', 1, 18, 0, 0);

---------------------------------------------------------------------------------------------------------------------------

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SprintSpeedMultiplier', '1.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CrouchSpeedMultiplier', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('TriggerCooldown', '0.5');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('SwitchCooldown', '0.1');
INSERT INTO ControlSettings (SettingKey, Value) VALUES ('CursorFollowDeadzonePixels', '10');

INSERT INTO ControlSettings (SettingKey, Value) VALUES ('DebugMaxRepeats', '25');

---------------------------------------------------------------------------------------------------------------------------
