# FX Correlation Dashboard

A Windows Forms desktop application for visualising rolling and full-sample correlations between G10 FX pairs and macro factors. Built with .NET 8, SQL Server LocalDB, and OxyPlot.

<img width="1379" height="783" alt="AppUI" src="https://github.com/user-attachments/assets/b91fe5fc-62aa-422b-8683-7334f609bb7b" />

## Features

- **Correlation Heatmap** — Rolling Pearson correlation matrix across FX pairs and macro factors, rendered as a colour-coded heatmap (blue = negative, red = positive).
- **Full-Sample Bar Chart** — Horizontal bar chart showing the full-sample log-return correlation between a selected FX pair and each macro factor (WTI, Brent, US 10Y, Silver, Gold).
- **Configurable Baskets** — Switch between pre-defined currency baskets:
  - G10 Majors (EURUSD, GBPUSD, USDJPY, AUDUSD, USDCAD, USDCHF, NZDUSD)
  - JPY Crosses (EURJPY, GBPJPY, AUDJPY, CADJPY, NZDJPY)
  - Commodity FX (AUDUSD, NZDUSD, USDCAD, USDNOK)
- **Rolling Window Selection** — Choose from 30, 60, 90, 120, or 252-day rolling windows.
- **Automatic Data Seeding** — On first run, downloads 3 years of daily close prices from Yahoo Finance for all instruments and populates the local database.

## Tech Stack

| Component | Technology |
|---|---|
| Runtime | .NET 8 (Windows) |
| UI Framework | Windows Forms |
| Charting | [OxyPlot](https://oxyplot.github.io/) 2.2 |
| Database | SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) |
| Data Client | Microsoft.Data.SqlClient 7.0 |
| Data Source | Yahoo Finance (v8 chart API) |

## Project Structure

```
App/
├── Program.cs                  # Entry point — seeds DB, launches MainForm
├── MainForm.cs                 # Dashboard UI — heatmap + bar chart panels
├── MainForm.resx               # WinForms designer resources
├── SeedData.cs                 # Yahoo Finance data loader (auto-seeds on first run)
├── SeedDatabase.sql            # Manual SQL script to create tables
├── Data/
│   ├── FxDataRepository.cs     # SQL queries for fx_price & macro_factor tables
│   └── Model.cs                # PriceSeries and CorrelationResult records
├── Engine/
│   ├── CorrelationEngine.cs    # Rolling & full-sample Pearson correlation
│   └── ReturnCalculator.cs     # Log-return computation
├── App.csproj                  # Project file (.NET 8, WinForms, NuGet refs)
└── App.sln                     # Visual Studio solution
```

## Prerequisites

- **Windows 10/11** (Windows Forms requirement)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server LocalDB** — included with Visual Studio, or install via [SQL Server Express LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/quynhnguyen141299-hub/FxCorrelationDashboard.git
cd FxCorrelationDashboard
```

### 2. Create the LocalDB database

```bash
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE FxCorr"
```

Alternatively, run `App/SeedDatabase.sql` against the `FxCorr` database to create the tables manually.

### 3. Build and run

```bash
cd App
dotnet restore
dotnet run
```

On the first launch, `SeedData` automatically downloads ~3 years of daily data from Yahoo Finance for all 13 FX pairs and 5 macro factors. This takes a minute or two — progress is logged to the console. Subsequent launches skip seeding if the data already exists (> 500 rows per instrument).

## How It Works

### Data Pipeline

1. **SeedData** fetches daily OHLC data from Yahoo Finance's v8 chart API for each instrument.
2. Prices are stored in two LocalDB tables:
   - `fx_price` — columns: `ccy_pair`, `ts`, `spot`
   - `macro_factor` — columns: `factor_name`, `ts`, `value`
3. `FxDataRepository` queries these tables with date-range filters and caches results in memory.

### Correlation Engine

1. **Log returns** are computed from daily close prices: `r(t) = ln(P(t) / P(t-1))`
2. **Rolling correlation** uses a sliding window of Pearson correlation over aligned log-return series.
3. The **heatmap** displays the most recent rolling correlation value for every pair in the matrix.
4. The **bar chart** shows the full-sample (all overlapping dates) Pearson correlation between a selected FX pair and each macro factor.

## Database Schema

```sql
CREATE TABLE fx_price (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    ccy_pair   NVARCHAR(10)  NOT NULL,
    ts         DATETIME2     NOT NULL,
    spot       FLOAT         NOT NULL,
    UNIQUE (ccy_pair, ts)
);

CREATE TABLE macro_factor (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    factor_name NVARCHAR(20)  NOT NULL,
    ts          DATETIME2     NOT NULL,
    value       FLOAT         NOT NULL,
    UNIQUE (factor_name, ts)
);
```

## Instruments

### FX Pairs
`EURUSD` · `GBPUSD` · `USDJPY` · `AUDUSD` · `USDCAD` · `USDCHF` · `NZDUSD` · `EURJPY` · `GBPJPY` · `AUDJPY` · `CADJPY` · `NZDJPY` · `USDNOK`

### Macro Factors
`WTI` (CL=F) · `BRENT` (BZ=F) · `US10Y` (^TNX) · `SILVER` (SI=F) · `GOLD` (GC=F)

## License

This project is provided as-is for educational and research purposes.
