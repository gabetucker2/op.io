# op.io conventions

This document captures everything Codex must follow when writing or editing code inside this repository. It explains the architecture, data flow, formatting rules, comment expectations, and the exact APIs that already exist so new work snaps into the current style without exceptions.

## Tech stack & build expectations
- **Runtime**: .NET 8.0 (`net8.0-windows`) WinExe that boots MonoGame DesktopGL (see `op.io.csproj`). Windows-only features such as `AllocConsole`, `SDL_GetWindowPosition`, and `System.Windows.Forms.Cursor` are relied upon.
- **Libraries**: MonoGame (`Microsoft.Xna.Framework.*`, `MonoGame.Framework.DesktopGL`), `System.Data.SQLite` + `Microsoft.Data.Sqlite`, `Newtonsoft.Json`, `ppy.SDL2-CS`, `System.Drawing.Common`.
- **Content pipeline**: `Content/Content.mgcb` is copied to the output; shapes are generated procedurally (no sprite assets yet).
- **Database**: SQLite file stored under `Data/op.io.db`. `Scripts/Database/DatabaseInitializer.cs` can recreate it from the SQL scripts when needed.
- **Run command**: `dotnet run` from the repo root (also listed in `README.md`). All data files in `Data/` are copied on build.

## Directory layout (authoring focus)

| Path | Purpose |
| --- | --- |
| `Scripts/Core.cs` | MonoGame `Game` subclass that holds global state (`Core.Instance`, graphics, player reference, timing fields). |
| `Scripts/GameInitializer.cs` | Bootstraps everything: database, settings, input states, console, window mode, physics, and game objects. |
| `Scripts/GameRenderer.cs`, `Scripts/GameUpdater.cs` | MonoGame `LoadContent`, `Draw`, and `Update` orchestration. |
| `Scripts/Controls/` | Input stack (`InputManager`, `InputTypeManager`, `ActionHandler`, `MouseFunctions`, `TriggerManager`, `ControlStateManager`). |
| `Scripts/GameObjects/` | Base `GameObject`, derived `Agent`, DTOs (`FarmData`), loaders (`AgentLoader`, `FarmProtoLoader`, etc.), initializers, and registries (`GameObjectRegister`). |
| `Scripts/Physics/` | Physics pipeline (`PhysicsManager`, `CollisionManager`, `CollisionResolver`, `ForcesManager`, `SATCollisionUtil`). |
| `Scripts/Shapes/` | Procedural shape drawing (`Shape`, `ShapeRenderer`, `ShapeManager`, `ShapeVertexGenerator`). |
| `Scripts/Debugging/` | Logging and console infrastructure (`DebugLogger`, `LogFormatter`, `ConsoleManager`, `DebugModeHandler`, `DebugRenderer`). |
| `Scripts/Database/` | Database config, connection helpers, query helpers, initialization scripts, and SQL runner. |
| `Scripts/UI/` | Window and docking helpers (`BlockManager`, `ScreenManager`). |
| `Scripts/TypeConversions/TypeConversionFunctions.cs` | Shared `Vector2` and bool/int conversion helpers. |
| `Data/` | SQLite database, schema/data SQL scripts, and shared enums/structs. |
| `Content/`, `Icon.*`, `app.manifest` | MonoGame content & application resources. |

`bin/` and `obj/` are build outputs; ignore them when editing manually.

