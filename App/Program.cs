using System;
using System.Windows.Forms;

namespace FxCorrelationDashboard.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string connStr = @"Server=(localdb)\MSSQLLocalDB;Database=FxCorr;Trusted_Connection=True;TrustServerCertificate=True;";

        // Seed DB with Yahoo Finance data on first run (skips if already populated)
        SeedData.Run(connStr);

        Application.Run(new MainForm(connStr));
    }
}
