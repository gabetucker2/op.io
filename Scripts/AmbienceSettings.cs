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
        private static readonly Color DefaultTerrainColor = new(0, 0, 0, 255);
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
                return ColorScheme.ToHex(FogOfWarColor);
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
                return ColorScheme.ToHex(WorldTintColor);
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
            BackgroundWavesColor = LoadColor(data, BackgroundWavesRowKey, _defaultBackgroundWavesColor, persistIfMissing: true);
            TerrainColor = LoadColor(data, TerrainRowKey, DefaultTerrainColor, persistIfMissing: true);
            WorldTintColor = LoadColor(data, WorldTintRowKey, DefaultWorldTintColor, persistIfMissing: true);

            ApplyFogOfWarColor(FogOfWarColor);
            ApplyOceanWaterColor(OceanWaterColor);
            ApplyBackgroundWavesColor(BackgroundWavesColor);
            _initialized = true;
        }

        public static void SetFogOfWarColor(Color color, bool persist)
        {
            EnsureInitialized();
            FogOfWarColor = color;
            ApplyFogOfWarColor(FogOfWarColor);
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
            if (persist)
            {
                PersistColor(OceanWaterRowKey, OceanWaterColor);
            }
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
            Vector3 signedOffset = new(
                NormalizeSignedOffsetComponent(WorldTintColor.R),
                NormalizeSignedOffsetComponent(WorldTintColor.G),
                NormalizeSignedOffsetComponent(WorldTintColor.B));
            float alphaStrength = WorldTintColor.A / 255f;
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
            FogOfWarManager.FogVisualParameters parameters = FogOfWarManager.VisualParameters;
            parameters.BaseColor = color;
            FogOfWarManager.VisualParameters = parameters;
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

        private static float NormalizeSignedOffsetComponent(byte component)
        {
            return MathHelper.Clamp((component - 128f) / 127f, -1f, 1f);
        }
    }
}