## Execution flow overview
1. `Program.Main` (`Scripts/Program.cs`) instantiates `Core` and runs it within an `STAThread`.
2. `Core` constructor sets `Core.Instance`, creates the `GraphicsDeviceManager`, and points the content root at `Content`.
3. `Core.Initialize` calls `GameInitializer.Initialize()` which performs the following order-sensitive steps (see `Scripts/GameInitializer.cs`):
   1. Guard that `Core.Instance` exists.
   2. `DatabaseInitializer.InitializeDatabase()` ensures the SQLite file matches `Data/*.sql`.
   3. `LoadGeneralSettings()` populates `Core` fields from `GeneralSettings` (viewport, colors, timing, window mode, vsync).
   4. `ControlStateManager.LoadControlSwitchStates()` hydrates switch states before they are used.
   5. `ConsoleManager.InitializeConsoleIfEnabled()` opens the debug console if switches allow it (respecting `DebugModeHandler` and `Core.ForceDebugMode` overrides).
   6. Sets default values (mouse visibility, `PhysicsManager`, fallback FPS/window values) before applying them.
   7. `BlockManager.ApplyWindowMode(Core.Instance)` mutates `Graphics`/`Window` flags, then `Graphics.ApplyChanges()` is called.
   8. `GameObjectInitializer.Initialize()` loads map objects, farms (via prototypes + data), then agents; everything ends up in `Core.Instance.GameObjects` while static items also populate `Core.Instance.StaticObjects`. Agents flagged `IsPlayer` assign `Core.Instance.Player`.
   9. All `GameObject`s call `LoadContent` so their `Shape`s bake textures via `ShapeRenderer`.
   10. `PhysicsManager.Initialize()` flips its `_initialized` guard.
4. `Core.LoadContent` delegates to `GameRenderer.LoadGraphics()` which ensures `SpriteBatch`, debug renderers, and `Shape` textures exist (using a `HashSet<Shape>` so each shape is loaded once).
5. `Core.Update` runs `GameUpdater.Update(gameTime)` which:
   1. Updates `Core.GAMETIME` and `Core.DELTATIME` (clamped to `>= 0.0001f`).
   2. Logs if `DELTATIME` is invalid (`DebugHelperFunctions.DeltaTimeZeroWarning`).
   3. Executes `ActionHandler.Tickwise_CheckActions()`, which in turn handles exits, toggles via `SwitchUpdate`, cursor reposition, etc.
   4. Reads player movement via `InputManager.GetMoveVector()` → `ActionHandler.Move(...)`.
   5. Aligns player rotation using `MouseFunctions.GetAngleToMouse`.
   6. Calls `Update()` on every `Core.Instance.GameObjects` element.
   7. Issues warnings if no game objects exist.
   8. Runs `PhysicsManager.Update(Core.Instance.GameObjects)` to resolve collisions and destruction.
   9. Resets triggers (`TriggerManager.Tickwise_TriggerReset()`) and snapshots switch history (`ControlStateManager.Tickwise_PrevSwitchTrackUpdate()`).
   10. **When you add any input that depends on current vs. previous device state, call `InputTypeManager.Update()` after reading inputs so `_previous*State` stays in sync.**
6. `Core.Draw` runs `GameRenderer.Draw()` which clears the screen, begins a `SpriteBatch`, invokes `ShapeManager.Instance.DrawShapes(spriteBatch)` (currently within the game object loop so the manager will draw the entire registry each iteration), then overlays debug circles/pointers if `DebugModeHandler.DEBUGENABLED`.

The update/draw calls depend on all the singleton managers being set up exactly as above; new code must stay within this flow (initialize state before use, log when guards fail, and never skip the `Core.Instance` checks).

## System responsibilities & how to extend them

### Core state and timing
- `Core.Instance` is the canonical singleton. Guard it before using.
- Fields like `Graphics`, `SpriteBatch`, `BackgroundColor`, `ViewportWidth/Height`, `VSyncEnabled`, `UseFixedTimeStep`, `TargetFrameRate`, `WindowMode`, `GameObjects`, and `StaticObjects` are public properties intentionally mutated during initialization.
- Timing helpers: `Core.GAMETIME` (seconds since start) and `Core.DELTATIME` (seconds between frames, clamped) are static so utility classes (physics, input) can reference them without a game reference.
- `Core.Player` wraps `_player` and logs if accessed before initialization; always set through the property to preserve the null warning.

