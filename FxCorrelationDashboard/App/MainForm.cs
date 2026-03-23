using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FxCorrelationDashboard.Data;
using FxCorrelationDashboard.Engine;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using OxyPlot.Annotations;

namespace FxCorrelationDashboard.App
{
    public partial class MainForm : Form
    {
        private readonly FxDataRepository _repo;
        private readonly PlotView _heatmapPlot;
        private readonly PlotView _timeSeriesPlot;
        private readonly ComboBox _basketCombo;
        private readonly ComboBox _windowCombo;
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
            _windowCombo.SelectedIndex = 0;

            _refreshBtn = new Button { Text = "Refresh", Width = 80 };
            _refreshBtn.Click += (_, _) => RefreshDashboard();

            panel.Controls.AddRange(new Control[]
            {
                new Label { Text = "Basket:", AutoSize = true, Padding = new Padding(5) },
                _basketCombo,
                new Label { Text = "Window:", AutoSize = true, Padding = new Padding(5) },
                _windowCombo,
                _refreshBtn
            });

            _heatmapPlot = new PlotView { Dock = DockStyle.Left, Width = 900 };
            _timeSeriesPlot = new PlotView { Dock = DockStyle.Fill };

            Controls.Add(_timeSeriesPlot);
            Controls.Add(_heatmapPlot);
            Controls.Add(panel);

            RefreshDashboard();
        }

        private void RefreshDashboard()
        {
            try
            {
                var basketName = _basketCombo.SelectedItem!.ToString()!;
                var fxPairs = _baskets[basketName];

                var from = DateTime.Today.AddYears(-3);
                var to = DateTime.Today;

                var allNames = fxPairs.Concat(_macroFactors).ToArray();
                var allSeries = new List<PriceSeries>();

                foreach (var pair in fxPairs)
                    allSeries.Add(_repo.GetFxSeries(pair, from, to));
                foreach (var factor in _macroFactors)
                    allSeries.Add(_repo.GetMacroSeries(factor, from, to));

                var matrix = CorrelationEngine.CorrelationMatrix(allSeries.ToArray());
                RenderHeatmap(matrix, allNames);

                RenderTimeSeries(
                    allSeries[0].Name,
                    _macroFactors,
                    matrix,
                    0,
                    fxPairs.Length
                );
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

            var catX = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                Angle = 45,
                GapWidth = 0
            };

            var catY = new CategoryAxis
            {
                Position = AxisPosition.Left,
                GapWidth = 0
            };

            catX.Labels.AddRange(names);
            catY.Labels.AddRange(names);

            var colorAxis = new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Palette = OxyPalettes.BlueWhiteRed(200),
                Minimum = -1,
                Maximum = 1,
                StringFormat = "0.0",
                MajorStep = 0.2
            };

            var heatmap = new HeatMapSeries
            {
                X0 = 0,
                X1 = n - 1,
                Y0 = 0,
                Y1 = n - 1,
                Data = matrix,
                Interpolate = false
            };

            model.Axes.Add(catX);
            model.Axes.Add(catY);
            model.Axes.Add(colorAxis);
            model.Series.Add(heatmap);

            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++)
                {
                    double value = matrix[y, x];

                    var text = new TextAnnotation
                    {
                        Text = value.ToString("0.00"),
                        TextPosition = new DataPoint(x, y),
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                        Stroke = OxyColors.Transparent,
                        TextColor = OxyColors.Black
                    };

                    model.Annotations.Add(text);
                }
            }
            _heatmapPlot.Model = model;
        }

        private void RenderTimeSeries(
            string baseName, string[] macroNames,
            double[,] matrix, int baseIndex, int macroStartIndex)
        {
            var model = new PlotModel
            {
                Title = $"Full-sample correlation: {baseName} vs Macro"
            };

            var catAxis = new CategoryAxis { Position = AxisPosition.Bottom };
            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = -1,
                Maximum = 1,
                Title = "Correlation",
                StringFormat = "0.0",
                MajorStep = 0.2,
                MinorStep = 0.1
            };

            model.Axes.Add(catAxis);
            model.Axes.Add(yAxis);

            var barSeries = new ColumnSeries
            {
                LabelPlacement = LabelPlacement.Outside,
                LabelFormatString = "{0:0.00}"
            };

            for (int i = 0; i < macroNames.Length; i++)
            {
                double corr = matrix[baseIndex, macroStartIndex + i];
                barSeries.Items.Add(new ColumnItem(corr));
                catAxis.Labels.Add(macroNames[i]);
            }

            model.Series.Add(barSeries);
            _timeSeriesPlot.Model = model;
        }
    }
}