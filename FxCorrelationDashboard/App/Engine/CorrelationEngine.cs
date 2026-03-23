using FxCorrelationDashboard.Data;

namespace FxCorrelationDashboard.Engine;

public static class CorrelationEngine
{
    // Full‑sample correlation (no rolling)
    public static CorrelationResult FullSampleCorrelation(PriceSeries a, PriceSeries b)
    {
        var commonDates = a.Dates.Intersect(b.Dates).OrderBy(d => d).ToArray();
        if (commonDates.Length < 2)
            return new CorrelationResult(a.Name, b.Name, Array.Empty<double>(), Array.Empty<DateTime>());

        var aMap = new Dictionary<DateTime, double>();
        for (int i = 0; i < a.Dates.Length; i++)
            aMap[a.Dates[i]] = a.Values[i];

        var bMap = new Dictionary<DateTime, double>();
        for (int i = 0; i < b.Dates.Length; i++)
            bMap[b.Dates[i]] = b.Values[i];

        var aPrices = commonDates.Select(d => aMap[d]).ToArray();
        var bPrices = commonDates.Select(d => bMap[d]).ToArray();

        var aRet = ReturnCalculator.LogReturns(aPrices);
        var bRet = ReturnCalculator.LogReturns(bPrices);

        int n = Math.Min(aRet.Length, bRet.Length);
        if (n < 2)
            return new CorrelationResult(a.Name, b.Name, Array.Empty<double>(), Array.Empty<DateTime>());

        var sliceA = new ArraySegment<double>(aRet, 0, n);
        var sliceB = new ArraySegment<double>(bRet, 0, n);
        double corr = Pearson(sliceA, sliceB);
        
        // debug print
        Console.WriteLine($"{a.Name}-{b.Name}: n={n}, corr={corr}");

        return new CorrelationResult(
            a.Name,
            b.Name,
            new[] { corr },
            new[] { commonDates[^1] } // last date
        );
    }

    // Matrix of full‑sample correlations
    public static double[,] CorrelationMatrix(PriceSeries[] series)
    {
        int n = series.Length;
        var matrix = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            matrix[i, i] = 1.0;
            for (int j = i + 1; j < n; j++)
            {
                var result = FullSampleCorrelation(series[i], series[j]);

                double corr = (result.RollingCorr != null && result.RollingCorr.Length == 1)
                    ? result.RollingCorr[0]
                    : 0.0;

                matrix[i, j] = corr;
                matrix[j, i] = corr;
            }
        }

        return matrix;
    }

    private static double Pearson(ArraySegment<double> x, ArraySegment<double> y)
    {
        int n = x.Count;
        if (n == 0) return 0.0;

        double mx = 0, my = 0;
        foreach (var v in x) mx += v;
        foreach (var v in y) my += v;
        mx /= n; my /= n;

        double cov = 0, sx = 0, sy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i + x.Offset] - mx;
            double dy = y[i + y.Offset] - my;
            cov += dx * dy;
            sx += dx * dx;
            sy += dy * dy;
        }

        double denom = Math.Sqrt(sx * sy);
        return denom < 1e-15 ? 0.0 : cov / denom;
    }
}