using System.Configuration;
using System.Data;
using System.Windows;

namespace Assy
{
    public partial class App : Application
    {
        // Property untuk menyimpan data user yang login
        public static string LoggedInSupervisor { get; set; }
        public static string UserNoreg { get; set; }
        public static string MachineNumber { get; set; }
        public static string PartNumber { get; set; }

        // Property untuk menyimpan data target (dari TargetWindow)
        public static int TargetPerShift { get; set; }
        public static string SelectedShift { get; set; }
        public static DateTime ProductionDate { get; set; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Set fullscreen untuk seluruh aplikasi
            Current.MainWindow = new Views.LoginWindow();
            Current.MainWindow.WindowState = WindowState.Maximized;
            Current.MainWindow.WindowStyle = WindowStyle.None;
            Current.MainWindow.Show();
        }
    }
}