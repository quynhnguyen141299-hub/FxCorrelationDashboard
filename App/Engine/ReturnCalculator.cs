namespace FxCorrelationDashboard.Engine;

public static class ReturnCalculator
{
    public static double[] LogReturns(double[] prices)
    {
        if (prices.Length < 2) return Array.Empty<double>();
        var ret = new double[prices.Length - 1];
        for (int i = 1; i < prices.Length; i++)
            ret[i - 1] = Math.Log(prices[i] / prices[i - 1]);
        return ret;
    }
}
