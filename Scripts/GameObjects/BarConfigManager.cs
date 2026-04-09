using System;
using System.Collections.Generic;
using System.Linq;

namespace op.io
{
    /// <summary>
    /// Manages how health, shield, and XP bars are arranged into rows and how many
    /// segments each uses. Replaces the old CombineHealthShieldBar toggle with a
    /// full row-based system: bars sharing the same BarRow render side-by-side.
    ///
    /// Two-tier state:
    ///   Global — what HealthBarManager and the Properties panel read (in-game display).
    ///   Local  — used exclusively by BarsBlock for preview; does not affect global display
    ///            until the user clicks Apply or Save.
    /// </summary>
    public static class BarConfigManager
    {
        // ── Config entry ────────────────────────────────────────────────────────

        public class BarEntry
        {
            public BarType Type           { get; set; }
            public int     BarRow         { get; set; }  // vertical stacking order (0 = topmost)
            public int     PositionInRow  { get; set; }  // left-to-right order within a row
            public int     SegmentCount   { get; set; }  // health pts per segment (0 = no segs)
            public bool    SegmentsEnabled{ get; set; }
            public bool    IsHidden       { get; set; }  // when true, not rendered in-game
            public bool    ShowPercent    { get; set; }  // when true, draw "XX%" text centered on the bar

            public BarEntry Clone() => new()
            {
                Type            = Type,
                BarRow          = BarRow,
                PositionInRow   = PositionInRow,
                SegmentCount    = SegmentCount,
                SegmentsEnabled = SegmentsEnabled,
                IsHidden        = IsHidden,
                ShowPercent     = ShowPercent
            };
        }

        // ── State ───────────────────────────────────────────────────────────────

        private static List<BarEntry> _global;
        private static bool _loaded;

        /// <summary>Master on/off for all bar rendering. Persisted to GeneralSettings.</summary>
        public static bool BarsVisible { get; set; } = true;

        // ── Default layout ──────────────────────────────────────────────────────

        public static List<BarEntry> GetDefaults() => new()
        {
            new BarEntry { Type = BarType.Shield,      BarRow = 0, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true,  IsHidden = false },
            new BarEntry { Type = BarType.Health,      BarRow = 1, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true,  IsHidden = false },
            new BarEntry { Type = BarType.XP,          BarRow = 2, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true,  IsHidden = true  },
            new BarEntry { Type = BarType.HealthRegen, BarRow = 3, PositionInRow = 0, SegmentCount = 10, SegmentsEnabled = true,  IsHidden = true  },
            new BarEntry { Type = BarType.ShieldRegen, BarRow = 3, PositionInRow = 1, SegmentCount = 10, SegmentsEnabled = true,  IsHidden = true  },
        };

        // ── Global access ───────────────────────────────────────────────────────

