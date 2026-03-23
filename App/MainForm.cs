using System.Drawing;
using System.Windows.Forms;
using FxCorrelationDashboard.Data;
using FxCorrelationDashboard.Engine;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace FxCorrelationDashboard.App;

public partial class MainForm : Form
{
    private readonly FxDataRepository _repo;
    private readonly PlotView _heatmapPlot;
    private readonly PlotView _barPlot;
    private readonly ComboBox _basketCombo;
    private readonly ComboBox _windowCombo;
    private readonly ComboBox _pairCombo;
    private readonly Button _refreshBtn;

    private readonly Dictionary<string, string[]> _baskets = new()
    {
        ["G10 Majors"] = new[] { "EURUSD", "GBPUSD", "USDJPY", "AUDUSD", "USDCAD", "USDCHF", "NZDUSD" },
        ["JPY Crosses"] = new[] { "EURJPY", "GBPJPY", "AUDJPY", "CADJPY", "NZDJPY" },
        ["Commodity FX"] = new[] { "AUDUSD", "NZDUSD", "USDCAD", "USDNOK" },
    };

    private readonly string[] _macroFactors = { "WTI", "BRENT", "US10Y", "SILVER", "GOLD" };

    public MainForm(string connStr)
    {
        _repo = new FxDataRepository(connStr);

        Text = "FX Correlation Dashboard";
        Size = new Size(1400, 800);

        var panel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45 };

        _basketCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        _basketCombo.Items.AddRange(_baskets.Keys.ToArray());
        _basketCombo.SelectedIndex = 0;

        _windowCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        _windowCombo.Items.AddRange(new object[] { "30d", "60d", "90d", "120d", "252d" });
        _windowCombo.SelectedIndex = 1;

        _pairCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        PopulatePairCombo();
        _basketCombo.SelectedIndexChanged += (_, _) => PopulatePairCombo();

        _refreshBtn = new Button { Text = "Refresh", Width = 80 };
        _refreshBtn.Click += (_, _) => RefreshDashboard();

        panel.Controls.AddRange(new Control[]
        {
            new Label { Text = "Basket:", AutoSize = true, Padding = new Padding(5) },
            _basketCombo,
            new Label { Text = "Window:", AutoSize = true, Padding = new Padding(5) },
            _windowCombo,
            new Label { Text = "Pair:", AutoSize = true, Padding = new Padding(5) },
            _pairCombo,
            _refreshBtn
        });

        _heatmapPlot = new PlotView { Dock = DockStyle.Left, Width = 650 };
        _barPlot = new PlotView { Dock = DockStyle.Fill };

        Controls.Add(_barPlot);
        Controls.Add(_heatmapPlot);
        Controls.Add(panel);

        RefreshDashboard();
    }

    private void PopulatePairCombo()
    {
        var basketName = _basketCombo.SelectedItem?.ToString();
        if (basketName == null) return;
        _pairCombo.Items.Clear();
        _pairCombo.Items.AddRange(_baskets[basketName]);
        _pairCombo.SelectedIndex = 0;
    }

    private void RefreshDashboard()
    {
        try
        {
            var basketName = _basketCombo.SelectedItem!.ToString()!;
            var fxPairs = _baskets[basketName];
            int window = int.Parse(_windowCombo.SelectedItem!.ToString()!.Replace("d", ""));

            var from = DateTime.Today.AddYears(-3);
            var to = DateTime.Today;

            var allNames = fxPairs.Concat(_macroFactors).ToArray();
            var allSeries = new List<PriceSeries>();

            foreach (var pair in fxPairs)
                allSeries.Add(_repo.GetFxSeries(pair, from, to));
            foreach (var factor in _macroFactors)
                allSeries.Add(_repo.GetMacroSeries(factor, from, to));

            // Debug: print series counts to console
            foreach (var s in allSeries)
                Console.WriteLine($"  {s.Name}: {s.Values.Length} data points");

            var matrix = CorrelationEngine.CorrelationMatrix(allSeries.ToArray(), window);
            RenderHeatmap(matrix, allNames);

            var selectedPair = _pairCombo.SelectedItem?.ToString() ?? fxPairs[0];
            int pairIdx = Array.IndexOf(fxPairs, selectedPair);
            if (pairIdx < 0) pairIdx = 0;

            RenderFullSampleBar(allSeries[pairIdx], allSeries.Skip(fxPairs.Length).ToArray());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Dashboard Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RenderHeatmap(double[,] matrix, string[] names)
    {
        var model = new PlotModel { Title = "Correlation Heatmap" };
        int n = names.Length;

        // CategoryAxis with Key so the HeatMapSeries binds to them
        var catX = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            Key = "xAxis",
            Angle = -45
        };
        catX.Labels.AddRange(names);

        var catY = new CategoryAxis
        {
            Position = AxisPosition.Left,
            Key = "yAxis"
        };
        catY.Labels.AddRange(names);

        var colorAxis = new LinearColorAxis
        {
            Position = AxisPosition.Right,
            Palette = OxyPalettes.BlueWhiteRed(200),
            Minimum = -1,
            Maximum = 1
        };

        var heatmap = new HeatMapSeries
        {
            X0 = 0,
            X1 = n - 1,
            Y0 = 0,
            Y1 = n - 1,
            XAxisKey = "xAxis",
            YAxisKey = "yAxis",
            Data = matrix,
            Interpolate = false,
            RenderMethod = HeatMapRenderMethod.Rectangles,
            LabelFontSize = 0.2,
            LabelFormatString = "0.00"
        };

        model.Axes.Add(catX);
        model.Axes.Add(catY);
        model.Axes.Add(colorAxis);
        model.Series.Add(heatmap);
        _heatmapPlot.Model = model;
    }

    /// <summary>
    /// Right panel: full-sample correlation bar chart (base FX pair vs each macro factor).
    /// </summary>
    private void RenderFullSampleBar(PriceSeries baseSeries, PriceSeries[] macroSeries)
    {
        var model = new PlotModel
        {
            Title = $"Full-sample correlation: {baseSeries.Name} vs Macro"
        };

        var catAxis = new CategoryAxis
        {
            Position = AxisPosition.Left    // BarSeries is horizontal: categories on Y
        };
        var valAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom, // values on X
            Minimum = -1,
            Maximum = 1,
            Title = "Correlation"
        };

        var barSeries = new BarSeries
        {
            LabelPlacement = LabelPlacement.Outside,
            LabelFormatString = "{0:0.00}"
        };

        foreach (var macro in macroSeries)
        {
            double corr = CorrelationEngine.FullSampleCorrelation(baseSeries, macro);
            catAxis.Labels.Add(macro.Name);
            barSeries.Items.Add(new BarItem(corr));
        }

        model.Axes.Add(catAxis);
        model.Axes.Add(valAxis);
        model.Series.Add(barSeries);
        _barPlot.Model = model;
    }
}
