using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using op.io.UI.BlockScripts.BlockUtilities;

namespace op.io
{
    internal static class AmbienceSettings
    {
        public const string FogOfWarRowKey = "AmbienceFogOfWarColor";
        public const string OceanWaterRowKey = "AmbienceOceanWaterColor";
        public const string BackgroundWavesRowKey = "AmbienceBackgroundWavesColor";
        public const string TerrainRowKey = "AmbienceTerrainColor";
        public const string WorldTintRowKey = "AmbienceWorldTintColor";

        private const float WorldTintOffsetStrength = 0.25f;

        private static bool _initialized;
        private static Color _defaultFogOfWarColor;
        private static Color _defaultOceanWaterColor;
        private static Color _defaultBackgroundWavesColor;
        private static readonly Color DefaultTerrainColor = new(
            TerrainColorPalette.LandR,
            TerrainColorPalette.LandG,
            TerrainColorPalette.LandB,
            TerrainColorPalette.LandA);
        private static readonly Color DefaultWorldTintColor = new(128, 128, 128, 255);

        public static Color FogOfWarColor { get; private set; }
        public static Color OceanWaterColor { get; private set; }
        public static Color BackgroundWavesColor { get; private set; }
        public static Color TerrainColor { get; private set; }
        public static Color WorldTintColor { get; private set; }