### Game objects, shapes, and rendering
- `GameObject` (`Scripts/GameObjects/GOTypes/GameObject.cs`) is the base type. It stores identity, transform, physics flags, colors, outline width, and a `Shape`. Constructor parameters must mirror DB columns. It registers itself with `GameObjectRegister` unless `isPrototype` is true.
- Derived objects (e.g., `Agent`) add gameplay data. Always call `base(...)` with the final `Shape` and colors so bounding radius and registration are accurate. Override `LoadContent`/`Update` to extend behavior and keep guard clauses (see `Agent.Update` or `GameObject.Update`).
- `GameObjectRegister` and `ShapeManager` control which shapes are drawn. When removing/destroying objects, call `GameObjectRegister.UnregisterGameObject` to keep draw lists aligned (see `GameObject.Dispose` and `CollisionResolver`).
- Shapes (`Scripts/Shapes/Shape*.cs`) are procedural: `Shape` holds metadata, `ShapeRenderer` creates `Texture2D`s with fill + outline, `ShapeVertexGenerator` produces vertex arrays for SAT collision, and `ShapeManager` draws everything. When cloning objects, either clone the `Shape` instance or ensure you’re not mutating shared prototypes.
- `GameRenderer.LoadGraphics` must be called before any shape draws; it iterates over all objects and calls `Shape.LoadContent` once per unique instance.
- Debug visuals come from `DebugRenderer` which reads `DebugVisuals` DB settings (`DebugCircleColor_*`, `DebugCircleRadius`) and draws either circles or rotation pointers.

### Input, actions, and control state
- `InputManager` lazily loads control bindings from `ControlKey` (columns `SettingKey`, `InputKey`, `InputType`). It caches dictionaries (`_controlKey`, `_cachedSpeedMultipliers`) so DB calls happen only once per setting. `GetMoveVector()` combines WASD movement with cursor relative movement when certain toggles are enabled. `SpeedMultiplier()` stacks sprint/crouch multipliers from `ControlSettings`. `IsInputActive(settingKey)` delegates to `InputTypeManager` based on `InputType`.
- `InputTypeManager` maintains previous keyboard/mouse state, multiple dictionaries for triggers/switches, and caches cooldown settings at runtime (`Agent.LoadTriggerCooldown/LoadSwitchCooldown`). It offers `IsKeyHeld`, `IsKeyTriggered`, `IsKeySwitch`, `IsMouseButtonHeld/Triggered/Switch`, and an `Update()` method that must run once per frame after inputs are processed to store `_previousKeyboardState`/`_previousMouseState`.
- `ControlStateManager` owns switch states. `_switchStates` is the current tick, `_switchStateBuffer` is the previous tick, `_prevSwitchStates` is used to detect transitions. Always call `SetSwitchState` (not direct dictionary writes) so DB defaults and persistence (`DatabaseConfig.UpdateSetting`) happen. `Tickwise_PrevSwitchTrackUpdate` (called at the end of `GameUpdater.Update`) copies current → prev, so toggles should compare `GetPrevTickSwitchState` vs. `GetSwitchState`.
- `TriggerManager` exposes one-shot booleans per tick. Any system can `PrimeTrigger(key)` or `PrimeTriggerIfTrue(key, condition)`; `Tickwise_TriggerReset()` clears them each frame.
- `ActionHandler` is the place to react to `InputManager` states. `Tickwise_CheckActions` handles quitting, toggles, cursor return, etc. `SwitchUpdate` is the existing pattern for syncing DB-driven switches to runtime handlers; if you add a switch-driven mode, append to the `modeHandlers` list and rely on `ControlStateManager` + `TriggerManager`. Movement uses `ActionHandler.Move`, which performs guard checks (null object, NaN direction, zero vector, non-positive speed, invalid delta time) and then applies frame-rate independent motion.
- `MouseFunctions` encapsulates screen-space math (`GetAngleToMouse`, `GetMousePosition`), handling NaNs and zero-length vectors gracefully.

### Database utilities & schema
- `DatabaseConfig` (`Scripts/Database/DatabaseConfig.cs`) resolves the project root, builds the `Data` directory path, exposes `DatabaseFilePath`/`ConnectionString`, and applies PRAGMA settings exactly once per process (`IsConfigured` flag). Always retrieve settings through `DatabaseConfig.GetSetting<T>` or `DatabaseFetch.GetValue<T>` instead of opening ad-hoc connections.
- `DatabaseInitializer` deletes the existing DB (after clearing SQLite pools) and recreates it from `Data/InitDB_*.sql` (structure then seed data). It verifies critical tables exist (`GameObjects`, `Agents`, `FarmData`, `MapData`) and logs success or failure.
- `DatabaseManager` opens/closes connections with diagnostic logging, ensures the DB file exists, exposes `UpdateSetting` for generic table updates, and offers `ClearConnectionPool`.
- `DatabaseQuery.ExecuteQuery` takes a SQL string and optional parameter dictionary, logs both, opens a new SQLite connection, and returns a `List<Dictionary<string, object>>` (each `Dictionary` uses column names as keys). It must be used for all queries that return rows.
- `DatabaseFetch.GetValue<T>` fetches a single column value (with a WHERE clause), logs errors, and returns defaults when nothing is found. `GetSetting<T>` is a more defensive variant with extra logging. `GetSingleValue<T>` executes arbitrary scalar queries. `GetColor` composes four `Value` rows from `<SettingKey>_R/G/B/A`.
- `SQLScriptExecutor` reads `.sql` files and executes them inside transactions; it rolls back if anything fails.

