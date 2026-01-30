using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Assy
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;
        private DispatcherTimer productionTimer;
        private bool isFullscreen = true;
        private bool isLoggingOut = false;
        private int targetPerShift = 100;
        private int totalActual = 0;
        private DateTime shiftStartTime;
        private string currentShift = "Shift 1";

        // Daftar slot waktu untuk Shift 1
        private readonly List<TimeSlot> shift1TimeSlots = new List<TimeSlot>
        {
            new TimeSlot("07:30 - 08:30", 13),
            new TimeSlot("08:30 - 09:30", 13),
            new TimeSlot("09:30 - 10:45", 14),
            new TimeSlot("10:45 - 11:45", 12),
            new TimeSlot("11:45 - 13:30", 12),
            new TimeSlot("13:30 - 14:30", 12),
            new TimeSlot("14:30 - 15:30", 12),
            new TimeSlot("15:30 - 16:30", 12)
        };

        // Daftar slot waktu untuk Shift 2
        private readonly List<TimeSlot> shift2TimeSlots = new List<TimeSlot>
        {
            new TimeSlot("19:30 - 20:30", 13),
            new TimeSlot("20:30 - 21:30", 13),
            new TimeSlot("21:30 - 22:45", 14),
            new TimeSlot("22:45 - 23:45", 12),
            new TimeSlot("23:45 - 01:30", 12),
            new TimeSlot("01:30 - 02:30", 12),
            new TimeSlot("02:30 - 03:30", 12),
            new TimeSlot("03:30 - 04:30", 12)
        };

        public MainWindow()
        {
            InitializeComponent();

            // Setup timer untuk update waktu
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            // Setup timer untuk simulasi produksi
            productionTimer = new DispatcherTimer();
            productionTimer.Interval = TimeSpan.FromSeconds(2);
            productionTimer.Tick += ProductionTimer_Tick;
            productionTimer.Start();

            // Setup keyboard shortcuts
            this.KeyDown += MainWindow_KeyDown;
            this.Loaded += MainWindow_Loaded;

            // Inisialisasi waktu shift
            shiftStartTime = DateTime.Now;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("MainWindow_Loaded started");

                // Tampilkan data user dari login
                if (txtUserName != null)
                    txtUserName.Text = App.LoggedInSupervisor ?? "Supervisor";

                if (txtUserNoreg != null)
                    txtUserNoreg.Text = $"Noreg: {App.UserNoreg ?? "000000"}";

                if (txtMachineNumber != null)
                    txtMachineNumber.Text = App.MachineNumber ?? "MCH-001";

                if (txtPartNumber != null)
                    txtPartNumber.Text = App.PartNumber ?? "PN-123456";

                // Tampilkan shift dari App (fixed)
                currentShift = App.SelectedShift ?? "Shift 1";
                if (txtShiftInfo != null)
                    txtShiftInfo.Text = currentShift == "Shift 1" ?
                        "Shift 1 (07:30 - 16:30)" : "Shift 2 (19:30 - 04:30)";

                // Set target dari App
                targetPerShift = App.TargetPerShift;

                if (txtTargetShift != null)
                    txtTargetShift.Text = targetPerShift.ToString("N0");

                // Hitung target per detik
                CalculateTargetPerSecond();

                // Setup tabel berdasarkan shift yang fixed
                UpdateTimeSlotsForCurrentShift();

                // Update semua metrics
                UpdateMetrics();

                // SET FOCUS KE TEXTBOX BARCODE
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtBarcode.Focus();
                    txtBarcode.CaretIndex = txtBarcode.Text.Length;
                }), DispatcherPriority.Render);

                Console.WriteLine("MainWindow_Loaded completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MainWindow_Loaded: {ex.Message}");
                MessageBox.Show($"Error loading window: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update waktu
            if (txtDateTime != null)
                txtDateTime.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void ProductionTimer_Tick(object sender, EventArgs e)
        {
            // Simulasi peningkatan output produksi
            Random rnd = new Random();
            int newProduction = rnd.Next(1, 3);
            totalActual += newProduction;

            // Tambah ke slot waktu yang sesuai
            AddProductionToCurrentSlot(newProduction);

            // Update metrics
            UpdateMetrics();
        }

        private void UpdateMetrics()
        {
            try
            {

                // Hitung efisiensi
                double efficiency = 0;
                if (targetPerShift > 0)
                    efficiency = (totalActual / (double)targetPerShift) * 100;

                if (txtEfficiency != null)
                    txtEfficiency.Text = $"{efficiency:F1}%";

                // Update warna efisiensi
                if (txtEfficiency != null)
                {
                    if (efficiency >= 100)
                        txtEfficiency.Foreground = Brushes.Green;
                    else if (efficiency >= 90)
                        txtEfficiency.Foreground = Brushes.Orange;
                    else
                        txtEfficiency.Foreground = Brushes.Red;
                }

               
            }
            catch (Exception ex)
            {
                // Log error untuk debugging
                Console.WriteLine($"Error in UpdateMetrics: {ex.Message}");
            }
        }

        private void CalculateTargetPerSecond()
        {
            try
            {
                // Target per detik = target shift / (9 jam * 3600 detik)
                double targetPerSecond = targetPerShift / (9.0 * 3600.0);
                if (txtTargetSecond != null)
                    txtTargetSecond.Text = targetPerSecond.ToString("F3");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CalculateTargetPerSecond: {ex.Message}");
            }
        }

        private void UpdateTimeSlotsForCurrentShift()
        {
            List<TimeSlot> currentSlots = currentShift == "Shift 1" ? shift1TimeSlots : shift2TimeSlots;

            // Update target per slot berdasarkan target shift yang baru
            UpdateSlotTargets(currentSlots, targetPerShift);

            // Update tampilan tabel
            UpdateTableDisplay(currentSlots);
        }

        private void UpdateSlotTargets(List<TimeSlot> slots, int totalTarget)
        {
            int totalStandard = 100; // Target standard untuk distribusi default

            if (totalTarget == 100)
            {
                // Gunakan distribusi default (13, 13, 14, 12, 12, 12, 12, 12)
                return;
            }

            // Hitung faktor scaling
            double scalingFactor = totalTarget / (double)totalStandard;

            // Update target untuk setiap slot
            for (int i = 0; i < slots.Count; i++)
            {
                int defaultTarget = slots[i].DefaultTarget;
                int newTarget = (int)Math.Round(defaultTarget * scalingFactor);
                slots[i].Target = newTarget;
            }

            // Pastikan total sama dengan target
            AdjustSlotTargetsToMatchTotal(slots, totalTarget);
        }

        private void AdjustSlotTargetsToMatchTotal(List<TimeSlot> slots, int totalTarget)
        {
            int currentTotal = 0;
            foreach (var slot in slots)
            {
                currentTotal += slot.Target;
            }

            int difference = totalTarget - currentTotal;

            // Distribusikan perbedaan ke slot pertama
            if (difference != 0 && slots.Count > 0)
            {
                slots[0].Target += difference;
            }
        }

        private void UpdateTableDisplay(List<TimeSlot> slots)
        {
            try
            {
                // Update setiap baris tabel
                var textBlocks = new[]
                {
                    (txtTimeSlot1, txtTargetSlot1, txtActualSlot1),
                    (txtTimeSlot2, txtTargetSlot2, txtActualSlot2),
                    (txtTimeSlot3, txtTargetSlot3, txtActualSlot3),
                    (txtTimeSlot4, txtTargetSlot4, txtActualSlot4),
                    (txtTimeSlot5, txtTargetSlot5, txtActualSlot5),
                    (txtTimeSlot6, txtTargetSlot6, txtActualSlot6),
                    (txtTimeSlot7, txtTargetSlot7, txtActualSlot7),
                    (txtTimeSlot8, txtTargetSlot8, txtActualSlot8)
                };

                for (int i = 0; i < Math.Min(slots.Count, textBlocks.Length); i++)
                {
                    var slot = slots[i];
                    var (timeBlock, targetBlock, actualBlock) = textBlocks[i];

                    // Null check
                    if (timeBlock != null && targetBlock != null && actualBlock != null)
                    {
                        timeBlock.Text = slot.TimeRange;
                        targetBlock.Text = slot.Target.ToString();
                        actualBlock.Text = slot.Actual.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateTableDisplay: {ex.Message}");
            }
        }

        private void AddProductionToCurrentSlot(int amount)
        {
            try
            {
                List<TimeSlot> currentSlots = currentShift == "Shift 1" ? shift1TimeSlots : shift2TimeSlots;
                DateTime currentTime = DateTime.Now;

                // Tentukan slot berdasarkan waktu saat ini
                string currentHour = currentTime.ToString("HH:mm");
                bool added = false;

                foreach (var slot in currentSlots)
                {
                    // Logika sederhana untuk demo: tambah ke slot berdasarkan waktu
                    // Untuk implementasi real, perlu parsing waktu yang lebih akurat
                    if (currentShift == "Shift 1")
                    {
                        if (currentTime.Hour >= 7 && currentTime.Hour < 8 && slot.TimeRange.Contains("07:30"))
                        {
                            slot.Actual += amount;
                            added = true;
                            break;
                        }
                        else if (currentTime.Hour >= 8 && currentTime.Hour < 9 && slot.TimeRange.Contains("08:30"))
                        {
                            slot.Actual += amount;
                            added = true;
                            break;
                        }
                    }
                    else // Shift 2
                    {
                        if (currentTime.Hour >= 19 && currentTime.Hour < 20 && slot.TimeRange.Contains("19:30"))
                        {
                            slot.Actual += amount;
                            added = true;
                            break;
                        }
                        else if (currentTime.Hour >= 20 && currentTime.Hour < 21 && slot.TimeRange.Contains("20:30"))
                        {
                            slot.Actual += amount;
                            added = true;
                            break;
                        }
                    }
                }

                // Jika tidak cocok dengan slot manapun, tambah ke slot pertama
                if (!added && currentSlots.Count > 0)
                {
                    currentSlots[0].Actual += amount;
                }

                // Update tampilan
                UpdateTableDisplay(currentSlots);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddProductionToCurrentSlot: {ex.Message}");
            }
        }

        // Event handler untuk tombol Update Target
       

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // F11 untuk toggle fullscreen
            if (e.Key == Key.F11)
            {
                ToggleFullscreen();
            }
            // Alt+F4 untuk close
            else if (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                CloseApplication();
            }
        }

        private void ToggleFullscreen()
        {
            if (isFullscreen)
            {
                // Switch to windowed mode
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                isFullscreen = false;
            }
            else
            {
                // Switch to fullscreen mode
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                isFullscreen = true;
            }
        }

        private void btnFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Yakin ingin logout dari sistem?",
                "Konfirmasi Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                isLoggingOut = true;

                // Reset data login
                App.LoggedInSupervisor = null;
                App.UserNoreg = null;
                App.MachineNumber = null;
                App.PartNumber = null;
                App.SelectedShift = null;
                App.TargetPerShift = 100;

                // Stop timer
                timer.Stop();
                productionTimer.Stop();

                // Kembali ke login window
                var loginWindow = new Views.LoginWindow();
                loginWindow.WindowState = WindowState.Maximized;
                loginWindow.Show();

                // Tutup main window
                this.Close();
            }
        }

        private void CloseApplication()
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

        private void btnEditPart_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Fungsi edit part number akan diimplementasikan di sini",
                          "Edit Part Number",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        private void btnDownloadReport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Laporan PDF sedang di-generate...",
                          "Download Report",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);

            // Simulasi download
            MessageBox.Show("Laporan berhasil di-download!",
                          "Success",
                          MessageBoxButton.OK,
                          MessageBoxImage.Information);
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtBarcode.Text))
            {
                // Tambah output
                totalActual++;
                AddProductionToCurrentSlot(1);
                UpdateMetrics();

                // Clear barcode setelah scan
                txtBarcode.Text = "";

                MessageBox.Show("Barcode berhasil di-scan!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void txtBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Auto-scan jika barcode panjang (simulasi)
            if (txtBarcode.Text.Length >= 10)
            {
                // Reset setelah 2 detik
                DispatcherTimer barcodeTimer = new DispatcherTimer();
                barcodeTimer.Interval = TimeSpan.FromSeconds(2);
                barcodeTimer.Tick += (s, args) =>
                {
                    txtBarcode.Text = "";
                    barcodeTimer.Stop();
                };
                barcodeTimer.Start();

                // Tambah output
                totalActual++;
                AddProductionToCurrentSlot(1);
                UpdateMetrics();
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

            if (productionTimer != null)
            {
                productionTimer.Stop();
                productionTimer = null;
            }

            base.OnClosed(e);
        }

        // Event handler untuk tombol Update Target
        private void btnUpdateTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Tampilkan dialog untuk update target
                var dialog = new UpdateTargetDialog();
                dialog.Owner = this;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.TargetValue = targetPerShift;

                if (dialog.ShowDialog() == true)
                {
                    // Update target
                    targetPerShift = dialog.TargetValue;

                    // Update tampilan
                    if (txtTargetShift != null)
                        txtTargetShift.Text = targetPerShift.ToString("N0");

                    // Update perhitungan target per detik
                    CalculateTargetPerSecond();

                    // Update distribusi target per slot
                    UpdateTimeSlotsForCurrentShift();

                    // Update metrics
                    UpdateMetrics();

                    MessageBox.Show($"Target berhasil diupdate menjadi {targetPerShift} unit",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in btnUpdateTarget_Click: {ex.Message}");
                MessageBox.Show($"Error updating target: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Jika ini logout, jangan tanya konfirmasi exit
            if (!isLoggingOut)
            {
                var result = MessageBox.Show("Yakin ingin keluar dari aplikasi?",
                    "Konfirmasi",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }

            base.OnClosing(e);
        }
        private void txtBarcode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Biarkan semua key kecuali kita tangani khusus
            if (e.Key == Key.Enter)
            {
                // Trigger scan ketika Enter ditekan
                btnScan_Click(sender, e);
                e.Handled = true; // Hindari beep sound
            }
        }
    }



    // Class helper untuk slot waktu
    public class TimeSlot
    {
        public string TimeRange { get; set; }
        public int DefaultTarget { get; set; }
        public int Target { get; set; }
        public int Actual { get; set; }

        public TimeSlot(string timeRange, int defaultTarget)
        {
            TimeRange = timeRange;
            DefaultTarget = defaultTarget;
            Target = defaultTarget;
            Actual = 0;
        }
    }
}