using System.Drawing;
using System.Windows.Forms;
using FxCorrelationDashboard.Data;
using FxCorrelationDashboard.Engine;
using OxyPlot;
using OxyPlot.Annotations;
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

        _basketCombo.SelectedIndexChanged += (_, _) => { PopulatePairCombo(); RefreshDashboard(); };
        _windowCombo.SelectedIndexChanged += (_, _) => RefreshDashboard();
        _pairCombo.SelectedIndexChanged += (_, _) => RefreshDashboard();

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

            foreach (var s in allSeries)
                Console.WriteLine($"  {s.Name}: {s.Values.Length} data points");

            // Single correlation matrix used by BOTH heatmap and bar chart
            var matrix = CorrelationEngine.CorrelationMatrix(allSeries.ToArray(), window);
            RenderHeatmap(matrix, allNames, window);

            var selectedPair = _pairCombo.SelectedItem?.ToString() ?? fxPairs[0];
            int pairIdx = Array.IndexOf(fxPairs, selectedPair);
            if (pairIdx < 0) pairIdx = 0;

            // Bar chart reads from the SAME matrix
            RenderBarChart(selectedPair, _macroFactors, matrix, pairIdx, fxPairs.Length, window);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Dashboard Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RenderHeatmap(double[,] matrix, string[] names, int window)
    {
        var model = new PlotModel { Title = $"Correlation Heatmap ({window}d rolling)" };
        int n = names.Length;

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
            Maximum = 1,
            StringFormat = "0.0"       // FIX: clean legend labels
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
            LabelFontSize = 0          // disable built-in labels (broken in OxyPlot 2014)
        };

        model.Axes.Add(catX);
        model.Axes.Add(catY);
        model.Axes.Add(colorAxis);
        model.Series.Add(heatmap);

        // Add text annotations for each cell instead
        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                double val = matrix[row, col];
                // White text on dark cells, black text on light cells
                var textColor = Math.Abs(val) > 0.45 ? OxyColors.White : OxyColors.Black;

                model.Annotations.Add(new TextAnnotation
                {
                    Text = val.ToString("0.00"),
                    TextPosition = new DataPoint(col, row),
                    TextColor = textColor,
                    FontSize = 9,
                    Stroke = OxyColors.Transparent,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                    XAxisKey = "xAxis",
                    YAxisKey = "yAxis"
                });
            }
        }

        _heatmapPlot.Model = model;
    }

    /// <summary>
    /// Right panel: bar chart showing correlation of selected FX pair vs each macro factor.
    /// Reads directly from the precomputed matrix so values match the heatmap exactly.
    /// </summary>
    private void RenderBarChart(string baseName, string[] macroNames,
        double[,] matrix, int baseIndex, int macroStartIndex, int window)
    {
        var model = new PlotModel
        {
            Title = $"{window}d correlation: {baseName} vs Macro"
        };

        var catAxis = new CategoryAxis
        {
            Position = AxisPosition.Left
        };

        var valAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = -1,
            Maximum = 1,
            Title = "Correlation",
            StringFormat = "0.0"       // FIX: clean axis labels
        };

        var barSeries = new BarSeries
        {
            LabelPlacement = LabelPlacement.Outside,
            LabelFormatString = "{0:0.00}"
        };

        for (int i = 0; i < macroNames.Length; i++)
        {
            double corr = matrix[baseIndex, macroStartIndex + i];
            catAxis.Labels.Add(macroNames[i]);
            barSeries.Items.Add(new BarItem(corr));
        }

        model.Axes.Add(catAxis);
        model.Axes.Add(valAxis);
        model.Series.Add(barSeries);
        _barPlot.Model = model;
    }
}