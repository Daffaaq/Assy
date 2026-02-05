using System.Configuration;
using System.Data;
using System.Windows;
using PdfSharp.Fonts;

namespace Assy
{
    public partial class App : Application
    {
        // Property untuk menyimpan data user yang login
        public static string? SupervisorNoreg { get; set; }
        public static string? SupervisorNama { get; set; }
        public static string? SupervisorDivisi { get; set; }
        public static string? SupervisorDept { get; set; }

        public static string? OperatorNama { get; set; }
        public static string? OperatorDivisi { get; set; }
        public static string? OperatorDept { get; set; }
        public static string? LoggedInUserNoreg { get; set; }
        public static string? MachineNumber { get; set; }
        public static string? PartNumber { get; set; }
        public static string? ItemFGS { get; set; }

        // Property untuk menyimpan data target (dari TargetWindow)
        public static int TargetPerShift { get; set; }
        public static int SelectedShift { get; set; }
        public static DateTime ProductionDate { get; set; }
        public static DateTime? CurrentServerTime { get; set; }

        public App()
        {
            // 🔥 WAJIB: tangkap crash Release
            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show(
                    $"UNHANDLED EXCEPTION:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "Application Crash",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                e.Handled = true;
            };
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Enable Windows fonts under Windows (jika menggunakan PDFsharp Core)
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            // Set fullscreen untuk seluruh aplikasi
            Current.MainWindow = new Views.LoginWindow();
            //Current.MainWindow.WindowStyle = WindowStyle.None;
            Current.MainWindow.Show();
        }

    }
}