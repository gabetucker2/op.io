using System;
using System.Collections.Generic;
using System.Linq;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io
{
    internal static class GameLevelManager
    {
        private const string ActiveLevelRowKey = "ActiveLevelKey";
        private const string LaunchLevelRowKey = "LaunchLevelKey";
        private const string DefaultLaunchLevelKey = "natural";

        private static readonly GameLevelDefinition[] DefinedLevels =
        [
            new GameLevelDefinition(
                GameLevelKind.Manual,
                "manual",
                "Manual",
                "Current authored test scene with scout, farms, walls, zones, terrain, and ocean.",
                spawnPlayer: true,
                loadMapObjects: true,
                loadAgents: true,
                loadFarms: true,
                loadZoneBlocks: true,
                terrainConfigurationKey: "default-generated-terrain",
                oceanZoneConfigurationKey: "generated-coastline-zones"),
            new GameLevelDefinition(
                GameLevelKind.Natural,
                "natural",
                "Natural",
                "Generated terrain and ocean with the runtime player, without the scout, farms, walls, or zones.",
                spawnPlayer: true,
                loadMapObjects: false,
                loadAgents: false,
                loadFarms: false,
                loadZoneBlocks: false,
                terrainConfigurationKey: "default-generated-terrain",
                oceanZoneConfigurationKey: "generated-coastline-zones")
        ];

        private static GameLevelDefinition _activeLevel = DefinedLevels[1];
        private static bool _activeLevelLoadedFromStore;
        private static bool _loadInProgress;
        private static int _reloadCount;

        public static IReadOnlyList<GameLevelDefinition> Levels => DefinedLevels;
        public static int LevelCount => DefinedLevels.Length;
        public static int ReloadCount => _reloadCount;
        public static bool LoadInProgress => _loadInProgress;
        public static string ActiveLevelKey => ActiveLevel.Key;
        public static string ActiveLevelName => ActiveLevel.DisplayName;
        public static string ActiveLevelTerrainConfiguration => ActiveLevel.TerrainConfigurationKey;
        public static string ActiveLevelOceanZoneConfiguration => ActiveLevel.OceanZoneConfigurationKey;
        public static string ActiveLevelLoadoutSummary => BuildLevelLoadoutSummary(ActiveLevel);

        public static GameLevelDefinition ActiveLevel
        {
            get
            {
                EnsureActiveLevelLoadedFromStore();
                return _activeLevel;
            }
        }

        public static bool TryGetLevel(string key, out GameLevelDefinition level)
        {
            level = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            level = DefinedLevels.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, key.Trim(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.DisplayName, key.Trim(), StringComparison.OrdinalIgnoreCase));
            return level != null;
        }

        public static GameLevelDefinition GetNextLevel(int direction)
        {
            EnsureActiveLevelLoadedFromStore();
            int index = Array.IndexOf(DefinedLevels, _activeLevel);
            if (index < 0)
            {
                index = 0;
            }

            int normalizedDirection = direction < 0 ? -1 : 1;
            int nextIndex = (index + normalizedDirection + DefinedLevels.Length) % DefinedLevels.Length;
            return DefinedLevels[nextIndex];
        }

        public static bool TryLoadLevel(GameLevelDefinition level, bool forceReload = false)
        {
            if (level == null)
            {
                return false;
            }

            EnsureActiveLevelLoadedFromStore();
            if (_loadInProgress)
            {
                DebugLogger.PrintWarning($"Ignored level load for '{level.DisplayName}' because another level load is already in progress.");
                return false;
            }

            bool levelChanged = !ReferenceEquals(_activeLevel, level);
            if (!levelChanged && !forceReload)
            {
                return false;
            }

            try
            {
                _loadInProgress = true;
                _activeLevel = level;
                PersistActiveLevel(level);
                _reloadCount++;
                DebugLogger.PrintGO($"Loading game level '{level.DisplayName}' ({BuildLevelLoadoutSummary(level)}).");
                GameObjectInitializer.ReloadActiveLevel();
                return true;
            }
            finally
            {
                _loadInProgress = false;
            }
        }

        public static bool TryLoadLevel(string key, bool forceReload = false)
        {
            return TryGetLevel(key, out GameLevelDefinition level) && TryLoadLevel(level, forceReload);
        }

        public static string BuildLevelLoadoutSummary(GameLevelDefinition level)
        {
            if (level == null)
            {
                return "none";
            }

            List<string> parts = new();
            if (level.SpawnPlayer) parts.Add("player");
            if (level.LoadAgents)
            {
                parts.Add("agents");
            }
            else if (level.LoadsSelectedAgents)
            {
                parts.Add($"agents: {string.Join(", ", level.IncludedAgentNames)}");
            }
            if (level.LoadFarms) parts.Add("farms");
            if (level.LoadMapObjects) parts.Add("map objects");
            if (level.LoadZoneBlocks) parts.Add("zones");
            parts.Add("terrain");
            parts.Add("ocean");
            return string.Join(", ", parts);
        }

        private static void EnsureActiveLevelLoadedFromStore()
        {
            if (_activeLevelLoadedFromStore)
            {
                return;
            }

            _activeLevelLoadedFromStore = true;
            try
            {
                Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.Levels);
                string storedKey = null;
                if (data != null)
                {
                    if (!data.TryGetValue(LaunchLevelRowKey, out storedKey) ||
                        string.IsNullOrWhiteSpace(storedKey))
                    {
                        data.TryGetValue(ActiveLevelRowKey, out storedKey);
                    }
                }

                if (!TryGetLevel(storedKey, out GameLevelDefinition storedLevel))
                {
                    TryGetLevel(DefaultLaunchLevelKey, out storedLevel);
                }

                if (storedLevel != null)
                {
                    _activeLevel = storedLevel;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"GameLevelManager could not read stored active level: {ex.Message}");
            }
        }

        private static void PersistActiveLevel(GameLevelDefinition level)
        {
            if (level == null)
            {
                return;
            }

            try
            {
                BlockDataStore.SetRowData(DockBlockKind.Levels, ActiveLevelRowKey, level.Key);
            }
            catch (Exception ex)
            {
                DebugLogger.PrintWarning($"GameLevelManager could not persist active level: {ex.Message}");
            }
        }
    }
}
