using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io
{
    public static class BarConfigManager
    {
        public const float DefaultVisibilityFadeOutSeconds = 0.18f;
        public const string AllDestructiblesGroupKey = "AllDestructibles";
        public const string MapObjectsGroupKey = "MapObjects";
        public const string FarmsGroupKey = "Farms";
        public const string UnitsGroupKey = "Units";
        public const string PlayersGroupKey = "Players";
        public const string YourPlayerGroupKey = "YourPlayer";

        public enum BarRelationName { BelowFull, Empty, Change, Spawn, Always }

        public readonly struct BarConfigGroupDefinition
        {
            public BarConfigGroupDefinition(string key, string label, string parentKey, int renderOrder)
            {
                Key = key; Label = label; ParentKey = parentKey; RenderOrder = renderOrder;
            }

            public string Key { get; }
            public string Label { get; }
            public string ParentKey { get; }
            public int RenderOrder { get; }
        }

        public sealed class BarRelation
        {
            public BarType SourceType { get; set; }
            public BarRelationName RelationName { get; set; }
            public BarRelation Clone() => new() { SourceType = SourceType, RelationName = RelationName };
        }

        public readonly struct BarSourceState
        {
            public BarSourceState(float current, float max, bool changedRecently, bool spawnedRecently, bool isKnown)
            {
                Current = current; Max = max; ChangedRecently = changedRecently; SpawnedRecently = spawnedRecently; IsKnown = isKnown;
            }

            public float Current { get; }
            public float Max { get; }
            public bool ChangedRecently { get; }
            public bool SpawnedRecently { get; }
            public bool IsKnown { get; }
        }

        public class BarEntry
        {
            public BarType Type { get; set; }
            public int BarRow { get; set; }
            public int PositionInRow { get; set; }
            public int SegmentCount { get; set; }
            public bool SegmentsEnabled { get; set; }
            public bool IsHidden { get; set; }
            public bool ShowPercent { get; set; }
            public float VisibilityFadeOutSeconds { get; set; } = DefaultVisibilityFadeOutSeconds;
            public List<BarRelation> VisibilityRelations { get; set; } = new();
            public bool HasVisibilityRelations => VisibilityRelations != null && VisibilityRelations.Count > 0;
            public BarEntry Clone() => new()
            {
                Type = Type,
                BarRow = BarRow,
                PositionInRow = PositionInRow,
                SegmentCount = SegmentCount,
                SegmentsEnabled = SegmentsEnabled,
                IsHidden = IsHidden,
                ShowPercent = ShowPercent,
                VisibilityFadeOutSeconds = VisibilityFadeOutSeconds,
                VisibilityRelations = CloneRelations(VisibilityRelations)
            };
        }

        private static readonly BarType[] RelationSourceOrder =
        {
            BarType.Health, BarType.Shield, BarType.XP, BarType.HealthRegen, BarType.ShieldRegen
        };

        private static readonly BarConfigGroupDefinition[] GroupDefinitions =
        {
            new(AllDestructiblesGroupKey, "All destructibles", null, 0),
            new(MapObjectsGroupKey, "Map objects", AllDestructiblesGroupKey, 1),
            new(FarmsGroupKey, "Farms", AllDestructiblesGroupKey, 2),
            new(UnitsGroupKey, "Units", AllDestructiblesGroupKey, 3),
            new(PlayersGroupKey, "Players", UnitsGroupKey, 4),
            new(YourPlayerGroupKey, "Your player", PlayersGroupKey, 5)
        };

        private static readonly Dictionary<string, BarConfigGroupDefinition> GroupDefinitionsByKey =
            GroupDefinitions.ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

        private static List<BarEntry> _global;
        private static readonly Dictionary<string, Dictionary<BarType, BarEntry>> _groupOverrides = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<BarEntry>> _resolvedEntriesByGroup = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<List<BarEntry>>> _groupedRowsByGroup = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static bool BarsVisible { get; set; } = true;

        public static List<BarEntry> GetDefaults() => new()
        {
            new() { Type = BarType.Shield, BarRow = 0, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true, IsHidden = false, VisibilityFadeOutSeconds = DefaultVisibilityFadeOutSeconds, VisibilityRelations = GetDefaultVisibilityRelations(BarType.Shield) },
            new() { Type = BarType.Health, BarRow = 1, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true, IsHidden = false, VisibilityFadeOutSeconds = DefaultVisibilityFadeOutSeconds, VisibilityRelations = GetDefaultVisibilityRelations(BarType.Health) },
            new() { Type = BarType.XP, BarRow = 2, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = false, IsHidden = false, VisibilityFadeOutSeconds = DefaultVisibilityFadeOutSeconds, VisibilityRelations = GetDefaultVisibilityRelations(BarType.XP) },
            new() { Type = BarType.HealthRegen, BarRow = 3, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true, IsHidden = true, VisibilityFadeOutSeconds = DefaultVisibilityFadeOutSeconds },
            new() { Type = BarType.ShieldRegen, BarRow = 3, PositionInRow = 1, SegmentCount = 10, SegmentsEnabled = true, IsHidden = true, VisibilityFadeOutSeconds = DefaultVisibilityFadeOutSeconds }
        };

        public static List<BarRelation> GetDefaultVisibilityRelations(BarType type) => type switch
        {
            BarType.XP => new() { new() { SourceType = BarType.XP, RelationName = BarRelationName.Change } },
            BarType.Health => new() { new() { SourceType = BarType.Shield, RelationName = BarRelationName.Empty } },
            BarType.Shield => new()
            {
                new() { SourceType = BarType.Shield, RelationName = BarRelationName.BelowFull },
                new() { SourceType = BarType.Health, RelationName = BarRelationName.BelowFull }
            },
            _ => new()
        };

        public static string GetDefaultVisibilityRelationsEncoded(BarType type) => EncodeVisibilityRelations(GetDefaultVisibilityRelations(type));
        public static IReadOnlyList<BarEntry> Global { get { EnsureLoaded(); return _resolvedEntriesByGroup[AllDestructiblesGroupKey]; } }
        public static IReadOnlyList<BarType> GetRelationSourceOrder() => RelationSourceOrder;
        public static IReadOnlyList<BarConfigGroupDefinition> GetGroupDefinitions() => GroupDefinitions;
        public static string GetDefaultGroupKey() => AllDestructiblesGroupKey;
        public static string GetGroupLabel(string groupKey) => GroupDefinitionsByKey[NormalizeGroupKey(groupKey)].Label;

        public static string NormalizeGroupKey(string groupKey)
        {
            if (!string.IsNullOrWhiteSpace(groupKey) && GroupDefinitionsByKey.TryGetValue(groupKey.Trim(), out BarConfigGroupDefinition def))
                return def.Key;
            return AllDestructiblesGroupKey;
        }

        public static string ResolveGroupKey(GameObject obj)
        {
            if (obj == null || !obj.IsDestructible) return AllDestructiblesGroupKey;
            if (ReferenceEquals(obj, Core.Instance?.Player)) return YourPlayerGroupKey;
            if (obj is Agent agent) return agent.IsPlayer ? PlayersGroupKey : UnitsGroupKey;
            if (obj.IsFarmObject) return FarmsGroupKey;
            return MapObjectsGroupKey;
        }

        public static bool DoesObjectMatchGroup(GameObject obj, string groupKey, bool includeDescendants = true)
        {
            if (obj == null || !obj.IsDestructible) return false;
            string desired = NormalizeGroupKey(groupKey);
            string actual = ResolveGroupKey(obj);
            return includeDescendants ? IsGroupInInheritanceChain(actual, desired) : string.Equals(actual, desired, StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<BarEntry> GetResolvedEntries(string groupKey)
        {
            EnsureLoaded();
            return _resolvedEntriesByGroup.TryGetValue(NormalizeGroupKey(groupKey), out List<BarEntry> entries)
                ? entries : _resolvedEntriesByGroup[AllDestructiblesGroupKey];
        }

        public static IReadOnlyList<BarEntry> GetResolvedEntries(GameObject obj) => GetResolvedEntries(ResolveGroupKey(obj));
        public static List<BarEntry> CloneGroup(string groupKey) => CloneEntries(GetResolvedEntries(groupKey));
        public static List<BarEntry> CloneGlobal() => CloneGroup(AllDestructiblesGroupKey);
        public static BarEntry Get(BarType type) => Get(AllDestructiblesGroupKey, type);
        public static BarEntry Get(string groupKey, BarType type) => GetResolvedEntries(groupKey).FirstOrDefault(e => e.Type == type) ?? GetDefaults().First(e => e.Type == type);
        public static int GetSegmentCount(BarType type) { BarEntry entry = Get(type); return entry.SegmentsEnabled ? Math.Max(1, entry.SegmentCount) : 0; }
        public static List<List<BarEntry>> GetGroupedByRow() => GetGroupedByRow(AllDestructiblesGroupKey);
        public static List<List<BarEntry>> GetGroupedByRow(string groupKey) { EnsureLoaded(); return _groupedRowsByGroup.TryGetValue(NormalizeGroupKey(groupKey), out List<List<BarEntry>> rows) ? rows : _groupedRowsByGroup[AllDestructiblesGroupKey]; }
        public static List<List<BarEntry>> GetGroupedByRow(GameObject obj) => GetGroupedByRow(ResolveGroupKey(obj));
        public static bool IsBarVisibleInAnyGroup(BarType type) => GroupDefinitions.Any(def => GetResolvedEntries(def.Key).Any(entry => entry.Type == type && !entry.IsHidden));

        public static bool AreVisibilityRelationsActive(BarEntry entry, Func<BarType, BarSourceState> sourceStateProvider)
        {
            if (entry == null || !entry.HasVisibilityRelations || sourceStateProvider == null) return false;
            foreach (BarRelation relation in entry.VisibilityRelations)
                if (relation != null && IsVisibilityRelationActive(sourceStateProvider(relation.SourceType), relation.RelationName))
                    return true;
            return false;
        }

        public static bool IsVisibilityRelationActive(BarSourceState source, BarRelationName relationName) => relationName switch
        {
            BarRelationName.Change => source.ChangedRecently,
            BarRelationName.Spawn => source.SpawnedRecently,
            BarRelationName.BelowFull => source.Max > 0.001f && source.Current < source.Max - 0.001f,
            BarRelationName.Empty => source.Current <= 0.001f,
            BarRelationName.Always => true,
            _ => false
        };

        public static void ToggleVisibilityRelation(BarEntry entry, BarType sourceType, BarRelationName relationName)
        {
            if (entry == null || (relationName == BarRelationName.Always && entry.Type != sourceType)) return;
            entry.VisibilityRelations ??= new();
            int idx = entry.VisibilityRelations.FindIndex(r => r != null && r.SourceType == sourceType && r.RelationName == relationName);
            if (idx >= 0) { entry.VisibilityRelations.RemoveAt(idx); return; }
            entry.VisibilityRelations.Add(new() { SourceType = sourceType, RelationName = relationName });
            SortRelations(entry);
        }

        public static void AddVisibilityRelation(BarEntry entry, BarType sourceType, BarRelationName relationName)
        {
            if (entry == null || (relationName == BarRelationName.Always && entry.Type != sourceType)) return;
            entry.VisibilityRelations ??= new();
            if (!entry.VisibilityRelations.Any(r => r != null && r.SourceType == sourceType && r.RelationName == relationName))
                entry.VisibilityRelations.Add(new() { SourceType = sourceType, RelationName = relationName });
            SortRelations(entry);
        }

        public static void ClearVisibilityRelations(BarEntry entry) { if (entry == null) return; entry.VisibilityRelations ??= new(); entry.VisibilityRelations.Clear(); }
        public static void RemoveVisibilityRelation(BarEntry entry, BarType sourceType, BarRelationName relationName)
        {
            if (entry?.VisibilityRelations == null) return;
            int idx = entry.VisibilityRelations.FindIndex(r => r != null && r.SourceType == sourceType && r.RelationName == relationName);
            if (idx >= 0) entry.VisibilityRelations.RemoveAt(idx);
        }

        public static string GetVisibilityRelationSummary(BarEntry entry) => entry == null || !entry.HasVisibilityRelations
            ? "No linked bars. This bar will never show."
            : string.Join("  |  ", entry.VisibilityRelations.Select(r => $"{GetBarShortLabel(r.SourceType)} {GetRelationLabel(r.RelationName)}"));

        public static string DescribeVisibilityRelation(BarType dependentType, BarRelation relation)
        {
            if (relation == null) return string.Empty;
            string sourceLabel = GetBarShortLabel(relation.SourceType);
            return relation.RelationName switch
            {
                BarRelationName.Change => $"Shows when {sourceLabel} changes.",
                BarRelationName.Spawn => $"Shows on spawn when {sourceLabel} exists.",
                BarRelationName.BelowFull => $"Shows when {sourceLabel} is not full.",
                BarRelationName.Empty => $"Shows when {sourceLabel} is empty.",
                BarRelationName.Always => relation.SourceType == dependentType ? $"Shows whenever {GetBarShortLabel(dependentType)} exists." : $"Shows whenever {sourceLabel} exists.",
                _ => $"{sourceLabel} {GetRelationLabel(relation.RelationName)}"
            };
        }

        public static string GetRelationLabel(BarRelationName relationName) => relationName switch
        {
            BarRelationName.Change => "changed",
            BarRelationName.Spawn => "spawn",
            BarRelationName.BelowFull => "not full",
            BarRelationName.Empty => "empty",
            BarRelationName.Always => "always",
            _ => relationName.ToString()
        };

        public static string EncodeVisibilityFade(float seconds) => MathF.Max(0f, seconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        public static float DecodeVisibilityFade(string encoded, float fallbackSeconds = DefaultVisibilityFadeOutSeconds)
        {
            if (!string.IsNullOrWhiteSpace(encoded) &&
                float.TryParse(encoded.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float seconds))
                return MathF.Max(0f, seconds);
            return fallbackSeconds;
        }

        public static string GetBarShortLabel(BarType type) => type switch
        {
            BarType.Health => "Health",
            BarType.Shield => "Shield",
            BarType.XP => "XP",
            BarType.HealthRegen => "H Regen",
            BarType.ShieldRegen => "S Regen",
            _ => type.ToString()
        };

        public static string GetBarAbbreviation(BarType type) => type switch
        {
            BarType.Health => "H",
            BarType.Shield => "S",
            BarType.XP => "XP",
            BarType.HealthRegen => "HR",
            BarType.ShieldRegen => "SR",
            _ => type.ToString()
        };

        public static string EncodeVisibilityRelations(IEnumerable<BarRelation> relations)
        {
            if (relations == null) return string.Empty;
            return string.Join("|", relations.Where(r => r != null).OrderBy(r => Array.IndexOf(RelationSourceOrder, r.SourceType)).ThenBy(r => (int)r.RelationName).Select(r => $"{r.SourceType}:{r.RelationName}"));
        }

        public static List<BarRelation> DecodeVisibilityRelations(string encoded)
        {
            List<BarRelation> relations = new();
            if (string.IsNullOrWhiteSpace(encoded)) return relations;
            foreach (string part in encoded.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] pieces = part.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (pieces.Length == 2 &&
                    Enum.TryParse(pieces[0], true, out BarType sourceType) &&
                    Enum.TryParse(pieces[1], true, out BarRelationName relationName))
                    relations.Add(new() { SourceType = sourceType, RelationName = relationName });
            }
            return relations.OrderBy(r => Array.IndexOf(RelationSourceOrder, r.SourceType)).ThenBy(r => (int)r.RelationName).ToList();
        }

        public static void ApplyGlobal(List<BarEntry> configs) => ApplyGroup(AllDestructiblesGroupKey, configs);
        public static void Save(List<BarEntry> configs) => Save(AllDestructiblesGroupKey, configs);

        public static void ApplyGroup(string groupKey, List<BarEntry> configs)
        {
            EnsureLoaded();
            string key = NormalizeGroupKey(groupKey);
            List<BarEntry> normalized = NormalizeEntries(configs);
            if (string.Equals(key, AllDestructiblesGroupKey, StringComparison.OrdinalIgnoreCase)) _global = normalized;
            else
            {
                Dictionary<BarType, BarEntry> overrides = BuildOverrideMap(key, normalized);
                if (overrides.Count == 0) _groupOverrides.Remove(key);
                else _groupOverrides[key] = overrides;
            }
            RebuildResolvedGroupCaches();
        }

        public static void Save(string groupKey, List<BarEntry> configs)
        {
            EnsureLoaded();
            string key = NormalizeGroupKey(groupKey);
            ApplyGroup(key, configs);
            try
            {
                if (string.Equals(key, AllDestructiblesGroupKey, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (BarEntry entry in _global)
                        DatabaseQuery.ExecuteNonQuery("UPDATE BarConfig SET BarRow=@row, PositionInRow=@pos, SegmentCount=@seg, SegmentsEnabled=@en, IsHidden=@hidden, VisibilityRelations=@relations, ShowPercent=@sp, VisibilityFade=@fade WHERE BarType=@type;", BuildBarEntryParameters(entry));
                }
                else
                {
                    DatabaseQuery.ExecuteNonQuery("DELETE FROM BarConfigGroupOverrides WHERE GroupKey=@groupKey;", new Dictionary<string, object> { ["@groupKey"] = key });
                    if (_groupOverrides.TryGetValue(key, out Dictionary<BarType, BarEntry> overrides))
                    {
                        foreach (BarEntry entry in overrides.Values.OrderBy(e => e.BarRow).ThenBy(e => e.PositionInRow))
                        {
                            Dictionary<string, object> p = BuildBarEntryParameters(entry);
                            p["@groupKey"] = key;
                            DatabaseQuery.ExecuteNonQuery("INSERT INTO BarConfigGroupOverrides (GroupKey, BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, IsHidden, VisibilityRelations, ShowPercent, VisibilityFade) VALUES (@groupKey, @type, @row, @pos, @seg, @en, @hidden, @relations, @sp, @fade);", p);
                        }
                    }
                }
            }
            catch (Exception ex) { DebugLogger.PrintError($"BarConfigManager.Save failed: {ex.Message}"); }
            SaveBarsVisible();
        }

        public static void Invalidate()
        {
            _loaded = false;
            _global = null;
            _groupOverrides.Clear();
            _resolvedEntriesByGroup.Clear();
            _groupedRowsByGroup.Clear();
        }

        public static void SaveBarsVisible()
        {
            try
            {
                DatabaseQuery.ExecuteNonQuery("INSERT OR REPLACE INTO GeneralSettings (SettingKey, Value) VALUES ('BarsVisible', @v);", new Dictionary<string, object> { ["@v"] = BarsVisible ? "true" : "false" });
            }
            catch (Exception ex) { DebugLogger.PrintError($"BarConfigManager.SaveBarsVisible failed: {ex.Message}"); }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            Load();
            LoadBarsVisible();
            _loaded = true;
        }

        private static void Load()
        {
            _global = new();
            _groupOverrides.Clear();
            _resolvedEntriesByGroup.Clear();
            _groupedRowsByGroup.Clear();
            try
            {
                var rows = DatabaseQuery.ExecuteQuery("SELECT BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, COALESCE(IsHidden, 0) AS IsHidden, COALESCE(VisibilityRelations, '') AS VisibilityRelations, COALESCE(ShowPercent, 0) AS ShowPercent, COALESCE(VisibilityFade, '') AS VisibilityFade FROM BarConfig ORDER BY BarRow, PositionInRow;");
                foreach (Dictionary<string, object> row in rows)
                    if (TryParseBarEntry(row, out BarEntry entry))
                        _global.Add(entry);
            }
            catch (Exception ex) { DebugLogger.PrintError($"BarConfigManager.Load failed: {ex.Message}"); }
            _global = _global.Count == 0 ? GetDefaults() : NormalizeEntries(_global);
            LoadGroupOverrides();
            RebuildResolvedGroupCaches();
        }

        private static void LoadBarsVisible()
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery("SELECT Value FROM GeneralSettings WHERE SettingKey = 'BarsVisible';");
                if (rows.Count > 0) BarsVisible = !string.Equals(rows[0]["Value"]?.ToString(), "false", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        private static void LoadGroupOverrides()
        {
            if (!TableExists("BarConfigGroupOverrides")) return;
            try
            {
                var rows = DatabaseQuery.ExecuteQuery("SELECT GroupKey, BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, COALESCE(IsHidden, 0) AS IsHidden, COALESCE(VisibilityRelations, '') AS VisibilityRelations, COALESCE(ShowPercent, 0) AS ShowPercent, COALESCE(VisibilityFade, '') AS VisibilityFade FROM BarConfigGroupOverrides ORDER BY GroupKey, BarRow, PositionInRow;");
                foreach (Dictionary<string, object> row in rows)
                {
                    string key = NormalizeGroupKey(row.TryGetValue("GroupKey", out object value) ? value?.ToString() : null);
                    if (string.Equals(key, AllDestructiblesGroupKey, StringComparison.OrdinalIgnoreCase) || !TryParseBarEntry(row, out BarEntry entry)) continue;
                    if (!_groupOverrides.TryGetValue(key, out Dictionary<BarType, BarEntry> overrides))
                    {
                        overrides = new();
                        _groupOverrides[key] = overrides;
                    }
                    overrides[entry.Type] = entry;
                }
            }
            catch (Exception ex) { DebugLogger.PrintError($"BarConfigManager.LoadGroupOverrides failed: {ex.Message}"); }
        }

        private static void RebuildResolvedGroupCaches()
        {
            _resolvedEntriesByGroup.Clear();
            _groupedRowsByGroup.Clear();
            List<BarEntry> baseEntries = NormalizeEntries(_global);
            _resolvedEntriesByGroup[AllDestructiblesGroupKey] = baseEntries;
            _groupedRowsByGroup[AllDestructiblesGroupKey] = BuildGroupedRows(baseEntries);
            foreach (BarConfigGroupDefinition def in GroupDefinitions.OrderBy(x => x.RenderOrder))
            {
                if (string.Equals(def.Key, AllDestructiblesGroupKey, StringComparison.OrdinalIgnoreCase)) continue;
                List<BarEntry> entries = CloneEntries(_resolvedEntriesByGroup[NormalizeGroupKey(def.ParentKey)]);
                if (_groupOverrides.TryGetValue(def.Key, out Dictionary<BarType, BarEntry> overrides))
                {
                    foreach (BarEntry overrideEntry in overrides.Values)
                    {
                        int idx = entries.FindIndex(e => e.Type == overrideEntry.Type);
                        if (idx >= 0) entries[idx] = overrideEntry.Clone();
                        else entries.Add(overrideEntry.Clone());
                    }
                }
                entries = NormalizeEntries(entries);
                _resolvedEntriesByGroup[def.Key] = entries;
                _groupedRowsByGroup[def.Key] = BuildGroupedRows(entries);
            }
        }

        private static Dictionary<BarType, BarEntry> BuildOverrideMap(string groupKey, List<BarEntry> resolvedEntries)
        {
            var overrides = new Dictionary<BarType, BarEntry>();
            IReadOnlyList<BarEntry> parentEntries = GetResolvedEntries(GetParentGroupKey(groupKey));
            foreach (BarEntry entry in resolvedEntries)
            {
                BarEntry parentEntry = parentEntries.FirstOrDefault(x => x.Type == entry.Type) ?? GetDefaults().First(x => x.Type == entry.Type);
                if (!AreEntriesEquivalent(entry, parentEntry))
                    overrides[entry.Type] = entry.Clone();
            }
            return overrides;
        }

        private static List<BarEntry> NormalizeEntries(IEnumerable<BarEntry> configs)
        {
            Dictionary<BarType, BarEntry> byType = new();
            if (configs != null)
                foreach (BarEntry entry in configs)
                    if (entry != null)
                        byType[entry.Type] = entry.Clone();
            foreach (BarEntry entry in GetDefaults())
                if (!byType.ContainsKey(entry.Type))
                    byType[entry.Type] = entry.Clone();
            return byType.Values.OrderBy(e => e.BarRow).ThenBy(e => e.PositionInRow).ThenBy(e => e.Type.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<List<BarEntry>> BuildGroupedRows(IEnumerable<BarEntry> entries)
        {
            List<BarEntry> normalized = NormalizeEntries(entries);
            return normalized.Select(e => e.BarRow).Distinct().OrderBy(x => x).Select(row => normalized.Where(e => e.BarRow == row).OrderBy(e => e.PositionInRow).ToList()).ToList();
        }

        private static Dictionary<string, object> BuildBarEntryParameters(BarEntry entry) => new()
        {
            ["@row"] = entry.BarRow,
            ["@pos"] = entry.PositionInRow,
            ["@seg"] = entry.SegmentCount,
            ["@en"] = entry.SegmentsEnabled ? 1 : 0,
            ["@hidden"] = entry.IsHidden ? 1 : 0,
            ["@relations"] = EncodeVisibilityRelations(entry.VisibilityRelations),
            ["@sp"] = entry.ShowPercent ? 1 : 0,
            ["@fade"] = EncodeVisibilityFade(entry.VisibilityFadeOutSeconds),
            ["@type"] = entry.Type.ToString()
        };

        private static bool TryParseBarEntry(Dictionary<string, object> row, out BarEntry entry)
        {
            entry = null;
            if (row == null || !Enum.TryParse(row["BarType"]?.ToString(), out BarType type)) return false;
            entry = new()
            {
                Type = type,
                BarRow = Convert.ToInt32(row["BarRow"]),
                PositionInRow = Convert.ToInt32(row["PositionInRow"]),
                SegmentCount = Convert.ToInt32(row["SegmentCount"]),
                SegmentsEnabled = Convert.ToBoolean(Convert.ToInt32(row["SegmentsEnabled"])),
                IsHidden = Convert.ToInt32(row["IsHidden"]) != 0,
                VisibilityRelations = DecodeVisibilityRelations(row["VisibilityRelations"]?.ToString()),
                ShowPercent = Convert.ToInt32(row["ShowPercent"]) != 0,
                VisibilityFadeOutSeconds = DecodeVisibilityFade(row["VisibilityFade"]?.ToString())
            };
            return true;
        }

        private static string GetParentGroupKey(string groupKey)
        {
            string key = NormalizeGroupKey(groupKey);
            return GroupDefinitionsByKey.TryGetValue(key, out BarConfigGroupDefinition def) && !string.IsNullOrWhiteSpace(def.ParentKey)
                ? NormalizeGroupKey(def.ParentKey) : AllDestructiblesGroupKey;
        }

        private static bool IsGroupInInheritanceChain(string descendantGroupKey, string ancestorGroupKey)
        {
            string current = NormalizeGroupKey(descendantGroupKey);
            string target = NormalizeGroupKey(ancestorGroupKey);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return true;
                if (!GroupDefinitionsByKey.TryGetValue(current, out BarConfigGroupDefinition def) || string.IsNullOrWhiteSpace(def.ParentKey)) break;
                current = NormalizeGroupKey(def.ParentKey);
            }
            return string.Equals(target, AllDestructiblesGroupKey, StringComparison.OrdinalIgnoreCase);
        }

        private static bool AreEntriesEquivalent(BarEntry a, BarEntry b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            return a.Type == b.Type &&
                a.BarRow == b.BarRow &&
                a.PositionInRow == b.PositionInRow &&
                a.SegmentCount == b.SegmentCount &&
                a.SegmentsEnabled == b.SegmentsEnabled &&
                a.IsHidden == b.IsHidden &&
                a.ShowPercent == b.ShowPercent &&
                MathF.Abs(a.VisibilityFadeOutSeconds - b.VisibilityFadeOutSeconds) <= 0.0001f &&
                AreRelationsEquivalent(a.VisibilityRelations, b.VisibilityRelations);
        }

        private static bool AreRelationsEquivalent(IEnumerable<BarRelation> a, IEnumerable<BarRelation> b)
        {
            List<BarRelation> left = a?.Where(x => x != null).OrderBy(x => Array.IndexOf(RelationSourceOrder, x.SourceType)).ThenBy(x => (int)x.RelationName).ToList() ?? new();
            List<BarRelation> right = b?.Where(x => x != null).OrderBy(x => Array.IndexOf(RelationSourceOrder, x.SourceType)).ThenBy(x => (int)x.RelationName).ToList() ?? new();
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i].SourceType != right[i].SourceType || left[i].RelationName != right[i].RelationName)
                    return false;
            return true;
        }

        private static void SortRelations(BarEntry entry)
        {
            entry.VisibilityRelations = entry.VisibilityRelations.Where(r => r != null).OrderBy(r => Array.IndexOf(RelationSourceOrder, r.SourceType)).ThenBy(r => (int)r.RelationName).ToList();
        }

        private static List<BarEntry> CloneEntries(IEnumerable<BarEntry> entries) => entries?.Select(x => x?.Clone()).Where(x => x != null).ToList() ?? new();
        private static List<BarRelation> CloneRelations(IEnumerable<BarRelation> relations) => relations?.Select(x => x?.Clone()).Where(x => x != null).ToList() ?? new();

        private static bool TableExists(string tableName)
        {
            try
            {
                return DatabaseQuery.ExecuteQuery("SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;", new Dictionary<string, object> { ["@tableName"] = tableName }).Count > 0;
            }
            catch { return false; }
        }
    }
}
