using FxCorrelationDashboard.Data;

namespace FxCorrelationDashboard.Engine;

public static class CorrelationEngine
{
    public static CorrelationResult RollingCorrelation(
        PriceSeries a, PriceSeries b, int window)
    {
        var commonDates = a.Dates.Intersect(b.Dates).OrderBy(d => d).ToArray();

        var aMap = a.Dates.Zip(a.Values).ToDictionary(x => x.First, x => x.Second);
        var bMap = b.Dates.Zip(b.Values).ToDictionary(x => x.First, x => x.Second);

        var aPrices = commonDates.Select(d => aMap[d]).ToArray();
        var bPrices = commonDates.Select(d => bMap[d]).ToArray();

        var aRet = ReturnCalculator.LogReturns(aPrices);
        var bRet = ReturnCalculator.LogReturns(bPrices);
        var retDates = commonDates.Skip(1).ToArray();

        int n = aRet.Length;
        var corrDates = new List<DateTime>();
        var corrVals = new List<double>();

        for (int i = window - 1; i < n; i++)
        {
            var sliceA = new ArraySegment<double>(aRet, i - window + 1, window);
            var sliceB = new ArraySegment<double>(bRet, i - window + 1, window);
            corrVals.Add(Pearson(sliceA, sliceB));
            corrDates.Add(retDates[i]);
        }

        return new CorrelationResult(a.Name, b.Name, corrVals.ToArray(), corrDates.ToArray());
    }

    /// <summary>
    /// Computes full-sample Pearson correlation between two price series
    /// using log returns over all overlapping dates.
    /// </summary>
    public static double FullSampleCorrelation(PriceSeries a, PriceSeries b)
    {
        var commonDates = a.Dates.Intersect(b.Dates).OrderBy(d => d).ToArray();

        var aMap = a.Dates.Zip(a.Values).ToDictionary(x => x.First, x => x.Second);
        var bMap = b.Dates.Zip(b.Values).ToDictionary(x => x.First, x => x.Second);

        var aPrices = commonDates.Select(d => aMap[d]).ToArray();
        var bPrices = commonDates.Select(d => bMap[d]).ToArray();

        var aRet = ReturnCalculator.LogReturns(aPrices);
        var bRet = ReturnCalculator.LogReturns(bPrices);

        if (aRet.Length < 2) return 0.0;

        return Pearson(
            new ArraySegment<double>(aRet),
            new ArraySegment<double>(bRet));
    }

    public static double[,] CorrelationMatrix(PriceSeries[] series, int window)
    {
        int n = series.Length;
        var matrix = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            matrix[i, i] = 1.0;
            for (int j = i + 1; j < n; j++)
            {
                var result = RollingCorrelation(series[i], series[j], window);
                double lastCorr = result.RollingCorr.Length > 0
                    ? result.RollingCorr[^1]
                    : 0.0;
                matrix[i, j] = lastCorr;
                matrix[j, i] = lastCorr;
            }
        }
        return matrix;
    }

    // ── FIX: ArraySegment<T>'s indexer already applies the offset internally.
    //    The original code did  x[i + x.Offset]  which double-applied the offset,
    //    reading from wrong memory and producing near-zero correlations.
    //    Correct usage is simply  x[i].
    private static double Pearson(ArraySegment<double> x, ArraySegment<double> y)
    {
        int n = x.Count;
        double mx = 0, my = 0;
        foreach (var v in x) mx += v;
        foreach (var v in y) my += v;
        mx /= n; my /= n;

        double cov = 0, sx = 0, sy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - mx;   // was: x[i + x.Offset] — WRONG
            double dy = y[i] - my;    // was: y[i + y.Offset] — WRONG
            cov += dx * dy;
            sx += dx * dx;
            sy += dy * dy;
        }

        double denom = Math.Sqrt(sx * sy);
        return denom < 1e-15 ? 0.0 : cov / denom;
    }
}
