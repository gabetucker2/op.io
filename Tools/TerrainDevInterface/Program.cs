using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using op.io;

namespace TerrainDevInterface;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--benchmark-render"))
        {
            TerrainRenderBenchmark.Run(worldPreview: false);
            return;
        }

        if (args.Contains("--benchmark-world-render"))
        {
            TerrainRenderBenchmark.Run(worldPreview: true);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TerrainEditorForm());
    }
}

internal static class TerrainRenderBenchmark
{
    public static void Run(bool worldPreview)
    {
        TerrainScene scene = TerrainScene.CreateDefault(TerrainWorldDefaults.DefaultSeed);
        scene.WorldPreviewEnabled = worldPreview;
        Size size = new(1500, 900);
        float benchmarkZoom = worldPreview ? 0.22f : 0.90f;
        Stopwatch stopwatch = Stopwatch.StartNew();
        using Bitmap draft = TerrainRenderer.Render(scene.BuildRenderScene(), size, 0f, 0f, benchmarkZoom, settledQuality: false, CancellationToken.None);
        stopwatch.Stop();
        Console.WriteLine($"DraftRenderBenchmarkMs={stopwatch.ElapsedMilliseconds}");
        Console.WriteLine($"DraftBitmap={draft.Width}x{draft.Height}");

        stopwatch.Restart();
        using Bitmap settled = TerrainRenderer.Render(scene.BuildRenderScene(), size, 0f, 0f, benchmarkZoom, settledQuality: true, CancellationToken.None);
        stopwatch.Stop();
        Console.WriteLine($"SettledRenderBenchmarkMs={stopwatch.ElapsedMilliseconds}");
        Console.WriteLine($"SettledBitmap={settled.Width}x{settled.Height}");
        PrintBitmapLandWaterRatio("SettledBitmap", settled);

        TerrainScene sampledScene = scene.BuildRenderScene();
        int land = 0;
        int water = 0;
        for (int y = -850; y <= 750; y += 32)
        {
            for (int x = -1300; x <= 1300; x += 32)
            {
                TerrainProcessCell cell = TerrainProcessPreview.SampleCell(x, y, sampledScene, includeDebugFields: false);
                if (cell.IsLand)
                {
                    land++;
                }
                else
                {
                    water++;
                }
            }
        }

        int total = Math.Max(1, land + water);
        Console.WriteLine($"DefaultLandPercent={(land * 100f / total).ToString("0.0", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"DefaultWaterPercent={(water * 100f / total).ToString("0.0", CultureInfo.InvariantCulture)}");
    }

    private static void PrintBitmapLandWaterRatio(string prefix, Bitmap bitmap)
    {
        int land = 0;
        int water = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color color = bitmap.GetPixel(x, y);
                if (color.ToArgb() == TerrainRenderer.LandColor.ToArgb())
                {
                    land++;
                }
                else if (TerrainRenderer.IsWaterColor(color))
                {
                    water++;
                }
            }
        }

        int total = Math.Max(1, land + water);
        Console.WriteLine($"{prefix}LandPercent={(land * 100f / total).ToString("0.0", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"{prefix}WaterPercent={(water * 100f / total).ToString("0.0", CultureInfo.InvariantCulture)}");
    }
}

internal sealed class TerrainEditorForm : Form
{
    private readonly TerrainCanvas _canvas;
    private readonly TabControl _tabs;
    private readonly FlowLayoutPanel _islandControls;
    private readonly FlowLayoutPanel _landformControls;
    private readonly FlowLayoutPanel _worldControls;
    private FlowLayoutPanel _controls;
    private readonly TreeView _structureTree;
    private readonly Label _selectedLabel;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly System.Windows.Forms.Timer _settledRenderTimer;
    private readonly List<Action> _controlRefreshers = [];
    private readonly TerrainScene _scene;

    public TerrainEditorForm()
    {
        Text = "op.io Terrain Dev Interface";
        MinimumSize = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _scene = TerrainScene.CreateDefault(TerrainWorldDefaults.DefaultSeed);
        _renderTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _renderTimer.Tick += (_, _) =>
        {
            _renderTimer.Stop();
            RenderNow();
        };
        _settledRenderTimer = new System.Windows.Forms.Timer { Interval = 850 };
        _settledRenderTimer.Tick += (_, _) =>
        {
            _settledRenderTimer.Stop();
            RenderSettled();
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 390f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _canvas = new TerrainCanvas
        {
            Dock = DockStyle.Fill,
            BackColor = TerrainRenderer.DeepOceanColor
        };
        _canvas.SetScene(_scene);
        _canvas.SelectionChanged += (_, _) => RefreshSelectionControls();
        _canvas.ViewChanged += (_, _) => ScheduleRender();
        layout.Controls.Add(_canvas, 0, 0);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill
        };
        _islandControls = BuildControlPanel();
        _landformControls = BuildControlPanel();
        _worldControls = BuildControlPanel();
        AddTab("Island", _islandControls);
        AddTab("Landforms", _landformControls);
        AddTab("World", _worldControls);
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            bool worldPreview = _tabs.SelectedTab?.Text == "World";
            if (_scene.WorldPreviewEnabled == worldPreview)
            {
                return;
            }

            _scene.WorldPreviewEnabled = worldPreview;
            if (worldPreview)
            {
                _canvas.ResetWorldView();
            }

            ScheduleRender();
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Width = 350,
            Height = 42,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(10, 46)
        };

        Button encode = BuildButton("Encode");
        encode.Width = 350;
        encode.Location = new Point(10, 8);
        encode.BackColor = Color.FromArgb(54, 92, 78);
        encode.Click += (_, _) => EncodeSettings();