**Tables referenced in code:**
- `GeneralSettings` supports `SettingKey`/`Value`. Keys: `BackgroundColor`, `WindowMode`, `ViewportWidth`, `ViewportHeight`, `VSync`, `FixedTimeStep`, `TargetFrameRate`, `DebugCircleColor_*`, `DebugCircleRadius`.
- `ControlSettings` has numeric settings (`TriggerCooldown`, `SwitchCooldown`, `SprintSpeedMultiplier`, `CrouchSpeedMultiplier`, `DebugMaxRepeats`, etc.).
- `ControlKey` includes `SettingKey`, `InputKey`, `InputType`, `SwitchStartState`.
- `GameObjects` stores render/physics data (IDs, name, type, `PositionX/Y`, `Rotation`, `Width`, `Height`, `Sides`, fill/outline RGBA, outline width, `IsCollidable`, `IsDestructible`, `Mass`, `StaticPhysics`, `Shape`). This table is the backbone for loaders.
- `Agents` extends `GameObjects` via joins with columns like `IsPlayer`, `TriggerCooldown`, `SwitchCooldown`, `BaseSpeed`.
- `FarmData` provides `ID` and `Count` for instantiating clones.
- `MapData` maps IDs to map-specific metadata (currently just acts as a join table).
- `DebugVisuals` stores color components for debug rendering.

When writing queries:
- Use `GameObjectManager.BuildJoinQuery` to compose joins between `GameObjects` (`g`) and a secondary table (`s`), optionally adding `WHERE` clauses and extra columns.
- Always parameterize inputs (see `AgentLoader.LoadAgent`) and log query context.
- Wrap DB work in `try/catch(Exception ex)` and log errors via `DebugLogger.PrintError`.

### Physics & collision pipeline
- `PhysicsManager.Initialize` guards against double initialization; `Update` expects a non-null list and currently delegates to `CollisionResolver` (future hooks for forces/gravity already noted in comments).
- `CollisionManager.CheckCollision` obtains transformed vertices from both shapes and runs SAT (via `SATCollisionUtil`). If vertices are missing it prints a warning and returns false.
- `CollisionResolver.ResolveCollisions` double-loops over the list, checks `IsCollidable`, uses SAT to confirm collisions, calls `HandlePhysicsCollision` when appropriate, and removes destructible objects (while unregistering them). Index adjustments (`i--`, `j--`) guard against skipping entries.
- `HandlePhysicsCollision` divvies up positional corrections based on static/dynamic flags and mass. `GetCollisionNormal` handles rectangle/circle special cases, falling back to normalized center-to-center vectors.
- `ForcesManager.ApplyForce` provides a reusable guard/acceleration helper for future physics features.

### Window, UI, and screen helpers
- `BlockManager.ApplyWindowMode(Core game)` adjusts `Graphics.IsFullScreen`, `Window.IsBorderless`, and buffer sizes according to `WindowMode` (bordered, borderless windowed/fullscreen, or legacy fullscreen). It also logs the outcome. `ScreenManager` currently mirrors the same logic.
- Docking-mode toggles are controlled via switches stored in `ControlStateManager` and surfaced through `BlockManager.DockingModeEnabled`.

