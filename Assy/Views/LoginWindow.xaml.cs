using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Assy.Views
{
    public partial class LoginWindow : Window
    {
        private DispatcherTimer timer;

        public LoginWindow()
        {
            InitializeComponent();

            // Set tanggal hari ini
            dpTanggal.SelectedDate = DateTime.Now;

            // Set default No Register (bisa dihapus karena manual)
            // txtNoRegister.Text = GenerateRegisterNumber();

            // Set focus ke input No Register
            Loaded += (s, e) => txtNoRegister.Focus();

            // Setup timer untuk system time
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update system time di sidebar
            if (txtSidebarTime != null)
                txtSidebarTime.Text = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
        }

        private string GenerateRegisterNumber()
        {
            return $"REG{DateTime.Now:yyyyMMddHHmmss}";
        }

        // Di method btnLogin_Click, ganti bagian yang membuka MainWindow:

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            // ... [validasi tetap sama] ...

            // Simpan data ke App properties
            App.LoggedInSupervisor = txtSupervisor.Text;
            App.MachineNumber = (cbMesin.SelectedItem as ComboBoxItem)?.Content?.ToString();
            App.PartNumber = (cbPartNo.SelectedItem as ComboBoxItem)?.Content?.ToString();

            // Tampilkan konfirmasi
            var result = MessageBox.Show(
                $"Konfirmasi Login:\n\n" +
                $"📅 Tanggal: {dpTanggal.SelectedDate:dd/MM/yyyy}\n" +
                $"🔢 No Register: {txtNoRegister.Text}\n" +
                $"⚙️ Mesin: {App.MachineNumber}\n" +
                $"👨‍💼 Supervisor: {App.LoggedInSupervisor}\n" +
                $"📦 Part No: {App.PartNumber}\n\n" +
                $"Apakah data sudah benar?",
                "Konfirmasi Login",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // BUKA TARGET WINDOW DULU, bukan MainWindow langsung
                var targetWindow = new TargetWindow(
                    App.LoggedInSupervisor,
                    App.MachineNumber,
                    App.PartNumber);

                targetWindow.WindowState = WindowState.Maximized;
                targetWindow.WindowStyle = WindowStyle.None;
                targetWindow.Show();

                // Tutup login window
                this.Close();
            }
        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Yakin ingin keluar dari aplikasi?",
                "Konfirmasi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Clean up timer
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            base.OnClosed(e);
        }
    }
}