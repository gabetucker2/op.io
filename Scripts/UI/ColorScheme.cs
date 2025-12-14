using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io
{
    public enum ColorRole
    {
        TransparentWindowKey,
        DefaultFallback,
        GameBackground,
        ScreenBackground,
        BlockBackground,
        BlockBorder,
        HeaderBackground,
        TextPrimary,
        TextMuted,
        Accent,
        AccentSoft,
        OverlayBackground,
        DragBarHoverTint,
        ResizeBar,
        ResizeBarHover,
        ResizeBarActive,
        ButtonNeutral,
        ButtonNeutralHover,
        ButtonPrimary,
        ButtonPrimaryHover,
        RowHover,
        RowDragging,
        DropIndicator,
        ToggleIdle,
        ToggleHover,
        ToggleActive,
        RebindScrim,
        Warning,
        ScrollTrack,
        ScrollThumb,
        ScrollThumbHover,
        IndicatorActive,
        IndicatorInactive,
        CloseBackground,
        CloseHoverBackground,
        CloseBorder,
        CloseHoverBorder,
        CloseOverlayBackground,
        CloseOverlayHoverBackground,
        CloseOverlayBorder,
        LockLockedFill,
        LockLockedHoverFill,
        LockUnlockedFill,
        LockUnlockedHoverFill,
        CloseGlyph,
        CloseGlyphHover
    }

    public sealed class ColorOption
    {
        public ColorOption(ColorRole role, string label, string category, Color defaultColor, string description = null, bool isLockedByDefault = false)
        {
            Role = role;
            Label = label ?? role.ToString();
            Category = category ?? string.Empty;
            DefaultColor = defaultColor;
            Description = description ?? string.Empty;
            Value = defaultColor;
            DefaultLocked = isLockedByDefault;
            IsLocked = isLockedByDefault;
        }

        public ColorRole Role { get; }
        public string Key => Role.ToString();
        public string Label { get; }
        public string Category { get; }
        public string Description { get; }
        public Color DefaultColor { get; }
        public Color Value { get; private set; }
        public bool IsLocked { get; private set; }
        public bool DefaultLocked { get; }

        public void Set(Color color)
        {
            Value = color;
        }

        public void SetLock(bool isLocked)
        {
            IsLocked = isLocked;
        }
    }

    /// <summary>
    /// Central palette for every project color. All colors must flow through this class.
    /// Backed by BlockDataStore for persistence so the Color Scheme block can edit values.
    /// </summary>
    public static class ColorScheme
    {
        private static readonly Dictionary<ColorRole, ColorOption> _options = new();
        private static readonly List<ColorRole> _orderedRoles = new();
        private static bool _initialized;
        private static bool _importedLegacyBackground;
        private static readonly Dictionary<string, ColorRole> _legacyRoleMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "DangerBackground", ColorRole.CloseBackground },
            { "DangerHoverBackground", ColorRole.CloseHoverBackground },
            { "DangerBorder", ColorRole.CloseBorder },
            { "DangerHoverBorder", ColorRole.CloseHoverBorder },
            { "DangerOverlayBackground", ColorRole.CloseOverlayBackground },
            { "DangerOverlayHoverBackground", ColorRole.CloseOverlayHoverBackground },
            { "DangerOverlayBorder", ColorRole.CloseOverlayBorder }
        };

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true; // prevent re-entry during load
            SafeLog("ColorScheme.Initialize: start");
            SeedDefaults();
            SafeLog("ColorScheme.Initialize: after SeedDefaults");
            LoadFromStore();
            SafeLog("ColorScheme.Initialize: after LoadFromStore");
            ApplySideEffects();
            SafeLog("ColorScheme.Initialize: complete");
        }

        public static void InitializeWithDefaultsOnly()
        {
            if (_initialized)
            {
                return;
            }

            SeedDefaults();
            _initialized = true;
            ApplySideEffects();
        }

        public static IReadOnlyList<ColorOption> GetOrderedOptions()
        {
            EnsureInitialized();
            return _orderedRoles
                .Where(role => _options.ContainsKey(role))
                .Select(role => _options[role])
                .ToList();
        }

        public static Color GetColor(ColorRole role)
        {
            EnsureInitialized();
            if (_options.TryGetValue(role, out ColorOption option))
            {
                return option.Value;
            }

            return _options.TryGetValue(ColorRole.DefaultFallback, out ColorOption fallback)
                ? fallback.Value
                : new Color(255, 105, 179);
        }

        public static string GetHex(ColorRole role)
        {
            return ToHex(GetColor(role));
        }

        public static bool TryUpdateColor(ColorRole role, Color color, bool persist = true)
        {
            EnsureInitialized();
            if (!_options.TryGetValue(role, out ColorOption option))
            {
                return false;
            }

            option.Set(color);
            ApplySideEffects(role, color);

            if (persist)
            {
                SaveColor(role, color);
            }

            return true;
        }

        public static void UpdateOrder(IReadOnlyList<ColorRole> roles, bool persist = true)
        {
            EnsureInitialized();
            if (roles == null || roles.Count == 0)
            {
                return;
            }

            HashSet<ColorRole> seen = new();
            _orderedRoles.Clear();
            foreach (ColorRole role in roles)
            {
                if (_options.ContainsKey(role) && seen.Add(role))
                {
                    _orderedRoles.Add(role);
                }
            }

            // Append any options that were not included in the supplied order
            foreach (ColorRole role in _options.Keys)
            {
                if (seen.Add(role))
                {
                    _orderedRoles.Add(role);
                }
            }

            if (persist)
            {
                PersistOrder();
            }
        }

        public static bool TryParseHex(string value, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            if (trimmed.Length != 6 && trimmed.Length != 8)
            {
                return false;
            }

            bool parsed = uint.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out uint hex);
            if (!parsed)
            {
                return false;
            }

            byte a = 255;
            if (trimmed.Length == 8)
            {
                a = (byte)(hex & 0xFF);
                hex >>= 8;
            }

            byte b = (byte)(hex & 0xFF);
            byte g = (byte)((hex >> 8) & 0xFF);
            byte r = (byte)((hex >> 16) & 0xFF);

            color = new Color(r, g, b, a);
            return true;
        }

        public static string ToHex(Color color, bool includeAlpha = true)
        {
            return includeAlpha
                ? $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private static void SeedDefaults()
        {
            _options.Clear();
            _orderedRoles.Clear();

            // System / engine colors
            Add(new ColorOption(ColorRole.TransparentWindowKey, "Transparent window key", "System", new Color(255, 105, 180), "Window chroma key", isLockedByDefault: true));
            Add(new ColorOption(ColorRole.DefaultFallback, "Default fallback", "System", new Color(255, 105, 179), "Used as a last-resort color when lookups fail.", isLockedByDefault: true));
            Add(new ColorOption(ColorRole.GameBackground, "Game background", "System", new Color(20, 20, 25), "Backbuffer clear color."));

            // UI layout
            Add(new ColorOption(ColorRole.ScreenBackground, "Screen background", "UI", new Color(18, 18, 18)));
            Add(new ColorOption(ColorRole.BlockBackground, "Block background", "UI", new Color(26, 26, 26)));
            Add(new ColorOption(ColorRole.BlockBorder, "Block border", "UI", new Color(48, 48, 48)));
            Add(new ColorOption(ColorRole.HeaderBackground, "Header background", "UI", new Color(35, 35, 35)));
            Add(new ColorOption(ColorRole.TextPrimary, "Text primary", "UI", new Color(226, 226, 226)));
            Add(new ColorOption(ColorRole.TextMuted, "Text muted", "UI", new Color(160, 160, 160)));
            Add(new ColorOption(ColorRole.Accent, "Accent", "UI", new Color(110, 142, 255)));
            Add(new ColorOption(ColorRole.AccentSoft, "Accent soft", "UI", new Color(110, 142, 255, 70)));
            Add(new ColorOption(ColorRole.OverlayBackground, "Overlay background", "UI", new Color(24, 24, 24, 230)));
            Add(new ColorOption(ColorRole.DragBarHoverTint, "Drag bar hover tint", "UI", new Color(26, 26, 26, 120)));

            Add(new ColorOption(ColorRole.ResizeBar, "Resize bar", "UI", new Color(58, 58, 58, 210)));
            Add(new ColorOption(ColorRole.ResizeBarHover, "Resize bar hover", "UI", new Color(110, 142, 255, 150)));
            Add(new ColorOption(ColorRole.ResizeBarActive, "Resize bar active", "UI", new Color(110, 142, 255, 220)));

            // Buttons
            Add(new ColorOption(ColorRole.ButtonNeutral, "Button neutral", "Buttons", new Color(34, 34, 34, 230)));
            Add(new ColorOption(ColorRole.ButtonNeutralHover, "Button neutral hover", "Buttons", new Color(52, 52, 52, 240)));
            Add(new ColorOption(ColorRole.ButtonPrimary, "Button primary", "Buttons", new Color(58, 78, 150, 235)));
            Add(new ColorOption(ColorRole.ButtonPrimaryHover, "Button primary hover", "Buttons", new Color(86, 116, 204, 240)));

            // Lists and rows
            Add(new ColorOption(ColorRole.RowHover, "Row hover", "Lists", new Color(38, 38, 38, 180)));
            Add(new ColorOption(ColorRole.RowDragging, "Row dragging", "Lists", new Color(24, 24, 24, 220)));
            Add(new ColorOption(ColorRole.DropIndicator, "Drop indicator", "Lists", new Color(110, 142, 255, 90)));

            // Toggles and overlays
            Add(new ColorOption(ColorRole.ToggleIdle, "Toggle idle fill", "Controls", new Color(38, 38, 38, 140)));
            Add(new ColorOption(ColorRole.ToggleHover, "Toggle hover fill", "Controls", new Color(68, 92, 160, 200)));
            Add(new ColorOption(ColorRole.ToggleActive, "Toggle active fill", "Controls", new Color(68, 92, 160, 230)));
            Add(new ColorOption(ColorRole.RebindScrim, "Rebind scrim", "Controls", new Color(8, 8, 8, 190)));
            Add(new ColorOption(ColorRole.Warning, "Warning", "Controls", new Color(240, 196, 64)));

            // Scroll bars
            Add(new ColorOption(ColorRole.ScrollTrack, "Scroll track", "Scrollbars", new Color(24, 24, 24, 220)));
            Add(new ColorOption(ColorRole.ScrollThumb, "Scroll thumb", "Scrollbars", new Color(95, 95, 95, 255)));
            Add(new ColorOption(ColorRole.ScrollThumbHover, "Scroll thumb hover", "Scrollbars", new Color(136, 136, 136, 255)));

            // Indicators
            Add(new ColorOption(ColorRole.IndicatorActive, "Indicator active", "Indicators", new Color(72, 201, 115)));
            Add(new ColorOption(ColorRole.IndicatorInactive, "Indicator inactive", "Indicators", new Color(192, 57, 43)));

            // Close buttons
            Add(new ColorOption(ColorRole.CloseBackground, "Close button background", "Close button", new Color(80, 20, 20, 220)));
            Add(new ColorOption(ColorRole.CloseHoverBackground, "Close button hover background", "Close button", new Color(140, 32, 32, 240)));
            Add(new ColorOption(ColorRole.CloseBorder, "Close button border", "Close button", new Color(160, 40, 40)));
            Add(new ColorOption(ColorRole.CloseHoverBorder, "Close button hover border", "Close button", new Color(220, 72, 72)));

            Add(new ColorOption(ColorRole.CloseOverlayBackground, "Close overlay background", "Close overlay", new Color(64, 24, 24, 240)));
            Add(new ColorOption(ColorRole.CloseOverlayHoverBackground, "Close overlay hover", "Close overlay", new Color(90, 36, 36, 240)));
            Add(new ColorOption(ColorRole.CloseOverlayBorder, "Close overlay border", "Close overlay", new Color(150, 40, 40)));

            // Lock glyphs
            Add(new ColorOption(ColorRole.LockLockedFill, "Lock fill (locked)", "Locks", new Color(38, 38, 38, 220)));
            Add(new ColorOption(ColorRole.LockLockedHoverFill, "Lock fill (locked hover)", "Locks", new Color(38, 38, 38, 240)));
            Add(new ColorOption(ColorRole.LockUnlockedFill, "Lock fill (unlocked)", "Locks", new Color(68, 92, 160, 230)));
            Add(new ColorOption(ColorRole.LockUnlockedHoverFill, "Lock fill (unlocked hover)", "Locks", new Color(68, 92, 160, 250)));
            Add(new ColorOption(ColorRole.CloseGlyph, "Close glyph", "Locks", Color.OrangeRed));
            Add(new ColorOption(ColorRole.CloseGlyphHover, "Close glyph hover", "Locks", Color.White));
        }

        private static void Add(ColorOption option)
        {
            if (option == null)
            {
                return;
            }

            _options[option.Role] = option;
            _orderedRoles.Add(option.Role);
        }

        private static void LoadFromStore()
        {
            SafeLog("ColorScheme.LoadFromStore: EnsureTables start");
            BlockDataStore.EnsureTables(null, DockBlockKind.ColorScheme);
            SafeLog("ColorScheme.LoadFromStore: EnsureTables done");

            Dictionary<string, string> storedData = BlockDataStore.LoadRowData(DockBlockKind.ColorScheme);
            Dictionary<string, bool> storedLocks = BlockDataStore.LoadRowLocks(DockBlockKind.ColorScheme);
            if (storedData.Count > 0)
            {
                foreach (var pair in storedData)
                {
                    if (TryMapRole(pair.Key, out ColorRole targetRole) && _options.TryGetValue(targetRole, out ColorOption option))
                    {
                        if (TryParseHex(pair.Value, out Color parsed))
                        {
                            option.Set(parsed);
                        }
                    }
                }
            }
            else
            {
                ImportLegacyBackgroundIfNeeded();
            }

            SafeLog("ColorScheme.LoadFromStore: locks");
            Dictionary<ColorRole, bool> resolvedLocks = new();
            foreach (var pair in storedLocks)
            {
                if (TryMapRole(pair.Key, out ColorRole role))
                {
                    resolvedLocks[role] = pair.Value;
                }
            }

            foreach (ColorRole role in _options.Keys.ToList())
            {
                if (!_options.TryGetValue(role, out ColorOption option))
                {
                    continue;
                }

                bool storedLock = resolvedLocks.TryGetValue(role, out bool lockValue) && lockValue;
                option.SetLock(option.DefaultLocked || storedLock);
            }

            SafeLog("ColorScheme.LoadFromStore: orders");
            Dictionary<string, int> storedOrders = BlockDataStore.LoadRowOrders(DockBlockKind.ColorScheme);
            if (storedOrders.Count > 0)
            {
                List<ColorRole> ordered = storedOrders
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => TryMapRole(kvp.Key, out ColorRole role) ? role : (ColorRole?)null)
                    .Where(r => r.HasValue)
                    .Select(r => r.Value)
                    .ToList();
                UpdateOrder(ordered, persist: false);
            }
        }

        private static bool TryMapRole(string key, out ColorRole role)
        {
            role = default;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (_legacyRoleMappings.TryGetValue(key, out ColorRole legacyMapped))
            {
                role = legacyMapped;
                return true;
            }

            return Enum.TryParse(key, out role);
        }

        private static void ImportLegacyBackgroundIfNeeded()
        {
            if (_importedLegacyBackground)
            {
                return;
            }

            try
            {
                Color legacyBackground = DatabaseFetch.GetColor("GeneralSettings", "SettingKey", "BackgroundColor");
                if (legacyBackground != default)
                {
                    TryUpdateColor(ColorRole.GameBackground, legacyBackground, persist: false);
                }
            }
            catch
            {
                // If the DB isn't ready yet, just skip; defaults remain.
            }

            _importedLegacyBackground = true;
        }

        private static void ApplySideEffects()
        {
            foreach (ColorRole role in _options.Keys)
            {
                ApplySideEffects(role, _options[role].Value);
            }
            SafeLog("ColorScheme.ApplySideEffects: done");
        }

        private static void ApplySideEffects(ColorRole role, Color value)
        {
            switch (role)
            {
                case ColorRole.TransparentWindowKey:
                    Core.TransparentWindowColor = value;
                    GameInitializer.RefreshTransparencyKey();
                    break;
                case ColorRole.DefaultFallback:
                    Core.DefaultColor = value;
                    break;
                case ColorRole.GameBackground:
                    if (Core.Instance != null)
                    {
                        Core.Instance.BackgroundColor = value;
                    }
                    break;
            }
        }

        private static void SaveColor(ColorRole role, Color color)
        {
            BlockDataStore.SetRowData(DockBlockKind.ColorScheme, role.ToString(), ToHex(color));
        }

        private static void PersistOrder()
        {
            var rows = new List<(string RowKey, int Order)>();
            for (int i = 0; i < _orderedRoles.Count; i++)
            {
                rows.Add((_orderedRoles[i].ToString(), i + 1));
            }

            BlockDataStore.SaveRowOrders(DockBlockKind.ColorScheme, rows);
        }

        private static void SafeLog(string message)
        {
            try
            {
                System.IO.File.AppendAllText("run_output.txt", $"{DateTime.Now:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // ignore logging failures
            }
        }
    }
}
