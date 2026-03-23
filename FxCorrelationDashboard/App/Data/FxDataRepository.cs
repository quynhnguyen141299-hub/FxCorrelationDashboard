using Microsoft.Data.SqlClient;

namespace FxCorrelationDashboard.Data;

public class FxDataRepository
{
    private readonly string _connStr;
    private readonly Dictionary<string, PriceSeries> _cache = new();

    public FxDataRepository(string connStr) => _connStr = connStr;

    public PriceSeries GetFxSeries(string ccyPair, DateTime from, DateTime to)
    {
        var key = $"{ccyPair}|{from:yyyy-MM-dd}|{to:yyyy-MM-dd}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var dates = new List<DateTime>();
        var values = new List<double>();

        try
        {
            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT ts, spot FROM fx_price WHERE ccy_pair = @pair " +
                "AND ts BETWEEN @from AND @to ORDER BY ts", conn);
            cmd.Parameters.AddWithValue("@pair", ccyPair);
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dates.Add(reader.GetDateTime(0));
                values.Add(reader.GetDouble(1));
            }
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"DB error loading {ccyPair}: {ex.Message}");
            return new PriceSeries(ccyPair, Array.Empty<DateTime>(), Array.Empty<double>());
        }

        var series = new PriceSeries(ccyPair, dates.ToArray(), values.ToArray());
        _cache[key] = series;
        return series;
    }

    public PriceSeries GetMacroSeries(string factorName, DateTime from, DateTime to)
    {
        var key = $"MACRO:{factorName}|{from:yyyy-MM-dd}|{to:yyyy-MM-dd}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var dates = new List<DateTime>();
        var values = new List<double>();

        try
        {
            using var conn = new SqlConnection(_connStr);
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT ts, value FROM macro_factor WHERE factor_name = @name " +
                "AND ts BETWEEN @from AND @to ORDER BY ts", conn);
            cmd.Parameters.AddWithValue("@name", factorName);
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dates.Add(reader.GetDateTime(0));
                values.Add(reader.GetDouble(1));
            }
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"DB error loading {factorName}: {ex.Message}");
            return new PriceSeries(factorName, Array.Empty<DateTime>(), Array.Empty<double>());
        }

        var series = new PriceSeries(factorName, dates.ToArray(), values.ToArray());
        _cache[key] = series;
        return series;
    }

    public void ClearCache() => _cache.Clear();
}