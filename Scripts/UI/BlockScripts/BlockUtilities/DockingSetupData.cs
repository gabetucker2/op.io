using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace op.io
{
    internal sealed class DockingSetupDefinition
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("menu")]
        public List<DockingSetupMenuEntry> Menu { get; set; } = new();

        [JsonPropertyName("panels")]
        public List<DockingSetupPanelGroup> Panels { get; set; } = new();

        [JsonPropertyName("groupBars")]
        public List<DockingSetupPanelGroup> GroupBars { get; set; } = new();

        [JsonPropertyName("layout")]
        public DockingSetupLayoutNode Layout { get; set; }

        [JsonPropertyName("blockLocks")]
        public Dictionary<string, bool> BlockLocks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("panelLocks")]
        public Dictionary<string, bool> PanelLocks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class DockingSetupMenuEntry
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("visible")]
        public bool Visible { get; set; }
    }

    internal sealed class DockingSetupPanelGroup
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("active")]
        public string Active { get; set; }

        [JsonPropertyName("blocks")]
        public List<string> Blocks { get; set; } = new();
    }

    internal sealed class DockingSetupLayoutNode
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("panel")]
        public string PanelId { get; set; }

        [JsonPropertyName("orientation")]
        public string Orientation { get; set; }

        [JsonPropertyName("ratio")]
        public float Ratio { get; set; }

        [JsonPropertyName("first")]
        public DockingSetupLayoutNode First { get; set; }

        [JsonPropertyName("second")]
        public DockingSetupLayoutNode Second { get; set; }
    }
}
