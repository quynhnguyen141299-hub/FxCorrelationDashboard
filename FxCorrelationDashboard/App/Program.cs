using System;
using System.Windows.Forms;

namespace FxCorrelationDashboard.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();

            string connStr = @"Server=(localdb)\MSSQLLocalDB;Database=FxCorr;Trusted_Connection=True;";
            Application.Run(new MainForm(connStr));
        }
    }
}