        Panel footer = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(27, 30, 32),
            Padding = new Padding(10, 8, 10, 8)
        };
        footer.Controls.Add(encode);
        footer.Controls.Add(_statusLabel);

        TableLayoutPanel sideLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(27, 30, 32)
        };
        sideLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));
        sideLayout.Controls.Add(_tabs, 0, 0);
        sideLayout.Controls.Add(footer, 0, 1);
        layout.Controls.Add(sideLayout, 1, 0);
        Controls.Add(layout);

        _structureTree = new TreeView
        {
            Width = 350,
            Height = 220,
            HideSelection = false,
            BackColor = Color.FromArgb(22, 24, 26),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        _structureTree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is int id && _scene.SelectedId != id)
            {
                _scene.SelectedId = id;
                RefreshSelectionControls();
            }
        };

        _selectedLabel = new Label
        {
            AutoSize = false,
            Width = 350,
            Height = 42,
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        BuildControls();
        KeyDown += OnEditorKeyDown;
        Shown += (_, _) => RenderNow();
    }

    private static FlowLayoutPanel BuildControlPanel()
    {
        return new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(27, 30, 32)
        };
    }

    private void AddTab(string title, FlowLayoutPanel panel)
    {
        TabPage page = new(title)
        {
            BackColor = Color.FromArgb(27, 30, 32)
        };
        page.Controls.Add(panel);
        _tabs.TabPages.Add(page);
    }

    private void BuildControls()
    {
        _controlRefreshers.Clear();
        _islandControls.Controls.Clear();
        _landformControls.Controls.Clear();
        _worldControls.Controls.Clear();

        _controls = _islandControls;
        AddHeader("Map");
        AddSeedControls();

        AddHeader("Island Properties");
        AddOverlayControls();
        AddSlider("Raster smoothing", () => _scene.RasterSmoothing, value => _scene.RasterSmoothing = value, 0f, 3.50f, 2);
        AddSlider("Global land emergence", () => _scene.GlobalEmergence, value => _scene.GlobalEmergence = value, 0.60f, 1.80f, 2);
        AddSlider("Island size", () => _scene.PrimaryIsland.Scale, value => _scene.PrimaryIsland.Scale = value, 0.65f, 1.70f, 2);
        AddSlider("Island length", () => _scene.PrimaryIsland.BaseLength, value => _scene.PrimaryIsland.BaseLength = value, 260f, 760f, 0);
        AddSlider("Island width", () => _scene.PrimaryIsland.BaseRadius, value => _scene.PrimaryIsland.BaseRadius = value, 130f, 480f, 0);
        AddSlider("Coast irregularity", () => _scene.PrimaryIsland.Roughness, value => _scene.PrimaryIsland.Roughness = value, 0f, 0.16f, 2);
        AddSlider("Island rotation", () => _scene.PrimaryIsland.RotationDegrees, value => _scene.PrimaryIsland.RotationDegrees = value, -180f, 180f, 0);
        AddSlider("Island solidity", () => _scene.PrimaryIsland.Emergence, value => _scene.PrimaryIsland.Emergence = value, 0.70f, 1.80f, 2);
        AddSlider("Global basin erosion", () => _scene.GlobalBasinCut, value => _scene.GlobalBasinCut = value, 0.40f, 1.60f, 2);
        AddSlider("Global opening width", () => _scene.GlobalOpeningWidth, value => _scene.GlobalOpeningWidth = value, 0.45f, 1.90f, 2);
        AddSlider("Global opening strength", () => _scene.GlobalOpeningStrength, value => _scene.GlobalOpeningStrength = value, 0.30f, 1.70f, 2);

        Button resetGlobal = BuildButton("Reset Global Variables");
        resetGlobal.Click += (_, _) =>
        {
            _scene.ResetGlobalVariables();
            RefreshSelectionControls();
            ScheduleRender();
        };
        _controls.Controls.Add(resetGlobal);

        Button resetView = BuildButton("Reset View");
        resetView.Click += (_, _) =>
        {
            _canvas.ResetView();
            ScheduleRender();
        };
        _controls.Controls.Add(resetView);

        _controls = _landformControls;
        AddHeader("Landform Tab");
        AddStructureTree();
        AddHeader("Add Landform");
        AddLandformButton("Add Giant River", LandformKind.GiantRiver);
        AddLandformButton("Add Calanque", LandformKind.CalanqueCove);
        AddLandformButton("Add Barrier Lagoon", LandformKind.BarrierIsland);
        AddLandformButton("Add Tower Stacks", LandformKind.StacksAndArches);
        _controls.Controls.Add(_selectedLabel);

        AddHeader("Selected Landform");
        AddSlider("Local X", SelectedCenterX, value => Selected().CenterX = value, -760f, 760f, 0);
        AddSlider("Local Y", SelectedCenterY, value => Selected().CenterY = value, -520f, 520f, 0);
        AddSlider("Region size", SelectedScale, value => Selected().Scale = value, 0.30f, 2.20f, 2);
        AddSlider("Base length", SelectedBaseLength, value => Selected().BaseLength = value, 100f, 900f, 0);
        AddSlider("Base width", SelectedBaseRadius, value => Selected().BaseRadius = value, 24f, 420f, 0);
        AddSlider("Lithology bias", SelectedWidthScale, value => Selected().WidthScale = value, 0.50f, 2.70f, 2);
        AddSlider("Basin erosion", SelectedBasinCut, value => Selected().BasinCut = value, 0f, 1.60f, 2);
        AddSlider("Opening width", SelectedOpeningWidth, value => Selected().OpeningWidth = value, 0.20f, 2.40f, 2);
        AddSlider("Process strength", SelectedOpeningStrength, value => Selected().OpeningStrength = value, 0f, 1.80f, 2);
        AddSlider("Emergence", SelectedEmergence, value => Selected().Emergence = value, 0.20f, 2.20f, 2);
        AddSlider("Structural trend", SelectedRotation, value => Selected().RotationDegrees = value, -180f, 180f, 0);
        AddSlider("Local roughness", SelectedRoughness, value => Selected().Roughness = value, 0f, 0.14f, 2);

        Button resetSelected = BuildButton("Reset Selected");
        resetSelected.Click += (_, _) =>
        {
            _scene.ResetSelected();
            RefreshSelectionControls();
            ScheduleRender();
        };
        _controls.Controls.Add(resetSelected);

        Button deleteSelected = BuildButton("Delete Selected Landform");
        deleteSelected.BackColor = Color.FromArgb(92, 54, 54);
        deleteSelected.Click += (_, _) => TryDeleteSelectedLandform();
        _controls.Controls.Add(deleteSelected);

        _controls = _worldControls;
        AddHeader("Archipelago Placement");
        AddWorldSlider("Island count", () => _scene.WorldIslandCount, value => _scene.WorldIslandCount = value, 3f, 12f, 0);
        AddWorldSlider("Minimum spacing", () => _scene.WorldMinimumSpacing, value => _scene.WorldMinimumSpacing = value, 420f, 1200f, 0);
        AddWorldSlider("Interaction spacing", () => _scene.WorldInteractionSpacing, value => _scene.WorldInteractionSpacing = value, 650f, 1600f, 0);
        AddWorldSlider("Placement jitter", () => _scene.WorldPlacementJitter, value => _scene.WorldPlacementJitter = value, 0f, 280f, 0);
        AddWorldSlider("Cluster min", () => _scene.WorldClusterCountMinimum, value => _scene.WorldClusterCountMinimum = value, 1f, 5f, 0);
        AddWorldSlider("Cluster max", () => _scene.WorldClusterCountMaximum, value => _scene.WorldClusterCountMaximum = value, 1f, 5f, 0);
        AddWorldSlider("Cluster spread", () => _scene.WorldClusterSpread, value => _scene.WorldClusterSpread = value, 0.20f, 1.20f, 2);
        AddWorldSlider("Ridge / chain bias", () => _scene.WorldChainBias, value => _scene.WorldChainBias = value, 0f, 1f, 2);
        AddToggle("Show preview text", () => _scene.WorldPreviewLabelsVisible, value => _scene.WorldPreviewLabelsVisible = value, renderTerrain: false);
        AddHeader("Water Depth Zones");
        AddWorldSlider("Shallow water distance", () => _scene.WaterShallowDistance, value => _scene.WaterShallowDistance = value, 24f, 260f, 0);
        AddWorldSlider("Sunlit water distance", () => _scene.WaterSunlitDistance, value => _scene.WaterSunlitDistance = value, 80f, 460f, 0);
        AddWorldSlider("Twilight water distance", () => _scene.WaterTwilightDistance, value => _scene.WaterTwilightDistance = value, 160f, 720f, 0);
        AddWorldSlider("Midnight water distance", () => _scene.WaterMidnightDistance, value => _scene.WaterMidnightDistance = value, 260f, 980f, 0);
        AddWorldSlider("Water stochastic reach", () => _scene.WaterStochasticReach, value => _scene.WaterStochasticReach = value, 0f, 180f, 0);
        AddWorldSlider("Water stochastic scale", () => _scene.WaterStochasticScale, value => _scene.WaterStochasticScale = value, 120f, 980f, 0);
        AddWorldSlider("Water coast rounding", () => _scene.WaterCoastShapeRounding, value => _scene.WaterCoastShapeRounding = value, 0f, 1f, 2);
        AddHeader("Archipelago Shape Seeds");
        AddWorldShapeSeedControl("Cluster shape seed", () => _scene.WorldClusterShapeSeed, value => _scene.WorldClusterShapeSeed = value, () => _scene.RerollWorldClusterShapeSeed());
        AddWorldShapeSeedControl("Island scatter seed", () => _scene.WorldIslandScatterSeed, value => _scene.WorldIslandScatterSeed = value, () => _scene.RerollWorldIslandScatterSeed());
        AddWorldShapeSeedControl("Island variation seed", () => _scene.WorldIslandVariationSeed, value => _scene.WorldIslandVariationSeed = value, () => _scene.RerollWorldIslandVariationSeed());
        AddWorldSlider("Island size variance", () => _scene.WorldIslandSizeVariance, value => _scene.WorldIslandSizeVariance = value, 0f, 0.60f, 2);
        AddHeader("Giant River RNG");
        AddWorldSlider("Giant river min", () => _scene.WorldGiantRiverRngMinimum, value => _scene.WorldGiantRiverRngMinimum = value, 0f, 2f, 0);
        AddWorldSlider("Giant river max", () => _scene.WorldGiantRiverRngMaximum, value => _scene.WorldGiantRiverRngMaximum = value, 0f, 2f, 0);
        AddWorldSlider("Giant river size min", () => _scene.WorldGiantRiverSizeMinimum, value => _scene.WorldGiantRiverSizeMinimum = value, 0.25f, 2.00f, 2);
        AddWorldSlider("Giant river size max", () => _scene.WorldGiantRiverSizeMaximum, value => _scene.WorldGiantRiverSizeMaximum = value, 0.25f, 2.00f, 2);

        AddHeader("Calanque RNG");
        AddWorldSlider("Calanque min", () => _scene.WorldCalanqueRngMinimum, value => _scene.WorldCalanqueRngMinimum = value, 0f, 5f, 0);
        AddWorldSlider("Calanque max", () => _scene.WorldCalanqueRngMaximum, value => _scene.WorldCalanqueRngMaximum = value, 0f, 5f, 0);
        AddWorldSlider("Calanque size min", () => _scene.WorldCalanqueSizeMinimum, value => _scene.WorldCalanqueSizeMinimum = value, 0.25f, 2.00f, 2);
        AddWorldSlider("Calanque size max", () => _scene.WorldCalanqueSizeMaximum, value => _scene.WorldCalanqueSizeMaximum = value, 0.25f, 2.00f, 2);

        AddHeader("Barrier Lagoon RNG");
        AddWorldSlider("Barrier lagoon min", () => _scene.WorldBarrierLagoonRngMinimum, value => _scene.WorldBarrierLagoonRngMinimum = value, 0f, 5f, 0);
        AddWorldSlider("Barrier lagoon max", () => _scene.WorldBarrierLagoonRngMaximum, value => _scene.WorldBarrierLagoonRngMaximum = value, 0f, 5f, 0);
        AddWorldSlider("Barrier lagoon size min", () => _scene.WorldBarrierLagoonSizeMinimum, value => _scene.WorldBarrierLagoonSizeMinimum = value, 0.25f, 2.00f, 2);
        AddWorldSlider("Barrier lagoon size max", () => _scene.WorldBarrierLagoonSizeMaximum, value => _scene.WorldBarrierLagoonSizeMaximum = value, 0.25f, 2.00f, 2);

        AddHeader("Tower Stacks RNG");
        AddWorldSlider("Tower stacks min", () => _scene.WorldTowerStacksRngMinimum, value => _scene.WorldTowerStacksRngMinimum = value, 0f, 5f, 0);
        AddWorldSlider("Tower stacks max", () => _scene.WorldTowerStacksRngMaximum, value => _scene.WorldTowerStacksRngMaximum = value, 0f, 5f, 0);
        AddWorldSlider("Tower stacks size min", () => _scene.WorldTowerStacksSizeMinimum, value => _scene.WorldTowerStacksSizeMinimum = value, 0.25f, 2.00f, 2);
        AddWorldSlider("Tower stacks size max", () => _scene.WorldTowerStacksSizeMaximum, value => _scene.WorldTowerStacksSizeMaximum = value, 0.25f, 2.00f, 2);
        AddHeader("Placement Preview");
        foreach (WorldIslandPlacement placement in _scene.BuildArchipelagoPlacements())
        {
            AddWorldPlacementLabel(placement);
        }

        RefreshSelectionControls();
    }

    private void AddLandformButton(string text, LandformKind kind)
    {
        Button button = BuildButton(text);
        button.Click += (_, _) =>
        {
            LandformInstance instance = _scene.AddLandform(kind);
            _scene.SelectedId = instance.Id;
            RebuildStructureTree();
            RefreshSelectionControls();
            ScheduleRender();
        };
        _controls.Controls.Add(button);
    }

    private void AddWorldPlacementLabel(WorldIslandPlacement placement)
    {
        Label label = new()
        {
            Text = $"{placement.Name}: cluster {placement.ClusterIndex + 1}, x {placement.X:0}, y {placement.Y:0}, forms {placement.LandformCount}{Environment.NewLine}river {placement.GiantRiverCount}, cove {placement.CalanqueCount}, lagoon {placement.BarrierLagoonCount}, stacks {placement.TowerStacksCount}",
            AutoSize = false,
            Width = 350,
            Height = 38,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _controls.Controls.Add(label);
    }

    private void AddStructureTree()
    {
        AddHeader("Structure Hierarchy");
        _controls.Controls.Add(_structureTree);
        RebuildStructureTree();
    }

    private void AddOverlayControls()
    {
        Panel row = new()
        {
            Width = 350,
            Height = 70,
            Margin = new Padding(0, 2, 0, 8)
        };

        Label label = new()
        {
            Text = "Debug overlay",
            Width = 350,
            Height = 22,
            ForeColor = Color.Gainsboro,
            Location = new Point(0, 0)
        };
        row.Controls.Add(label);

        ComboBox combo = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 344,
            Location = new Point(0, 30)
        };
        foreach (TerrainOverlayMode mode in Enum.GetValues<TerrainOverlayMode>())
        {
            combo.Items.Add(mode);
        }

        combo.Format += (_, e) =>
        {
            if (e.ListItem is TerrainOverlayMode mode)
            {
                e.Value = TerrainProcessPreview.FormatOverlayMode(mode);
            }
        };
        combo.SelectedItem = _scene.OverlayMode;
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedItem is TerrainOverlayMode mode)
            {
                _scene.OverlayMode = mode;
                ScheduleRender();
            }
        };

        _controlRefreshers.Add(() => combo.SelectedItem = _scene.OverlayMode);
        row.Controls.Add(combo);
        _controls.Controls.Add(row);
    }

    private void AddSeedControls()
    {
        Panel row = new()
        {
            Width = 350,
            Height = 78,
            Margin = new Padding(0, 2, 0, 8)
        };

        Label label = new()
        {
            Text = "Map seed",
            Width = 350,
            Height = 22,
            ForeColor = Color.Gainsboro,
            Location = new Point(0, 0)
        };
        row.Controls.Add(label);

        NumericUpDown seedInput = new()
        {
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            Value = _scene.Seed,
            Width = 174,
            DecimalPlaces = 0,
            Increment = 1,
            Location = new Point(0, 30)
        };
        seedInput.ValueChanged += (_, _) =>
        {
            _scene.SetSeed((int)seedInput.Value);
            RefreshSelectionControls();
            ScheduleRender();
        };
        row.Controls.Add(seedInput);

        Button reroll = new()
        {
            Text = "Reroll",
            Width = 154,
            Height = 30,
            Location = new Point(190, 29),
            BackColor = Color.FromArgb(54, 59, 66),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        reroll.Click += (_, _) =>
        {
            _scene.RerollSeed();
            seedInput.Value = _scene.Seed;
            RefreshSelectionControls();
            ScheduleRender();
        };
        row.Controls.Add(reroll);

        _controlRefreshers.Add(() => seedInput.Value = Math.Clamp((decimal)_scene.Seed, seedInput.Minimum, seedInput.Maximum));
        _controls.Controls.Add(row);
    }

    private void AddWorldShapeSeedControl(string labelText, Func<int> getValue, Action<int> setValue, Action rerollValue)
    {
        Panel row = new()
        {
            Width = 350,
            Height = 78,
            Margin = new Padding(0, 2, 0, 8)
        };

        Label label = new()
        {
            Text = labelText,
            Width = 350,
            Height = 22,
            ForeColor = Color.Gainsboro,
            Location = new Point(0, 0)
        };
        row.Controls.Add(label);

        NumericUpDown seedInput = new()
        {
            Minimum = int.MinValue,
            Maximum = int.MaxValue,
            Value = getValue(),
            Width = 174,
            DecimalPlaces = 0,
            Increment = 1,
            Location = new Point(0, 30)
        };
        seedInput.ValueChanged += (_, _) =>
        {
            setValue((int)seedInput.Value);
            RefreshSelectionControls();
            ScheduleRender();
        };
        row.Controls.Add(seedInput);

        Button reroll = new()
        {
            Text = "Reroll",
            Width = 154,
            Height = 30,
            Location = new Point(190, 29),
            BackColor = Color.FromArgb(54, 59, 66),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        reroll.Click += (_, _) =>
        {
            rerollValue();
            seedInput.Value = getValue();
            RefreshSelectionControls();
            ScheduleRender();
        };
        row.Controls.Add(reroll);

        _controlRefreshers.Add(() => seedInput.Value = Math.Clamp((decimal)getValue(), seedInput.Minimum, seedInput.Maximum));
        _controls.Controls.Add(row);
    }

    private Button BuildButton(string text)
    {
        return new Button
        {
            Text = text,
            Width = 350,
            Height = 34,
            Margin = new Padding(0, 3, 0, 5),
            BackColor = Color.FromArgb(54, 59, 66),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
    }

    private void AddHeader(string text)
    {
        Label label = new()
        {
            Text = text,
            AutoSize = false,
            Width = 350,
            Height = 30,
            Margin = new Padding(0, 16, 0, 2),
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomLeft
        };
        _controls.Controls.Add(label);
    }

    private void AddToggle(string label, Func<bool> getValue, Action<bool> setValue, bool renderTerrain)
    {
        CheckBox toggle = new()
        {
            Text = label,
            Checked = getValue(),
            AutoSize = false,
            Width = 350,
            Height = 32,
            Margin = new Padding(0, 4, 0, 6),
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(27, 30, 32),
            FlatStyle = FlatStyle.Flat
        };

        toggle.CheckedChanged += (_, _) =>
        {
            setValue(toggle.Checked);
            if (renderTerrain)
            {
                ScheduleRender();
            }
            else
            {
                _canvas.Invalidate();
            }
        };

        _controlRefreshers.Add(() => toggle.Checked = getValue());
        _controls.Controls.Add(toggle);
    }

    private void AddSlider(
        string label,
        Func<float> getValue,
        Action<float> setValue,
        float min,
        float max,
        int decimals,
        float? typedMaximum = null)
    {
        float sliderMax = max;
        decimal inputMaximum = typedMaximum.HasValue
            ? (decimal)typedMaximum.Value
            : (decimal)max;
        Panel row = new()
        {
            Width = 350,
            Height = 72,
            Margin = new Padding(0, 2, 0, 6)
        };
        Label text = new()
        {
            Text = label,
            Width = 350,
            Height = 22,
            ForeColor = Color.Gainsboro,
            Location = new Point(0, 0)
        };
        row.Controls.Add(text);

        TrackBar track = new()
        {
            Minimum = 0,
            Maximum = 1000,
            TickFrequency = 100,
            Width = 205,
            Height = 42,
            Location = new Point(0, 25),
            BackColor = Color.FromArgb(27, 30, 32)
        };
        NumericUpDown input = new()
        {
            Minimum = (decimal)min,
            Maximum = Math.Max((decimal)min, inputMaximum),
            DecimalPlaces = decimals,
            Increment = decimals == 0 ? 1m : (decimal)MathF.Pow(10f, -decimals),
            Width = 132,
            Location = new Point(212, 28)
        };

        bool syncing = false;
        void SetTrackFromValue(float value)
        {
            float normalized = Math.Clamp((value - min) / Math.Max(0.0001f, sliderMax - min), 0f, 1f);
            track.Value = Math.Clamp((int)MathF.Round(normalized * track.Maximum), track.Minimum, track.Maximum);
        }

        void SetInputFromValue(float value)
        {
            input.Value = Math.Clamp((decimal)value, input.Minimum, input.Maximum);
        }

        void Refresh()
        {
            syncing = true;
            float value = getValue();
            SetTrackFromValue(value);
            SetInputFromValue(value);
            syncing = false;
        }

        Refresh();
        _controlRefreshers.Add(Refresh);

        track.Scroll += (_, _) =>
        {
            if (syncing)
            {
                return;
            }

            syncing = true;
            float value = min + ((track.Value / (float)track.Maximum) * (sliderMax - min));
            setValue(value);
            SetInputFromValue(value);
            syncing = false;
            ScheduleRender();
        };

        input.ValueChanged += (_, _) =>
        {
            if (syncing)
            {
                return;
            }

            syncing = true;
            float value = (float)input.Value;
            setValue(value);
            SetTrackFromValue(value);
            syncing = false;
            ScheduleRender();
        };

        row.Controls.Add(track);
        row.Controls.Add(input);
        _controls.Controls.Add(row);
    }

    private void AddWorldSlider(
        string label,
        Func<float> getValue,
        Action<float> setValue,
        float min,
        float max,
        int decimals)
    {
        AddSlider(label, getValue, setValue, min, max * 2f, decimals, typedMaximum: 1_000_000f);
    }

    private LandformInstance Selected() => _scene.Selected;
    private float SelectedCenterX() => Selected().CenterX;
    private float SelectedCenterY() => Selected().CenterY;
    private float SelectedBaseRadius() => Selected().BaseRadius;
    private float SelectedBaseLength() => Selected().BaseLength;
    private float SelectedScale() => Selected().Scale;
    private float SelectedWidthScale() => Selected().WidthScale;
    private float SelectedBasinCut() => Selected().BasinCut;
    private float SelectedOpeningWidth() => Selected().OpeningWidth;
    private float SelectedOpeningStrength() => Selected().OpeningStrength;
    private float SelectedEmergence() => Selected().Emergence;
    private float SelectedRotation() => Selected().RotationDegrees;
    private float SelectedRoughness() => Selected().Roughness;

    private void RefreshSelectionControls()
    {
        SelectStructureTreeNode(_scene.SelectedId);
        _selectedLabel.Text = $"Focus: {_scene.BuildHierarchyPath(_scene.Selected)}";
        for (int i = 0; i < _controlRefreshers.Count; i++)
        {
            _controlRefreshers[i]();
        }

        ScheduleRender();
    }

    private void RebuildStructureTree()
    {
        _structureTree.BeginUpdate();
        _structureTree.Nodes.Clear();
        AddStructureTreeChildren(_structureTree.Nodes, parentId: 1);
        _structureTree.ExpandAll();
        SelectStructureTreeNode(_scene.SelectedId);
        _structureTree.EndUpdate();
    }

    private void AddStructureTreeChildren(TreeNodeCollection nodes, int parentId)
    {
        foreach (LandformInstance instance in _scene.Landforms.Where(landform => landform.ParentId == parentId).OrderBy(landform => landform.Id))
        {
            TreeNode node = new(instance.DisplayName) { Tag = instance.Id };
            nodes.Add(node);
            AddStructureTreeChildren(node.Nodes, instance.Id);
        }
    }

    private void SelectStructureTreeNode(int id)
    {
        TreeNode node = FindStructureTreeNode(_structureTree.Nodes, id);
        if (node != null && !ReferenceEquals(_structureTree.SelectedNode, node))
        {
            _structureTree.SelectedNode = node;
        }
    }

    private static TreeNode FindStructureTreeNode(TreeNodeCollection nodes, int id)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is int nodeId && nodeId == id)
            {
                return node;
            }

            TreeNode child = FindStructureTreeNode(node.Nodes, id);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private void ScheduleRender()
    {
        _settledRenderTimer.Stop();
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private void RenderNow()
    {
        _canvas.Render(settledQuality: false);
        _settledRenderTimer.Stop();
        _settledRenderTimer.Start();
        _statusLabel.Text = $"seed {_scene.Seed} | selected {_scene.BuildHierarchyPath(_scene.Selected)} | zoom {_canvas.Zoom:0.00} | draft";
    }

    private void RenderSettled()
    {
        _canvas.Render(settledQuality: true);
        _statusLabel.Text = $"seed {_scene.Seed} | selected {_scene.BuildHierarchyPath(_scene.Selected)} | zoom {_canvas.Zoom:0.00} | settled";
    }

    private void EncodeSettings()
    {
        string outputDirectory = Path.Combine(ResolveToolRoot(), "LocalLogs");
        Directory.CreateDirectory(outputDirectory);
        string fileName = $"terrain_settings_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string path = Path.Combine(outputDirectory, fileName);
        File.WriteAllText(path, TerrainSettingsEncoder.Encode(_scene, _canvas), Encoding.UTF8);
        _statusLabel.Text = $"encoded {Path.Combine("Tools", "TerrainDevInterface", "LocalLogs", fileName)}";

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Encoded terrain settings to:\n{path}\n\nWindows could not open the file automatically:\n{ex.Message}", "Terrain Encoded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string ResolveToolRoot()
    {
        string[] roots =
        [
            AppContext.BaseDirectory,
            Application.StartupPath,
            Directory.GetCurrentDirectory()
        ];

        for (int i = 0; i < roots.Length; i++)
        {
            DirectoryInfo directory = new(Path.GetFullPath(roots[i]));
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "TerrainDevInterface.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && !IsTextEditingControl(ActiveControl))
        {
            TryDeleteSelectedLandform();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (_canvas.HandleNavigationKey(e.KeyCode))
        {
            e.Handled = true;
            ScheduleRender();
        }
    }

    private void TryDeleteSelectedLandform()
    {
        if (!_scene.TryDeleteSelectedLandform(out string deletedName, out int deletedCount))
        {
            _statusLabel.Text = "select an added landform to delete";
            return;
        }

        RebuildStructureTree();
        RefreshSelectionControls();
        _statusLabel.Text = deletedCount == 1
            ? $"deleted {deletedName}"
            : $"deleted {deletedName} and {deletedCount - 1} child landforms";
    }

    private static bool IsTextEditingControl(Control control)
    {
        if (control == null)
        {
            return false;
        }

        if (control is TextBoxBase or NumericUpDown or ComboBox)
        {
            return true;
        }

        foreach (Control child in control.Controls)
        {
            if (child.ContainsFocus && IsTextEditingControl(child))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class TerrainCanvas : Control
{
    private Bitmap _bitmap;
    private TerrainScene _scene;
    private bool _panning;
    private bool _rendering;
    private float _bitmapCameraX;
    private float _bitmapCameraY;
    private float _bitmapZoom;
    private Size _bitmapClientSize;
    private TerrainScene _labelSceneSnapshot;
    private Point _lastMouse;
    private int _renderRequestId;
    private CancellationTokenSource _renderCancellation;

    public event EventHandler ViewChanged;
    public event EventHandler SelectionChanged;

    public TerrainCanvas()
    {
        DoubleBuffered = true;
        TabStop = true;
        CameraX = 0f;
        CameraY = 0f;
        Zoom = 0.90f;
        Resize += (_, _) => ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public float CameraX { get; private set; }
    public float CameraY { get; private set; }
    public float Zoom { get; private set; }

    public void SetScene(TerrainScene scene)
    {
        _scene = scene;
    }

    public void ResetView()
    {
        CameraX = 0f;
        CameraY = 0f;
        Zoom = 0.90f;
    }

    public void ResetWorldView()
    {
        CameraX = -849.366f;
        CameraY = 1327.653f;
        Zoom = 0.169283f;
    }

    public bool HandleNavigationKey(Keys key)
    {
        float step = 96f / Math.Max(0.05f, Zoom);
        switch (key)
        {
            case Keys.Left:
                CameraX -= step;
                return true;
            case Keys.Right:
                CameraX += step;
                return true;
            case Keys.Up:
                CameraY -= step;
                return true;
            case Keys.Down:
                CameraY += step;
                return true;
            case Keys.Oemplus:
            case Keys.Add:
                ZoomAt(GetClientCenter(), 1.18f);
                return true;
            case Keys.OemMinus:
            case Keys.Subtract:
                ZoomAt(GetClientCenter(), 1f / 1.18f);
                return true;
            default:
                return false;
        }
    }

    public void Render(bool settledQuality)
    {
        if (_scene == null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        TerrainScene snapshot = _scene.BuildRenderScene();
        _labelSceneSnapshot = snapshot;
        int requestId = Interlocked.Increment(ref _renderRequestId);
        CancellationTokenSource previousCancellation = _renderCancellation;
        previousCancellation?.Cancel();
        CancellationTokenSource currentCancellation = new();
        _renderCancellation = currentCancellation;
        CancellationToken cancellationToken = currentCancellation.Token;
        Size size = ClientSize;
        float cameraX = CameraX;
        float cameraY = CameraY;
        float zoom = Zoom;
        _rendering = true;
        Invalidate();

        Task.Run(() => TerrainRenderer.Render(snapshot, size, cameraX, cameraY, zoom, settledQuality, cancellationToken), cancellationToken)
            .ContinueWith(task =>
            {
                if (IsDisposed)
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        task.Result.Dispose();
                    }

                    currentCancellation.Dispose();
                    return;
                }

                BeginInvoke(new Action(() =>
                {
                    ApplyRenderResult(requestId, task, cameraX, cameraY, zoom, size);
                    currentCancellation.Dispose();
                    if (ReferenceEquals(_renderCancellation, currentCancellation))
                    {
                        _renderCancellation = null;
                    }
                }));
            }, TaskScheduler.Default);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(TerrainRenderer.DeepOceanColor);
        if (_bitmap != null)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            RectangleF destination = BuildBitmapDestination();
            e.Graphics.DrawImage(_bitmap, destination);
        }

        if (_scene != null && (!_scene.WorldPreviewEnabled || _scene.WorldPreviewLabelsVisible))
        {
            TerrainScene labelScene = _scene.WorldPreviewEnabled ? _labelSceneSnapshot : null;
            TerrainLabelRenderer.Draw(e.Graphics, ClientSize, CameraX, CameraY, Zoom, labelScene ?? _scene);
        }

        if (_rendering)
        {
            DrawRenderingBadge(e.Graphics);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left && _scene != null && !_scene.WorldPreviewEnabled)
        {
            LandformInstance clicked = TerrainLabelRenderer.HitTest(e.Location, ClientSize, CameraX, CameraY, Zoom, _scene);
            if (clicked != null)
            {
                _scene.SelectedId = clicked.Id;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        if (e.Button != MouseButtons.Middle)
        {
            return;
        }

        _panning = true;
        _lastMouse = e.Location;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_panning)
        {
            return;
        }

        int dx = e.X - _lastMouse.X;
        int dy = e.Y - _lastMouse.Y;
        CameraX -= dx / Math.Max(0.05f, Zoom);
        CameraY -= dy / Math.Max(0.05f, Zoom);
        _lastMouse = e.Location;
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Middle)
        {
            _panning = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.Location, e.Delta > 0 ? 1.14f : 1f / 1.14f);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyRenderResult(int requestId, Task<Bitmap> task, float renderedCameraX, float renderedCameraY, float renderedZoom, Size renderedClientSize)
    {
        if (task.Status != TaskStatus.RanToCompletion)
        {
            if (requestId == _renderRequestId)
            {
                _rendering = false;
            }

            return;
        }

        Bitmap next = task.Result;
        if (requestId != _renderRequestId)
        {
            next.Dispose();
            return;
        }

        Bitmap previous = _bitmap;
        _bitmap = next;
        _bitmapCameraX = renderedCameraX;
        _bitmapCameraY = renderedCameraY;
        _bitmapZoom = renderedZoom;
        _bitmapClientSize = renderedClientSize;
        _rendering = false;
        previous?.Dispose();
        Invalidate();
    }

    private RectangleF BuildBitmapDestination()
    {
        if (_bitmap == null || _bitmapClientSize.Width <= 0 || _bitmapClientSize.Height <= 0 || _bitmapZoom <= 0f)
        {
            return ClientRectangle;
        }

        float scale = Zoom / Math.Max(0.05f, _bitmapZoom);
        float width = _bitmapClientSize.Width * scale;
        float height = _bitmapClientSize.Height * scale;
        float centerX = (ClientSize.Width * 0.5f) + ((_bitmapCameraX - CameraX) * Zoom);
        float centerY = (ClientSize.Height * 0.5f) + ((_bitmapCameraY - CameraY) * Zoom);
        return new RectangleF(centerX - (width * 0.5f), centerY - (height * 0.5f), width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderCancellation?.Cancel();
            _renderCancellation?.Dispose();
            _bitmap?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ZoomAt(Point screenPoint, float zoomFactor)
    {
        PointF before = ScreenToWorld(screenPoint);
        Zoom = Math.Clamp(Zoom * zoomFactor, 0.08f, 2.4f);
        PointF after = ScreenToWorld(screenPoint);
        CameraX += before.X - after.X;
        CameraY += before.Y - after.Y;
    }

    private Point GetClientCenter()
    {
        return new Point(ClientSize.Width / 2, ClientSize.Height / 2);
    }

    private PointF ScreenToWorld(Point screenPoint)
    {
        float x = CameraX + ((screenPoint.X - (ClientSize.Width * 0.5f)) / Math.Max(0.05f, Zoom));
        float y = CameraY + ((screenPoint.Y - (ClientSize.Height * 0.5f)) / Math.Max(0.05f, Zoom));
        return new PointF(x, y);
    }

    private void DrawRenderingBadge(Graphics graphics)
    {
        Rectangle badge = new(12, 12, 92, 26);
        using SolidBrush background = new(TerrainRenderer.LandColor);
        using SolidBrush foreground = new(TerrainRenderer.WaterColor);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.FillRectangle(background, badge);
        graphics.DrawString("Rendering", Font, foreground, badge.X + 10, badge.Y + 6);
    }
}

internal static class TerrainRenderer
{
    private const float PreviewRenderScale = 1f;
    public static readonly Color ShallowWaterColor = Color.FromArgb(TerrainColorPalette.ShallowWaterR, TerrainColorPalette.ShallowWaterG, TerrainColorPalette.ShallowWaterB);
    public static readonly Color SunlitWaterColor = Color.FromArgb(TerrainColorPalette.SunlitWaterR, TerrainColorPalette.SunlitWaterG, TerrainColorPalette.SunlitWaterB);
    public static readonly Color TwilightWaterColor = Color.FromArgb(TerrainColorPalette.TwilightWaterR, TerrainColorPalette.TwilightWaterG, TerrainColorPalette.TwilightWaterB);
    public static readonly Color MidnightWaterColor = Color.FromArgb(TerrainColorPalette.MidnightWaterR, TerrainColorPalette.MidnightWaterG, TerrainColorPalette.MidnightWaterB);
    public static readonly Color AbyssWaterColor = Color.FromArgb(TerrainColorPalette.AbyssWaterR, TerrainColorPalette.AbyssWaterG, TerrainColorPalette.AbyssWaterB);
    public static readonly Color WaterColor = SunlitWaterColor;
    public static readonly Color DeepOceanColor = AbyssWaterColor;
    public static readonly Color LandColor = Color.FromArgb(TerrainColorPalette.LandR, TerrainColorPalette.LandG, TerrainColorPalette.LandB);

    public static Color GetWaterColor(TerrainWaterType waterType)
    {
        return waterType switch
        {
            TerrainWaterType.Shallow => ShallowWaterColor,
            TerrainWaterType.Sunlit => SunlitWaterColor,
            TerrainWaterType.Twilight => TwilightWaterColor,
            TerrainWaterType.Midnight => MidnightWaterColor,
            TerrainWaterType.Abyss => AbyssWaterColor,
            _ => SunlitWaterColor
        };
    }

    public static bool IsWaterColor(Color color)
    {
        int argb = color.ToArgb();
        return argb == ShallowWaterColor.ToArgb() ||
            argb == SunlitWaterColor.ToArgb() ||
            argb == TwilightWaterColor.ToArgb() ||
            argb == MidnightWaterColor.ToArgb() ||
            argb == AbyssWaterColor.ToArgb();
    }

    public static Bitmap Render(TerrainScene scene, Size size, float cameraX, float cameraY, float zoom, bool settledQuality, CancellationToken cancellationToken)
    {
        return TerrainProcessPreview.RenderBitmap(scene, size, cameraX, cameraY, zoom, settledQuality, cancellationToken);
    }

    private static void DrawLandform(
        Graphics graphics,
        Brush landBrush,
        Brush waterBrush,
        LandformInstance source,
        TerrainScene scene,
        int viewportWidth,
        int viewportHeight,
        float cameraX,
        float cameraY,
        float zoom)
    {
        float roughness = Math.Clamp(source.Roughness + scene.GlobalRoughness, 0f, 0.42f);
        float seedScale = 1f + ((Hash(source.Id, scene.Seed + 11) - 0.5f) * roughness * 0.85f);
        float scale = source.Scale * scene.GlobalScale * seedScale;
        float widthScale = source.WidthScale * scene.GlobalWidthScale * (1f + ((Hash(source.Id, scene.Seed + 17) - 0.5f) * roughness * 0.75f));
        float radius = source.BaseRadius * scale * zoom * PreviewRenderScale;
        float length = source.BaseLength * scale * zoom * PreviewRenderScale;
        float landWidth = source.BaseWidth * widthScale * zoom * PreviewRenderScale;
        float basinCut = Math.Clamp(source.BasinCut * scene.GlobalBasinCut, 0f, 2f);
        float openingWidth = Math.Clamp(source.OpeningWidth * scene.GlobalOpeningWidth, 0.05f, 4f);
        float openingStrength = Math.Clamp(source.OpeningStrength * scene.GlobalOpeningStrength, 0f, 3f);
        float rotation = source.RotationDegrees + ((Hash(source.Id, scene.Seed + 29) - 0.5f) * (10f + (roughness * 70f)));
        float phase = Hash(source.Id, scene.Seed + 53) * 360f;
        PointF center = WorldToRender(source.CenterX, source.CenterY, viewportWidth, viewportHeight, cameraX, cameraY, zoom);

        switch (source.Kind)
        {
            case LandformKind.AtollLagoon:
                FillEllipse(graphics, landBrush, center, radius * 1.12f, radius * 0.88f);
                FillEllipse(graphics, waterBrush, center, radius * (0.48f + (basinCut * 0.10f)), radius * (0.34f + (basinCut * 0.08f)));
                FillRotatedRectangle(graphics, waterBrush, center, rotation + phase, radius * 0.56f, landWidth * openingWidth * 1.8f, radius * 0.90f);
                break;

            case LandformKind.ReefBarrierLagoon:
                FillRotatedRectangle(graphics, landBrush, center, rotation, length, landWidth * 1.55f, 0f);
                FillRotatedRectangle(graphics, waterBrush, center, rotation, length * 0.12f, landWidth * openingWidth * 2.5f, length * 0.42f * openingStrength);
                FillRotatedRectangle(graphics, waterBrush, center, rotation, length * 0.12f, landWidth * openingWidth * 2.1f, -length * 0.44f * openingStrength);
                break;

            case LandformKind.BarrierIslandLagoon:
                FillRotatedRectangle(graphics, landBrush, center, rotation, length * 1.05f, landWidth * 1.75f, 0f);
                FillEllipse(graphics, landBrush, Offset(center, rotation, length * 0.78f, landWidth * 1.65f), landWidth * 2.6f, landWidth * 1.4f);
                FillRotatedRectangle(graphics, waterBrush, center, rotation, length * 0.13f, landWidth * openingWidth * 2.4f, length * 0.40f * openingStrength);
                break;

            case LandformKind.KarstHongLagoon:
                FillEllipse(graphics, landBrush, center, radius * 0.98f, radius * 1.08f);
                FillEllipse(graphics, waterBrush, center, radius * (0.40f + (basinCut * 0.10f)), radius * (0.44f + (basinCut * 0.11f)));
                FillRotatedRectangle(graphics, waterBrush, center, rotation + phase, radius * 0.46f, landWidth * openingWidth * 1.7f, radius * 0.52f);
                break;

            case LandformKind.IslandRingLagoon:
                int beadCount = 8;
                for (int beadIndex = 0; beadIndex < beadCount; beadIndex++)
                {
                    float angle = ((beadIndex / (float)beadCount) * MathF.Tau) + DegreesToRadians(phase);
                    PointF beadCenter = new(
                        center.X + (MathF.Cos(angle) * radius * 0.80f),
                        center.Y + (MathF.Sin(angle) * radius * 0.62f));
                    FillEllipse(graphics, landBrush, beadCenter, landWidth * 2.8f, landWidth * 2.0f);
                }
                break;

            case LandformKind.CalanqueCoveLagoon:
                FillRotatedRectangle(graphics, landBrush, center, rotation, length * 0.78f, landWidth * 1.45f, landWidth * 3.2f);
                FillRotatedRectangle(graphics, landBrush, center, rotation, length * 0.78f, landWidth * 1.45f, -landWidth * 3.2f);
                FillEllipse(graphics, landBrush, Offset(center, rotation, -length * 0.70f, 0f), landWidth * 4.8f, landWidth * 4.4f);
                FillRotatedRectangle(graphics, waterBrush, center, rotation, length * 0.74f, landWidth * openingWidth * 3.0f, 0f);
                break;
        }
    }

    private static void FillEllipse(Graphics graphics, Brush brush, PointF center, float radiusX, float radiusY)
    {
        graphics.FillEllipse(brush, center.X - radiusX, center.Y - radiusY, radiusX * 2f, radiusY * 2f);
    }

    private static void FillRotatedRectangle(
        Graphics graphics,
        Brush brush,
        PointF center,
        float degrees,
        float halfLength,
        float width,
        float alongOffset)
    {
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(center.X, center.Y);
        graphics.RotateTransform(degrees);
        graphics.FillRectangle(brush, alongOffset - halfLength, width * -0.5f, halfLength * 2f, width);
        graphics.Restore(state);
    }

    private static PointF Offset(PointF center, float degrees, float along, float across)
    {
        float radians = DegreesToRadians(degrees);
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        return new PointF(
            center.X + (cos * along) - (sin * across),
            center.Y + (sin * along) + (cos * across));
    }

    private static PointF WorldToRender(float worldX, float worldY, int viewportWidth, int viewportHeight, float cameraX, float cameraY, float zoom)
    {
        return new PointF(
            (((worldX - cameraX) * zoom) + (viewportWidth * 0.5f)) * PreviewRenderScale,
            (((worldY - cameraY) * zoom) + (viewportHeight * 0.5f)) * PreviewRenderScale);
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    private static float Hash(int x, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 374761393u;
            h = (h << 13) | (h >> 19);
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }
}

internal static class TerrainLabelRenderer
{
    private static readonly Color LabelForeground = Color.White;
    private static readonly Color LabelShadow = Color.FromArgb(150, 0, 0, 0);

    public static void Draw(Graphics graphics, Size size, float cameraX, float cameraY, float zoom, TerrainScene scene)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        using Font font = new("Segoe UI", 9f, FontStyle.Bold);
        using SolidBrush shadow = new(LabelShadow);
        using SolidBrush foreground = new(LabelForeground);
        foreach (LandformInstance instance in scene.Landforms)
        {
            if (instance.Kind == LandformKind.ArchipelagoRegion)
            {
                continue;
            }

            RectangleF labelBounds = BuildLabelBounds(graphics, font, instance, size, cameraX, cameraY, zoom);
            graphics.DrawString(instance.DisplayName, font, shadow, labelBounds.X + 8f, labelBounds.Y + 5f);
            graphics.DrawString(instance.DisplayName, font, foreground, labelBounds.X + 7f, labelBounds.Y + 4f);
        }
    }

    public static LandformInstance HitTest(Point screenPoint, Size size, float cameraX, float cameraY, float zoom, TerrainScene scene)
    {
        using Bitmap scratch = new(1, 1);
        using Graphics graphics = Graphics.FromImage(scratch);
        using Font font = new("Segoe UI", 9f, FontStyle.Bold);

        for (int i = scene.Landforms.Count - 1; i >= 0; i--)
        {
            LandformInstance instance = scene.Landforms[i];
            if (instance.Kind == LandformKind.ArchipelagoRegion)
            {
                continue;
            }

            RectangleF labelBounds = BuildLabelBounds(graphics, font, instance, size, cameraX, cameraY, zoom);
            if (labelBounds.Contains(screenPoint))
            {
                return instance;
            }
        }

        for (int i = scene.Landforms.Count - 1; i >= 0; i--)
        {
            LandformInstance instance = scene.Landforms[i];
            if (instance.Kind == LandformKind.ArchipelagoRegion)
            {
                continue;
            }

            RectangleF bodyBounds = BuildBodyBounds(instance, size, cameraX, cameraY, zoom);
            if (bodyBounds.Contains(screenPoint))
            {
                return instance;
            }
        }

        return null;
    }

    private static RectangleF BuildLabelBounds(
        Graphics graphics,
        Font font,
        LandformInstance instance,
        Size size,
        float cameraX,
        float cameraY,
        float zoom)
    {
        PointF center = WorldToScreen(instance.CenterX, instance.CenterY, size, cameraX, cameraY, zoom);
        SizeF textSize = graphics.MeasureString(instance.DisplayName, font);
        float width = textSize.Width + 14f;
        float height = textSize.Height + 8f;
        return new RectangleF(center.X - (width * 0.5f), center.Y - instance.LabelOffsetY - height, width, height);
    }

    private static RectangleF BuildBodyBounds(LandformInstance instance, Size size, float cameraX, float cameraY, float zoom)
    {
        PointF center = WorldToScreen(instance.CenterX, instance.CenterY, size, cameraX, cameraY, zoom);
        float radius = MathF.Max(instance.BaseRadius, instance.BaseLength) * instance.Scale * zoom * 1.05f;
        return new RectangleF(center.X - radius, center.Y - radius, radius * 2f, radius * 2f);
    }

    private static PointF WorldToScreen(float worldX, float worldY, Size size, float cameraX, float cameraY, float zoom)
    {
        return new PointF(
            ((worldX - cameraX) * zoom) + (size.Width * 0.5f),
            ((worldY - cameraY) * zoom) + (size.Height * 0.5f));
    }
}

internal static class TerrainGenerator
{
    public static float SampleScene(float worldX, float worldY, TerrainScene scene)
    {
        float field = -0.34f;
        for (int i = 0; i < scene.Landforms.Count; i++)
        {
            field = MathF.Max(field, SampleLandform(worldX, worldY, scene.Landforms[i], scene));
        }

        return field;
    }

    private static float SampleLandform(float worldX, float worldY, LandformInstance source, TerrainScene scene)
    {
        LandformInstance instance = source.Clone();
        instance.Scale *= scene.GlobalScale;
        instance.WidthScale *= scene.GlobalWidthScale;
        instance.BasinCut *= scene.GlobalBasinCut;
        instance.OpeningWidth *= scene.GlobalOpeningWidth;
        instance.OpeningStrength *= scene.GlobalOpeningStrength;
        instance.Roughness = Math.Clamp(instance.Roughness + scene.GlobalRoughness, 0f, 0.42f);

        float angle = DegreesToRadians(instance.RotationDegrees);
        float radius = instance.BaseRadius * instance.Scale * (0.92f + (Hash(instance.Id, scene.Seed + 11) * 0.16f));
        float length = instance.BaseLength * instance.Scale * (0.92f + (Hash(instance.Id, scene.Seed + 23) * 0.16f));
        float width = instance.BaseWidth * instance.WidthScale * (0.94f + (Hash(instance.Id, scene.Seed + 37) * 0.12f));
        float phase = Hash(instance.Id, scene.Seed + 51) * MathF.Tau;
        Rotate(worldX - instance.CenterX, worldY - instance.CenterY, -angle, out float localX, out float localY);

        float field = instance.Kind switch
        {
            LandformKind.AtollLagoon => Atoll(localX, localY, radius, width, instance, phase),
            LandformKind.ReefBarrierLagoon => ReefBarrier(localX, localY, length, width, instance, phase),
            LandformKind.BarrierIslandLagoon => BarrierIsland(localX, localY, length, width, instance, phase),
            LandformKind.KarstHongLagoon => KarstHong(localX, localY, radius, width, instance, phase),
            LandformKind.IslandRingLagoon => IslandRing(localX, localY, radius, width, instance, phase),
            LandformKind.CalanqueCoveLagoon => Calanque(localX, localY, length, width, instance),
            _ => -0.34f
        };

        if (instance.Roughness > 0f)
        {
            float noise = ValueNoise(
                (worldX * 0.006f) + (instance.Id * 9.7f),
                (worldY * 0.006f) - (instance.Id * 5.2f),
                scene.Seed + instance.Id * 101);
            field += (noise - 0.5f) * instance.Roughness;
        }

        return field;
    }

    private static float Atoll(float x, float y, float radius, float width, LandformInstance instance, float phase)
    {
        float distance = EllipseDistance(x, y, radius * 1.08f, radius * 0.86f);
        float ring = Bell(distance, 1f, Math.Max(0.08f, width / radius) * 1.55f) * 1.15f;
        float lagoon = SmoothStep(0.74f, 0.16f, distance) * instance.BasinCut;
        float opening = RadialOpeningCut(x, y, radius, width, instance, 1, phase);
        return ring - lagoon - opening - 0.09f;
    }

    private static float ReefBarrier(float x, float y, float length, float width, LandformInstance instance, float phase)
    {
        float curve = MathF.Sin((x / Math.Max(1f, length)) * MathF.PI + phase) * width * 1.25f;
        float barrier = Ridge(x, y - curve, length, width * 1.85f) * 1.08f;
        float shoal = Ridge(x, y + (width * 3.6f), length * 0.82f, width * 1.12f) * 0.28f;
        float opening = LinearOpeningCut(x, y - curve, length, width, instance);
        return barrier + shoal - opening - 0.18f;
    }

    private static float BarrierIsland(float x, float y, float length, float width, LandformInstance instance, float phase)
    {
        float curve = MathF.Sin((x / Math.Max(1f, length)) * MathF.PI * 1.15f + phase) * width * 1.7f;
        float island = Ridge(x, y - curve, length * 1.04f, width * 2.08f) * 1.08f;
        float hook = Ridge(x - (length * 0.72f), y + (width * 1.6f), length * 0.24f, width * 1.6f) * 0.36f;
        float opening = LinearOpeningCut(x, y - curve, length, width, instance);
        return island + hook - opening - 0.21f;
    }

    private static float KarstHong(float x, float y, float radius, float width, LandformInstance instance, float phase)
    {
        float distance = EllipseDistance(x, y, radius * 0.92f, radius * 1.04f);
        float wall = Bell(distance, 0.76f, Math.Max(0.08f, width / radius) * 2.25f) * 1.22f;
        float interior = SmoothStep(0.54f, 0.12f, distance) * instance.BasinCut;
        float breach = RadialOpeningCut(x, y, radius * 0.72f, width, instance, 1, phase) * 0.72f;
        return wall - interior - breach - 0.12f;
    }

    private static float IslandRing(float x, float y, float radius, float width, LandformInstance instance, float phase)
    {
        float distance = EllipseDistance(x, y, radius, radius * 0.82f);
        float theta = MathF.Atan2(y / 0.82f, x);
        float beads = 0.64f + (0.36f * MathF.Pow(MathF.Max(0f, MathF.Sin((theta * 5f) + phase)), 0.55f));
        float ring = Bell(distance, 0.88f, Math.Max(0.09f, width / radius) * 2.8f) * beads * 1.16f;
        float basin = SmoothStep(0.58f, 0.16f, distance) * instance.BasinCut * 0.84f;
        float openings = RadialOpeningCut(x, y, radius, width, instance, 2, phase) * 0.66f;
        return ring - basin - openings - 0.16f;
    }

    private static float Calanque(float x, float y, float length, float width, LandformInstance instance)
    {
        float halfLength = length * 0.76f;
        float upperWall = Ridge(x, y - (width * 3.2f), halfLength, width * 1.55f);
        float lowerWall = Ridge(x, y + (width * 3.2f), halfLength, width * 1.55f);
        float backWall = Bell(EllipseDistance(x + (halfLength * 0.78f), y, width * 4.9f, width * 3.8f), 1f, 0.34f);
        float coveWater = SmoothStep(width * 4.4f, width * 1.05f, MathF.Abs(y)) *
            SmoothStep(halfLength, -halfLength * 0.78f, x) *
            instance.BasinCut;
        return ((upperWall + lowerWall + backWall) * 0.90f) - coveWater - 0.18f;
    }

    private static float RadialOpeningCut(float x, float y, float radius, float width, LandformInstance instance, int openingCount, float phase)
    {
        float cut = 0f;
        for (int i = 0; i < openingCount; i++)
        {
            float angle = phase + ((i / (float)openingCount) * MathF.Tau);
            float gateX = MathF.Cos(angle) * radius * 0.88f;
            float gateY = MathF.Sin(angle) * radius * 0.88f;
            Rotate(x - gateX, y - gateY, -angle, out float along, out float across);
            cut = MathF.Max(cut, Ridge(along, across, radius * 0.28f, width * instance.OpeningWidth * 2.35f) * instance.OpeningStrength);
        }

        return cut;
    }

    private static float LinearOpeningCut(float x, float y, float length, float width, LandformInstance instance)
    {
        float left = Ridge(x + (length * 0.44f), y, length * 0.10f, width * instance.OpeningWidth * 1.9f);
        float right = Ridge(x - (length * 0.46f), y, length * 0.10f, width * instance.OpeningWidth * 1.7f);
        return MathF.Max(left, right) * instance.OpeningStrength;
    }

    private static float Ridge(float localX, float localY, float halfLength, float width)
    {
        float along = SmoothStep(1.08f, 0.72f, MathF.Abs(localX) / Math.Max(1f, halfLength));
        float across = SmoothStep(1.08f, 0.10f, MathF.Abs(localY) / Math.Max(1f, width));
        return along * across;
    }

    private static float Bell(float value, float center, float width)
    {
        float normalized = (value - center) / Math.Max(0.001f, width);
        return MathF.Exp(-(normalized * normalized));
    }

    private static float EllipseDistance(float x, float y, float radiusX, float radiusY)
    {
        float nx = x / Math.Max(1f, radiusX);
        float ny = y / Math.Max(1f, radiusY);
        return MathF.Sqrt((nx * nx) + (ny * ny));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float denominator = edge1 - edge0;
        if (MathF.Abs(denominator) < 0.0001f)
        {
            return value >= edge1 ? 1f : 0f;
        }

        float t = Math.Clamp((value - edge0) / denominator, 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static void Rotate(float x, float y, float angle, out float rotatedX, out float rotatedY)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        rotatedX = (x * cos) - (y * sin);
        rotatedY = (x * sin) + (y * cos);
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int x0 = FastFloor(x);
        int y0 = FastFloor(y);
        float tx = x - x0;
        float ty = y - y0;
        tx = tx * tx * (3f - (2f * tx));
        ty = ty * ty * (3f - (2f * ty));

        float a = Hash(x0, y0, seed);
        float b = Hash(x0 + 1, y0, seed);
        float c = Hash(x0, y0 + 1, seed);
        float d = Hash(x0 + 1, y0 + 1, seed);
        float ab = a + ((b - a) * tx);
        float cd = c + ((d - c) * tx);
        return ab + ((cd - ab) * ty);
    }

    private static float Hash(int x, int seed)
    {
        return Hash(x, x * 31, seed);
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 374761393u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 668265263u;
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static int FastFloor(float value)
    {
        int integer = (int)value;
        return value < integer ? integer - 1 : integer;
    }
}

internal sealed class TerrainScene
{
    private const int WorldIslandGenerationLimit = TerrainWorldDefaults.WorldIslandGenerationLimit;
    private const int WorldLandformTypeGenerationLimit = TerrainWorldDefaults.WorldLandformTypeGenerationLimit;
    private readonly Dictionary<int, int> _landformIndexCache = [];
    private readonly Dictionary<int, EffectiveLandformValues> _effectiveLandformCache = [];
    private TerrainScene _cachedWorldPreviewScene;
    private string _cachedWorldPreviewSignature = string.Empty;
    private bool _renderCachesReady;

    public int Seed { get; private set; }
    public int SelectedId { get; set; }
    public float GlobalScale { get; set; } = TerrainWorldDefaults.GlobalScale;
    public float GlobalWidthScale { get; set; } = TerrainWorldDefaults.GlobalWidthScale;
    public float GlobalBasinCut { get; set; } = TerrainWorldDefaults.GlobalBasinCut;
    public float GlobalOpeningWidth { get; set; } = TerrainWorldDefaults.GlobalOpeningWidth;
    public float GlobalOpeningStrength { get; set; } = TerrainWorldDefaults.GlobalOpeningStrength;
    public float GlobalRoughness { get; set; } = TerrainWorldDefaults.GlobalRoughness;
    public float GlobalEmergence { get; set; } = TerrainWorldDefaults.GlobalEmergence;
    public float RasterSmoothing { get; set; } = TerrainWorldDefaults.RasterSmoothing;
    public float WaterShallowDistance { get; set; } = TerrainWorldDefaults.WaterShallowDistance;
    public float WaterSunlitDistance { get; set; } = TerrainWorldDefaults.WaterSunlitDistance;
    public float WaterTwilightDistance { get; set; } = TerrainWorldDefaults.WaterTwilightDistance;
    public float WaterMidnightDistance { get; set; } = TerrainWorldDefaults.WaterMidnightDistance;
    public float WaterStochasticReach { get; set; } = TerrainWorldDefaults.WaterStochasticReach;
    public float WaterStochasticScale { get; set; } = TerrainWorldDefaults.WaterStochasticScale;
    public float WaterCoastShapeRounding { get; set; } = TerrainWorldDefaults.WaterCoastShapeRounding;
    public float WorldIslandCount { get; set; } = TerrainWorldDefaults.WorldIslandCount;
    public float WorldMinimumSpacing { get; set; } = TerrainWorldDefaults.WorldMinimumSpacing;
    public float WorldInteractionSpacing { get; set; } = TerrainWorldDefaults.WorldInteractionSpacing;
    public float WorldPlacementJitter { get; set; } = TerrainWorldDefaults.WorldPlacementJitter;
    public float WorldClusterCountMinimum { get; set; } = TerrainWorldDefaults.WorldClusterCountMinimum;
    public float WorldClusterCountMaximum { get; set; } = TerrainWorldDefaults.WorldClusterCountMaximum;
    public float WorldClusterSpread { get; set; } = TerrainWorldDefaults.WorldClusterSpread;
    public float WorldChainBias { get; set; } = TerrainWorldDefaults.WorldChainBias;
    public int WorldClusterShapeSeed { get; set; } = TerrainWorldDefaults.WorldClusterShapeSeed;
    public int WorldIslandScatterSeed { get; set; } = TerrainWorldDefaults.WorldIslandScatterSeed;
    public int WorldIslandVariationSeed { get; set; } = TerrainWorldDefaults.WorldIslandVariationSeed;
    public float WorldIslandSizeVariance { get; set; } = TerrainWorldDefaults.WorldIslandSizeVariance;
    public float WorldGiantRiverRngMinimum { get; set; } = TerrainWorldDefaults.WorldGiantRiverRngMinimum;
    public float WorldGiantRiverRngMaximum { get; set; } = TerrainWorldDefaults.WorldGiantRiverRngMaximum;
    public float WorldCalanqueRngMinimum { get; set; } = TerrainWorldDefaults.WorldCalanqueRngMinimum;
    public float WorldCalanqueRngMaximum { get; set; } = TerrainWorldDefaults.WorldCalanqueRngMaximum;
    public float WorldBarrierLagoonRngMinimum { get; set; } = TerrainWorldDefaults.WorldBarrierLagoonRngMinimum;
    public float WorldBarrierLagoonRngMaximum { get; set; } = TerrainWorldDefaults.WorldBarrierLagoonRngMaximum;
    public float WorldTowerStacksRngMinimum { get; set; } = TerrainWorldDefaults.WorldTowerStacksRngMinimum;
    public float WorldTowerStacksRngMaximum { get; set; } = TerrainWorldDefaults.WorldTowerStacksRngMaximum;
    public float WorldGiantRiverSizeMinimum { get; set; } = TerrainWorldDefaults.WorldGiantRiverSizeMinimum;
    public float WorldGiantRiverSizeMaximum { get; set; } = TerrainWorldDefaults.WorldGiantRiverSizeMaximum;
    public float WorldCalanqueSizeMinimum { get; set; } = TerrainWorldDefaults.WorldCalanqueSizeMinimum;
    public float WorldCalanqueSizeMaximum { get; set; } = TerrainWorldDefaults.WorldCalanqueSizeMaximum;
    public float WorldBarrierLagoonSizeMinimum { get; set; } = TerrainWorldDefaults.WorldBarrierLagoonSizeMinimum;
    public float WorldBarrierLagoonSizeMaximum { get; set; } = TerrainWorldDefaults.WorldBarrierLagoonSizeMaximum;
    public float WorldTowerStacksSizeMinimum { get; set; } = TerrainWorldDefaults.WorldTowerStacksSizeMinimum;
    public float WorldTowerStacksSizeMaximum { get; set; } = TerrainWorldDefaults.WorldTowerStacksSizeMaximum;
    public bool WorldPreviewEnabled { get; set; }
    public bool WorldPreviewLabelsVisible { get; set; } = false;
    public TerrainOverlayMode OverlayMode { get; set; } = TerrainOverlayMode.SolidLandWater;
    public List<LandformInstance> Landforms { get; } = [];
    public LandformInstance Selected => Landforms.Find(landform => landform.Id == SelectedId) ?? Landforms[0];
    public LandformInstance PrimaryIsland => Landforms.Find(landform => landform.Kind == LandformKind.MainIsland && landform.ParentId == 1) ?? Landforms[0];

    public static TerrainScene CreateDefault(int seed)
    {
        TerrainScene scene = new() { Seed = seed };
        scene.Landforms.Add(new LandformInstance(1, LandformKind.ArchipelagoRegion, "world placement root", TerrainWorldDefaults.RootCenterX, TerrainWorldDefaults.RootCenterY, TerrainWorldDefaults.RootBaseRadius, TerrainWorldDefaults.RootBaseLength, TerrainWorldDefaults.RootBaseWidth, TerrainWorldDefaults.RootRotationDegrees, TerrainWorldDefaults.RootLabelOffsetY, TerrainWorldDefaults.RootScale, TerrainWorldDefaults.RootWidthScale, TerrainWorldDefaults.RootBasinCut, TerrainWorldDefaults.RootOpeningWidth, TerrainWorldDefaults.RootOpeningStrength, TerrainWorldDefaults.RootRoughness, TerrainWorldDefaults.RootEmergence));
        scene.Landforms.Add(new LandformInstance(10, LandformKind.MainIsland, "island body", TerrainWorldDefaults.MainIslandCenterX, TerrainWorldDefaults.MainIslandCenterY, TerrainWorldDefaults.MainIslandBaseRadius, TerrainWorldDefaults.MainIslandBaseLength, TerrainWorldDefaults.MainIslandBaseWidth, TerrainWorldDefaults.MainIslandRotationDegrees, TerrainWorldDefaults.MainIslandLabelOffsetY, TerrainWorldDefaults.MainIslandScale, TerrainWorldDefaults.MainIslandWidthScale, TerrainWorldDefaults.MainIslandBasinCut, TerrainWorldDefaults.MainIslandOpeningWidth, TerrainWorldDefaults.MainIslandOpeningStrength, TerrainWorldDefaults.MainIslandRoughness, TerrainWorldDefaults.MainIslandEmergence, 1));
        scene.SelectedId = 10;
        return scene;
    }

    public void SetSeed(int seed)
    {
        Seed = seed;
    }

    public void RerollSeed()
    {
        unchecked
        {
            Seed = (Seed * 1103515245) + 12345;
        }
    }

    public void RerollWorldClusterShapeSeed()
    {
        WorldClusterShapeSeed = RerollSeedValue(WorldClusterShapeSeed, 0x41C64E6D);
    }

    public void RerollWorldIslandScatterSeed()
    {
        WorldIslandScatterSeed = RerollSeedValue(WorldIslandScatterSeed, 0x2C1B3C6D);
    }

    public void RerollWorldIslandVariationSeed()
    {
        WorldIslandVariationSeed = RerollSeedValue(WorldIslandVariationSeed, 0x165667B1);
    }

    private static int RerollSeedValue(int seed, int salt)
    {
        unchecked
        {
            return (seed * 1103515245) + 12345 + salt;
        }
    }

    public void ResetSelected()
    {
        TerrainScene defaults = CreateDefault(Seed);
        LandformInstance defaultInstance = defaults.Landforms.Find(landform => landform.Id == SelectedId);
        LandformInstance selected = Selected;
        if (defaultInstance == null)
        {
            return;
        }

        selected.CopyEditableFrom(defaultInstance);
    }

    public void ResetGlobalVariables()
    {
        GlobalScale = TerrainWorldDefaults.GlobalScale;
        GlobalWidthScale = TerrainWorldDefaults.GlobalWidthScale;
        GlobalBasinCut = TerrainWorldDefaults.GlobalBasinCut;
        GlobalOpeningWidth = TerrainWorldDefaults.GlobalOpeningWidth;
        GlobalOpeningStrength = TerrainWorldDefaults.GlobalOpeningStrength;
        GlobalRoughness = TerrainWorldDefaults.GlobalRoughness;
        GlobalEmergence = TerrainWorldDefaults.GlobalEmergence;
        RasterSmoothing = TerrainWorldDefaults.RasterSmoothing;
        WaterShallowDistance = TerrainWorldDefaults.WaterShallowDistance;
        WaterSunlitDistance = TerrainWorldDefaults.WaterSunlitDistance;
        WaterTwilightDistance = TerrainWorldDefaults.WaterTwilightDistance;
        WaterMidnightDistance = TerrainWorldDefaults.WaterMidnightDistance;
        WaterStochasticReach = TerrainWorldDefaults.WaterStochasticReach;
        WaterStochasticScale = TerrainWorldDefaults.WaterStochasticScale;
        WaterCoastShapeRounding = TerrainWorldDefaults.WaterCoastShapeRounding;
        WorldIslandCount = TerrainWorldDefaults.WorldIslandCount;
        WorldMinimumSpacing = TerrainWorldDefaults.WorldMinimumSpacing;
        WorldInteractionSpacing = TerrainWorldDefaults.WorldInteractionSpacing;
        WorldPlacementJitter = TerrainWorldDefaults.WorldPlacementJitter;
        WorldClusterCountMinimum = TerrainWorldDefaults.WorldClusterCountMinimum;
        WorldClusterCountMaximum = TerrainWorldDefaults.WorldClusterCountMaximum;
        WorldClusterSpread = TerrainWorldDefaults.WorldClusterSpread;
        WorldChainBias = TerrainWorldDefaults.WorldChainBias;
        WorldClusterShapeSeed = TerrainWorldDefaults.WorldClusterShapeSeed;
        WorldIslandScatterSeed = TerrainWorldDefaults.WorldIslandScatterSeed;
        WorldIslandVariationSeed = TerrainWorldDefaults.WorldIslandVariationSeed;
        WorldIslandSizeVariance = TerrainWorldDefaults.WorldIslandSizeVariance;
        WorldGiantRiverRngMinimum = TerrainWorldDefaults.WorldGiantRiverRngMinimum;
        WorldGiantRiverRngMaximum = TerrainWorldDefaults.WorldGiantRiverRngMaximum;
        WorldCalanqueRngMinimum = TerrainWorldDefaults.WorldCalanqueRngMinimum;
        WorldCalanqueRngMaximum = TerrainWorldDefaults.WorldCalanqueRngMaximum;
        WorldBarrierLagoonRngMinimum = TerrainWorldDefaults.WorldBarrierLagoonRngMinimum;
        WorldBarrierLagoonRngMaximum = TerrainWorldDefaults.WorldBarrierLagoonRngMaximum;
        WorldTowerStacksRngMinimum = TerrainWorldDefaults.WorldTowerStacksRngMinimum;
        WorldTowerStacksRngMaximum = TerrainWorldDefaults.WorldTowerStacksRngMaximum;
        WorldGiantRiverSizeMinimum = TerrainWorldDefaults.WorldGiantRiverSizeMinimum;
        WorldGiantRiverSizeMaximum = TerrainWorldDefaults.WorldGiantRiverSizeMaximum;
        WorldCalanqueSizeMinimum = TerrainWorldDefaults.WorldCalanqueSizeMinimum;
        WorldCalanqueSizeMaximum = TerrainWorldDefaults.WorldCalanqueSizeMaximum;
        WorldBarrierLagoonSizeMinimum = TerrainWorldDefaults.WorldBarrierLagoonSizeMinimum;
        WorldBarrierLagoonSizeMaximum = TerrainWorldDefaults.WorldBarrierLagoonSizeMaximum;
        WorldTowerStacksSizeMinimum = TerrainWorldDefaults.WorldTowerStacksSizeMinimum;
        WorldTowerStacksSizeMaximum = TerrainWorldDefaults.WorldTowerStacksSizeMaximum;
        WorldPreviewLabelsVisible = false;
    }

    public LandformInstance AddLandform(LandformKind kind)
    {
        if (kind == LandformKind.Atoll)
        {
            kind = LandformKind.BarrierIsland;
        }
        else if (kind == LandformKind.KarstHong)
        {
            kind = LandformKind.CalanqueCove;
        }

        int id = Landforms.Count == 0 ? 10 : Landforms.Max(landform => landform.Id) + 1;
        int childCount = Landforms.Count(landform => landform.ParentId == PrimaryIsland.Id);
        LandformPlacement placement = ResolveNewLandformPlacement(kind, id, childCount);

        LandformInstance instance = kind switch
        {
            LandformKind.GiantRiver => new LandformInstance(id, kind, "giant river", placement.CenterX, placement.CenterY, TerrainWorldDefaults.GiantRiverBaseRadius, TerrainWorldDefaults.GiantRiverBaseLength, TerrainWorldDefaults.GiantRiverBaseWidth, placement.RotationDegrees, TerrainWorldDefaults.GiantRiverLabelOffsetY, 1.06f, TerrainWorldDefaults.GiantRiverWidthScale, TerrainWorldDefaults.GiantRiverBasinCut, TerrainWorldDefaults.GiantRiverOpeningWidth, TerrainWorldDefaults.GiantRiverOpeningStrength, 0.02f, TerrainWorldDefaults.GiantRiverEmergence, PrimaryIsland.Id),
            LandformKind.CalanqueCove => new LandformInstance(id, kind, "calanque", placement.CenterX, placement.CenterY, TerrainWorldDefaults.CalanqueBaseRadius, TerrainWorldDefaults.CalanqueBaseLength, TerrainWorldDefaults.CalanqueBaseWidth, placement.RotationDegrees, TerrainWorldDefaults.CalanqueLabelOffsetY, TerrainWorldDefaults.CalanqueScaleMultiplier, TerrainWorldDefaults.CalanqueWidthScale, TerrainWorldDefaults.CalanqueBasinCut, TerrainWorldDefaults.CalanqueOpeningWidth, TerrainWorldDefaults.CalanqueOpeningStrength, 0.012f, TerrainWorldDefaults.CalanqueEmergence, PrimaryIsland.Id),
            LandformKind.BarrierIsland => new LandformInstance(id, kind, "barrier lagoon", placement.CenterX, placement.CenterY, TerrainWorldDefaults.BarrierLagoonBaseRadius, TerrainWorldDefaults.BarrierLagoonBaseLength, TerrainWorldDefaults.BarrierLagoonBaseWidth, placement.RotationDegrees, TerrainWorldDefaults.BarrierLagoonLabelOffsetY, TerrainWorldDefaults.BarrierLagoonScaleMultiplier, TerrainWorldDefaults.BarrierLagoonWidthScale, TerrainWorldDefaults.BarrierLagoonBasinCut, TerrainWorldDefaults.BarrierLagoonOpeningWidth, TerrainWorldDefaults.BarrierLagoonOpeningStrength, TerrainWorldDefaults.BarrierLagoonRoughness, TerrainWorldDefaults.BarrierLagoonEmergence, PrimaryIsland.Id),
            LandformKind.StacksAndArches => new LandformInstance(id, kind, "tower stacks", placement.CenterX, placement.CenterY, TerrainWorldDefaults.TowerStacksBaseRadius, TerrainWorldDefaults.TowerStacksBaseLength, TerrainWorldDefaults.TowerStacksBaseWidth, placement.RotationDegrees, TerrainWorldDefaults.TowerStacksLabelOffsetY, 1.00f, TerrainWorldDefaults.TowerStacksWidthScale, TerrainWorldDefaults.TowerStacksBasinCut, TerrainWorldDefaults.TowerStacksOpeningWidth, TerrainWorldDefaults.TowerStacksOpeningStrength, 0.012f, TerrainWorldDefaults.TowerStacksEmergence, PrimaryIsland.Id),
            _ => new LandformInstance(id, kind, kind.ToString(), placement.CenterX, placement.CenterY, 118f, 280f, 48f, placement.RotationDegrees, -6f, 1f, 1f, 0.8f, 0.8f, 0.8f, 0.012f, 0.8f, PrimaryIsland.Id)
        };

        Landforms.Add(instance);
        return instance;
    }

    private LandformPlacement ResolveNewLandformPlacement(LandformKind kind, int id, int childCount)
    {
        LandformInstance island = PrimaryIsland;
        if (kind == LandformKind.GiantRiver)
        {
            float trend = island.RotationDegrees + ((Hash(Seed + id * 19, childCount + 3) - 0.5f) * 34f);
            return new LandformPlacement(island.CenterX, island.CenterY, NormalizeDegrees(trend));
        }

        if (TryResolveRecognizedShorePlacement(kind, id, childCount, out LandformPlacement placement))
        {
            return placement;
        }

        float islandScale = island.Scale * GlobalScale;
        float islandWidthScale = island.WidthScale * GlobalWidthScale;
        float halfLength = Math.Max(96f, island.BaseLength * islandScale);
        float halfWidth = Math.Max(72f, island.BaseRadius * islandScale * (0.62f + (islandWidthScale * 0.22f)));
        float angle = (childCount * 2.3999631f) + ((Hash(Seed + id * 23, childCount + 11) - 0.5f) * 0.72f);
        float borderDistance = kind switch
        {
            LandformKind.CalanqueCove => 0.92f,
            LandformKind.BarrierIsland => 1.08f,
            LandformKind.StacksAndArches => 1.05f,
            _ => 0.92f
        };

        float localX = MathF.Cos(angle) * halfLength * borderDistance;
        float localY = MathF.Sin(angle) * halfWidth * borderDistance;
        RotateVector(localX, localY, DegreesToRadians(island.RotationDegrees), out float rotatedX, out float rotatedY);
        RotateVector(MathF.Cos(angle), MathF.Sin(angle), DegreesToRadians(island.RotationDegrees), out float normalX, out float normalY);
        float outwardDegrees = RadiansToDegrees(MathF.Atan2(normalY, normalX));
        float tangentJitter = (Hash(Seed + id * 29, childCount + 17) - 0.5f) * 18f;
        float rotation = kind switch
        {
            LandformKind.CalanqueCove => outwardDegrees,
            LandformKind.BarrierIsland => outwardDegrees + 90f + tangentJitter,
            LandformKind.StacksAndArches => outwardDegrees + 90f + tangentJitter,
            _ => outwardDegrees
        };

        return new LandformPlacement(
            island.CenterX + rotatedX,
            island.CenterY + rotatedY,
            NormalizeDegrees(rotation));
    }

    private bool TryResolveRecognizedShorePlacement(LandformKind kind, int id, int childCount, out LandformPlacement placement)
    {
        placement = default;
        TerrainScene shoreScene = CreateIslandBodyOnlyScene();
        LandformInstance island = shoreScene.PrimaryIsland;
        float islandScale = island.Scale * shoreScene.GlobalScale;
        float islandWidthScale = island.WidthScale * shoreScene.GlobalWidthScale;
        float halfLength = Math.Max(96f, island.BaseLength * islandScale);
        float halfWidth = Math.Max(72f, island.BaseRadius * islandScale * (0.62f + (islandWidthScale * 0.22f)));
        float maxTraceDistance = Math.Max(halfLength, halfWidth) * 1.55f;
        float preferredAngle = (childCount * 2.3999631f) + (Hash(Seed + id * 23, childCount + 11) * MathF.Tau);

        ShoreCandidate best = default;
        float bestScore = float.NegativeInfinity;
        const int candidateCount = 112;
        for (int i = 0; i < candidateCount; i++)
        {
            float angle = preferredAngle + (i * 2.3999631f);
            if (!TryTraceShoreCandidate(shoreScene, island, angle, maxTraceDistance, out ShoreCandidate candidate))
            {
                continue;
            }

            float score = ScoreShoreCandidate(kind, candidate, angle, preferredAngle, id);
            if (score <= bestScore)
            {
                continue;
            }

            best = candidate;
            bestScore = score;
        }

        if (bestScore <= float.NegativeInfinity)
        {
            return false;
        }

        float centerOffset = kind switch
        {
            LandformKind.CalanqueCove => -72f,
            LandformKind.BarrierIsland => -54f,
            LandformKind.StacksAndArches => -42f,
            _ => -48f
        };
        float centerX = best.X + (best.NormalX * centerOffset);
        float centerY = best.Y + (best.NormalY * centerOffset);
        NudgePlacementOntoRecognizedShore(shoreScene, best, ref centerX, ref centerY);

        float outwardDegrees = RadiansToDegrees(MathF.Atan2(best.NormalY, best.NormalX));
        float tangentJitter = (Hash(Seed + id * 29, childCount + 17) - 0.5f) * 16f;
        float rotation = kind switch
        {
            LandformKind.CalanqueCove => outwardDegrees,
            LandformKind.BarrierIsland => outwardDegrees + 90f + tangentJitter,
            LandformKind.StacksAndArches => outwardDegrees + 90f + tangentJitter,
            _ => outwardDegrees
        };

        placement = new LandformPlacement(centerX, centerY, NormalizeDegrees(rotation));
        return true;
    }

    private TerrainScene CreateIslandBodyOnlyScene()
    {
        TerrainScene scene = Clone();
        int rootId = scene.Landforms.Find(landform => landform.Kind == LandformKind.ArchipelagoRegion)?.Id ?? 1;
        int islandId = scene.PrimaryIsland.Id;
        scene.Landforms.RemoveAll(landform => landform.Id != rootId && landform.Id != islandId);
        scene.SelectedId = islandId;
        return scene;
    }

    private static bool TryTraceShoreCandidate(
        TerrainScene shoreScene,
        LandformInstance island,
        float angle,
        float maxTraceDistance,
        out ShoreCandidate candidate)
    {
        candidate = default;
        float normalX = MathF.Cos(angle);
        float normalY = MathF.Sin(angle);
        float step = Math.Clamp(maxTraceDistance / 58f, 10f, 18f);
        bool sawLand = false;
        float lastLandX = island.CenterX;
        float lastLandY = island.CenterY;
        float landDistance = 0f;

        for (float distance = step; distance <= maxTraceDistance; distance += step)
        {
            float x = island.CenterX + (normalX * distance);
            float y = island.CenterY + (normalY * distance);
            TerrainProcessCell cell = TerrainProcessPreview.SampleCell(x, y, shoreScene, includeDebugFields: false);
            if (cell.IsLand)
            {
                sawLand = true;
                lastLandX = x;
                lastLandY = y;
                landDistance = distance;
                continue;
            }

            if (!sawLand || landDistance < maxTraceDistance * 0.26f)
            {
                continue;
            }

            float landRun = CountSamples(shoreScene, lastLandX, lastLandY, -normalX, -normalY, step, wantLand: true);
            float waterRun = CountSamples(shoreScene, lastLandX, lastLandY, normalX, normalY, step, wantLand: false);
            if (landRun < 3f || waterRun < 3f)
            {
                return false;
            }

            TerrainProcessCell shoreCell = TerrainProcessPreview.SampleCell(lastLandX, lastLandY, shoreScene, includeDebugFields: true);
            TerrainProcessCell waterCell = TerrainProcessPreview.SampleCell(
                lastLandX + (normalX * step * 2f),
                lastLandY + (normalY * step * 2f),
                shoreScene,
                includeDebugFields: true);
            candidate = new ShoreCandidate(lastLandX, lastLandY, normalX, normalY, landDistance / Math.Max(1f, maxTraceDistance), landRun, waterRun, shoreCell, waterCell);
            return true;
        }

        return false;
    }

    private static float CountSamples(TerrainScene scene, float startX, float startY, float dirX, float dirY, float step, bool wantLand)
    {
        float count = 0f;
        for (int i = 1; i <= 7; i++)
        {
            TerrainProcessCell cell = TerrainProcessPreview.SampleCell(
                startX + (dirX * step * i),
                startY + (dirY * step * i),
                scene,
                includeDebugFields: false);
            if (cell.IsLand != wantLand)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private float ScoreShoreCandidate(LandformKind kind, ShoreCandidate candidate, float angle, float preferredAngle, int id)
    {
        float score = 0f;
        score += MathF.Min(candidate.InlandSamples, 6f) * 1.25f;
        score += MathF.Min(candidate.OutwardWaterSamples, 6f) * 1.40f;
        score += SmoothStep(0.38f, 0.86f, candidate.RadialFraction) * 4.0f;
        score -= SmoothStep(0.88f, 1.0f, candidate.RadialFraction) * 1.1f;
        score -= AngularDistance(angle, preferredAngle) * 0.16f;

        score += kind switch
        {
            LandformKind.CalanqueCove => (candidate.ShoreCell.FractureStrength * 2.3f) + (candidate.ShoreCell.Slope * 1.7f) + (candidate.ShoreCell.WaveExposure * 0.7f),
            LandformKind.BarrierIsland => (candidate.WaterCell.Sediment * 2.1f) + ((1f - candidate.WaterCell.WaveExposure) * 1.3f),
            LandformKind.StacksAndArches => (candidate.ShoreCell.WaveExposure * 2.0f) + (candidate.ShoreCell.Slope * 1.3f) + (candidate.ShoreCell.FractureStrength * 1.1f),
            _ => candidate.ShoreCell.Slope + candidate.ShoreCell.FractureStrength
        };

        for (int i = 0; i < Landforms.Count; i++)
        {
            LandformInstance existing = Landforms[i];
            if (existing.ParentId != PrimaryIsland.Id)
            {
                continue;
            }

            float dx = candidate.X - existing.CenterX;
            float dy = candidate.Y - existing.CenterY;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            float desiredSpacing = Math.Max(90f, (existing.BaseRadius * existing.Scale) + 70f);
            if (distance < desiredSpacing)
            {
                score -= (desiredSpacing - distance) / desiredSpacing * 4.0f;
            }
        }

        score += (Hash(Seed + id * 37, (int)(candidate.X * 3f) ^ (int)(candidate.Y * 5f)) - 0.5f) * 0.35f;
        return score;
    }

    private static void NudgePlacementOntoRecognizedShore(TerrainScene shoreScene, ShoreCandidate shore, ref float centerX, ref float centerY)
    {
        for (int i = 0; i < 8; i++)
        {
            TerrainProcessCell cell = TerrainProcessPreview.SampleCell(centerX, centerY, shoreScene, includeDebugFields: false);
            if (cell.IsLand)
            {
                return;
            }

            centerX -= shore.NormalX * 10f;
            centerY -= shore.NormalY * 10f;
        }

        centerX = shore.X;
        centerY = shore.Y;
    }

    public bool TryDeleteSelectedLandform(out string deletedName, out int deletedCount)
    {
        deletedName = string.Empty;
        deletedCount = 0;
        LandformInstance selected = Selected;
        if (selected.Kind is LandformKind.ArchipelagoRegion or LandformKind.MainIsland || selected.ParentId == 0)
        {
            return false;
        }

        deletedName = BuildHierarchyPath(selected);
        HashSet<int> deleteIds = [];
        CollectDescendantIds(selected.Id, deleteIds);
        deleteIds.Add(selected.Id);
        deletedCount = Landforms.RemoveAll(landform => deleteIds.Contains(landform.Id));
        SelectedId = PrimaryIsland.Id;
        return deletedCount > 0;
    }

    public List<WorldIslandPlacement> BuildArchipelagoPlacements()
    {
        int count = Math.Clamp((int)MathF.Round(WorldIslandCount), 1, WorldIslandGenerationLimit);
        float variance = Math.Clamp(WorldIslandSizeVariance, 0f, 2f);
        float baseRadius = PrimaryIsland.BaseRadius * PrimaryIsland.Scale;
        float minSpacing = Math.Max(baseRadius * 0.82f, WorldMinimumSpacing);
        float archipelagoRadius = Math.Max(WorldInteractionSpacing, minSpacing) * (1.25f + (count * 0.18f));
        int clusterCount = ResolveWorldClusterCount(count);
        float clusterSpread = Math.Clamp(WorldClusterSpread, 0.12f, 3.0f);
        float chainBias = Math.Clamp(WorldChainBias, 0f, 1f);
        List<WorldClusterAnchor> clusters = BuildWorldClusterAnchors(clusterCount, archipelagoRadius, minSpacing, clusterSpread, chainBias);
        int[] clusterUseCounts = new int[clusterCount];
        List<WorldIslandPlacement> placements = [];

        for (int i = 0; i < count; i++)
        {
            int clusterIndex = ResolveIslandClusterIndex(i, clusterUseCounts);
            WorldClusterAnchor cluster = clusters[clusterIndex];
            float scale = Math.Clamp(1f + ((Hash(WorldIslandVariationSeed + i * 17, i) - 0.5f) * 2f * variance), 0.24f, 3.50f);
            float widthScale = Math.Clamp(1f + ((Hash(WorldIslandVariationSeed + i * 19, i) - 0.5f) * variance * 1.15f), 0.25f, 3.00f);
            float radius = baseRadius * scale;
            float rotation = NormalizeDegrees(PrimaryIsland.RotationDegrees + ((Hash(WorldIslandVariationSeed + i * 21, i) - 0.5f) * 120f));
            int giantRiverCount = ResolveWorldLandformCount(LandformKind.GiantRiver, i);
            int calanqueCount = ResolveWorldLandformCount(LandformKind.CalanqueCove, i);
            int barrierLagoonCount = ResolveWorldLandformCount(LandformKind.BarrierIsland, i);
            int towerStacksCount = ResolveWorldLandformCount(LandformKind.StacksAndArches, i);
            float x;
            float y;
            if (i == 0)
            {
                x = cluster.X;
                y = cluster.Y;
            }
            else
            {
                float bestScore = float.NegativeInfinity;
                x = cluster.X;
                y = cluster.Y;
                for (int attempt = 0; attempt < 128; attempt++)
                {
                    ResolveClusteredIslandCandidate(cluster, clusterUseCounts[clusterIndex], i, attempt, minSpacing, chainBias, out float candidateX, out float candidateY);
                    float score = ScoreClusteredIslandCandidate(
                        candidateX,
                        candidateY,
                        radius,
                        clusterIndex,
                        cluster,
                        placements,
                        minSpacing,
                        archipelagoRadius,
                        chainBias);
                    score += Hash(WorldIslandScatterSeed + i * 113, attempt * 31) * 0.35f;
                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    x = candidateX;
                    y = candidateY;
                }
            }

            placements.Add(new WorldIslandPlacement(
                $"island {i + 1}",
                x,
                y,
                radius,
                scale,
                widthScale,
                rotation,
                clusterIndex,
                giantRiverCount,
                calanqueCount,
                barrierLagoonCount,
                towerStacksCount));
            clusterUseCounts[clusterIndex]++;
        }

        return placements;
    }

    private int ResolveWorldClusterCount(int islandCount)
    {
        int upperLimit = Math.Clamp(islandCount, 1, 12);
        int minimum = Math.Clamp((int)MathF.Round(WorldClusterCountMinimum), 1, upperLimit);
        int maximum = Math.Clamp((int)MathF.Round(WorldClusterCountMaximum), 1, upperLimit);
        if (maximum < minimum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        if (maximum <= minimum)
        {
            return minimum;
        }

        float roll = Hash(WorldClusterShapeSeed + 4093, islandCount + 17);
        int span = (maximum - minimum) + 1;
        return Math.Clamp(minimum + (int)MathF.Floor(roll * span), minimum, maximum);
    }

    private List<WorldClusterAnchor> BuildWorldClusterAnchors(
        int clusterCount,
        float archipelagoRadius,
        float minSpacing,
        float clusterSpread,
        float chainBias)
    {
        List<WorldClusterAnchor> clusters = [];
        float ridgeAngle = Hash(WorldClusterShapeSeed + 3031, clusterCount * 17) * MathF.Tau;
        float ridgeLength = archipelagoRadius * (0.52f + (clusterCount * 0.11f));

        for (int clusterIndex = 0; clusterIndex < clusterCount; clusterIndex++)
        {
            float centerX = 0f;
            float centerY = 0f;
            if (clusterIndex > 0)
            {
                int ring = (clusterIndex + 1) / 2;
                float side = clusterIndex % 2 == 0 ? -1f : 1f;
                float t = side * ring / Math.Max(1f, (clusterCount + 1) * 0.5f);
                float along = t * ridgeLength * (0.84f + (Hash(WorldClusterShapeSeed + clusterIndex * 307, clusterIndex) * 0.34f));
                float crossRange = archipelagoRadius * (0.16f + (clusterSpread * 0.11f)) * (1f - (chainBias * 0.42f));
                float cross = (Hash(WorldClusterShapeSeed + clusterIndex * 311, clusterIndex + 9) - 0.5f) * 2f * crossRange;
                RotateVector(along, cross, ridgeAngle, out centerX, out centerY);
            }

            float radius = Math.Max(
                minSpacing * (1.05f + (clusterSpread * 0.58f)),
                archipelagoRadius * (0.12f + (clusterSpread * 0.16f)) * (0.72f + (Hash(WorldClusterShapeSeed + clusterIndex * 313, clusterIndex + 3) * 0.68f)));
            float angle = ridgeAngle + ((Hash(WorldClusterShapeSeed + clusterIndex * 317, clusterIndex + 5) - 0.5f) * (0.42f + (clusterSpread * 0.48f)));
            clusters.Add(new WorldClusterAnchor(centerX, centerY, radius, angle));
        }

        return clusters;
    }

    private int ResolveIslandClusterIndex(int islandIndex, int[] clusterUseCounts)
    {
        if (clusterUseCounts.Length <= 1)
        {
            return 0;
        }

        if (islandIndex < clusterUseCounts.Length)
        {
            return islandIndex;
        }

        float totalWeight = 0f;
        for (int i = 0; i < clusterUseCounts.Length; i++)
        {
            totalWeight += ResolveClusterAssignmentWeight(i, clusterUseCounts[i]);
        }

        float roll = Hash(WorldIslandScatterSeed + islandIndex * 331, islandIndex + 11) * Math.Max(0.0001f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < clusterUseCounts.Length; i++)
        {
            cumulative += ResolveClusterAssignmentWeight(i, clusterUseCounts[i]);
            if (roll <= cumulative)
            {
                return i;
            }
        }

        return clusterUseCounts.Length - 1;
    }

    private float ResolveClusterAssignmentWeight(int clusterIndex, int clusterUseCount)
    {
        float weight = 0.72f + (Hash(WorldClusterShapeSeed + clusterIndex * 337, clusterIndex + 13) * 1.65f);
        if (clusterIndex == 0)
        {
            weight *= 1.18f;
        }

        return weight / (1f + (clusterUseCount * 0.055f));
    }

    private void ResolveClusteredIslandCandidate(
        WorldClusterAnchor cluster,
        int clusterUseCount,
        int islandIndex,
        int attempt,
        float minSpacing,
        float chainBias,
        out float candidateX,
        out float candidateY)
    {
        float localRadius = Math.Max(minSpacing * 0.82f, cluster.Radius * (0.58f + Math.Min(1.05f, clusterUseCount * 0.09f)));
        float along;
        float cross;
        float chainRoll = Hash(WorldIslandScatterSeed + islandIndex * 347, attempt + 19);
        if (chainRoll < 0.46f + (chainBias * 0.44f))
        {
            float rank = ((clusterUseCount * 0.6180339f) + Hash(WorldIslandScatterSeed + islandIndex * 349, attempt + 23)) % 1f;
            along = (rank - 0.5f) * 2f * localRadius * (0.82f + (chainBias * 0.86f));
            cross = (Hash(WorldIslandScatterSeed + islandIndex * 353, attempt + 29) - 0.5f) * 2f * localRadius * (0.68f - (chainBias * 0.38f));
        }
        else
        {
            float angle = Hash(WorldIslandScatterSeed + islandIndex * 359, attempt + 31) * MathF.Tau;
            float distance = (0.16f + (MathF.Sqrt(Hash(WorldIslandScatterSeed + islandIndex * 367, attempt + 37)) * 0.92f)) * localRadius;
            along = MathF.Cos(angle) * distance;
            cross = MathF.Sin(angle) * distance * (1f - (chainBias * 0.36f));
        }

        RotateVector(along, cross, cluster.Angle, out float rotatedX, out float rotatedY);
        float jitterScale = Math.Clamp(WorldPlacementJitter, 0f, 1200f) * 0.42f;
        candidateX = cluster.X + rotatedX + ((Hash(WorldIslandScatterSeed + islandIndex * 373, attempt + 41) - 0.5f) * jitterScale);
        candidateY = cluster.Y + rotatedY + ((Hash(WorldIslandScatterSeed + islandIndex * 379, attempt + 43) - 0.5f) * jitterScale);
    }

    private float ScoreClusteredIslandCandidate(
        float candidateX,
        float candidateY,
        float radius,
        int clusterIndex,
        WorldClusterAnchor cluster,
        List<WorldIslandPlacement> placements,
        float minSpacing,
        float archipelagoRadius,
        float chainBias)
    {
        float dxCluster = candidateX - cluster.X;
        float dyCluster = candidateY - cluster.Y;
        float axisX = MathF.Cos(cluster.Angle);
        float axisY = MathF.Sin(cluster.Angle);
        float along = (dxCluster * axisX) + (dyCluster * axisY);
        float cross = (dxCluster * -axisY) + (dyCluster * axisX);
        float clusterRadiusX = cluster.Radius * (1.18f + (chainBias * 0.52f));
        float clusterRadiusY = cluster.Radius * (1.08f - (chainBias * 0.36f));
        float clusterPenalty =
            Square(along / Math.Max(1f, clusterRadiusX)) +
            Square(cross / Math.Max(1f, clusterRadiusY));
        float score = -clusterPenalty * 1.55f;
        float nearestAny = float.PositiveInfinity;
        float nearestSameCluster = float.PositiveInfinity;
        int sameClusterCount = 0;

        for (int i = 0; i < placements.Count; i++)
        {
            WorldIslandPlacement other = placements[i];
            float dx = candidateX - other.X;
            float dy = candidateY - other.Y;
            float distance = MathF.Sqrt((dx * dx) + (dy * dy));
            nearestAny = MathF.Min(nearestAny, distance);
            float desiredClearance = Math.Max(minSpacing, (radius + other.Radius) * 0.68f);
            if (distance < desiredClearance)
            {
                float overlap = (desiredClearance - distance) / Math.Max(1f, desiredClearance);
                score -= overlap * overlap * 42f;
            }

            if (other.ClusterIndex == clusterIndex)
            {
                sameClusterCount++;
                nearestSameCluster = MathF.Min(nearestSameCluster, distance);
            }
        }

        if (sameClusterCount > 0 && !float.IsPositiveInfinity(nearestSameCluster))
        {
            float preferredClusterSpacing = minSpacing * (0.84f + (chainBias * 0.34f));
            float closeness = 1f - MathF.Abs((nearestSameCluster / Math.Max(1f, preferredClusterSpacing)) - 1f);
            score += Math.Clamp(closeness, -0.75f, 1f) * 1.45f;
            if (nearestSameCluster > cluster.Radius * 1.65f)
            {
                score -= (nearestSameCluster / Math.Max(1f, cluster.Radius * 1.65f) - 1f) * 1.25f;
            }
        }
        else
        {
            score += Math.Clamp(1f - clusterPenalty, 0f, 1f) * 0.95f;
        }

        if (!float.IsPositiveInfinity(nearestAny) && nearestAny > minSpacing * 4.8f)
        {
            score -= (nearestAny / Math.Max(1f, minSpacing * 4.8f) - 1f) * 1.10f;
        }

        float outerDistance = MathF.Sqrt((candidateX * candidateX) + (candidateY * candidateY * 1.42f * 1.42f));
        if (outerDistance > archipelagoRadius * 1.16f)
        {
            score -= (outerDistance / Math.Max(1f, archipelagoRadius * 1.16f) - 1f) * 4.2f;
        }

        return score;
    }

    public TerrainScene BuildRenderScene()
    {
        TerrainScene scene = WorldPreviewEnabled ? BuildRememberedWorldPreviewScene().Clone() : Clone();
        scene.RebuildRenderCaches();
        return scene;
    }

    private TerrainScene BuildRememberedWorldPreviewScene()
    {
        string signature = BuildWorldPreviewSignature();
        if (_cachedWorldPreviewScene == null || !string.Equals(_cachedWorldPreviewSignature, signature, StringComparison.Ordinal))
        {
            _cachedWorldPreviewScene = BuildWorldPreviewScene();
            _cachedWorldPreviewSignature = signature;
        }

        return _cachedWorldPreviewScene;
    }

    private string BuildWorldPreviewSignature()
    {
        StringBuilder builder = new();
        builder.Append(Seed).Append('|')
            .Append(GlobalScale.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(GlobalWidthScale.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(GlobalBasinCut.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(GlobalOpeningWidth.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(GlobalOpeningStrength.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(GlobalRoughness.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(GlobalEmergence.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(RasterSmoothing.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterShallowDistance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterSunlitDistance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterTwilightDistance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterMidnightDistance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterStochasticReach.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterStochasticScale.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WaterCoastShapeRounding.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldIslandCount.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldMinimumSpacing.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldInteractionSpacing.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldPlacementJitter.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldClusterCountMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldClusterCountMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldClusterSpread.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldChainBias.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldClusterShapeSeed).Append('|')
            .Append(WorldIslandScatterSeed).Append('|')
            .Append(WorldIslandVariationSeed).Append('|')
            .Append(WorldIslandSizeVariance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldGiantRiverRngMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldGiantRiverRngMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldCalanqueRngMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldCalanqueRngMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldBarrierLagoonRngMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldBarrierLagoonRngMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldTowerStacksRngMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldTowerStacksRngMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldGiantRiverSizeMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldGiantRiverSizeMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldCalanqueSizeMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldCalanqueSizeMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldBarrierLagoonSizeMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldBarrierLagoonSizeMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldTowerStacksSizeMinimum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(WorldTowerStacksSizeMaximum.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(OverlayMode).Append('|');

        for (int i = 0; i < Landforms.Count; i++)
        {
            LandformInstance landform = Landforms[i];
            builder.Append(landform.Id).Append(':')
                .Append(landform.Kind).Append(':')
                .Append(landform.ParentId).Append(':')
                .Append(landform.CenterX.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.CenterY.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.BaseRadius.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.BaseLength.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.BaseWidth.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.LabelOffsetY.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.Scale.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.WidthScale.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.BasinCut.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.OpeningWidth.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.OpeningStrength.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.RotationDegrees.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.Roughness.ToString("R", CultureInfo.InvariantCulture)).Append(':')
                .Append(landform.Emergence.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        return builder.ToString();
    }

    private TerrainScene BuildWorldPreviewScene()
    {
        TerrainScene world = CloneWithoutLandforms();
        world.WorldPreviewEnabled = true;
        LandformInstance root = Landforms.Find(landform => landform.Kind == LandformKind.ArchipelagoRegion)?.Clone() ??
            new LandformInstance(1, LandformKind.ArchipelagoRegion, "world placement root", TerrainWorldDefaults.RootCenterX, TerrainWorldDefaults.RootCenterY, TerrainWorldDefaults.RootBaseRadius, TerrainWorldDefaults.RootBaseLength, TerrainWorldDefaults.RootBaseWidth, TerrainWorldDefaults.RootRotationDegrees, TerrainWorldDefaults.RootLabelOffsetY, TerrainWorldDefaults.RootScale, TerrainWorldDefaults.RootWidthScale, TerrainWorldDefaults.RootBasinCut, TerrainWorldDefaults.RootOpeningWidth, TerrainWorldDefaults.RootOpeningStrength, TerrainWorldDefaults.RootRoughness, TerrainWorldDefaults.RootEmergence);
        world.Landforms.Add(root);

        LandformInstance template = PrimaryIsland;
        List<WorldIslandPlacement> placements = BuildArchipelagoPlacements();
        int nextId = 1000;
        for (int i = 0; i < placements.Count; i++)
        {
            WorldIslandPlacement placement = placements[i];
            int islandId = nextId++;
            LandformInstance island = new(
                islandId,
                LandformKind.MainIsland,
                placement.Name,
                placement.X,
                placement.Y,
                template.BaseRadius,
                template.BaseLength,
                template.BaseWidth,
                placement.RotationDegrees,
                -8f,
                template.Scale * placement.Scale,
                template.WidthScale * placement.WidthScale,
                template.BasinCut,
                template.OpeningWidth,
                template.OpeningStrength,
                Math.Clamp(template.Roughness + ((Hash(WorldIslandVariationSeed + i * 211, i) - 0.5f) * 0.035f), 0f, 0.16f),
                template.Emergence * (0.92f + (Hash(WorldIslandVariationSeed + i * 213, i) * 0.18f)),
                root.Id);
            world.Landforms.Add(island);

            int landformSlot = 0;
            for (int landformIndex = 0; landformIndex < placement.GiantRiverCount; landformIndex++)
            {
                world.Landforms.Add(CreateWorldLandform(nextId++, LandformKind.GiantRiver, island, i, landformSlot++));
            }

            for (int landformIndex = 0; landformIndex < placement.CalanqueCount; landformIndex++)
            {
                world.Landforms.Add(CreateWorldLandform(nextId++, LandformKind.CalanqueCove, island, i, landformSlot++));
            }

            for (int landformIndex = 0; landformIndex < placement.BarrierLagoonCount; landformIndex++)
            {
                world.Landforms.Add(CreateWorldLandform(nextId++, LandformKind.BarrierIsland, island, i, landformSlot++));
            }

            for (int landformIndex = 0; landformIndex < placement.TowerStacksCount; landformIndex++)
            {
                world.Landforms.Add(CreateWorldLandform(nextId++, LandformKind.StacksAndArches, island, i, landformSlot++));
            }
        }

        world.SelectedId = world.Landforms.Count > 1 ? world.Landforms[1].Id : root.Id;
        return world;
    }

    private TerrainScene CloneWithoutLandforms()
    {
        return new TerrainScene
        {
            Seed = Seed,
            SelectedId = SelectedId,
            GlobalScale = GlobalScale,
            GlobalWidthScale = GlobalWidthScale,
            GlobalBasinCut = GlobalBasinCut,
            GlobalOpeningWidth = GlobalOpeningWidth,
            GlobalOpeningStrength = GlobalOpeningStrength,
            GlobalRoughness = GlobalRoughness,
            GlobalEmergence = GlobalEmergence,
            RasterSmoothing = RasterSmoothing,
            WaterShallowDistance = WaterShallowDistance,
            WaterSunlitDistance = WaterSunlitDistance,
            WaterTwilightDistance = WaterTwilightDistance,
            WaterMidnightDistance = WaterMidnightDistance,
            WaterStochasticReach = WaterStochasticReach,
            WaterStochasticScale = WaterStochasticScale,
            WaterCoastShapeRounding = WaterCoastShapeRounding,
            WorldIslandCount = WorldIslandCount,
            WorldMinimumSpacing = WorldMinimumSpacing,
            WorldInteractionSpacing = WorldInteractionSpacing,
            WorldPlacementJitter = WorldPlacementJitter,
            WorldClusterCountMinimum = WorldClusterCountMinimum,
            WorldClusterCountMaximum = WorldClusterCountMaximum,
            WorldClusterSpread = WorldClusterSpread,
            WorldChainBias = WorldChainBias,
            WorldClusterShapeSeed = WorldClusterShapeSeed,
            WorldIslandScatterSeed = WorldIslandScatterSeed,
            WorldIslandVariationSeed = WorldIslandVariationSeed,
            WorldIslandSizeVariance = WorldIslandSizeVariance,
            WorldGiantRiverRngMinimum = WorldGiantRiverRngMinimum,
            WorldGiantRiverRngMaximum = WorldGiantRiverRngMaximum,
            WorldCalanqueRngMinimum = WorldCalanqueRngMinimum,
            WorldCalanqueRngMaximum = WorldCalanqueRngMaximum,
            WorldBarrierLagoonRngMinimum = WorldBarrierLagoonRngMinimum,
            WorldBarrierLagoonRngMaximum = WorldBarrierLagoonRngMaximum,
            WorldTowerStacksRngMinimum = WorldTowerStacksRngMinimum,
            WorldTowerStacksRngMaximum = WorldTowerStacksRngMaximum,
            WorldGiantRiverSizeMinimum = WorldGiantRiverSizeMinimum,
            WorldGiantRiverSizeMaximum = WorldGiantRiverSizeMaximum,
            WorldCalanqueSizeMinimum = WorldCalanqueSizeMinimum,
            WorldCalanqueSizeMaximum = WorldCalanqueSizeMaximum,
            WorldBarrierLagoonSizeMinimum = WorldBarrierLagoonSizeMinimum,
            WorldBarrierLagoonSizeMaximum = WorldBarrierLagoonSizeMaximum,
            WorldTowerStacksSizeMinimum = WorldTowerStacksSizeMinimum,
            WorldTowerStacksSizeMaximum = WorldTowerStacksSizeMaximum,
            WorldPreviewEnabled = WorldPreviewEnabled,
            WorldPreviewLabelsVisible = WorldPreviewLabelsVisible,
            OverlayMode = OverlayMode
        };
    }

    private int ResolveWorldLandformCount(LandformKind kind, int islandIndex)
    {
        (float minimum, float maximum, int seedOffset) = ResolveWorldLandformRange(kind);
        int min = Math.Clamp((int)MathF.Round(minimum), 0, WorldLandformTypeGenerationLimit);
        int max = Math.Clamp((int)MathF.Round(maximum), 0, WorldLandformTypeGenerationLimit);
        if (max < min)
        {
            (min, max) = (max, min);
        }

        int range = Math.Max(0, max - min);
        float roll = Hash(Seed + (islandIndex * 227) + seedOffset, islandIndex + seedOffset * 17);
        return min + (int)MathF.Floor(roll * (range + 0.999f));
    }

    private (float Minimum, float Maximum, int SeedOffset) ResolveWorldLandformRange(LandformKind kind)
    {
        return kind switch
        {
            LandformKind.GiantRiver => (WorldGiantRiverRngMinimum, WorldGiantRiverRngMaximum, 31),
            LandformKind.CalanqueCove => (WorldCalanqueRngMinimum, WorldCalanqueRngMaximum, 47),
            LandformKind.BarrierIsland => (WorldBarrierLagoonRngMinimum, WorldBarrierLagoonRngMaximum, 59),
            LandformKind.StacksAndArches => (WorldTowerStacksRngMinimum, WorldTowerStacksRngMaximum, 71),
            _ => (0f, 0f, 83)
        };
    }

    private float ResolveWorldLandformSizeScale(LandformKind kind, int id, int islandIndex, int landformIndex)
    {
        (float minimum, float maximum, int seedOffset) = ResolveWorldLandformSizeRange(kind);
        minimum = Math.Max(0.01f, minimum);
        maximum = Math.Max(0.01f, maximum);
        if (maximum < minimum)
        {
            (minimum, maximum) = (maximum, minimum);
        }

        float roll = Hash(Seed + (id * 37) + seedOffset, islandIndex * 31 + landformIndex);
        return minimum + (roll * (maximum - minimum));
    }

    private (float Minimum, float Maximum, int SeedOffset) ResolveWorldLandformSizeRange(LandformKind kind)
    {
        return kind switch
        {
            LandformKind.GiantRiver => (WorldGiantRiverSizeMinimum, WorldGiantRiverSizeMaximum, 131),
            LandformKind.CalanqueCove => (WorldCalanqueSizeMinimum, WorldCalanqueSizeMaximum, 147),
            LandformKind.BarrierIsland => (WorldBarrierLagoonSizeMinimum, WorldBarrierLagoonSizeMaximum, 159),
            LandformKind.StacksAndArches => (WorldTowerStacksSizeMinimum, WorldTowerStacksSizeMaximum, 171),
            _ => (1f, 1f, 183)
        };
    }

    private LandformInstance CreateWorldLandform(int id, LandformKind kind, LandformInstance island, int islandIndex, int landformIndex)
    {
        float scale = ResolveWorldLandformSizeScale(kind, id, islandIndex, landformIndex);
        if (kind == LandformKind.GiantRiver)
        {
            float trend = island.RotationDegrees + ((Hash(Seed + id * 19, islandIndex + landformIndex) - 0.5f) * 44f);
            return new LandformInstance(id, kind, $"{island.DisplayName} river", island.CenterX, island.CenterY, TerrainWorldDefaults.GiantRiverBaseRadius, TerrainWorldDefaults.GiantRiverBaseLength, TerrainWorldDefaults.GiantRiverBaseWidth, NormalizeDegrees(trend), TerrainWorldDefaults.GiantRiverLabelOffsetY, scale, TerrainWorldDefaults.GiantRiverWidthScale, TerrainWorldDefaults.GiantRiverBasinCut, TerrainWorldDefaults.GiantRiverOpeningWidth, TerrainWorldDefaults.GiantRiverOpeningStrength, TerrainWorldDefaults.GiantRiverRoughness, TerrainWorldDefaults.GiantRiverEmergence, island.Id);
        }

        float angle = (landformIndex * 2.3999631f) + (Hash(Seed + id * 29, islandIndex * 41) * MathF.Tau);
        float halfLength = Math.Max(96f, island.BaseLength * island.Scale);
        float halfWidth = Math.Max(72f, island.BaseRadius * island.Scale * (0.62f + (island.WidthScale * 0.22f)));
        float radial = kind switch
        {
            LandformKind.CalanqueCove => 0.84f,
            LandformKind.BarrierIsland => 0.94f,
            LandformKind.StacksAndArches => 0.98f,
            _ => 0.86f
        };
        float localX = MathF.Cos(angle) * halfLength * radial;
        float localY = MathF.Sin(angle) * halfWidth * radial;
        RotateVector(localX, localY, DegreesToRadians(island.RotationDegrees), out float rotatedX, out float rotatedY);
        RotateVector(MathF.Cos(angle), MathF.Sin(angle), DegreesToRadians(island.RotationDegrees), out float normalX, out float normalY);
        float outwardDegrees = RadiansToDegrees(MathF.Atan2(normalY, normalX));
        float tangentJitter = (Hash(Seed + id * 31, landformIndex + 23) - 0.5f) * 18f;

        return kind switch
        {
            LandformKind.CalanqueCove => new LandformInstance(id, kind, $"{island.DisplayName} calanque", island.CenterX + rotatedX - (normalX * 72f), island.CenterY + rotatedY - (normalY * 72f), TerrainWorldDefaults.CalanqueBaseRadius, TerrainWorldDefaults.CalanqueBaseLength, TerrainWorldDefaults.CalanqueBaseWidth, NormalizeDegrees(outwardDegrees), TerrainWorldDefaults.CalanqueLabelOffsetY, scale * TerrainWorldDefaults.CalanqueScaleMultiplier, TerrainWorldDefaults.CalanqueWidthScale, TerrainWorldDefaults.CalanqueBasinCut, TerrainWorldDefaults.CalanqueOpeningWidth, TerrainWorldDefaults.CalanqueOpeningStrength, TerrainWorldDefaults.CalanqueRoughness, TerrainWorldDefaults.CalanqueEmergence, island.Id),
            LandformKind.BarrierIsland => new LandformInstance(id, kind, $"{island.DisplayName} barrier", island.CenterX + rotatedX - (normalX * 62f), island.CenterY + rotatedY - (normalY * 62f), TerrainWorldDefaults.BarrierLagoonBaseRadius, TerrainWorldDefaults.BarrierLagoonBaseLength, TerrainWorldDefaults.BarrierLagoonBaseWidth, NormalizeDegrees(outwardDegrees + 90f + tangentJitter), TerrainWorldDefaults.BarrierLagoonLabelOffsetY, scale * TerrainWorldDefaults.BarrierLagoonScaleMultiplier, TerrainWorldDefaults.BarrierLagoonWidthScale, TerrainWorldDefaults.BarrierLagoonBasinCut, TerrainWorldDefaults.BarrierLagoonOpeningWidth, TerrainWorldDefaults.BarrierLagoonOpeningStrength, TerrainWorldDefaults.BarrierLagoonRoughness, TerrainWorldDefaults.BarrierLagoonEmergence, island.Id),
            LandformKind.StacksAndArches => new LandformInstance(id, kind, $"{island.DisplayName} stacks", island.CenterX + rotatedX - (normalX * 42f), island.CenterY + rotatedY - (normalY * 42f), TerrainWorldDefaults.TowerStacksBaseRadius, TerrainWorldDefaults.TowerStacksBaseLength, TerrainWorldDefaults.TowerStacksBaseWidth, NormalizeDegrees(outwardDegrees + 90f + tangentJitter), TerrainWorldDefaults.TowerStacksLabelOffsetY, scale, TerrainWorldDefaults.TowerStacksWidthScale, TerrainWorldDefaults.TowerStacksBasinCut, TerrainWorldDefaults.TowerStacksOpeningWidth, TerrainWorldDefaults.TowerStacksOpeningStrength, TerrainWorldDefaults.TowerStacksRoughness, TerrainWorldDefaults.TowerStacksEmergence, island.Id),
            _ => new LandformInstance(id, kind, $"{island.DisplayName} landform", island.CenterX + rotatedX, island.CenterY + rotatedY, 118f, 280f, 48f, NormalizeDegrees(outwardDegrees), -6f, scale, 1f, 0.8f, 0.8f, 0.8f, 0.010f, 0.8f, island.Id)
        };
    }

    public TerrainScene Clone()
    {
        TerrainScene clone = CloneWithoutLandforms();
        for (int i = 0; i < Landforms.Count; i++)
        {
            clone.Landforms.Add(Landforms[i].Clone());
        }

        return clone;
    }

    public string BuildHierarchyPath(LandformInstance instance)
    {
        if (instance == null)
        {
            return string.Empty;
        }

        Stack<string> names = new();
        LandformInstance current = instance;
        int guard = 0;
        while (current != null && guard++ < 32)
        {
            names.Push(current.DisplayName);
            current = current.ParentId == 0 ? null : Landforms.Find(landform => landform.Id == current.ParentId);
        }

        return string.Join(" > ", names);
    }

    private void CollectDescendantIds(int parentId, HashSet<int> ids)
    {
        for (int i = 0; i < Landforms.Count; i++)
        {
            LandformInstance child = Landforms[i];
            if (child.ParentId != parentId || ids.Contains(child.Id))
            {
                continue;
            }

            ids.Add(child.Id);
            CollectDescendantIds(child.Id, ids);
        }
    }

    public void RebuildRenderCaches()
    {
        _renderCachesReady = false;
        _landformIndexCache.Clear();
        _effectiveLandformCache.Clear();

        for (int i = 0; i < Landforms.Count; i++)
        {
            _landformIndexCache[Landforms[i].Id] = i;
        }

        for (int i = 0; i < Landforms.Count; i++)
        {
            LandformInstance landform = Landforms[i];
            _effectiveLandformCache[landform.Id] = new EffectiveLandformValues(
                ResolveEffectiveMultiplierUncached(landform, item => item.Scale, 0.55f),
                ResolveEffectiveMultiplierUncached(landform, item => item.WidthScale, 0.45f),
                ResolveEffectiveMultiplierUncached(landform, item => item.BasinCut, 0.45f),
                ResolveEffectiveMultiplierUncached(landform, item => item.OpeningWidth, 0.40f),
                ResolveEffectiveMultiplierUncached(landform, item => item.OpeningStrength, 0.40f),
                ResolveEffectiveMultiplierUncached(landform, item => item.Emergence, 0.55f),
                ResolveEffectiveRoughnessUncached(landform));
        }

        _renderCachesReady = true;
    }

    public int ResolveLandformIndex(int id)
    {
        return _renderCachesReady && _landformIndexCache.TryGetValue(id, out int index) ? index : -1;
    }

    public float ResolveEffectiveScale(LandformInstance instance) =>
        TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.Scale : ResolveEffectiveMultiplierUncached(instance, landform => landform.Scale, 0.55f);

    public float ResolveEffectiveWidthScale(LandformInstance instance) =>
        TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.WidthScale : ResolveEffectiveMultiplierUncached(instance, landform => landform.WidthScale, 0.45f);

    public float ResolveEffectiveBasinCut(LandformInstance instance) =>
        TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.BasinCut : ResolveEffectiveMultiplierUncached(instance, landform => landform.BasinCut, 0.45f);

    public float ResolveEffectiveOpeningWidth(LandformInstance instance) =>
        TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.OpeningWidth : ResolveEffectiveMultiplierUncached(instance, landform => landform.OpeningWidth, 0.40f);

    public float ResolveEffectiveOpeningStrength(LandformInstance instance) =>
        TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.OpeningStrength : ResolveEffectiveMultiplierUncached(instance, landform => landform.OpeningStrength, 0.40f);

    public float ResolveEffectiveEmergence(LandformInstance instance) =>
        TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.Emergence : ResolveEffectiveMultiplierUncached(instance, landform => landform.Emergence, 0.55f);

    public float ResolveEffectiveRoughness(LandformInstance instance)
    {
        return TryResolveEffectiveValues(instance, out EffectiveLandformValues values) ? values.Roughness : ResolveEffectiveRoughnessUncached(instance);
    }

    private bool TryResolveEffectiveValues(LandformInstance instance, out EffectiveLandformValues values)
    {
        if (_renderCachesReady && _effectiveLandformCache.TryGetValue(instance.Id, out values))
        {
            return true;
        }

        values = default;
        return false;
    }

    private float ResolveEffectiveRoughnessUncached(LandformInstance instance)
    {
        float roughness = instance.Roughness;
        LandformInstance current = instance;
        int guard = 0;
        while (current.ParentId != 0 && guard++ < 32)
        {
            current = Landforms.Find(landform => landform.Id == current.ParentId);
            if (current == null)
            {
                break;
            }

            roughness += current.Roughness * 0.50f;
        }

        return roughness;
    }

    private float ResolveEffectiveMultiplierUncached(LandformInstance instance, Func<LandformInstance, float> selector, float inheritanceWeight)
    {
        float multiplier = selector(instance);
        LandformInstance current = instance;
        int guard = 0;
        while (current.ParentId != 0 && guard++ < 32)
        {
            current = Landforms.Find(landform => landform.Id == current.ParentId);
            if (current == null)
            {
                break;
            }

            multiplier *= 1f + ((selector(current) - 1f) * inheritanceWeight);
        }

        return multiplier;
    }

    private static void RotateVector(float x, float y, float radians, out float rotatedX, out float rotatedY)
    {
        float cos = MathF.Cos(radians);
        float sin = MathF.Sin(radians);
        rotatedX = (x * cos) - (y * sin);
        rotatedY = (x * sin) + (y * cos);
    }

    private static float Square(float value)
    {
        return value * value;
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    private static float RadiansToDegrees(float radians)
    {
        return radians * 180f / MathF.PI;
    }

    private static float NormalizeDegrees(float degrees)
    {
        float normalized = degrees % 360f;
        if (normalized <= -180f)
        {
            normalized += 360f;
        }
        else if (normalized > 180f)
        {
            normalized -= 360f;
        }

        return normalized;
    }

    private static float AngularDistance(float left, float right)
    {
        float delta = MathF.Abs(left - right) % MathF.Tau;
        return delta > MathF.PI ? MathF.Tau - delta : delta;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float denominator = edge1 - edge0;
        if (MathF.Abs(denominator) < 0.0001f)
        {
            return value >= edge1 ? 1f : 0f;
        }

        float t = Math.Clamp((value - edge0) / denominator, 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393);
            h ^= (uint)y * 668265263u;
            h = (h << 13) | (h >> 19);
            h *= 1274126177u;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }
}

internal readonly struct LandformPlacement
{
    public LandformPlacement(float centerX, float centerY, float rotationDegrees)
    {
        CenterX = centerX;
        CenterY = centerY;
        RotationDegrees = rotationDegrees;
    }

    public float CenterX { get; }
    public float CenterY { get; }
    public float RotationDegrees { get; }
}

internal readonly struct ShoreCandidate
{
    public ShoreCandidate(
        float x,
        float y,
        float normalX,
        float normalY,
        float radialFraction,
        float inlandSamples,
        float outwardWaterSamples,
        TerrainProcessCell shoreCell,
        TerrainProcessCell waterCell)
    {
        X = x;
        Y = y;
        NormalX = normalX;
        NormalY = normalY;
        RadialFraction = radialFraction;
        InlandSamples = inlandSamples;
        OutwardWaterSamples = outwardWaterSamples;
        ShoreCell = shoreCell;
        WaterCell = waterCell;
    }

    public float X { get; }
    public float Y { get; }
    public float NormalX { get; }
    public float NormalY { get; }
    public float RadialFraction { get; }
    public float InlandSamples { get; }
    public float OutwardWaterSamples { get; }
    public TerrainProcessCell ShoreCell { get; }
    public TerrainProcessCell WaterCell { get; }
}

internal sealed class WorldIslandPlacement
{
    public WorldIslandPlacement(
        string name,
        float x,
        float y,
        float radius,
        float scale,
        float widthScale,
        float rotationDegrees,
        int clusterIndex,
        int giantRiverCount,
        int calanqueCount,
        int barrierLagoonCount,
        int towerStacksCount)
    {
        Name = name;
        X = x;
        Y = y;
        Radius = radius;
        Scale = scale;
        WidthScale = widthScale;
        RotationDegrees = rotationDegrees;
        ClusterIndex = clusterIndex;
        GiantRiverCount = giantRiverCount;
        CalanqueCount = calanqueCount;
        BarrierLagoonCount = barrierLagoonCount;
        TowerStacksCount = towerStacksCount;
    }

    public string Name { get; }
    public float X { get; }
    public float Y { get; }
    public float Radius { get; }
    public float Scale { get; }
    public float WidthScale { get; }
    public float RotationDegrees { get; }
    public int ClusterIndex { get; }
    public int GiantRiverCount { get; }
    public int CalanqueCount { get; }
    public int BarrierLagoonCount { get; }
    public int TowerStacksCount { get; }
    public int LandformCount => GiantRiverCount + CalanqueCount + BarrierLagoonCount + TowerStacksCount;
}

internal readonly struct WorldClusterAnchor
{
    public WorldClusterAnchor(float x, float y, float radius, float angle)
    {
        X = x;
        Y = y;
        Radius = radius;
        Angle = angle;
    }

    public float X { get; }
    public float Y { get; }
    public float Radius { get; }
    public float Angle { get; }
}

internal readonly struct EffectiveLandformValues
{
    public EffectiveLandformValues(
        float scale,
        float widthScale,
        float basinCut,
        float openingWidth,
        float openingStrength,
        float emergence,
        float roughness)
    {
        Scale = scale;
        WidthScale = widthScale;
        BasinCut = basinCut;
        OpeningWidth = openingWidth;
        OpeningStrength = openingStrength;
        Emergence = emergence;
        Roughness = roughness;
    }

    public float Scale { get; }
    public float WidthScale { get; }
    public float BasinCut { get; }
    public float OpeningWidth { get; }
    public float OpeningStrength { get; }
    public float Emergence { get; }
    public float Roughness { get; }
}

internal sealed class LandformInstance
{
    public LandformInstance(
        int id,
        LandformKind kind,
        string displayName,
        float centerX,
        float centerY,
        float baseRadius,
        float baseLength,
        float baseWidth,
        float rotationDegrees,
        float labelOffsetY,
        float scale,
        float widthScale,
        float basinCut,
        float openingWidth,
        float openingStrength,
        float roughness,
        float emergence,
        int parentId = 0)
    {
        Id = id;
        Kind = kind;
        DisplayName = displayName;
        ParentId = parentId;
        CenterX = centerX;
        CenterY = centerY;
        BaseRadius = baseRadius;
        BaseLength = baseLength;
        BaseWidth = baseWidth;
        RotationDegrees = rotationDegrees;
        LabelOffsetY = labelOffsetY;
        Scale = scale;
        WidthScale = widthScale;
        BasinCut = basinCut;
        OpeningWidth = openingWidth;
        OpeningStrength = openingStrength;
        Roughness = roughness;
        Emergence = emergence;
    }

    public int Id { get; }
    public LandformKind Kind { get; }
    public string DisplayName { get; }
    public int ParentId { get; }
    public float CenterX { get; set; }
    public float CenterY { get; set; }
    public float BaseRadius { get; set; }
    public float BaseLength { get; set; }
    public float BaseWidth { get; set; }
    public float LabelOffsetY { get; set; }
    public float Scale { get; set; }
    public float WidthScale { get; set; }
    public float BasinCut { get; set; }
    public float OpeningWidth { get; set; }
    public float OpeningStrength { get; set; }
    public float RotationDegrees { get; set; }
    public float Roughness { get; set; }
    public float Emergence { get; set; }

    public LandformInstance Clone()
    {
        return new LandformInstance(
            Id,
            Kind,
            DisplayName,
            CenterX,
            CenterY,
            BaseRadius,
            BaseLength,
            BaseWidth,
            RotationDegrees,
            LabelOffsetY,
            Scale,
            WidthScale,
            BasinCut,
            OpeningWidth,
            OpeningStrength,
            Roughness,
            Emergence,
            ParentId);
    }

    public void CopyEditableFrom(LandformInstance source)
    {
        Scale = source.Scale;
        CenterX = source.CenterX;
        CenterY = source.CenterY;
        BaseRadius = source.BaseRadius;
        BaseLength = source.BaseLength;
        BaseWidth = source.BaseWidth;
        WidthScale = source.WidthScale;
        BasinCut = source.BasinCut;
        OpeningWidth = source.OpeningWidth;
        OpeningStrength = source.OpeningStrength;
        RotationDegrees = source.RotationDegrees;
        Roughness = source.Roughness;
        Emergence = source.Emergence;
    }
}

internal enum LandformKind
{
    ArchipelagoRegion,
    ReefShelf,
    Atoll,
    BarrierReef,
    SedimentShelf,
    BarrierIsland,
    SpitAndTombolo,
    IslandCluster,
    MainIsland,
    CliffCoast,
    CalanqueCove,
    KarstTowerField,
    KarstHong,
    StacksAndArches,
    IslandRing,
    GiantRiver,
    AtollLagoon,
    ReefBarrierLagoon,
    BarrierIslandLagoon,
    KarstHongLagoon,
    IslandRingLagoon,
    CalanqueCoveLagoon
}

internal static class TerrainSettingsEncoder
{
    public static string Encode(TerrainScene scene, TerrainCanvas canvas)
    {
        StringBuilder builder = new();
        builder.AppendLine("TerrainSettingsFormat=op.io.dev.terrain.layered_process.v3");
        builder.AppendLine($"EncodedAtLocal={DateTime.Now:O}");
        builder.AppendLine($"Seed={scene.Seed.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Selected={scene.Selected.DisplayName}");
        builder.AppendLine($"SelectedHierarchy={scene.BuildHierarchyPath(scene.Selected)}");
        builder.AppendLine($"GenerationPipeline=macro mask > lithology > fractures > pre-flood terrain > karst dissolution > flooding > erosion > sediment > reef growth > classification");
        builder.AppendLine($"Overlay={scene.OverlayMode}");
        Append(builder, "CameraX", canvas.CameraX);
        Append(builder, "CameraY", canvas.CameraY);
        Append(builder, "Zoom", canvas.Zoom);
        Append(builder, "GlobalScale", scene.GlobalScale);
        Append(builder, "GlobalWidthScale", scene.GlobalWidthScale);
        Append(builder, "GlobalBasinCut", scene.GlobalBasinCut);
        Append(builder, "GlobalOpeningWidth", scene.GlobalOpeningWidth);
        Append(builder, "GlobalOpeningStrength", scene.GlobalOpeningStrength);
        Append(builder, "GlobalRoughness", scene.GlobalRoughness);
        Append(builder, "GlobalEmergence", scene.GlobalEmergence);
        Append(builder, "RasterSmoothing", scene.RasterSmoothing);
        Append(builder, "WaterShallowDistance", scene.WaterShallowDistance);
        Append(builder, "WaterSunlitDistance", scene.WaterSunlitDistance);
        Append(builder, "WaterTwilightDistance", scene.WaterTwilightDistance);
        Append(builder, "WaterMidnightDistance", scene.WaterMidnightDistance);
        Append(builder, "WaterStochasticReach", scene.WaterStochasticReach);
        Append(builder, "WaterStochasticScale", scene.WaterStochasticScale);
        Append(builder, "WaterCoastShapeRounding", scene.WaterCoastShapeRounding);
        Append(builder, "WorldIslandCount", scene.WorldIslandCount);
        Append(builder, "WorldMinimumSpacing", scene.WorldMinimumSpacing);
        Append(builder, "WorldInteractionSpacing", scene.WorldInteractionSpacing);
        Append(builder, "WorldPlacementJitter", scene.WorldPlacementJitter);
        Append(builder, "WorldClusterCountMinimum", scene.WorldClusterCountMinimum);
        Append(builder, "WorldClusterCountMaximum", scene.WorldClusterCountMaximum);
        Append(builder, "WorldClusterSpread", scene.WorldClusterSpread);
        Append(builder, "WorldChainBias", scene.WorldChainBias);
        Append(builder, "WorldClusterShapeSeed", scene.WorldClusterShapeSeed);
        Append(builder, "WorldIslandScatterSeed", scene.WorldIslandScatterSeed);
        Append(builder, "WorldIslandVariationSeed", scene.WorldIslandVariationSeed);
        Append(builder, "WorldIslandSizeVariance", scene.WorldIslandSizeVariance);
        Append(builder, "WorldGiantRiverRngMinimum", scene.WorldGiantRiverRngMinimum);
        Append(builder, "WorldGiantRiverRngMaximum", scene.WorldGiantRiverRngMaximum);
        Append(builder, "WorldCalanqueRngMinimum", scene.WorldCalanqueRngMinimum);
        Append(builder, "WorldCalanqueRngMaximum", scene.WorldCalanqueRngMaximum);
        Append(builder, "WorldBarrierLagoonRngMinimum", scene.WorldBarrierLagoonRngMinimum);
        Append(builder, "WorldBarrierLagoonRngMaximum", scene.WorldBarrierLagoonRngMaximum);
        Append(builder, "WorldTowerStacksRngMinimum", scene.WorldTowerStacksRngMinimum);
        Append(builder, "WorldTowerStacksRngMaximum", scene.WorldTowerStacksRngMaximum);
        Append(builder, "WorldGiantRiverSizeMinimum", scene.WorldGiantRiverSizeMinimum);
        Append(builder, "WorldGiantRiverSizeMaximum", scene.WorldGiantRiverSizeMaximum);
        Append(builder, "WorldCalanqueSizeMinimum", scene.WorldCalanqueSizeMinimum);
        Append(builder, "WorldCalanqueSizeMaximum", scene.WorldCalanqueSizeMaximum);
        Append(builder, "WorldBarrierLagoonSizeMinimum", scene.WorldBarrierLagoonSizeMinimum);
        Append(builder, "WorldBarrierLagoonSizeMaximum", scene.WorldBarrierLagoonSizeMaximum);
        Append(builder, "WorldTowerStacksSizeMinimum", scene.WorldTowerStacksSizeMinimum);
        Append(builder, "WorldTowerStacksSizeMaximum", scene.WorldTowerStacksSizeMaximum);
        Append(builder, "WorldPreviewLabelsVisible", scene.WorldPreviewLabelsVisible);

        builder.AppendLine();
        builder.AppendLine("[IslandTabSliders]");
        Append(builder, "Raster smoothing", scene.RasterSmoothing);
        Append(builder, "Global land emergence", scene.GlobalEmergence);
        Append(builder, "Island size", scene.PrimaryIsland.Scale);
        Append(builder, "Island length", scene.PrimaryIsland.BaseLength);
        Append(builder, "Island width", scene.PrimaryIsland.BaseRadius);
        Append(builder, "Coast irregularity", scene.PrimaryIsland.Roughness);
        Append(builder, "Island rotation", scene.PrimaryIsland.RotationDegrees);
        Append(builder, "Island solidity", scene.PrimaryIsland.Emergence);
        Append(builder, "Global basin erosion", scene.GlobalBasinCut);
        Append(builder, "Global opening width", scene.GlobalOpeningWidth);
        Append(builder, "Global opening strength", scene.GlobalOpeningStrength);

        builder.AppendLine();
        builder.AppendLine("[SelectedLandformTabSliders]");
        LandformInstance selected = scene.Selected;
        builder.AppendLine($"SelectedId={selected.Id.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"SelectedKind={selected.Kind}");
        Append(builder, "Local X", selected.CenterX);
        Append(builder, "Local Y", selected.CenterY);
        Append(builder, "Region size", selected.Scale);
        Append(builder, "Base length", selected.BaseLength);
        Append(builder, "Base width", selected.BaseRadius);
        Append(builder, "Lithology bias", selected.WidthScale);
        Append(builder, "Basin erosion", selected.BasinCut);
        Append(builder, "Opening width", selected.OpeningWidth);
        Append(builder, "Process strength", selected.OpeningStrength);
        Append(builder, "Emergence", selected.Emergence);
        Append(builder, "Structural trend", selected.RotationDegrees);
        Append(builder, "Local roughness", selected.Roughness);

        builder.AppendLine();
        builder.AppendLine("[WorldTabSliders]");
        Append(builder, "Island count", scene.WorldIslandCount);
        Append(builder, "Minimum spacing", scene.WorldMinimumSpacing);
        Append(builder, "Interaction spacing", scene.WorldInteractionSpacing);
        Append(builder, "Placement jitter", scene.WorldPlacementJitter);
        Append(builder, "Cluster min", scene.WorldClusterCountMinimum);
        Append(builder, "Cluster max", scene.WorldClusterCountMaximum);
        Append(builder, "Cluster spread", scene.WorldClusterSpread);
        Append(builder, "Ridge / chain bias", scene.WorldChainBias);
        Append(builder, "Show preview text", scene.WorldPreviewLabelsVisible);
        Append(builder, "Shallow water distance", scene.WaterShallowDistance);
        Append(builder, "Sunlit water distance", scene.WaterSunlitDistance);
        Append(builder, "Twilight water distance", scene.WaterTwilightDistance);
        Append(builder, "Midnight water distance", scene.WaterMidnightDistance);
        Append(builder, "Water stochastic reach", scene.WaterStochasticReach);
        Append(builder, "Water stochastic scale", scene.WaterStochasticScale);
        Append(builder, "Water coast rounding", scene.WaterCoastShapeRounding);
        Append(builder, "Cluster shape seed", scene.WorldClusterShapeSeed);
        Append(builder, "Island scatter seed", scene.WorldIslandScatterSeed);
        Append(builder, "Island variation seed", scene.WorldIslandVariationSeed);
        Append(builder, "Island size variance", scene.WorldIslandSizeVariance);
        Append(builder, "Giant river min", scene.WorldGiantRiverRngMinimum);
        Append(builder, "Giant river max", scene.WorldGiantRiverRngMaximum);
        Append(builder, "Giant river size min", scene.WorldGiantRiverSizeMinimum);
        Append(builder, "Giant river size max", scene.WorldGiantRiverSizeMaximum);
        Append(builder, "Calanque min", scene.WorldCalanqueRngMinimum);
        Append(builder, "Calanque max", scene.WorldCalanqueRngMaximum);
        Append(builder, "Calanque size min", scene.WorldCalanqueSizeMinimum);
        Append(builder, "Calanque size max", scene.WorldCalanqueSizeMaximum);
        Append(builder, "Barrier lagoon min", scene.WorldBarrierLagoonRngMinimum);
        Append(builder, "Barrier lagoon max", scene.WorldBarrierLagoonRngMaximum);
        Append(builder, "Barrier lagoon size min", scene.WorldBarrierLagoonSizeMinimum);
        Append(builder, "Barrier lagoon size max", scene.WorldBarrierLagoonSizeMaximum);
        Append(builder, "Tower stacks min", scene.WorldTowerStacksRngMinimum);
        Append(builder, "Tower stacks max", scene.WorldTowerStacksRngMaximum);
        Append(builder, "Tower stacks size min", scene.WorldTowerStacksSizeMinimum);
        Append(builder, "Tower stacks size max", scene.WorldTowerStacksSizeMaximum);

        foreach (LandformInstance instance in scene.Landforms)
        {
            builder.AppendLine();
            builder.AppendLine($"[{instance.DisplayName}]");
            builder.AppendLine($"Kind={instance.Kind}");
            builder.AppendLine($"ParentId={instance.ParentId.ToString(CultureInfo.InvariantCulture)}");
            builder.AppendLine($"Hierarchy={scene.BuildHierarchyPath(instance)}");
            Append(builder, "CenterX", instance.CenterX);
            Append(builder, "CenterY", instance.CenterY);
            Append(builder, "BaseRadius", instance.BaseRadius);
            Append(builder, "BaseLength", instance.BaseLength);
            Append(builder, "BaseWidth", instance.BaseWidth);
            Append(builder, "Scale", instance.Scale);
            Append(builder, "WidthScale", instance.WidthScale);
            Append(builder, "BasinCut", instance.BasinCut);
            Append(builder, "OpeningWidth", instance.OpeningWidth);
            Append(builder, "OpeningStrength", instance.OpeningStrength);
            Append(builder, "RotationDegrees", instance.RotationDegrees);
            Append(builder, "Roughness", instance.Roughness);
            Append(builder, "Emergence", instance.Emergence);
        }

        builder.AppendLine();
        builder.AppendLine("[ArchipelagoPlacements]");
        foreach (WorldIslandPlacement placement in scene.BuildArchipelagoPlacements())
        {
            builder.AppendLine($"{placement.Name}=cluster:{(placement.ClusterIndex + 1).ToString(CultureInfo.InvariantCulture)},x:{placement.X.ToString("0.###", CultureInfo.InvariantCulture)},y:{placement.Y.ToString("0.###", CultureInfo.InvariantCulture)},radius:{placement.Radius.ToString("0.###", CultureInfo.InvariantCulture)},river:{placement.GiantRiverCount.ToString(CultureInfo.InvariantCulture)},calanque:{placement.CalanqueCount.ToString(CultureInfo.InvariantCulture)},barrierLagoon:{placement.BarrierLagoonCount.ToString(CultureInfo.InvariantCulture)},towerStacks:{placement.TowerStacksCount.ToString(CultureInfo.InvariantCulture)}");
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string key, float value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.AppendLine(value.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder builder, string key, int value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder builder, string key, bool value)
    {
        builder.Append(key);
        builder.Append('=');
        builder.AppendLine(value ? "true" : "false");
    }
}