### Debugging infrastructure
- `DebugLogger` is the only logging surface. It provides level-specific helpers (`Print`, `PrintSystem`, `PrintTemporary`, `PrintError`, `PrintWarning`, `PrintDatabase`, `PrintDebug`, `PrintUI`, `PrintGO`, `PrintPlayer`, `PrintPhysics`). Each call ultimately runs through `Log(...)`, which:
  - Guards against recursion with `IsLoggingInternally`.
  - Builds stack traces to provide source info unless the caller provided its own.
  - Uses `LogFormatter` to sanitize message text, attach depth markers, and track repetition counts (for suppression).
  - Queues messages until `ConsoleManager.ConsoleInitialized` is true, then flushes them with preserved suppression behavior.
- `LogFormatter.SuppressMessageBehavior` relies on `DebugModeHandler.MAXMSGREPEATS`. Set `DebugModeHandler.DEBUGENABLED` to force logging even when the DB says otherwise (and warn if `ForceDebugMode` blocks writes).
- `ConsoleManager` uses `AllocConsole` and adjusts buffer/window sizes on Windows. It only initializes once and flushes queued logs immediately.
- `DebugRenderer` draws world-space debug circles and rotation pointers when `DebugModeHandler.DEBUGENABLED` is true.
- `DebugHelperFunctions` supplies `DeltaTimeZeroWarning` and `GenerateSourceTrace`.

## Coding style & formatting rules

### Files, namespaces, and using statements
- Every C# file (except legacy ones like `Scripts/GameObjects/GameObjectManager.cs` and `Scripts/GameObjects/Loaders/MapObjectLoader.cs`) lives inside the `namespace op.io`. When touching those legacy files, keep them as-is; new files must include the namespace.
- Keep one public type per file and match the filename (`Agent.cs` contains `Agent`).
- Group `using` directives by origin: `System.*`, third-party (`Microsoft.Xna.Framework`, `Microsoft.Xna.Framework.Graphics`, etc.), then internal namespaces. Leave a blank line between `using` statements and the namespace declaration.

### Layout, spacing, and braces
- Indent with four spaces. Braces always go on new lines (Allman style) for namespaces, classes, methods, `if/else`, loops, and properties.
- Use blank lines to separate logical sections (e.g., between property groups, constructors, and method blocks). Avoid trailing spaces.
- Keep method bodies tight with guard clauses up front:

```csharp
public static void ApplyForce(GameObject gameObject, Vector2 force)
{
    if (gameObject == null)
    {
        DebugLogger.PrintError("ApplyForce failed: GameObject is null.");
        return;
    }

    if (force == Vector2.Zero)
    {
        DebugLogger.PrintDebug("ApplyForce skipped: Force vector is zero.");
        return;
    }

    // main logic ...
}
```

### Naming
- `PascalCase` for classes, methods, properties, and enums (`GameObjectInitializer`, `LoadFarmObjects`, `IsDestructible`). Use descriptive names; do not abbreviate.
- `camelCase` for method parameters and local variables.
- `_camelCase` for private fields (see `_player`, `_switchStates`, `_cachedTriggerCooldown`). Boolean fields still use `_is...` style.
- Constants use PascalCase (e.g., `MAXMSGREPEATS`, `ForceDebugMode`) unless they are `const`, then use PascalCase or ALL_CAPS when the rest of the file does (`defaultNBack` is `const int`).
- Enum members are PascalCase (`WindowMode.BorderlessWindowed`).

### Properties and expression-bodied members
- Prefer auto-properties with getters/setters when no custom logic is needed. Use expression-bodied properties for computed values (`public float BoundingRadius => ...;`).
- When custom logic or logging is required, use full accessors with braces (see `Core.Player`, `Agent.IsCrouching`, `DebugModeHandler.DEBUGENABLED`).

### Object & collection initialization
- Use target-typed `new()` or `[]` when the type is obvious from the left-hand side:
  - `public List<GameObject> GameObjects { get; set; } = [];`
  - `private static readonly Dictionary<string, bool> _switchStates = new();`
- When clarity would suffer (e.g., variable declared with `var`), fall back to explicit construction (`List<GameObject> farmPrototypes = new List<GameObject>();`).
- For arrays, prefer explicit lengths: `Color[] data = new Color[diameter * diameter];`.

