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
        DragBarBackground,
        TextPrimary,
        TextMuted,
        Accent,
        AccentSoft,
        OverlayBackground,
        DragBarHoverTint,
        ResizeEdge,
        ResizeEdgeHover,
        ResizeEdgeActive,
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
        private const string ActiveSchemeRowKey = "__ActiveScheme";
        private const string SchemePrefix = "Scheme:";
        private const string SchemeDelimiter = "::";
        public const string DefaultSchemeName = "DarkMode";
        private const string LegacyDefaultSchemeName = "DefaultScheme";
        public const string LightSchemeName = "LightMode";
        private static string _activeSchemeName = DefaultSchemeName;
        private static readonly Dictionary<string, ColorRole> _legacyRoleMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "DangerBackground", ColorRole.CloseBackground },
            { "DangerHoverBackground", ColorRole.CloseHoverBackground },
            { "DangerBorder", ColorRole.CloseBorder },
            { "DangerHoverBorder", ColorRole.CloseHoverBorder },
            { "DangerOverlayBackground", ColorRole.CloseOverlayBackground },
            { "DangerOverlayHoverBackground", ColorRole.CloseOverlayHoverBackground },
            { "DangerOverlayBorder", ColorRole.CloseOverlayBorder },
            { "HeaderBackground", ColorRole.DragBarBackground },
            { "ResizeBar", ColorRole.ResizeEdge },
            { "ResizeBarHover", ColorRole.ResizeEdgeHover },
            { "ResizeBarActive", ColorRole.ResizeEdgeActive }
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

        public static string ActiveSchemeName
        {
            get
            {
                EnsureInitialized();
                return _activeSchemeName;
            }
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

        public static IReadOnlyList<string> GetAvailableSchemeNames()
        {
            EnsureInitialized();
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(_activeSchemeName))
            {
                names.Add(_activeSchemeName);
            }

            names.Add(DefaultSchemeName);

            Dictionary<string, string> storedData = BlockDataStore.LoadRowData(DockBlockKind.ColorScheme);
            foreach (string rowKey in storedData.Keys)
            {
                if (TryParseSchemeRowKey(rowKey, out string schemeName, out _))
                {
                    names.Add(schemeName);
                }
            }

            return names.OrderBy(n => n).ToList();
        }

        public static bool SaveCurrentScheme(string schemeName, bool makeActive = true)
        {
            EnsureInitialized();
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            Dictionary<ColorRole, Color> palette = CaptureCurrentPalette();
            SaveSchemePalette(normalized, palette);

            if (makeActive)
            {
                _activeSchemeName = normalized;
                PersistActiveSchemeName(normalized);
            }

            return true;
        }

        public static bool DeleteScheme(string schemeName)
        {
            EnsureInitialized();
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (string.Equals(normalized, DefaultSchemeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, LightSchemeName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Dictionary<string, string> storedData = BlockDataStore.LoadRowData(DockBlockKind.ColorScheme);
            List<string> rowsToDelete = storedData.Keys
                .Where(k => TryParseSchemeRowKey(k, out string scheme, out _) && scheme.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rowsToDelete.Count == 0)
            {
                return false;
            }

            BlockDataStore.DeleteRows(DockBlockKind.ColorScheme, rowsToDelete);

            bool wasActive = string.Equals(_activeSchemeName, normalized, StringComparison.OrdinalIgnoreCase);
            if (wasActive)
            {
                string fallback = ResolveFallbackScheme(normalized);
                _activeSchemeName = fallback;
                PersistActiveSchemeName(_activeSchemeName);
                TryLoadScheme(_activeSchemeName, persistActive: false, applySideEffects: true);
            }

            return true;
        }

        public static bool TryLoadScheme(string schemeName, bool persistActive = true, bool applySideEffects = true)
        {
            EnsureInitialized();
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (!TryReadSchemeColors(normalized, out Dictionary<ColorRole, Color> palette))
            {
                return false;
            }

            foreach (var pair in palette)
            {
                if (_options.TryGetValue(pair.Key, out ColorOption option))
                {
                    option.Set(pair.Value);
                }
            }

            if (persistActive)
            {
                foreach (var pair in palette)
                {
                    SaveColor(pair.Key, pair.Value);
                }

                PersistActiveSchemeName(normalized);
                _activeSchemeName = normalized;
            }

            if (applySideEffects)
            {
                ApplySideEffects();
            }

            return true;
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
            _activeSchemeName = DefaultSchemeName;
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
            Add(new ColorOption(ColorRole.DragBarBackground, "Drag bar background", "UI", new Color(35, 35, 35)));
            Add(new ColorOption(ColorRole.TextPrimary, "Text primary", "UI", new Color(226, 226, 226)));
            Add(new ColorOption(ColorRole.TextMuted, "Text muted", "UI", new Color(160, 160, 160)));
            Add(new ColorOption(ColorRole.Accent, "Accent", "UI", new Color(110, 142, 255)));
            Add(new ColorOption(ColorRole.AccentSoft, "Accent soft", "UI", new Color(110, 142, 255, 70)));
            Add(new ColorOption(ColorRole.OverlayBackground, "Overlay background", "UI", new Color(24, 24, 24, 230)));
            Add(new ColorOption(ColorRole.DragBarHoverTint, "Drag bar hover tint", "UI", new Color(26, 26, 26, 120)));

            Add(new ColorOption(ColorRole.ResizeEdge, "Resize edge", "UI", new Color(58, 58, 58, 210)));
            Add(new ColorOption(ColorRole.ResizeEdgeHover, "Resize edge hover", "UI", new Color(110, 142, 255, 150)));
            Add(new ColorOption(ColorRole.ResizeEdgeActive, "Resize edge active", "UI", new Color(110, 142, 255, 220)));

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
            string storedActiveScheme = ResolveActiveSchemeName(storedData);
            Dictionary<string, bool> storedLocks = BlockDataStore.LoadRowLocks(DockBlockKind.ColorScheme);
            if (storedData.Count > 0)
            {
                foreach (var pair in storedData)
                {
                    if (string.Equals(pair.Key, ActiveSchemeRowKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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

            if (!string.IsNullOrWhiteSpace(storedActiveScheme))
            {
                _activeSchemeName = storedActiveScheme;
            }

            if (!TryLoadScheme(_activeSchemeName, persistActive: false, applySideEffects: false))
            {
                _activeSchemeName = DefaultSchemeName;
            }

            EnsureDefaultSchemesSeeded();
        }

        private static string ResolveActiveSchemeName(Dictionary<string, string> storedData)
        {
            if (storedData == null)
            {
                return null;
            }

            if (storedData.TryGetValue(ActiveSchemeRowKey, out string rawName) && !string.IsNullOrWhiteSpace(rawName))
            {
                return NormalizeSchemeName(rawName);
            }

            return null;
        }

        private static void EnsureDefaultSchemesSeeded()
        {
            try
            {
                EnsureNamedSchemeExists(DefaultSchemeName, CaptureCurrentPalette());
                EnsureNamedSchemeExists(LightSchemeName, GetLightModeDefaults());
                if (string.IsNullOrWhiteSpace(_activeSchemeName))
                {
                    _activeSchemeName = DefaultSchemeName;
                }

                PersistActiveSchemeName(_activeSchemeName);
            }
            catch (Exception ex)
            {
                SafeLog($"EnsureDefaultSchemesSeeded failed: {ex.Message}");
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

        private static void EnsureNamedSchemeExists(string schemeName, IDictionary<ColorRole, Color> palette)
        {
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized) || palette == null)
            {
                return;
            }

            if (TryReadSchemeColors(normalized, out Dictionary<ColorRole, Color> existing) && existing.Count > 0)
            {
                return;
            }

            SaveSchemePalette(normalized, palette);
        }

        private static Dictionary<ColorRole, Color> CaptureCurrentPalette()
        {
            Dictionary<ColorRole, Color> palette = new();
            foreach (var pair in _options)
            {
                palette[pair.Key] = pair.Value.Value;
            }

            return palette;
        }

        private static Dictionary<ColorRole, Color> GetLightModeDefaults()
        {
            return new Dictionary<ColorRole, Color>
            {
                { ColorRole.TransparentWindowKey, new Color(255, 105, 180) },
                { ColorRole.DefaultFallback, new Color(255, 105, 179) },
                { ColorRole.GameBackground, new Color(245, 245, 248) },
                { ColorRole.ScreenBackground, new Color(250, 250, 252) },
                { ColorRole.BlockBackground, new Color(255, 255, 255) },
                { ColorRole.BlockBorder, new Color(215, 218, 226) },
                { ColorRole.DragBarBackground, new Color(240, 242, 248) },
                { ColorRole.TextPrimary, new Color(32, 36, 48) },
                { ColorRole.TextMuted, new Color(96, 104, 128) },
                { ColorRole.Accent, new Color(74, 108, 210) },
                { ColorRole.AccentSoft, new Color(74, 108, 210, 60) },
                { ColorRole.OverlayBackground, new Color(240, 240, 245, 230) },
                { ColorRole.DragBarHoverTint, new Color(226, 230, 240, 160) },
                { ColorRole.ResizeEdge, new Color(210, 214, 222, 210) },
                { ColorRole.ResizeEdgeHover, new Color(74, 108, 210, 150) },
                { ColorRole.ResizeEdgeActive, new Color(74, 108, 210, 220) },
                { ColorRole.ButtonNeutral, new Color(246, 247, 250, 255) },
                { ColorRole.ButtonNeutralHover, new Color(232, 236, 244, 255) },
                { ColorRole.ButtonPrimary, new Color(74, 108, 210, 235) },
                { ColorRole.ButtonPrimaryHover, new Color(92, 126, 230, 240) },
                { ColorRole.RowHover, new Color(236, 240, 248, 200) },
                { ColorRole.RowDragging, new Color(226, 230, 240, 240) },
                { ColorRole.DropIndicator, new Color(74, 108, 210, 110) },
                { ColorRole.ToggleIdle, new Color(232, 236, 244, 190) },
                { ColorRole.ToggleHover, new Color(74, 108, 210, 200) },
                { ColorRole.ToggleActive, new Color(74, 108, 210, 230) },
                { ColorRole.RebindScrim, new Color(240, 240, 245, 190) },
                { ColorRole.Warning, new Color(200, 160, 32) },
                { ColorRole.ScrollTrack, new Color(232, 236, 244, 220) },
                { ColorRole.ScrollThumb, new Color(180, 184, 195, 255) },
                { ColorRole.ScrollThumbHover, new Color(160, 168, 182, 255) },
                { ColorRole.IndicatorActive, new Color(38, 160, 75) },
                { ColorRole.IndicatorInactive, new Color(210, 70, 56) },
                { ColorRole.CloseBackground, new Color(255, 232, 232, 240) },
                { ColorRole.CloseHoverBackground, new Color(255, 220, 220, 255) },
                { ColorRole.CloseBorder, new Color(220, 140, 140) },
                { ColorRole.CloseHoverBorder, new Color(240, 120, 120) },
                { ColorRole.CloseOverlayBackground, new Color(255, 236, 236, 245) },
                { ColorRole.CloseOverlayHoverBackground, new Color(250, 222, 222, 245) },
                { ColorRole.CloseOverlayBorder, new Color(220, 140, 140) },
                { ColorRole.LockLockedFill, new Color(230, 230, 235, 230) },
                { ColorRole.LockLockedHoverFill, new Color(226, 226, 232, 240) },
                { ColorRole.LockUnlockedFill, new Color(74, 108, 210, 230) },
                { ColorRole.LockUnlockedHoverFill, new Color(74, 108, 210, 245) },
                { ColorRole.CloseGlyph, new Color(200, 70, 52) },
                { ColorRole.CloseGlyphHover, new Color(255, 255, 255) }
            };
        }

        private static bool TryReadSchemeColors(string schemeName, out Dictionary<ColorRole, Color> palette)
        {
            palette = new Dictionary<ColorRole, Color>();
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            Dictionary<string, string> storedData = BlockDataStore.LoadRowData(DockBlockKind.ColorScheme);
            foreach (var pair in storedData)
            {
                if (TryParseSchemeRowKey(pair.Key, out string name, out ColorRole role) &&
                    name.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
                    TryParseHex(pair.Value, out Color parsed))
                {
                    palette[role] = parsed;
                }
            }

            if (palette.Count == 0)
            {
                return false;
            }

            foreach (ColorRole role in _options.Keys)
            {
                if (!palette.ContainsKey(role))
                {
                    palette[role] = _options[role].Value;
                }
            }

            return true;
        }

        private static void SaveSchemePalette(string schemeName, IDictionary<ColorRole, Color> palette)
        {
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized) || palette == null)
            {
                return;
            }

            foreach (var pair in palette)
            {
                string rowKey = EncodeSchemeRowKey(normalized, pair.Key);
                BlockDataStore.SetRowData(DockBlockKind.ColorScheme, rowKey, ToHex(pair.Value));
            }
        }

        private static string NormalizeSchemeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            trimmed = trimmed.Replace(SchemeDelimiter, "-").Replace(":", "-");
            if (string.Equals(trimmed, LegacyDefaultSchemeName, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = DefaultSchemeName;
            }
            return trimmed;
        }

        private static string EncodeSchemeRowKey(string schemeName, ColorRole role)
        {
            return $"{SchemePrefix}{schemeName}{SchemeDelimiter}{role}";
        }

        private static bool TryParseSchemeRowKey(string key, out string schemeName, out ColorRole role)
        {
            schemeName = null;
            role = default;
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string remainder = key[SchemePrefix.Length..];
            int separatorIndex = remainder.IndexOf(SchemeDelimiter, StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                return false;
            }

            string name = NormalizeSchemeName(remainder[..separatorIndex]);
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            string roleKey = remainder[(separatorIndex + SchemeDelimiter.Length)..];
            if (Enum.TryParse(roleKey, out role))
            {
                schemeName = name;
                return true;
            }

            return false;
        }

        private static void PersistActiveSchemeName(string schemeName)
        {
            string normalized = NormalizeSchemeName(schemeName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            BlockDataStore.SetRowData(DockBlockKind.ColorScheme, ActiveSchemeRowKey, normalized);
        }

        private static string ResolveFallbackScheme(string deletedName)
        {
            Dictionary<string, string> remainingData = BlockDataStore.LoadRowData(DockBlockKind.ColorScheme);
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase)
            {
                DefaultSchemeName,
                LightSchemeName
            };

            foreach (string key in remainingData.Keys)
            {
                if (TryParseSchemeRowKey(key, out string scheme, out _))
                {
                    names.Add(scheme);
                }
            }

            return names
                .Where(name => !string.Equals(name, deletedName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? DefaultSchemeName;
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
