-- Run this against your FxCorr database on (localdb)\MSSQLLocalDB
-- Creates tables if they don't exist and shows row counts.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'fx_price')
CREATE TABLE fx_price (
    id         INT IDENTITY(1,1) PRIMARY KEY,
    ccy_pair   NVARCHAR(10)  NOT NULL,
    ts         DATETIME2     NOT NULL,
    spot       FLOAT         NOT NULL,
    UNIQUE (ccy_pair, ts)
);

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'macro_factor')
CREATE TABLE macro_factor (
    id          INT IDENTITY(1,1) PRIMARY KEY,
    factor_name NVARCHAR(20)  NOT NULL,
    ts          DATETIME2     NOT NULL,
    value       FLOAT         NOT NULL,
    UNIQUE (factor_name, ts)
);

-- Check what you have
SELECT 'fx_price' AS [Table], COUNT(*) AS Rows FROM fx_price
UNION ALL
SELECT 'macro_factor', COUNT(*) FROM macro_factor;

SELECT DISTINCT ccy_pair FROM fx_price;
SELECT DISTINCT factor_name FROM macro_factor;
