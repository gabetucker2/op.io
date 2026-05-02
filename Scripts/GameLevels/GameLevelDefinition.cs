using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io
{
    internal enum GameLevelKind
    {
        Manual,
        Natural
    }

    internal sealed class GameLevelDefinition
    {
        public GameLevelDefinition(
            GameLevelKind kind,
            string key,
            string displayName,
            string description,
            bool spawnPlayer,
            bool loadMapObjects,
            bool loadAgents,
            bool loadFarms,
            bool loadZoneBlocks,
            string terrainConfigurationKey,
            string oceanZoneConfigurationKey,
            IEnumerable<string> includedAgentNames = null)
        {
            Kind = kind;
            Key = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Level key is required.", nameof(key)) : key.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName.Trim();
            Description = description?.Trim() ?? string.Empty;
            SpawnPlayer = spawnPlayer;
            LoadMapObjects = loadMapObjects;
            LoadAgents = loadAgents;
            LoadFarms = loadFarms;
            LoadZoneBlocks = loadZoneBlocks;
            TerrainConfigurationKey = string.IsNullOrWhiteSpace(terrainConfigurationKey) ? "default-terrain" : terrainConfigurationKey.Trim();
            OceanZoneConfigurationKey = string.IsNullOrWhiteSpace(oceanZoneConfigurationKey) ? "generated-coastline-zones" : oceanZoneConfigurationKey.Trim();
            IncludedAgentNames = includedAgentNames?
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
        }

        public GameLevelKind Kind { get; }
        public string Key { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool SpawnPlayer { get; }
        public bool LoadMapObjects { get; }
        public bool LoadAgents { get; }
        public bool LoadFarms { get; }
        public bool LoadZoneBlocks { get; }
        public string TerrainConfigurationKey { get; }
        public string OceanZoneConfigurationKey { get; }
        public IReadOnlyList<string> IncludedAgentNames { get; }
        public bool LoadsSelectedAgents => IncludedAgentNames.Count > 0;
        public bool LoadsAnySceneObjects => SpawnPlayer || LoadMapObjects || LoadAgents || LoadsSelectedAgents || LoadFarms || LoadZoneBlocks;
    }
}