### `var` usage
- Use `var` only when the right-hand side already states the type (e.g., `var controls = DatabaseQuery.ExecuteQuery(...);`). Explicit types improve readability when the RHS is not immediately obvious (e.g., `string query = ...;`, `HashSet<Shape> loadedShapes = [];`).

### Strings & interpolation
- Prefer string interpolation (`$"Initialized switch state for '{settingKey}'..."`) over concatenation. Multi-line SQL statements should use verbatim `$@" ... "` strings to keep indentation readable. When logging lists or dictionaries, use `string.Join`.

### Error handling and guard clauses
- Every interaction with mutable global state (`Core.Instance`, `GraphicsDevice`, DB connections) must guard against null or invalid values, log through `DebugLogger`, and return early if assumptions fail.
- Catch `SQLiteException` separately when interacting with the database to log SQL-specific errors, then catch general `Exception` for everything else.
- When a method can fail harmlessly, log at `PrintWarning`; only use `PrintError` when it indicates something that breaks expected behavior.

### Comments & documentation style
- Inline comments use `//` with full sentences that explain *why* something happens, not *what* the code does (“`// Load general settings BEFORE initializing anything else`”).
- Multi-line remarks stack `//` per line. Block comments (`/* ... */`) are unused.
- Public helpers that need extra context (especially in shared managers) get XML docs (`/// <summary>...</summary>`), as seen in `PhysicsManager` and `GameObjectLoader`.
- `TODO` notes follow the form `// TODO: <action> ...` and can appear inline (e.g., after a `namespace` declaration) or inside methods.
- Do not add noisy comments like “// Increment counter”; only comment when the reason is non-obvious, the order matters, or there is a known follow-up task.

## Logging & comment usage rules
- Always log via `DebugLogger` before returning early due to invalid state. Select the method based on category:
  - `Print` – general info.
  - `PrintSystem` – infrastructure information (e.g., manager initialization).
  - `PrintTemporary` – ad-hoc diagnostics that may be removed later.
  - `PrintError` – critical issues that stop a flow.
  - `PrintWarning` – recoverable anomalies.
  - `PrintDatabase` – SQL/DB operations.
  - `PrintDebug` – developer-only noise.
  - `PrintUI`, `PrintGO`, `PrintPlayer`, `PrintPhysics` – domain-specific channels (UI, game objects, player, physics).
- Use depth/stackTrace parameters sparingly; defaults (`depth = 0`, `stackTraceNBack = 3`) are correct in almost every call.
- Never write to `Console` directly. All console output must funnel through `DebugLogger.PrintToConsole` or the queued log mechanism to preserve suppression behavior.
- When deferring work, prefer inline `// TODO:` comments with actionable notes.

## Patterns to follow when authoring new code

### Adding or modifying game objects
1. Extend `GameObject` if you need new behavior (e.g., a new agent type). Keep constructor parameters aligned with DB columns and pass `isPrototype` when the object should not register/draw.
2. Update loaders as needed:
   - Use `GameObjectLoader.DeserializeGameObject` as the single place that converts DB rows to `GameObject`s. If new columns are required, add them there.
   - Build join queries via `GameObjectManager.BuildJoinQuery`.
   - Handle conversion errors with `try/catch` blocks and log row-level issues separately (`catch (Exception exRow)`).
3. When cloning prototypes (`FarmObjectLoader`), remember to supply unique IDs (`GameObjectManager.GetNextID()`), adjust positions/rotations, and load the shape content before returning the clone.
4. Register/deregister with `GameObjectRegister` via the constructor/dispose logic. Never manipulate `_gameObjects` in `GameObjectRegister` directly.

### Updating input or adding actions
1. Insert new bindings into the `ControlKey` and/or `ControlSettings` tables (and their initialization scripts) so `InputManager` can load them.
2. Use `InputManager.IsInputActive("SettingKey")` to read state. Choose `Hold`, `Trigger`, or `Switch` input types to match the action semantics.
3. Process toggles inside `ActionHandler.SwitchUpdate` by adding new entries to the `modeHandlers` list and adjusting `ControlStateManager` as required.
4. If the action needs per-frame movement or logic, tie it into `ActionHandler.Tickwise_CheckActions`. Remember to call `InputTypeManager.Update()` by the end of each frame to capture previous states when the action depends on transitions.

