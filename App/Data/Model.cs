namespace FxCorrelationDashboard.Data;

public record PriceSeries(string Name, DateTime[] Dates, double[] Values);
public record CorrelationResult(string RowName, string ColName, double[] RollingCorr, DateTime[] Dates);