        public static string FogOfWarHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(CurrentFogOfWarColor);
            }
        }

        public static Color CurrentFogOfWarColor
        {
            get
            {
                EnsureInitialized();
                return FogOfWarManager.CurrentBaseColor;
            }
        }

        public static string CurrentFogOfWarHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(CurrentFogOfWarColor);
            }
        }

        public static string OceanWaterHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(OceanWaterColor);
            }
        }

        public static Color CurrentOceanWaterColor
        {
            get
            {
                EnsureInitialized();
                return GameBlockOceanBackground.CurrentBaseColor;
            }
        }

        public static Color CurrentBackgroundWavesColor
        {
            get
            {
                EnsureInitialized();
                return GameBlockOceanBackground.CurrentWaveColor;
            }
        }

        public static Color CurrentWorldTintColor
        {
            get
            {
                EnsureInitialized();
                return WorldTintColor;
            }
        }

        public static string CurrentOceanWaterHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(CurrentOceanWaterColor);
            }
        }

        public static string CurrentBackgroundWavesHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(CurrentBackgroundWavesColor);
            }
        }

        public static string BackgroundWavesHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(BackgroundWavesColor);
            }
        }

        public static string TerrainHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(TerrainColor);
            }
        }

        public static string WorldTintHex
        {
            get
            {
                EnsureInitialized();
                return ColorScheme.ToHex(CurrentWorldTintColor);
            }
        }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _defaultFogOfWarColor = FogOfWarManager.VisualParameters.BaseColor;
            _defaultOceanWaterColor = ToColor(GameBlockOceanBackground.BaseColor, GameBlockOceanBackground.BaseAlpha);
            _defaultBackgroundWavesColor = ToColor(GameBlockOceanBackground.WaveColor, GameBlockOceanBackground.WaveAlpha);

            Dictionary<string, string> data = BlockDataStore.LoadRowData(DockBlockKind.Ambience);

            FogOfWarColor = LoadColor(data, FogOfWarRowKey, _defaultFogOfWarColor, persistIfMissing: true);
            OceanWaterColor = LoadColor(data, OceanWaterRowKey, _defaultOceanWaterColor, persistIfMissing: true);
            if (IsLegacyDefaultOceanWaterColor(OceanWaterColor))
            {
                OceanWaterColor = _defaultOceanWaterColor;
                PersistColor(OceanWaterRowKey, OceanWaterColor);
            }

            BackgroundWavesColor = LoadColor(data, BackgroundWavesRowKey, _defaultBackgroundWavesColor, persistIfMissing: true);
            TerrainColor = LoadColor(data, TerrainRowKey, DefaultTerrainColor, persistIfMissing: true);
            if (IsLegacyDefaultTerrainColor(TerrainColor))
            {
                TerrainColor = DefaultTerrainColor;
                PersistColor(TerrainRowKey, TerrainColor);
            }

            WorldTintColor = LoadColor(data, WorldTintRowKey, DefaultWorldTintColor, persistIfMissing: true);

            ApplyOceanWaterColor(OceanWaterColor);
            ApplyBackgroundWavesColor(BackgroundWavesColor);
            Color configuredFogColor = ResolveFogOfWarColor(OceanWaterColor, FogOfWarColor.A);
            FogOfWarColor = ResolveFogOfWarColor(GameBlockOceanBackground.CurrentBaseColor, FogOfWarColor.A);
            ApplyFogOfWarColor(configuredFogColor);
            ApplyFogOfWarIntensity(configuredFogColor, FogOfWarColor);
            _initialized = true;
        }

        public static void SetFogOfWarColor(Color color, bool persist)
        {
            EnsureInitialized();
            Color configuredFogColor = ResolveFogOfWarColor(OceanWaterColor, color.A);
            FogOfWarColor = ResolveFogOfWarColor(GameBlockOceanBackground.CurrentBaseColor, color.A);
            ApplyFogOfWarColor(configuredFogColor);
            ApplyFogOfWarIntensity(configuredFogColor, FogOfWarColor);
            if (persist)
            {
                PersistColor(FogOfWarRowKey, FogOfWarColor);
            }
        }

        public static void SetOceanWaterColor(Color color, bool persist)
        {
            EnsureInitialized();
            OceanWaterColor = color;
            ApplyOceanWaterColor(OceanWaterColor);
            SyncFogOfWarWithOceanWater(BuildOceanColorAtCurrentZoneDarkness(OceanWaterColor));
            if (persist)
            {
                PersistColor(OceanWaterRowKey, OceanWaterColor);
            }
        }

        public static void SyncFogOfWarWithOceanWater()
        {
            EnsureInitialized();
            SyncFogOfWarWithOceanWater(GameBlockOceanBackground.CurrentBaseColor);
        }

        public static void SyncFogOfWarWithOceanWater(Color oceanColor)
        {
            EnsureInitialized();
            Color configuredFogColor = ResolveFogOfWarColor(OceanWaterColor, FogOfWarColor.A);
            FogOfWarColor = ResolveFogOfWarColor(oceanColor, FogOfWarColor.A);
            ApplyFogOfWarColor(configuredFogColor);
            ApplyFogOfWarIntensity(configuredFogColor, FogOfWarColor);
        }

        public static void SetBackgroundWavesColor(Color color, bool persist)
        {
            EnsureInitialized();
            BackgroundWavesColor = color;
            ApplyBackgroundWavesColor(BackgroundWavesColor);
            if (persist)
            {
                PersistColor(BackgroundWavesRowKey, BackgroundWavesColor);
            }
        }

        public static void SetTerrainColor(Color color, bool persist)
        {
            EnsureInitialized();
            TerrainColor = color;
            if (persist)
            {
                PersistColor(TerrainRowKey, TerrainColor);
            }
        }

        public static void SetWorldTintColor(Color color, bool persist)
        {
            EnsureInitialized();
            WorldTintColor = color;
            if (persist)
            {
                PersistColor(WorldTintRowKey, WorldTintColor);
            }
        }

        public static Vector3 GetWorldTintOffset()
        {
            EnsureInitialized();
            Color tintColor = CurrentWorldTintColor;
            Vector3 signedOffset = new(
                NormalizeSignedOffsetComponent(tintColor.R),
                NormalizeSignedOffsetComponent(tintColor.G),
                NormalizeSignedOffsetComponent(tintColor.B));
            float alphaStrength = tintColor.A / 255f;
            return signedOffset * (WorldTintOffsetStrength * alphaStrength);
        }

        public static Vector3 GetWorldTintMultiplier()
        {
            Vector3 offset = GetWorldTintOffset();
            return Vector3.Clamp(Vector3.One + (offset * 2f), Vector3.Zero, new Vector3(2f));
        }

        public static bool HasWorldTintOffset()
        {
            Vector3 offset = GetWorldTintOffset();
            return offset.LengthSquared() > 0.000001f;
        }

        public static Color ApplyWorldTint(Color color)
        {
            EnsureInitialized();

            if (color.A == 0 || !HasWorldTintOffset())
            {
                return color;
            }

            Vector3 tinted = Vector3.Clamp(color.ToVector3() * GetWorldTintMultiplier(), Vector3.Zero, Vector3.One);
            return new Color(tinted.X, tinted.Y, tinted.Z, color.A / 255f);
        }

        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private static Color LoadColor(Dictionary<string, string> data, string rowKey, Color fallback, bool persistIfMissing)
        {
            if (data != null &&
                data.TryGetValue(rowKey, out string stored) &&
                ColorScheme.TryParseHex(stored, out Color parsed))
            {
                return parsed;
            }

            Color resolved = fallback;
            if (persistIfMissing)
            {
                PersistColor(rowKey, resolved);
            }

            return resolved;
        }

        private static void PersistColor(string rowKey, Color color)
        {
            BlockDataStore.SetRowData(DockBlockKind.Ambience, rowKey, ColorScheme.ToHex(color));
        }

        private static void ApplyFogOfWarColor(Color color)
        {
            Color resolvedColor = ResolveFogOfWarColor(color, color.A);
            FogOfWarManager.FogVisualParameters parameters = FogOfWarManager.VisualParameters;
            if (parameters.BaseColor.PackedValue == resolvedColor.PackedValue)
            {
                return;
            }

            parameters.BaseColor = resolvedColor;
            FogOfWarManager.VisualParameters = parameters;
        }

        private static void ApplyFogOfWarIntensity(Color configuredColor, Color liveColor)
        {
            FogOfWarManager.SetFogColorIntensity(ResolveColorIntensity(configuredColor, liveColor));
        }

        private static void ApplyOceanWaterColor(Color color)
        {
            GameBlockOceanBackground.BaseColor = color.ToVector3();
            GameBlockOceanBackground.BaseAlpha = color.A / 255f;
        }

        private static void ApplyBackgroundWavesColor(Color color)
        {
            GameBlockOceanBackground.WaveColor = color.ToVector3();
            GameBlockOceanBackground.WaveAlpha = color.A / 255f;
        }

        private static Color ToColor(Vector3 color, float alpha)
        {
            Vector3 clamped = Vector3.Clamp(color, Vector3.Zero, Vector3.One);
            return new Color(clamped.X, clamped.Y, clamped.Z, Math.Clamp(alpha, 0f, 1f));
        }

        private static Color ResolveFogOfWarColor(Color oceanColor, byte alpha)
        {
            return new Color(oceanColor.R, oceanColor.G, oceanColor.B, alpha);
        }

        private static Color BuildOceanColorAtCurrentZoneDarkness(Color oceanColor)
        {
            float darkness = MathHelper.Clamp(GameBlockOceanBackground.OceanZoneCurrentDarkness, 0.05f, 2f);
            Vector3 rgb = Vector3.Clamp(oceanColor.ToVector3() * darkness, Vector3.Zero, Vector3.One);
            return new Color(rgb.X, rgb.Y, rgb.Z, oceanColor.A / 255f);
        }

        private static float ResolveColorIntensity(Color configuredColor, Color liveColor)
        {
            float configuredLuminance = PerceivedLuminance(configuredColor);
            if (configuredLuminance <= 0.0001f)
            {
                return 1f;
            }

            return MathHelper.Clamp(PerceivedLuminance(liveColor) / configuredLuminance, 0.05f, 2f);
        }

        private static float PerceivedLuminance(Color color)
        {
            return ((0.2126f * color.R) + (0.7152f * color.G) + (0.0722f * color.B)) / 255f;
        }

        private static bool IsLegacyDefaultTerrainColor(Color color)
        {
            return color.A == 255 &&
                ((color.R == 0 &&
                    color.G == 0 &&
                    color.B == 0) ||
                (color.R == 16 &&
                    color.G == 68 &&
                    color.B == 36));
        }

        private static bool IsLegacyDefaultOceanWaterColor(Color color)
        {
            if (color.A != 255)
            {
                return false;
            }

            return (color.R == 40 &&
                    color.G == 176 &&
                    color.B == 206) ||
                (color.R == 0 &&
                    color.G == 92 &&
                    color.B == 164);
        }

        private static float NormalizeSignedOffsetComponent(byte component)
        {
            return MathHelper.Clamp((component - 128f) / 127f, -1f, 1f);
        }
    }
}