### Working with the database
1. For read-heavy code, prefer `DatabaseQuery.ExecuteQuery` followed by deserialization (see `AgentLoader.DeserializeAgentGO`). Validate keys before accessing the dictionary.
2. For single-value lookup, use `DatabaseFetch.GetValue<T>` or `DatabaseFetch.GetSingleValue<T>`.
3. To persist settings, use `DatabaseConfig.UpdateSetting` or `DatabaseManager.UpdateSetting` (depending on context) so PRAGMA configuration and logging remain consistent.
4. Always close/`Dispose` connections (`using var connection = ...;`). `DatabaseManager.OpenConnection()` already opens a connection with configured pragmas; call `DatabaseManager.CloseConnection(connection)` in `finally` blocks when you opened it manually.
5. Wrap SQL operations in `try/catch`. On failure, log the query, parameters, and exception message, then return safe defaults.

### Rendering & debug drawing
1. Only draw via `ShapeManager.Instance.DrawShapes(spriteBatch)` or `DebugRenderer` helpers inside `GameRenderer.Draw`.
2. Load any graphics resources through `Shape.LoadContent` or dedicated manager initialization functions; always guard against `GraphicsDevice == null`.
3. Place debug-only drawing inside `if (DebugModeHandler.DEBUGENABLED)` blocks so they respect runtime toggles.

### Physics updates
1. Before applying any physics logic, ensure `Core.Instance.GameObjects` is not null/empty and log warnings when no actors exist.
2. When adding new collision responses, route them through `CollisionResolver` to keep object removal and `GameObjectRegister` cleanup in one place.
3. Use `Core.DELTATIME` for all time-based math (movement, cooldowns) to keep frame rate independence.

### Windowing/UI adjustments
1. Apply `WindowMode` changes via `BlockManager.ApplyWindowMode(Core.Instance)` and never manipulate `GraphicsDeviceManager` flags elsewhere.
2. When toggling docking/fullscreen states, wire them to switch settings so `ControlStateManager` persists them and `ConsoleManager` can react accordingly.

### Utilities and conversions
- Use `TypeConversionFunctions.Vector2ToPoint` when bridging to APIs that need `System.Drawing.Point`.
- Convert bool/int values via `TypeConversionFunctions.IntToBool` and `BoolToInt` so DB rows remain consistent.
- Use `GameObjectFunctions.GetGO(Global|Local)ScreenPosition` to translate between world coordinates and actual screen pixels before calling Windows/SDL APIs.

## Running, testing, and data seeding
- To completely rebuild the database, delete `Data/op.io.db` and run the game—`DatabaseInitializer.InitializeDatabase()` will recreate it from `Data/InitDB_*.sql`. You can also run SQLite manually (`sqlite3 Data/op.io.db` + `.read Data/InitDatabase.sql`) per `README.md`.
- Use DB Browser for SQLite to inspect the tables when debugging loader issues (per the README).
- Run `dotnet run` from the repo root to launch the game. Ensure `sqlite3` tooling is installed and on the PATH if you need to interact with the database during development.

## Checklist before finishing a change
1. Files live in the correct directory, use the `op.io` namespace, and import the right namespaces.
2. Guard clauses log via `DebugLogger` and exit early on invalid state.
3. Collected data (DB queries, random seeds, cached settings) are logged through `DebugLogger.PrintDatabase` or the appropriate channel so issues are traceable.
4. Inputs, switches, and triggers are updated in the correct order (`InputManager` → `ActionHandler` → `TriggerManager`/`ControlStateManager` tickwise methods → `InputTypeManager.Update()`).
5. New game objects register/dispose correctly and have their shapes loaded before drawing.
6. SQL statements are parameterized and live either in the DB scripts or strongly typed strings that follow the existing formatting.
7. Comments explain intent, `TODO`s describe actionable work, and no stray `Console.WriteLine` calls leak outside `DebugLogger`.
8. You can run `dotnet run` without errors, and runtime logs accurately describe what the change is doing.

Following this document guarantees that Codex produces code indistinguishable from the existing codebase and keeps the runtime behavior—input handling, rendering, database access, and debug tooling—working exactly as authored.