        public static IReadOnlyList<BarEntry> Global
        {
            get { EnsureLoaded(); return _global; }
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
            _global = new List<BarEntry>();
            try
            {
                var results = DatabaseQuery.ExecuteQuery(
                    "SELECT BarType, BarRow, PositionInRow, SegmentCount, SegmentsEnabled, " +
                    "COALESCE(IsHidden, 0) AS IsHidden, COALESCE(ShowPercent, 0) AS ShowPercent " +
                    "FROM BarConfig ORDER BY BarRow, PositionInRow;");

                foreach (var row in results)
                {
                    if (!Enum.TryParse<BarType>(row["BarType"]?.ToString(), out var bt)) continue;
                    _global.Add(new BarEntry
                    {
                        Type            = bt,
                        BarRow          = Convert.ToInt32(row["BarRow"]),
                        PositionInRow   = Convert.ToInt32(row["PositionInRow"]),
                        SegmentCount    = Convert.ToInt32(row["SegmentCount"]),
                        SegmentsEnabled = Convert.ToBoolean(Convert.ToInt32(row["SegmentsEnabled"])),
                        IsHidden        = Convert.ToInt32(row["IsHidden"]) != 0,
                        ShowPercent     = Convert.ToInt32(row["ShowPercent"]) != 0
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"BarConfigManager.Load failed: {ex.Message}");
            }

            if (_global.Count == 0)
                _global = GetDefaults();
        }

        private static void LoadBarsVisible()
        {
            try
            {
                var rows = DatabaseQuery.ExecuteQuery(
                    "SELECT Value FROM GeneralSettings WHERE SettingKey = 'BarsVisible';");
                if (rows.Count > 0)
                    BarsVisible = !string.Equals(rows[0]["Value"]?.ToString(), "false",
                        System.StringComparison.OrdinalIgnoreCase);
            }
            catch { /* keep default true */ }
        }

        /// <summary>Persists BarsVisible to GeneralSettings.</summary>
        public static void SaveBarsVisible()
        {
            try
            {
                DatabaseQuery.ExecuteNonQuery(
                    "INSERT OR REPLACE INTO GeneralSettings (SettingKey, Value) VALUES ('BarsVisible', @v);",
                    new Dictionary<string, object> { ["@v"] = BarsVisible ? "true" : "false" });
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"BarConfigManager.SaveBarsVisible failed: {ex.Message}");
            }
        }

        // ── Queries ─────────────────────────────────────────────────────────────

        /// <summary>Deep copy of the current global configs.</summary>
        public static List<BarEntry> CloneGlobal()
        {
            EnsureLoaded();
            return _global.Select(e => e.Clone()).ToList();
        }

        /// <summary>Config for a specific bar type (falls back to default if missing).</summary>
        public static BarEntry Get(BarType type)
        {
            EnsureLoaded();
            return _global.FirstOrDefault(e => e.Type == type)
                ?? GetDefaults().First(e => e.Type == type);
        }

        /// <summary>
        /// Segment count to pass to bar-drawing functions.
        /// Returns 0 when segments are disabled so callers can skip tick rendering.
        /// </summary>
        public static int GetSegmentCount(BarType type)
        {
            var e = Get(type);
            return e.SegmentsEnabled ? Math.Max(1, e.SegmentCount) : 0;
        }

        /// <summary>
        /// Returns entries grouped by bar row (ascending), each sub-list sorted by
        /// PositionInRow. Useful for rendering both in-game bars and BarsBlock.
        /// </summary>
        public static List<List<BarEntry>> GetGroupedByRow()
        {
            EnsureLoaded();
            var rowIndices = _global.Select(e => e.BarRow).Distinct().OrderBy(r => r).ToList();
            return rowIndices
                .Select(r => _global.Where(e => e.BarRow == r).OrderBy(e => e.PositionInRow).ToList())
                .ToList();
        }

        // ── Mutations ───────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces the in-memory global configs without writing to the database.
        /// Used by BarsBlock "Apply" to update in-game display immediately.
        /// </summary>
        public static void ApplyGlobal(List<BarEntry> configs)
        {
            _global = configs.Select(e => e.Clone()).ToList();
            _loaded = true;
        }

        /// <summary>
        /// Applies configs globally AND persists them to the database.
        /// Used by BarsBlock "Save".
        /// </summary>
        public static void Save(List<BarEntry> configs)
        {
            ApplyGlobal(configs);
            try
            {
                foreach (var entry in configs)
                {
                    DatabaseQuery.ExecuteNonQuery(
                        "UPDATE BarConfig " +
                        "SET BarRow=@row, PositionInRow=@pos, SegmentCount=@seg, SegmentsEnabled=@en, IsHidden=@hidden, ShowPercent=@sp " +
                        "WHERE BarType=@type;",
                        new Dictionary<string, object>
                        {
                            ["@row"]    = entry.BarRow,
                            ["@pos"]    = entry.PositionInRow,
                            ["@seg"]    = entry.SegmentCount,
                            ["@en"]     = entry.SegmentsEnabled ? 1 : 0,
                            ["@hidden"] = entry.IsHidden ? 1 : 0,
                            ["@sp"]     = entry.ShowPercent ? 1 : 0,
                            ["@type"]   = entry.Type.ToString()
                        });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.PrintError($"BarConfigManager.Save failed: {ex.Message}");
            }
            SaveBarsVisible();
        }

        /// <summary>Forces a reload from the database on next access.</summary>
        public static void Invalidate()
        {
            _loaded = false;
            _global = null;
        }
    }
}
