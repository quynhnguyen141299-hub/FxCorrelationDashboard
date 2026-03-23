// SeedData.cs — Add this file to your App project.
// Call SeedData.Run(connStr) from Program.cs before Application.Run().
// It downloads 3 years of daily data from Yahoo Finance for all instruments
// and inserts into fx_price / macro_factor tables (skips existing rows).
// Requires: dotnet add package Microsoft.Data.SqlClient  (already added)

using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace FxCorrelationDashboard.App;

public static class SeedData
{
    // Yahoo Finance ticker mapping
    private static readonly Dictionary<string, (string Ticker, string Table, string Column)> _instruments = new()
    {
        // FX pairs
        ["EURUSD"] = ("EURUSD=X", "fx_price", "ccy_pair"),
        ["GBPUSD"] = ("GBPUSD=X", "fx_price", "ccy_pair"),
        ["USDJPY"] = ("USDJPY=X", "fx_price", "ccy_pair"),
        ["AUDUSD"] = ("AUDUSD=X", "fx_price", "ccy_pair"),
        ["USDCAD"] = ("USDCAD=X", "fx_price", "ccy_pair"),
        ["USDCHF"] = ("USDCHF=X", "fx_price", "ccy_pair"),
        ["NZDUSD"] = ("NZDUSD=X", "fx_price", "ccy_pair"),
        ["EURJPY"] = ("EURJPY=X", "fx_price", "ccy_pair"),
        ["GBPJPY"] = ("GBPJPY=X", "fx_price", "ccy_pair"),
        ["AUDJPY"] = ("AUDJPY=X", "fx_price", "ccy_pair"),
        ["CADJPY"] = ("CADJPY=X", "fx_price", "ccy_pair"),
        ["NZDJPY"] = ("NZDJPY=X", "fx_price", "ccy_pair"),
        ["USDNOK"] = ("USDNOK=X", "fx_price", "ccy_pair"),
        // Macro factors
        ["WTI"]    = ("CL=F",     "macro_factor", "factor_name"),
        ["BRENT"]  = ("BZ=F",     "macro_factor", "factor_name"),
        ["US10Y"]  = ("^TNX",     "macro_factor", "factor_name"),
        ["SILVER"] = ("SI=F",     "macro_factor", "factor_name"),
        ["GOLD"]   = ("GC=F",     "macro_factor", "factor_name"),
    };

    public static void Run(string connStr)
    {
        Console.WriteLine("=== Seeding database ===");
        EnsureTables(connStr);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

        var to = DateTimeOffset.UtcNow;
        var from = to.AddYears(-3);
        long period1 = from.ToUnixTimeSeconds();
        long period2 = to.ToUnixTimeSeconds();

        foreach (var (name, info) in _instruments)
        {
            try
            {
                int count = GetExistingCount(connStr, info.Table, info.Column, name);
                if (count > 500)
                {
                    Console.WriteLine($"  {name}: already has {count} rows, skipping.");
                    continue;
                }

                Console.Write($"  {name} ({info.Ticker})... ");
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{info.Ticker}" +
                          $"?period1={period1}&period2={period2}&interval=1d";

                var json = http.GetStringAsync(url).Result;
                var doc = JsonDocument.Parse(json);
                var result = doc.RootElement
                    .GetProperty("chart")
                    .GetProperty("result")[0];

                var timestamps = result.GetProperty("timestamp");
                var closes = result.GetProperty("indicators")
                    .GetProperty("quote")[0]
                    .GetProperty("close");

                int inserted = 0;
                using var conn = new SqlConnection(connStr);
                conn.Open();

                for (int i = 0; i < timestamps.GetArrayLength(); i++)
                {
                    if (closes[i].ValueKind == JsonValueKind.Null) continue;

                    var dt = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64())
                        .UtcDateTime.Date;
                    var val = closes[i].GetDouble();

                    string sql = info.Table == "fx_price"
                        ? @"IF NOT EXISTS (SELECT 1 FROM fx_price WHERE ccy_pair=@n AND ts=@d)
                           INSERT INTO fx_price(ccy_pair,ts,spot) VALUES(@n,@d,@v)"
                        : @"IF NOT EXISTS (SELECT 1 FROM macro_factor WHERE factor_name=@n AND ts=@d)
                           INSERT INTO macro_factor(factor_name,ts,value) VALUES(@n,@d,@v)";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@n", name);
                    cmd.Parameters.AddWithValue("@d", dt);
                    cmd.Parameters.AddWithValue("@v", val);
                    inserted += cmd.ExecuteNonQuery();
                }

                Console.WriteLine($"{inserted} rows inserted.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }
        }

        Console.WriteLine("=== Seeding complete ===\n");
    }

    private static void EnsureTables(string connStr)
    {
        using var conn = new SqlConnection(connStr);
        conn.Open();

        var sql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='fx_price')
            CREATE TABLE fx_price (
                id INT IDENTITY(1,1) PRIMARY KEY,
                ccy_pair NVARCHAR(10) NOT NULL,
                ts DATETIME2 NOT NULL,
                spot FLOAT NOT NULL,
                UNIQUE(ccy_pair,ts)
            );
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name='macro_factor')
            CREATE TABLE macro_factor (
                id INT IDENTITY(1,1) PRIMARY KEY,
                factor_name NVARCHAR(20) NOT NULL,
                ts DATETIME2 NOT NULL,
                value FLOAT NOT NULL,
                UNIQUE(factor_name,ts)
            );";

        using var cmd = new SqlCommand(sql, conn);
        cmd.ExecuteNonQuery();
    }

    private static int GetExistingCount(string connStr, string table, string col, string name)
    {
        using var conn = new SqlConnection(connStr);
        conn.Open();
        using var cmd = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE {col}=@n", conn);
        cmd.Parameters.AddWithValue("@n", name);
        return (int)cmd.ExecuteScalar();
    }
}
