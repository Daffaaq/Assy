using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Xps.Packaging;
using Assy.Views;
using Microsoft.Data.SqlClient;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
namespace Assy
{
    public partial class MainWindow : Window
    {
        #region Fields & Properties
        private DispatcherTimer timer;
        private bool isLoggingOut = false;
        private bool isReloading = false;
        private bool isExportingPdf = false;
        private int targetPerShift = 100;
        private int totalActual = 0;
        private string currentShift = "1";
        private string connectionString = @"Server=YOUR_SERVER_NAME;Database=assy;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=true;";

        // Daftar slot waktu untuk Shift 1
        private readonly List<TimeSlot> shift1TimeSlots = new List<TimeSlot>
        {
            new TimeSlot("07:30 - 08:30"),
            new TimeSlot("08:30 - 09:30"),
            new TimeSlot("09:30 - 10:45"),
            new TimeSlot("10:45 - 11:45"),
            new TimeSlot("11:45 - 13:30"),
            new TimeSlot("13:30 - 14:30"),
            new TimeSlot("14:30 - 15:30"),
            new TimeSlot("15:30 - 16:30")
        };

        // Daftar slot waktu untuk Shift 2
        private readonly List<TimeSlot> shift2TimeSlots = new List<TimeSlot>
        {
            new TimeSlot("19:30 - 20:30"),
            new TimeSlot("20:30 - 21:30"),
            new TimeSlot("21:30 - 22:45"),
            new TimeSlot("22:45 - 23:45"),
            new TimeSlot("23:45 - 01:30"),
            new TimeSlot("01:30 - 02:30"),
            new TimeSlot("02:30 - 03:30"),
            new TimeSlot("03:30 - 04:30")
        };
        #endregion

        #region constructors & Initialization
        public MainWindow()
        {
            // Initialize font resolver
            FontInitializer.Initialize();
            InitializeComponent();


            // Setup timer hanya untuk update waktu display (jam saja)
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            // Setup keyboard shortcuts
            this.KeyDown += MainWindow_KeyDown;
            this.Loaded += MainWindow_Loaded;
        }
        #endregion

        #region Window Lifecycle & Event
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Debug.WriteLine("MainWindow_Loaded started");
                Console.WriteLine("MainWindow_Loaded started");

                // Tampilkan data user dari login
                if (txtUserName != null)
                    txtUserName.Text = App.OperatorNama ?? "Operator";

                if (txtUserNoreg != null)
                    txtUserNoreg.Text = $"Noreg: {App.LoggedInUserNoreg ?? "000000"}";

                if (txtMachineNumber != null)
                    txtMachineNumber.Text = App.MachineNumber ?? "MCH-001";

                if (txtPartNumber != null)
                    txtPartNumber.Text = App.PartNumber ?? "PN-123456";

                // Tampilkan shift dari App (fixed)
                currentShift = App.SelectedShift.ToString();
                if (txtShiftInfo != null)
                    txtShiftInfo.Text = currentShift == "1" ?
                        "Shift 1 (07:30 - 16:30)" : "Shift 2 (19:30 - 04:30)";

                // Set target dari App
                targetPerShift = App.TargetPerShift;

                if (txtTargetShift != null)
                    txtTargetShift.Text = targetPerShift.ToString("N0");

                // Hitung target per detik
                CalculateTargetPerSecond();

                // Setup tabel berdasarkan shift yang fixed
                UpdateTimeSlotsForCurrentShift();

                // AMBIL DATA AWAL DARI DATABASE SAAT PERTAMA KALI LOAD
                LoadInitialDataFromDatabase();

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

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+E untuk edit part number
            if (e.Key == Key.E && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                btnEditPart_Click(sender, e);
                e.Handled = true;
            }

            // Alt+F4 untuk close
            if (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                CloseApplication();
                e.Handled = true;
            }

            // Fokus ke barcode jika Escape ditekan
            if (e.Key == Key.Escape)
            {
                if (txtBarcode != null)
                {
                    txtBarcode.Focus();
                    txtBarcode.SelectAll();
                }
                e.Handled = true;
            }
        }
        #endregion

        #region Database Loading Methods
        private void LoadInitialDataFromDatabase()
        {
            try
            {
                // Ambil total actual output dari database
                totalActual = GetActualOutputFromDatabase();

                // Ambil data per slot dari database
                LoadSlotDataFromDatabase();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadInitialDataFromDatabase: {ex.Message}");
            }
        }

        private void LoadSlotDataFromDatabase()
        {
            try
            {
                // Panggil method baru yang pakai stored procedure
                GetSlotDataFromDatabase();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadSlotDataFromDatabase: {ex.Message}");
            }
        }
        #endregion

        #region Timer Methods
        private void Timer_Tick(object? sender, EventArgs e)
        {
                if (isReloading) return;
            try
            {
                // PERBAIKAN: SIMPAN HASIL KE totalActual!
                totalActual = GetActualOutputFromDatabase(); // <-- INI YANG HILANG!
                // Update waktu display dari server setiap detik
                 // 2. Update slot data (untuk tabel)
                GetSlotDataFromDatabase();
                LoadServerDateTime();

                // Update highlight slot yang sedang berjalan
                UpdateCurrentSlotHighlight();
                CalculateTargetPerSecond();

                // UPDATE UI DENGAN DATA TERBARU
                UpdateMetrics();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Timer_Tick: {ex.Message}");
            }
        }
        #endregion

        #region Server Time Methods
        private void LoadServerDateTime()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Query untuk ambil tanggal dan waktu dari server
                    string query = "SELECT GETDATE() AS ServerDateTime";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                DateTime serverDateTime = reader.GetDateTime(0);

                                // Update UI di thread UI
                                Dispatcher.Invoke(() =>
                                {
                                    txtDateDisplay.Text = serverDateTime.ToString("dd MMMM yyyy");
                                    txtTimeDisplay.Text = serverDateTime.ToString("HH:mm:ss");

                                    // Simpan waktu server untuk menentukan slot
                                    App.CurrentServerTime = serverDateTime;
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Jika gagal ambil dari server, pakai waktu lokal sebagai fallback
                Dispatcher.Invoke(() =>
                {
                    txtDateDisplay.Text = DateTime.Now.ToString("dd MMMM yyyy");
                    txtTimeDisplay.Text = DateTime.Now.ToString("HH:mm:ss");
                    App.CurrentServerTime = DateTime.Now;
                });

                // Log error jika diperlukan
                Console.WriteLine("Error getting server time: " + ex.Message);
            }
        }
        #endregion

        #region slot highlight Methods
        private void UpdateCurrentSlotHighlight()
        {
            try
            {
                // Hanya update jika ada waktu server
                if (App.CurrentServerTime == null) return;

                DateTime currentTime = App.CurrentServerTime.Value;
                List<TimeSlot> currentSlots = currentShift == "1" ? shift1TimeSlots : shift2TimeSlots;

                // Cari slot yang sedang berjalan
                int currentSlotIndex = FindCurrentSlotIndex(currentTime, currentSlots);

                // Update highlight di UI thread
                Dispatcher.Invoke(() =>
                {
                    UpdateTableWithHighlight(currentSlots, currentSlotIndex);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateCurrentSlotHighlight: {ex.Message}");
            }
        }

        private int FindCurrentSlotIndex(DateTime currentTime, List<TimeSlot> slots)
        {
            int currentMinutes = currentTime.Hour * 60 + currentTime.Minute;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (IsTimeInSlot(currentMinutes, slot.TimeRange))
                {
                    return i; // Return index slot yang sedang berjalan
                }
            }

            return -1; // Tidak ada slot yang sedang berjalan
        }

        private bool IsTimeInSlot(int currentMinutes, string timeRange)
        {
            try
            {
                // Parse waktu dari format "07:30 - 08:30"
                string[] times = timeRange.Split('-');
                if (times.Length >= 2)
                {
                    string startTimeStr = times[0].Trim();
                    string endTimeStr = times[1].Trim();

                    // Parse waktu mulai
                    string[] startParts = startTimeStr.Split(':');
                    if (startParts.Length >= 2)
                    {
                        int startHour = int.Parse(startParts[0]);
                        int startMinute = int.Parse(startParts[1]);
                        int startMinutes = startHour * 60 + startMinute;

                        // Parse waktu akhir
                        string[] endParts = endTimeStr.Split(':');
                        if (endParts.Length >= 2)
                        {
                            int endHour = int.Parse(endParts[0]);
                            int endMinute = int.Parse(endParts[1]);
                            int endMinutes = endHour * 60 + endMinute;

                            // Handle kasus melewati tengah malam (untuk shift 2)
                            if (endMinutes < startMinutes)
                            {
                                endMinutes += 24 * 60; // Tambah 24 jam
                            }

                            // Cek apakah waktu saat ini berada dalam rentang
                            return currentMinutes >= startMinutes && currentMinutes < endMinutes;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing time range: {ex.Message}");
                return false;
            }
        }

        private void UpdateTableWithHighlight(List<TimeSlot> slots, int currentSlotIndex)
        {
            try
            {
                // Kumpulkan semua border dari baris tabel
                System.Windows.Controls.Border[] borders = new System.Windows.Controls.Border[]
                {
                    FindBorderByName("borderSlot1"),
                    FindBorderByName("borderSlot2"),
                    FindBorderByName("borderSlot3"),
                    FindBorderByName("borderSlot4"),
                    FindBorderByName("borderSlot5"),
                    FindBorderByName("borderSlot6"),
                    FindBorderByName("borderSlot7"),
                    FindBorderByName("borderSlot8")
                };

                // Reset semua border ke default
                for (int i = 0; i < borders.Length; i++)
                {
                    if (borders[i] != null)
                    {
                        // Reset ke style normal
                        var style = this.FindResource("TableRowStyle") as System.Windows.Style;
                        borders[i].Style = style;

                        // Clear background dan border khusus
                        borders[i].Background = Brushes.Transparent;
                        borders[i].BorderBrush = Brushes.Transparent;
                        borders[i].BorderThickness = new Thickness(0, 0, 0, 1);
                    }
                }

                // Apply highlight ke slot yang sedang berjalan
                if (currentSlotIndex >= 0 && currentSlotIndex < borders.Length)
                {
                    var currentBorder = borders[currentSlotIndex];
                    if (currentBorder != null)
                    {
                        // Set highlight hijau untuk slot yang sedang berjalan
                        currentBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 180, 99)); // Hijau
                        currentBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 155, 86)); // Border hijau
                        currentBorder.BorderThickness = new Thickness(2);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateTableWithHighlight: {ex.Message}");
            }
        }

        private System.Windows.Controls.Border FindBorderByName(string name)
        {
            // Fungsi helper untuk mencari Border berdasarkan nama
            // Tanda ! artinya: "Percaya sama saya, ini nggak akan null!"
            return (this.FindName(name) as System.Windows.Controls.Border)!;
        }
        #endregion

        #region UI Update Methods
        private void UpdateMetrics()
        {
            try
            {
                // Pastikan dijalankan di UI thread
                if (Dispatcher.CheckAccess())
                {
                    // Sudah di UI thread
                    UpdateMetricsUI();
                }
                else
                {
                    // Panggil ke UI thread
                    Dispatcher.Invoke(() => UpdateMetricsUI());
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateMetrics: {ex.Message}");
            }
        }

        private void UpdateMetricsUI()
        {
            try
            {
                Debug.WriteLine($"UpdateMetricsUI: Updating txtAktual with {totalActual}");

                if (txtAktual != null)
                {
                    // Format dengan pemisah ribuan dan pastikan tidak null
                    txtAktual.Text = totalActual.ToString("N0");
                    Debug.WriteLine($"txtAktual updated to: {txtAktual.Text}");
                }
                else
                {
                    Debug.WriteLine("ERROR: txtAktual is null!");

                    // Coba cari lagi
                    txtAktual = FindName("txtAktual") as TextBlock;
                    if (txtAktual != null)
                    {
                        txtAktual.Text = totalActual.ToString("N0");
                        Debug.WriteLine($"txtAktual found and updated to: {txtAktual.Text}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in UpdateMetricsUI: {ex.Message}");
            }
        }
        #endregion

        #region Target Calculation Methods
        private void CalculateTargetPerSecond()
        {
            try
            {
                if (App.CurrentServerTime == null) return;

                DateTime now = App.CurrentServerTime.Value;
                int hour = now.Hour;
                int minute = now.Minute;

                List<TimeSlot> currentSlots = currentShift == "1" ? shift1TimeSlots : shift2TimeSlots;

                double targetPerSecond = 0;

                // LOGIKA PERSIS SAMA DENGAN PHP
                if ((hour == 7 && minute >= 30) || (hour == 8 && minute < 30) ||
                    (hour == 19 && minute >= 30) || (hour == 20 && minute < 30))
                {
                    // Slot 1 sedang berjalan (07:30-08:30 atau 19:30-20:30)
                    targetPerSecond = Math.Round(((hour == 7 || hour == 19) ?
                        (minute - 30) : (minute + 30)) * currentSlots[0].Target / 60.0);
                }
                else if ((hour == 8 && minute >= 30) || (hour == 9 && minute < 30) ||
                         (hour == 20 && minute >= 30) || (hour == 21 && minute < 30))
                {
                    // Slot 2 sedang berjalan (08:30-09:30 atau 20:30-21:30)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        (((hour == 8 || hour == 20) ? (minute - 30) : (minute + 30)) *
                        currentSlots[1].Target / 60.0)));
                }
                else if ((hour == 9 && minute >= 30) || (hour == 10 && minute < 45) ||
                         (hour == 21 && minute >= 30) || (hour == 22 && minute < 45))
                {
                    // Slot 3 sedang berjalan (09:30-10:45 atau 21:30-22:45)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        (((hour == 9 || hour == 21) ? (minute - 30) : (minute + 30)) *
                        currentSlots[2].Target / 75.0)));
                }
                else if ((hour == 10 && minute >= 45) || (hour == 11 && minute < 45) ||
                         (hour == 22 && minute >= 45) || (hour == 23 && minute < 45))
                {
                    // Slot 4 sedang berjalan (10:45-11:45 atau 22:45-23:45)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        currentSlots[2].Target +
                        (((hour == 10 || hour == 22) ? (minute - 45) : (minute + 15)) *
                        currentSlots[3].Target / 60.0)));
                }
                else if ((hour == 11 && minute >= 45) || (hour == 13 && minute < 30) ||
                         (hour == 23 && minute >= 45) || (hour == 1 && minute < 30))
                {
                    // Slot 5 sedang berjalan (11:45-13:30 atau 23:45-01:30)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        currentSlots[2].Target +
                        currentSlots[3].Target +
                        (((hour == 11 || hour == 23) ? (minute - 45) : (minute + 15)) *
                        currentSlots[4].Target / 105.0)));
                }
                else if (hour == 12 || hour == 0)
                {
                    // Istirahat (12:00-13:00 atau 00:00-01:00)
                    // Tidak ada progress, target tetap sama seperti di akhir slot 4
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        currentSlots[2].Target +
                        currentSlots[3].Target +
                        (15 * currentSlots[4].Target / 105.0)));
                }
                else if ((hour == 13 && minute >= 30) || (hour == 14 && minute < 30) ||
                         (hour == 1 && minute >= 30) || (hour == 2 && minute < 30))
                {
                    // Slot 6 sedang berjalan (13:30-14:30 atau 01:30-02:30)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        currentSlots[2].Target +
                        currentSlots[3].Target +
                        currentSlots[4].Target +
                        (((hour == 13 || hour == 1) ? (minute - 30) : (minute + 30)) *
                        currentSlots[5].Target / 60.0)));
                }
                else if ((hour == 14 && minute >= 30) || (hour == 15 && minute < 30) ||
                         (hour == 2 && minute >= 30) || (hour == 3 && minute < 30))
                {
                    // Slot 7 sedang berjalan (14:30-15:30 atau 02:30-03:30)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        currentSlots[2].Target +
                        currentSlots[3].Target +
                        currentSlots[4].Target +
                        currentSlots[5].Target +
                        (((hour == 14 || hour == 2) ? (minute - 30) : (minute + 30)) *
                        currentSlots[6].Target / 60.0)));
                }
                else if ((hour == 15 && minute >= 30) || (hour == 16 && minute < 30) ||
                         (hour == 3 && minute >= 30) || (hour == 4 && minute < 30))
                {
                    // Slot 8 sedang berjalan (15:30-16:30 atau 03:30-04:30)
                    targetPerSecond = Math.Round((currentSlots[0].Target +
                        currentSlots[1].Target +
                        currentSlots[2].Target +
                        currentSlots[3].Target +
                        currentSlots[4].Target +
                        currentSlots[5].Target +
                        currentSlots[6].Target +
                        (((hour == 15 || hour == 3) ? (minute - 30) : (minute + 30)) *
                        currentSlots[7].Target / 60.0)));
                }
                else if ((hour == 16 && minute >= 30) || (hour == 17) || (hour == 18) ||
                         (hour == 19 && minute < 30) || (hour == 4 && minute >= 30) ||
                         (hour == 5) || (hour == 6) || (hour == 7 && minute < 30))
                {
                    // Diluar jam shift, target = target shift
                    targetPerSecond = targetPerShift;
                }

                // Update UI
                if (txtTargetSecond != null)
                    txtTargetSecond.Text = ((int)targetPerSecond).ToString("N0");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CalculateTargetPerSecond: {ex.Message}");
            }
        }
        #endregion

        #region Slot Management Methods
        private void UpdateTimeSlotsForCurrentShift()
        {
            List<TimeSlot> currentSlots = currentShift == "1" ? shift1TimeSlots : shift2TimeSlots;

            // Update target per slot menggunakan logika PHP
            UpdateSlotTargetsPHPLogic(currentSlots, targetPerShift);

            // Update tampilan tabel
            UpdateTableDisplay(currentSlots);
        }

        private void UpdateSlotTargetsPHPLogic(List<TimeSlot> slots, int targetShift)
        {
            int slotCount = slots.Count;

            if (slotCount == 0) return;

            // 1. Hitung target dasar per sesi (sama rata)
            int baseTargetPerSession = targetShift / slotCount;

            // 2. Hitung sisa
            int remainder = targetShift % slotCount;

            // 3. Set target dasar untuk semua slot
            foreach (var slot in slots)
            {
                slot.Target = baseTargetPerSession;
            }

            // 4. Distribusi sisa sesuai logika PHP
            // Logika PHP: while (i < remainder / 2) {
            //   sessions[i].target += 1;
            //   sessions[2].target += 1;
            //   i = i + 1;
            // }

            int i = 0;
            while (i < remainder / 2)
            {
                // Tambah ke slot i (dimulai dari slot 0)
                if (i < slots.Count)
                {
                    slots[i].Target += 1;
                }

                // Tambah ke slot index 2 (slot ketiga)
                if (2 < slots.Count)
                {
                    slots[2].Target += 1;
                }

                i = i + 1;
            }

            // Log untuk debugging
            Console.WriteLine($"Distribusi Target: Total={targetShift}, Base={baseTargetPerSession}, Remainder={remainder}");
            for (int idx = 0; idx < slots.Count; idx++)
            {
                Console.WriteLine($"Slot {idx}: {slots[idx].TimeRange} - Target: {slots[idx].Target}");
            }
        }

        private void UpdateTableDisplay(List<TimeSlot> slots)
        {
            try
            {
                // Update setiap baris tabel
                var textBlocks = new[]
                {
            (txtTimeSlot1, txtTargetSlot1, txtActualSlot1, "Slot1"),
            (txtTimeSlot2, txtTargetSlot2, txtActualSlot2, "Slot2"),
            (txtTimeSlot3, txtTargetSlot3, txtActualSlot3, "Slot3"),
            (txtTimeSlot4, txtTargetSlot4, txtActualSlot4, "Slot4"),
            (txtTimeSlot5, txtTargetSlot5, txtActualSlot5, "Slot5"),
            (txtTimeSlot6, txtTargetSlot6, txtActualSlot6, "Slot6"),
            (txtTimeSlot7, txtTargetSlot7, txtActualSlot7, "Slot7"),
            (txtTimeSlot8, txtTargetSlot8, txtActualSlot8, "Slot8")
        };

                for (int i = 0; i < textBlocks.Length; i++)
                {
                    var (timeBlock, targetBlock, actualBlock, name) = textBlocks[i];

                    if (i < slots.Count)
                    {
                        var slot = slots[i];

                        if (timeBlock != null)
                            timeBlock.Text = slot.TimeRange;

                        if (targetBlock != null)
                            targetBlock.Text = slot.Target.ToString();

                        if (actualBlock != null)
                            actualBlock.Text = slot.Actual.ToString();
                    }
                    else
                    {
                        if (timeBlock != null)
                            timeBlock.Text = "--:-- - --:--";

                        if (targetBlock != null)
                            targetBlock.Text = "0";

                        if (actualBlock != null)
                            actualBlock.Text = "0";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateTableDisplay: {ex.Message}");
            }
        }
        #endregion

        #region Database Query Methods
        private int GetActualOutputFromDatabase()
        {
            try
            {
                string? noreg = App.LoggedInUserNoreg;

                if (string.IsNullOrEmpty(noreg))
                {
                    Debug.WriteLine($"Data tidak lengkap: noreg={noreg}");
                    Console.WriteLine("Data tidak lengkap untuk query actual output");
                    return 0;
                }

                // QUERY TANPA SHIFT
                string query = @"
            SELECT COUNT(id) AS jumlah 
            FROM trrphassyscan 
            WHERE CAST(tanggal AS DATE) = CAST(GETDATE() AS DATE) 
                AND operator = @operator 
                AND jamselesai IS NOT NULL";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@operator", noreg);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int actualOutput = reader.GetInt32(0);
                                Debug.WriteLine($"DEBUG: Actual output = {actualOutput}");
                                Console.WriteLine($"Actual output: {actualOutput}");
                                return actualOutput;
                            }
                            else
                            {
                                Debug.WriteLine("DEBUG: No data returned");
                                return 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActualOutputFromDatabase: {ex.Message}");
                Debug.WriteLine($"ERROR: {ex}");
                return 0;
            }
        }

        private void GetSlotDataFromDatabase()
        {
            try
            {
                string? noreg = App.LoggedInUserNoreg;
                string? shift = currentShift;
                string? mesin = App.MachineNumber;

                if (string.IsNullOrEmpty(noreg) || string.IsNullOrEmpty(mesin))
                {
                    Debug.WriteLine($"Data tidak lengkap untuk slot data");
                    return;
                }

                // Gunakan stored procedure untuk data per slot
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("countActualScanAssy", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@noreg", noreg);
                        cmd.Parameters.AddWithValue("@shift", shift);
                        cmd.Parameters.AddWithValue("@mesin", mesin);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                List<TimeSlot> currentSlots = currentShift == "1" ? shift1TimeSlots : shift2TimeSlots;

                                // Set actual untuk setiap slot
                                currentSlots[0].Actual = Convert.ToInt32(reader["jam1"]);
                                currentSlots[1].Actual = Convert.ToInt32(reader["jam2"]);
                                currentSlots[2].Actual = Convert.ToInt32(reader["jam3"]);
                                currentSlots[3].Actual = Convert.ToInt32(reader["jam4"]);
                                currentSlots[4].Actual = Convert.ToInt32(reader["jam5"]);
                                currentSlots[5].Actual = Convert.ToInt32(reader["jam6"]);
                                currentSlots[6].Actual = Convert.ToInt32(reader["jam7"]);
                                currentSlots[7].Actual = Convert.ToInt32(reader["jam8"]);

                                // Hitung total dari slot (sebagai double check)
                                int totalFromSlots = currentSlots.Sum(s => s.Actual);
                                Debug.WriteLine($"GetSlotDataFromDatabase: Total dari slot = {totalFromSlots}");

                                // Update tabel display
                                UpdateTableDisplay(currentSlots);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetSlotDataFromDatabase: {ex.Message}");
            }
        }
        #endregion

        #region Logout and Close
        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("=== LOGOUT PROCESS START ===");

                MessageBoxResult result = MessageBox.Show(
                    "Yakin ingin logout dari sistem?",
                    "Konfirmasi Logout",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                Console.WriteLine($"MessageBox result: {result}");

                if (result == MessageBoxResult.Yes)
                {
                    Console.WriteLine("User confirmed logout");

                    // Set flag bahwa ini adalah logout
                    isLoggingOut = true;

                    // **Reset data login**
                    ResetAppData();

                    Console.WriteLine("App data cleared");

                    // Stop timer
                    if (timer != null)
                    {
                        timer.Stop();
                        Console.WriteLine("Timer stopped");
                    }

                    // **SOLUSI: Langsung buka LoginWindow, jangan restart**
                    ShowLoginWindow();
                }
                else
                {
                    Console.WriteLine("Logout cancelled");
                }

                Console.WriteLine("=== LOGOUT PROCESS END ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in btnLogout_Click: {ex.Message}");
                MessageBox.Show($"Error saat logout: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Method untuk reset data
        private void ResetAppData()
        {
            App.OperatorNama = string.Empty;
            App.LoggedInUserNoreg = string.Empty;
            App.MachineNumber = string.Empty;
            App.PartNumber = string.Empty;
            App.SelectedShift = 0;
            App.TargetPerShift = 100;
            App.SupervisorNoreg = string.Empty;
            App.CurrentServerTime = null;
        }

        // Method untuk tampilkan LoginWindow
        private void ShowLoginWindow()
        {
            try
            {
                Console.WriteLine("Opening LoginWindow...");

                // Buat LoginWindow baru
                var loginWindow = new LoginWindow();

                // Tampilkan LoginWindow
                loginWindow.Show();

                // Tutup MainWindow ini
                this.Close();

                // Optional: Set sebagai MainWindow aplikasi
                Application.Current.MainWindow = loginWindow;

                Console.WriteLine("LoginWindow opened successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening LoginWindow: {ex.Message}");
                // Fallback: restart aplikasi
                RestartApplication();
            }
        }

        // Fallback: restart aplikasi kalau ShowLoginWindow gagal
        private void RestartApplication()
        {
            try
            {
                Console.WriteLine("Fallback: Restarting application...");
                string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                System.Diagnostics.Process.Start(appPath);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Restart failed: {ex.Message}");
                Application.Current.Shutdown();
            }
        }

        // Sisanya tetap sama...
        private void CloseApplication()
        {
            try
            {
                var result = MessageBox.Show("Yakin ingin keluar dari aplikasi?",
                    "Konfirmasi",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    Console.WriteLine("Application shutdown requested");
                    isLoggingOut = false;
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in CloseApplication: {ex.Message}");
                Application.Current.Shutdown();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Console.WriteLine("MainWindow OnClosed");

            if (timer != null)
            {
                timer.Stop();
            }

            base.OnClosed(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            Console.WriteLine($"MainWindow OnClosing - isLoggingOut: {isLoggingOut}");

            // Jika ini logout, skip konfirmasi exit
            if (isLoggingOut)
            {
                Console.WriteLine("Logout in progress - skipping exit confirmation");
                // Biarkan window ditutup
            }
            // Jika reloading atau exporting PDF, skip konfirmasi
            else if (!isReloading && !isExportingPdf)
            {
                Console.WriteLine("Showing exit confirmation...");

                var result = MessageBox.Show("Yakin ingin keluar dari aplikasi?",
                    "Konfirmasi",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    Console.WriteLine("Exit cancelled");
                    e.Cancel = true;
                    return;
                }
                else
                {
                    Console.WriteLine("Exit confirmed");
                    if (timer != null)
                    {
                        timer.Stop();
                    }
                }
            }

            base.OnClosing(e);
        }
        #endregion

        #region Scan Processing
        private void UpdateAfterScan()
        {
            try
            {
                // Refresh total actual dari stored procedure
                totalActual = GetActualOutputFromDatabase();

                // Refresh data per slot
                GetSlotDataFromDatabase();

                // Update metrics
                UpdateMetrics();

                // Update UI
                Dispatcher.Invoke(() =>
                {

                    // Update table
                    UpdateTimeSlotsForCurrentShift();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateAfterScan: {ex.Message}");
            }
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtBarcode.Text))
            {
                try
                {
                    string barcode = txtBarcode.Text.Trim();

                    // Parse barcode format "LOT.SERI"
                    string[] parts = barcode.Split('.');
                    if (parts.Length != 2 || parts[0].Length != 10 || parts[1].Length != 3)
                    {
                        // TAMBAH KE HISTORY DENGAN STATUS FORMAT ERROR
                        AddToScanHistory(barcode, "FORMAT_ERROR");

                        // HAPUS POPUP, TIDAK PERLU SHOW MESSAGE
                        // Cukup clear dan focus
                        txtBarcode.Text = "";
                        txtBarcode.Focus();
                        return;
                    }

                    string lot = parts[0];
                    string seriStr = parts[1];

                    // 1. CEK LOT NO DI DATABASE (cek_partno.php equivalent)
                    var lotInfo = CheckLotNoInDatabase(lot);

                    if (lotInfo != null && !string.IsNullOrEmpty(lotInfo.PartNameFG))
                    {
                        // 2. VALIDASI: Cek apakah part number sesuai dengan yang login
                        if (lotInfo.PartNameFG != App.PartNumber)
                        {
                            // TAMBAH KE HISTORY DENGAN STATUS PART MISMATCH
                            AddToScanHistory(barcode, "PART_MISMATCH");

                            txtBarcode.Text = "";
                            txtBarcode.Focus();
                            return;
                        }

                        // 3. Parse kategori dari ItemFGS (sesuai dengan JavaScript)
                        string kategori = "WH"; // default
                        if (!string.IsNullOrEmpty(lotInfo.ItemFGS))
                        {
                            string[] itemParts = lotInfo.ItemFGS.Split('.');
                            if (itemParts.Length >= 2)
                            {
                                kategori = itemParts[1];
                            }
                        }

                        // 4. PROSES SCAN (process_scan.php equivalent)
                        ScanRequest request = new ScanRequest
                        {
                            Input = barcode,
                            Shift = App.SelectedShift,
                            Supervisor = App.SupervisorNoreg,
                            Operator = App.LoggedInUserNoreg,
                            PartNo = lotInfo.PartNameFG,
                            Mesin = App.MachineNumber,
                            Lot = lot,
                            Seri = Convert.ToInt32(seriStr),
                            ItemId = lotInfo.ItemFGS,
                            Kategori = kategori
                        };

                        // 5. EXECUTE STORED PROCEDURE
                        ScanResult scanResult = ProcessScan(request);

                        // 6. HANDLE RESPONSE - Tambahkan pengecekan null di sini
                        if (scanResult == null || string.IsNullOrEmpty(scanResult.Result))
                        {
                            AddToScanHistory(barcode, "DATABASE_ERROR");
                        }
                        else
                        {
                            // Gunakan switch dengan nilai yang sudah pasti ada
                            switch (scanResult.Result.ToUpper())
                            {
                                case "INSERT":
                                    AddToScanHistory(barcode, "INSERT");
                                    UpdateAfterScan();
                                    break;

                                case "SKIP":
                                    AddToScanHistory(barcode, "SKIP");
                                    break;

                                case "ERROR":
                                    AddToScanHistory(barcode, scanResult.Message ?? "ERROR");
                                    break;

                                default:
                                    AddToScanHistory(barcode, "UNKNOWN_ERROR");
                                    break;
                            }
                        }
                    }
                    else
                    {
                        // LOT tidak ditemukan - TAMBAH KE HISTORY
                        AddToScanHistory(barcode, "LOT_NOT_FOUND");
                    }

                    // 7. CLEAR INPUT dan FOCUS
                    txtBarcode.Text = "";
                    txtBarcode.Focus();
                }
                catch (FormatException)
                {
                    // Format error - TAMBAH KE HISTORY
                    AddToScanHistory(txtBarcode.Text, "SERI_NOT_NUMERIC");
                    txtBarcode.Text = "";
                    txtBarcode.Focus();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in btnScan_Click: {ex.Message}");
                    // System error - TAMBAH KE HISTORY
                    AddToScanHistory(txtBarcode.Text, "SYSTEM_ERROR");
                    txtBarcode.Text = "";
                    txtBarcode.Focus();
                }
            }
        }

        public class ScanResult
        {
            public string? Result { get; set; } // "INSERT", "SKIP", "ERROR"
            public string? Message { get; set; }
        }

        public class ScanRequest
        {
            public string? Input { get; set; }          // LOT.SERI (full barcode)
            public int Shift { get; set; }             // 1, 2, 3
            public string? Supervisor { get; set; }     // Supervisor noreg
            public string? Operator { get; set; }       // Operator ID
            public string? PartNo { get; set; }         // Part number
            public string? Mesin { get; set; }          // Machine number
            public string? Lot { get; set; }            // LOT (10 digit)
            public int Seri { get; set; }              // Seri (3 digit)
            public string? ItemId { get; set; }         // Item ID
            public string? Kategori { get; set; }       // Category (WH, dll)
        }

        public ScanResult ProcessScan(ScanRequest request)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Prepare stored procedure call
                    using (SqlCommand cmd = new SqlCommand("inputScanAssy", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Add parameters - URUTAN HARUS SAMA DENGAN PHP!
                        cmd.Parameters.AddWithValue("@input", request.Input);
                        cmd.Parameters.AddWithValue("@shift", request.Shift);
                        cmd.Parameters.AddWithValue("@supervisor", request.Supervisor);
                        cmd.Parameters.AddWithValue("@operator", request.Operator);
                        cmd.Parameters.AddWithValue("@part_no", request.PartNo);
                        cmd.Parameters.AddWithValue("@mesin", request.Mesin);
                        cmd.Parameters.AddWithValue("@itemid", request.ItemId);
                        cmd.Parameters.AddWithValue("@lot", request.Lot);
                        cmd.Parameters.AddWithValue("@seri", request.Seri);
                        cmd.Parameters.AddWithValue("@kategori", request.Kategori);

                        // Execute stored procedure
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();

                                // Anggap SP return kolom "result" dan "message"
                                // Sesuaikan dengan struktur return SP Anda
                                return new ScanResult
                                {
                                    Result = reader["result"]?.ToString(),
                                    Message = reader["message"]?.ToString()
                                };
                            }
                            else
                            {
                                // Coba next result set (jika SP punya multiple result sets)
                                if (reader.NextResult() && reader.HasRows)
                                {
                                    reader.Read();
                                    return new ScanResult
                                    {
                                        Result = reader["result"]?.ToString(),
                                        Message = reader["message"]?.ToString()
                                    };
                                }
                                else
                                {
                                    return new ScanResult
                                    {
                                        Result = "ERROR",
                                        Message = "No response from stored procedure"
                                    };
                                }
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // Log error
                Console.WriteLine($"SQL Error: {sqlEx.Message}");
                return new ScanResult
                {
                    Result = "ERROR",
                    Message = $"Database error: {sqlEx.Message}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ProcessScan: {ex.Message}");
                return new ScanResult
                {
                    Result = "ERROR",
                    Message = $"System error: {ex.Message}"
                };
            }
        }

        // Class untuk menyimpan informasi lot
        public class LotInfo
        {
            public string? PartNameFG { get; set; }
            public string? ItemFGS { get; set; }
        }

        private LotInfo? CheckLotNoInDatabase(string lotNo)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // =======================
                    // 1. Cari di TRRPHMESIN
                    // =======================
                    string sql1 = "SELECT TOP 1 PartNameFG, ItemFGS FROM TRRPHMESIN WHERE LotNo = @lotNo";

                    using (SqlCommand cmd1 = new SqlCommand(sql1, conn))
                    {
                        cmd1.Parameters.AddWithValue("@lotNo", lotNo);

                        using (SqlDataReader reader1 = cmd1.ExecuteReader())
                        {
                            if (reader1.HasRows)
                            {
                                reader1.Read();
                                return new LotInfo
                                {
                                    PartNameFG = reader1["PartNameFG"]?.ToString(),
                                    ItemFGS = reader1["ItemFGS"]?.ToString()
                                };
                            }
                        }
                    }

                    // =======================
                    // 2. Cari di LOTPERPRODUCTIONORDER + tblitem
                    // =======================
                    // =======================
                    // testing local
                    // =======================
                    string sql2 = @"SELECT TOP 1
                        LPPO.ITEMID AS ItemFGS,
                        INV.TIPARTNUMBER AS PartNameFG
                    FROM LOTPERPRODUCTIONORDER AS LPPO
                    JOIN tblitem AS INV
                        ON LPPO.ITEMID = INV.TIID
                    WHERE LPPO.INVENTBATCHID = @lotNo";
                    // =======================
                    // Production Server
                    // =======================
                    //string sql2 = @"SELECT TOP 1
                    //            LPPO.ITEMID AS ItemFGS,
                    //            INV.TIPARTNUMBER AS PartNameFG
                    //        FROM DMS..LOTPERPRODUCTIONORDER AS LPPO
                    //        JOIN DBINVENTORY2.dbo.tblitem AS INV
                    //            ON LPPO.ITEMID = INV.TIID COLLATE SQL_Latin1_General_CP1_CI_AS
                    //        WHERE LPPO.INVENTBATCHID = @lotNo";
                    using (SqlCommand cmd2 = new SqlCommand(sql2, conn))
                    {
                        cmd2.Parameters.AddWithValue("@lotNo", lotNo);

                        using (SqlDataReader reader2 = cmd2.ExecuteReader())
                        {
                            if (reader2.HasRows)
                            {
                                reader2.Read();
                                return new LotInfo
                                {
                                    PartNameFG = reader2["PartNameFG"]?.ToString(),
                                    ItemFGS = reader2["ItemFGS"]?.ToString()
                                };
                            }
                        }
                    }

                    // =======================
                    // 3. Jika tidak ditemukan di kedua tabel
                    // =======================
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CheckLotNoInDatabase: {ex.Message}");
                throw; // Re-throw exception agar bisa ditangani di caller
            }
        }

        private void txtBarcode_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Auto-scan jika barcode panjang (misal minimal 10 karakter)
            if (txtBarcode.Text.Length >= 14)
            {
                // Trigger scan otomatis
                btnScan_Click(sender, e);

                // Auto clear setelah delay kecil
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtBarcode.Text = "";
                }), DispatcherPriority.Background);
            }
        }

        // Optional: Tambahkan event handler untuk tombol Enter
        private void txtBarcode_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Trigger scan ketika Enter ditekan
                btnScan_Click(sender, e);
                e.Handled = true;
            }
        }
        #endregion

        #region Update Target
        private void btnUpdateTarget_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Console.WriteLine("btnUpdateTarget_Click started");

                // Tampilkan dialog
                var dialog = new UpdateTargetDialog();
                dialog.Owner = this;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.TargetValue = targetPerShift;

                if (dialog.ShowDialog() == true)
                {
                    Console.WriteLine($"Dialog confirmed, new target: {dialog.TargetValue}");

                    // SIMPLIFIED VERSION: Langsung proses tanpa async dulu
                    ProcessTargetUpdate(dialog.TargetValue);
                }
            }
            catch (Exception ex)
            {
                // CATCH SEMUA EXCEPTION DI LEVEL INI
                Console.WriteLine($"CRITICAL ERROR in btnUpdateTarget_Click: {ex}");

                // Log ke file untuk debugging
                LogErrorToFile("btnUpdateTarget_Click", ex);

                // Tampilkan error yang user-friendly
                MessageBox.Show($"Terjadi kesalahan saat mengupdate target:\n\n{ex.Message}\n\nSilahkan coba lagi atau hubungi IT.",
                    "Error Update Target",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ProcessTargetUpdate(int newTarget)
        {
            try
            {
                Console.WriteLine($"ProcessTargetUpdate started with value: {newTarget}");

                // 1. VALIDASI DATA USER
                if (string.IsNullOrEmpty(App.LoggedInUserNoreg) ||
                    string.IsNullOrEmpty(App.PartNumber) ||
                    string.IsNullOrEmpty(App.MachineNumber))
                {
                    MessageBox.Show("Data user tidak lengkap. Silahkan login ulang.",
                        "Data Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Console.WriteLine($"User data valid: Noreg={App.LoggedInUserNoreg}, Part={App.PartNumber}, Machine={App.MachineNumber}");

                // 2. SIMPAN KE DATABASE
                bool saveSuccess = SaveTargetToDatabase(
                    App.LoggedInUserNoreg,
                    App.PartNumber,
                    App.MachineNumber,
                    newTarget);

                if (saveSuccess)
                {
                    Console.WriteLine("Database save successful");

                    // 3. UPDATE LOCAL VARIABLES
                    targetPerShift = newTarget;
                    App.TargetPerShift = newTarget;

                    // 4. UPDATE UI - Pastikan di UI Thread
                    Dispatcher.Invoke(() =>
                    {
                        if (txtTargetShift != null)
                            txtTargetShift.Text = targetPerShift.ToString("N0");

                        CalculateTargetPerSecond();
                        UpdateTimeSlotsForCurrentShift();

                        MessageBox.Show($"Target berhasil diupdate menjadi {targetPerShift} unit",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                else
                {
                    MessageBox.Show("Gagal menyimpan target ke database.",
                        "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ProcessTargetUpdate: {ex}");
                LogErrorToFile("ProcessTargetUpdate", ex);

                throw; // Re-throw agar ditangkap di caller
            }
        }

        private bool SaveTargetToDatabase(string noreg, string partNumber, string machineNumber, int newTarget)
        {
            Console.WriteLine($"SaveTargetToDatabase: noreg={noreg}, part={partNumber}, machine={machineNumber}, target={newTarget}");

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    Console.WriteLine("Database connection opened");

                    // SEDERHANAKAN: Hanya pakai INSERT atau UPDATE sederhana
                    string query = @"
                    UPDATE target_data 
                    SET target = @target
                    WHERE noreg = @noreg AND partno = @partno AND mesin = @mesin";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@noreg", noreg);
                        cmd.Parameters.AddWithValue("@partno", partNumber);
                        cmd.Parameters.AddWithValue("@mesin", machineNumber);
                        cmd.Parameters.AddWithValue("@target", newTarget);
                        cmd.Parameters.AddWithValue("@shift", App.SelectedShift.ToString());
                        cmd.Parameters.AddWithValue("@supervisor", App.SupervisorNoreg ?? "SPV000");

                        int rowsAffected = cmd.ExecuteNonQuery();
                        Console.WriteLine($"Rows affected: {rowsAffected}");

                        return rowsAffected > 0;
                    }
                }
                catch (SqlException sqlEx)
                {
                    Console.WriteLine($"SQL ERROR: {sqlEx.Message}");
                    Console.WriteLine($"SQL Error Number: {sqlEx.Number}");
                    LogErrorToFile("SaveTargetToDatabase - SQL", sqlEx);
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"General ERROR: {ex.Message}");
                    LogErrorToFile("SaveTargetToDatabase", ex);
                    return false;
                }
            }
        }

        // Helper method untuk logging error
        private void LogErrorToFile(string methodName, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {methodName}:\n" +
                                   $"Message: {ex.Message}\n" +
                                   $"Stack Trace: {ex.StackTrace}\n" +
                                   $"Inner Exception: {ex.InnerException?.Message}\n" +
                                   new string('-', 80) + "\n";

                File.AppendAllText(logPath, logMessage);
            }
            catch
            {
                // Jika gagal log, ignore saja
            }
        }
        #endregion

        #region history Scan
        private void AddToScanHistory(string barcode, string status)
        {
            try
            {
                // Jika dipanggil dari thread non-UI, pakai Dispatcher
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => AddToScanHistory(barcode, status));
                    return;
                }

                // Create new history item
                var historyItem = new ScanHistoryItem(barcode, status);

                // Get or initialize history list
                var historyList = scanHistoryList.ItemsSource as ObservableCollection<ScanHistoryItem>;
                if (historyList == null)
                {
                    historyList = new ObservableCollection<ScanHistoryItem>();
                    scanHistoryList.ItemsSource = historyList;
                }

                // Add to history (NO LIMIT)
                historyList.Insert(0, historyItem);

                // Auto-scroll ke item terbaru
                if (scanHistoryList.Items.Count > 0)
                {
                    scanHistoryList.ScrollIntoView(scanHistoryList.Items[0]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding to scan history: {ex.Message}");
            }
        }
        #endregion

        #region Edit Part Number
        private void btnEditPart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // DEBUG: Tampilkan data yang akan digunakan
                Console.WriteLine($"Edit Part Clicked - Current Data:");
                Console.WriteLine($"  Noreg: {App.LoggedInUserNoreg}");
                Console.WriteLine($"  Shift: {App.SelectedShift}");
                Console.WriteLine($"  Supervisor: {App.SupervisorNoreg}");
                Console.WriteLine($"  Current Part: {App.PartNumber}");
                Console.WriteLine($"  Machine: {App.MachineNumber}");
                Console.WriteLine($"  Target: {App.TargetPerShift}");

                // Membuka popup window untuk edit part number
                var editWindow = new EditPartNumberWindow();
                editWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                if (editWindow.ShowDialog() == true)
                {
                    string newPartNumber = editWindow.SelectedPartNumber ?? "";

                    // Validasi
                    if (string.IsNullOrEmpty(newPartNumber) || newPartNumber == "-- Select Part Number --")
                    {
                        // GANTI: Pakai MessageBox default
                        MessageBox.Show("Please select a valid part number!",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Jika part number tidak berubah, tidak perlu update
                    if (newPartNumber == App.PartNumber)
                    {
                        // GANTI: Pakai MessageBox default
                        MessageBox.Show("Part number is already set to this value.",
                            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Konfirmasi update
                    // GANTI: Pakai MessageBox default
                    var confirmResult = MessageBox.Show(
                        $"Change part number from '{App.PartNumber}' to '{newPartNumber}'?",
                        "Confirm Change",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmResult != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    // Simpan ke database
                    bool saveSuccess = SavePartNumberToDatabase(newPartNumber);

                    if (saveSuccess)
                    {
                        // Update UI terlebih dahulu
                        if (txtPartNumber != null)
                            txtPartNumber.Text = newPartNumber;

                        // Tampilkan pesan sukses
                        // GANTI: Pakai MessageBox default
                        MessageBox.Show(
                            $"✅ Part number berhasil diupdate!\n" +
                            $"Part Number: {newPartNumber}\n",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                        // RE-LOAD Halaman MainWindow
                        ReloadMainWindow();
                    }
                    else
                    {
                        // GANTI: Pakai MessageBox default
                        MessageBox.Show("Failed to save part number to database.",
                            "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                // GANTI: Pakai MessageBox default
                MessageBox.Show($"Error editing part number: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Method untuk menyimpan ke database
        private bool SavePartNumberToDatabase(string newPartNumber)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. UPDATE App.PartNumber untuk UI
                    App.PartNumber = newPartNumber;

                    // 2. INSERT KE target_data (SESUAI FORMAT PHP)
                    string insertQuery = @"
        INSERT INTO target_data 
            (noreg, shift, supervisor, partno, mesin, target, created_at) 
        VALUES 
            (@noreg, @shift, @supervisor, @partno, @mesin, @target, GETDATE())";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        // Parameter sesuai format PHP: noreg, shift, supervisor, partno, mesin, target
                        insertCmd.Parameters.AddWithValue("@noreg", App.LoggedInUserNoreg ?? "000000");
                        insertCmd.Parameters.AddWithValue("@shift", App.SelectedShift.ToString() ?? "1");
                        insertCmd.Parameters.AddWithValue("@supervisor", App.SupervisorNoreg ?? "SPV000");
                        insertCmd.Parameters.AddWithValue("@partno", newPartNumber);
                        insertCmd.Parameters.AddWithValue("@mesin", App.MachineNumber ?? "MCH-001");
                        insertCmd.Parameters.AddWithValue("@target", App.TargetPerShift);

                        int rowsInserted = insertCmd.ExecuteNonQuery();

                        if (rowsInserted > 0)
                        {
                            Console.WriteLine($"Data inserted into target_data: " +
                                $"noreg={App.LoggedInUserNoreg}, " +
                                $"shift={App.SelectedShift}, " +
                                $"supervisor={App.SupervisorNoreg}, " +
                                $"partno={newPartNumber}, " +
                                $"mesin={App.MachineNumber}, " +
                                $"target={App.TargetPerShift}");

                            return true;
                        }
                        else
                        {
                            Console.WriteLine("Failed to insert into target_data");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Tetap pakai MessageBox default di sini (sudah benar)
                MessageBox.Show($"DATABASE ERROR:\n{ex.Message}\n\nDetail: {ex.StackTrace}",
                                "Insert Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Method untuk reload/refresh MainWindow
        private void ReloadMainWindow()
        {
            try
            {
                isReloading = true;
                Console.WriteLine("Reloading MainWindow...");

                // 1. Ambil data terbaru yang sudah diupdate dari proses Save tadi
                var backupNoreg = App.LoggedInUserNoreg;
                var backupMch = App.MachineNumber;
                var backupPart = App.PartNumber; // Ini sudah part baru
                var backupShift = App.SelectedShift;
                var backupTarget = App.TargetPerShift;
                var backupSpv = App.SupervisorNoreg;

                // 2. Stop Timer window lama agar tidak bentrok
                if (timer != null)
                {
                    timer.Stop();
                    timer.Tick -= Timer_Tick; // WAJIB kalau ada
                }

                // 3. PENTING: Pastikan variabel Global App sudah terisi data TERBARU 
                // SEBELUM membuat instance MainWindow baru
                App.LoggedInUserNoreg = backupNoreg;
                App.MachineNumber = backupMch;
                App.PartNumber = backupPart;
                App.SelectedShift = backupShift;
                App.TargetPerShift = backupTarget;
                App.SupervisorNoreg = backupSpv;

                // 2. BUAT WINDOW BARU
                var newMainWindow = new MainWindow
                {
                    WindowState = WindowState.Maximized
                };

                // 3. SHOW WINDOW BARU
                newMainWindow.Show();

                // 4. SET SEBAGAI MAINWINDOW
                Application.Current.MainWindow = newMainWindow;

                // 5. SEMBUNYIKAN WINDOW LAMA (BUKAN CLOSE)
                this.Hide();
                this.IsEnabled = false;
            }
            catch (Exception ex)
            {
                isReloading = false;
                MessageBox.Show($"Fatal Error during reload: {ex.Message}");
            }
        }
        #endregion

        #region Export PDF Report
        private async void btnDownloadReport_Click(object sender, RoutedEventArgs e)
        {
            // Cek apakah sedang proses, biar nggak double klik
            if (isExportingPdf) return;

            try
            {
                isExportingPdf = true;

                // 1. Pastikan dialog jalan di UI Thread
                Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"Laporan_Produksi_Assy_{DateTime.Now:yyyyMMdd_HHmm}",
                    DefaultExt = ".pdf",
                    Filter = "PDF Documents (.pdf)|*.pdf"
                };

                if (saveFileDialog.ShowDialog() != true)
                {
                    isExportingPdf = false;
                    return;
                }

                string targetPath = saveFileDialog.FileName;

                // 2. Validasi data sebelum masuk ke background thread
                string noreg = App.LoggedInUserNoreg ?? string.Empty;
                string mesin = App.MachineNumber ?? string.Empty;
                string shift = currentShift;

                if (string.IsNullOrEmpty(noreg) || string.IsNullOrEmpty(mesin))
                {
                    MessageBox.Show("Data operator atau mesin tidak lengkap!",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    isExportingPdf = false;
                    return;
                }

                MessageBox.Show("Sedang membuat laporan PDF...", "Memproses",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                string? errorDetails = null;

                // 3. Jalankan kalkulasi berat di background
                await Task.Run(() =>
                {
                    try
                    {
                        GenerateProductionReport(targetPath, noreg, mesin, shift);
                    }
                    catch (Exception ex)
                    {
                        errorDetails = ex.Message + "\n" + ex.StackTrace;
                    }
                });

                if (!string.IsNullOrEmpty(errorDetails))
                {
                    MessageBox.Show($"Terjadi kesalahan:\n{errorDetails}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Laporan berhasil disimpan!", "Selesai",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF Export Error: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}",
                    "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isExportingPdf = false;
            }
        }

        private List<ProductionReportData> GetProductionReportData(string noreg, string mesin, string shift)
        {
            List<ProductionReportData> dataList = new List<ProductionReportData>();

            try
            {
                Console.WriteLine($"Getting report data for noreg: {noreg}, mesin: {mesin}, shift: {shift}");

                if (string.IsNullOrEmpty(noreg) || string.IsNullOrEmpty(mesin))
                {
                    Console.WriteLine("ERROR: noreg or mesin is null or empty!");
                    return dataList;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    Console.WriteLine("Database connection opened successfully");

                    string query = @"SELECT * FROM TRRPHASSYScan 
                   WHERE CAST(tanggal AS DATE) = CAST(GETDATE() AS DATE) 
                   AND kodemesin = @mesin 
                   AND operator = @operator 
                   AND shift = @shift";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@mesin", mesin);
                        cmd.Parameters.AddWithValue("@operator", noreg);
                        cmd.Parameters.AddWithValue("@shift", shift);

                        Console.WriteLine($"Executing query: {query}");
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            Console.WriteLine("Reading data from database...");
                            int no = 1;
                            while (reader.Read())
                            {
                                ProductionReportData data = new ProductionReportData
                                {
                                    No = no++,
                                    LotNumber = reader["Lotno"]?.ToString() ?? "-",
                                    TglUpdate = reader["TglUpdate"] as DateTime?,
                                    Tanggal = reader["tanggal"] as DateTime?,
                                    Shift = reader["shift"]?.ToString() ?? "-",
                                    ItemId = reader["itemid"]?.ToString() ?? "-",
                                    JamUpdate = reader["jamupdate"]?.ToString() ?? "-",
                                    Operator = reader["operator"]?.ToString() ?? "-",
                                    KodeMesin = reader["kodemesin"]?.ToString() ?? "-",
                                    Lot = reader["lot"]?.ToString() ?? "-",
                                    NoSeri = reader["noseri"]?.ToString() ?? "-",
                                    State = reader["state"]?.ToString() ?? "-",
                                    KeteranganErr = reader["keteranganerr"]?.ToString() ?? "-",
                                    Supervisor = reader["supervisor"]?.ToString() ?? "-",
                                    Kategori = reader["kategori"]?.ToString() ?? "-",
                                    PartName = reader["PartName"]?.ToString() ?? "-",
                                    JamMulai = reader["jammulai"]?.ToString() ?? "-",
                                    JamSelesai = reader["jamselesai"]?.ToString() ?? "-",
                                    Lama = reader["lama"]?.ToString() ?? "-"
                                };

                                dataList.Add(data);
                            }
                            Console.WriteLine($"Read {dataList.Count} records from database");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting report data: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw;
            }

            return dataList;
        }

        private void GenerateProductionReport(string filePath, string noreg, string mesin, string shift)
        {
            try
            {
                FontInitializer.Initialize();

                // Kirim parameternya ke fungsi ambil data
                var reportData = GetProductionReportData(noreg, mesin, shift);

                // 2. Create document
                Document document = CreatePdfDocument(reportData);

                // 3. Render dengan suppress warning untuk obsolete constructor
#pragma warning disable CS0618 // Type or member is obsolete
                PdfDocumentRenderer pdfRenderer = new PdfDocumentRenderer(true);
#pragma warning restore CS0618 // Type or member is obsolete
                pdfRenderer.Document = document;
                pdfRenderer.RenderDocument();

                // 4. Save ke path yang dipilih user tadi
                pdfRenderer.PdfDocument.Save(filePath);
            }
            catch (Exception)
            {
                throw; // Lempar ke pemanggil (UI thread) untuk ditampilkan
            }
        }
        private Document CreatePdfDocument(List<ProductionReportData> data)
        {
            Document document = new Document();
            document.Info.Title = "Laporan Produksi Actual Assy";
            document.Info.Author = "PT INDOPRIMA GEMILANG";

            // DEFINE STYLE seperti CSS di PHP
            MigraDoc.DocumentObjectModel.Style style = document.Styles["Normal"]!;
            style.Font.Name = "Arial";
            style.Font.Size = 10; // Sama seperti PHP: font-size: 12px

            // Style untuk header
            style = document.Styles["Heading1"]!;
            style.Font.Size = 14;
            style.Font.Bold = true;
            style.ParagraphFormat.Alignment = ParagraphAlignment.Center;

            // Style untuk tabel
            style = document.Styles.AddStyle("Table", "Normal");
            style.Font.Size = 9; // Lebih kecil untuk tabel
            style.ParagraphFormat.Alignment = ParagraphAlignment.Center;

            // Style untuk header tabel
            style = document.Styles.AddStyle("TableHeader", "Table");
            style.Font.Bold = true;
            style.ParagraphFormat.Alignment = ParagraphAlignment.Center;

            // Add section
            Section section = document.AddSection();
            section.PageSetup.Orientation = MigraDoc.DocumentObjectModel.Orientation.Portrait;

            // MARGIN seperti di PHP
            section.PageSetup.TopMargin = "1cm";
            section.PageSetup.BottomMargin = "1cm";
            section.PageSetup.LeftMargin = "0.5cm";
            section.PageSetup.RightMargin = "0.5cm";

            AddHeader(section);
            AddInfoTable(section);
            AddDataTable(section, data);
            AddSignatureTable(section);

            return document;
        }
        private void AddHeader(Section section)
        {
            // Title 1
            Paragraph title1 = section.AddParagraph();
            title1.Format.Alignment = ParagraphAlignment.Center;
            title1.Format.Font.Size = 14;
            title1.Format.Font.Bold = true;
            title1.AddText("PT INDOPRIMA GEMILANG");

            section.AddParagraph().AddLineBreak();

            // Title 2
            Paragraph title2 = section.AddParagraph();
            title2.Format.Alignment = ParagraphAlignment.Center;
            title2.Format.Font.Size = 14;
            title2.Format.Font.Bold = true;
            title2.AddText("Laporan Produksi Actual Assy");

            section.AddParagraph().AddLineBreak();
        }

        private void AddInfoTable(Section section)
        {
            Table table = section.AddTable();
            table.Style = "Table";

            // **PERHITUNGAN LEBAR PENUH**
            // A4 Landscape: 29.7cm - margin kiri 1cm - margin kanan 1cm = 27.7cm
            double totalWidth = 20.0; // cm


            // **8 KOLOM DENGAN LEBAR PROPORSIONAL**
            double columnWidth = totalWidth / 8; // 3.4625cm per kolom

            // **COBA TAMBAH SEDIKIT UNTUK FULL WIDTH:**
            // Jika masih ada space, tambah 5-10%
            //columnWidth = columnWidth * 1.02; // Tambah 1%

            for (int i = 0; i < 8; i++)
            {
                table.AddColumn($"{columnWidth:N2}cm");
            }

            // **PASTIKAN TABEL MENGISI LEBAR PENUH**
            table.LeftPadding = 0;
            table.RightPadding = 0;

            // Border tipis
            table.Borders.Width = 0.25;
            table.Borders.Color = MigraDoc.DocumentObjectModel.Colors.Black;

            // **SET BORDER VISIBILITY**
            table.Borders.Visible = true;
            table.Borders.Left.Visible = true;
            table.Borders.Right.Visible = true;
            table.Borders.Top.Visible = true;
            table.Borders.Bottom.Visible = true;

            // Row 1: Header
            Row headerRow = table.AddRow();
            headerRow.Format.Font.Bold = true;
            //headerRow.Shading.Color = MigraDoc.DocumentObjectModel.Colors.LightGray;
            headerRow.Height = "0.8cm";
            headerRow.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;

            // Set colspan 2
            headerRow.Cells[0].AddParagraph("Tanggal");
            headerRow.Cells[0].MergeRight = 1;

            headerRow.Cells[2].AddParagraph("Operator");
            headerRow.Cells[2].MergeRight = 1;

            headerRow.Cells[4].AddParagraph("Mesin");
            headerRow.Cells[4].MergeRight = 1;

            headerRow.Cells[6].AddParagraph("Shift");
            headerRow.Cells[6].MergeRight = 1;

            // Set alignment dan border untuk semua cell
            for (int i = 0; i < 8; i++)
            {
                var cell = headerRow.Cells[i];
                cell.Format.Alignment = ParagraphAlignment.Center;
                cell.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;

                // **SET BORDER UNTUK SEMUA SISI**
                cell.Borders.Width = 0.25;
                cell.Borders.Left.Width = 0.25;
                cell.Borders.Right.Width = 0.25;
                cell.Borders.Top.Width = 0.25;
                cell.Borders.Bottom.Width = 0.25;
                cell.Borders.Color = MigraDoc.DocumentObjectModel.Colors.Black;
            }

            // Row 2: Data
            Row dataRow = table.AddRow();
            dataRow.Height = "0.8cm";
            dataRow.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;

            // Set colspan 2
            dataRow.Cells[0].AddParagraph(DateTime.Now.ToString("dd-MM-yyyy"));
            dataRow.Cells[0].MergeRight = 1;

            dataRow.Cells[2].AddParagraph(App.LoggedInUserNoreg ?? "-");
            dataRow.Cells[2].MergeRight = 1;

            dataRow.Cells[4].AddParagraph(App.MachineNumber ?? "-");
            dataRow.Cells[4].MergeRight = 1;

            dataRow.Cells[6].AddParagraph(currentShift);
            dataRow.Cells[6].MergeRight = 1;

            // Set alignment dan border untuk semua cell
            for (int i = 0; i < 8; i++)
            {
                var cell = dataRow.Cells[i];
                cell.Format.Alignment = ParagraphAlignment.Center;
                cell.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;

                // **SET BORDER UNTUK SEMUA SISI**
                cell.Borders.Width = 0.25;
                cell.Borders.Left.Width = 0.25;
                cell.Borders.Right.Width = 0.25;
                cell.Borders.Top.Width = 0.25;
                cell.Borders.Bottom.Width = 0.25;
                cell.Borders.Color = MigraDoc.DocumentObjectModel.Colors.Black;
            }

            // Tambah jarak
            section.AddParagraph();
            section.AddParagraph();
        }

        private void AddDataTable(Section section, List<ProductionReportData> data)
        {
            // Total Lebar: 27.7 cm (Disesuaikan agar teks tidak numpuk)
            double[] columnWidths = new double[]
            {
        0.5,   // No
        1.7,   // Lot Number
        1.6,   // Update
        1.3,   // Produksi
        0.5,   // Shift
        1.9,   // Item ID
        1.0,   // Jam Update
        1.1,   // Operator
        0.9,   // Kode Mesin
        1.3,   // Lot
        0.7,   // No Seri
        0.7,   // Status
        0.8,   // Error
        1.3,   // Supervisor
        1.1,   // Kategori
        1.6,   // Nama Part
        0.9,   // Jam Mulai
        0.9,   // Jam Selesai
        0.8    // Durasi
            };

            Table table = section.AddTable();
            table.Style = "Table";
            table.Borders.Width = 0.25;
            table.Borders.Color = MigraDoc.DocumentObjectModel.Colors.Black;

            foreach (var width in columnWidths)
            {
                table.AddColumn(Unit.FromCentimeter(width));
            }

            // --- HEADER ROW ---
            Row headerRow = table.AddRow();
            headerRow.HeadingFormat = true;
            headerRow.Format.Font.Bold = true;
            headerRow.Format.Font.Size = 6; // Font Header dikecilkan dikit lagi biar aman
            //headerRow.Shading.Color = MigraDoc.DocumentObjectModel.Colors.LightGray;
            headerRow.Height = "0.6cm";
            headerRow.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;

            string[] headers = {
        "No", "Lot Number", "Update", "Produksi", "Shift", "Item ID", "Jam Update",
        "Operator", "Kode Mesin", "Lot", "No Seri", "Status", "Error", "Supervisor",
        "Kategori", "Nama Part", "Jam Mulai", "Jam Selesai", "Durasi"
    };

            for (int i = 0; i < headers.Length; i++)
            {
                var p = headerRow.Cells[i].AddParagraph(headers[i]);
                p.Format.Alignment = ParagraphAlignment.Center;
            }

            // --- DATA ROWS ---
            foreach (var item in data)
            {
                Row row = table.AddRow();
                row.Height = "0.5cm";
                row.Format.Font.Size = 5.5; // Kunci utama: Font Data harus cukup kecil (6.5 - 7)
                row.VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;

                row.Cells[0].AddParagraph(item.No.ToString());
                row.Cells[1].AddParagraph(item.LotNumber ?? "-");
                row.Cells[2].AddParagraph(item.TglUpdate?.ToString("dd-MM-yyyy HH:mm") ?? "-");
                row.Cells[3].AddParagraph(item.Tanggal?.ToString("dd-MM-yyyy") ?? "-");
                row.Cells[4].AddParagraph(item.Shift ?? "-");
                row.Cells[5].AddParagraph(item.ItemId ?? "-");
                row.Cells[6].AddParagraph(item.JamUpdate ?? "-");
                row.Cells[7].AddParagraph(item.Operator ?? "-");
                row.Cells[8].AddParagraph(item.KodeMesin ?? "-");
                row.Cells[9].AddParagraph(item.Lot ?? "-");
                row.Cells[10].AddParagraph(item.NoSeri ?? "-");
                row.Cells[11].AddParagraph(item.State ?? "-");
                row.Cells[12].AddParagraph(item.KeteranganErr ?? "-");
                row.Cells[13].AddParagraph(item.Supervisor ?? "-");
                row.Cells[14].AddParagraph(item.Kategori ?? "-");
                row.Cells[15].AddParagraph(item.PartName ?? "-");
                row.Cells[16].AddParagraph(item.JamMulai ?? "-");
                row.Cells[17].AddParagraph(item.JamSelesai ?? "-");
                row.Cells[18].AddParagraph(item.Lama ?? "-");

                for (int i = 0; i < 19; i++)
                {
                    row.Cells[i].Format.Alignment = ParagraphAlignment.Center;
                    // Tips: Matikan LeftIndent/RightIndent di sel agar teks tidak terdorong
                    row.Cells[i].Format.LeftIndent = 0;
                    row.Cells[i].Format.RightIndent = 0;
                }
            }
        }
        private void AddSignatureTable(Section section)
        {
            section.AddParagraph().Format.SpaceBefore = "0.5cm";

            Table table = section.AddTable();
            table.Borders.Width = 0.5;
            table.Borders.Color = MigraDoc.DocumentObjectModel.Colors.Black;

            // --- PENYESUAIAN LEBAR KOLOM (Total 6.6cm) ---
            // Opt & SPV Produksi dikasih 2.4cm, SPV QC diperkecil ke 1.8cm
            double col1 = 2.4;
            double col2 = 2.4;
            double col3 = 1.8;
            double totalWidth = col1 + col2 + col3; // 6.6cm

            table.Rows.Alignment = RowAlignment.Left;
            // Sisa ruang: 20cm - 6.6cm = 13.4cm
            table.Rows.LeftIndent = Unit.FromCentimeter(13.4);

            table.AddColumn(Unit.FromCentimeter(col1));
            table.AddColumn(Unit.FromCentimeter(col2));
            table.AddColumn(Unit.FromCentimeter(col3));

            // --- ROW 1: HEADER (DIBUAT & DIPERIKSA) ---
            Row row1 = table.AddRow();
            row1.Height = "0.6cm"; // Sedikit lebih tinggi biar kelihatan center-nya
            row1.Format.Font.Size = 7;
            row1.Format.Font.Bold = true;

            // Cell DIBUAT
            row1.Cells[0].AddParagraph("DIBUAT").Format.Alignment = ParagraphAlignment.Center;
            row1.Cells[0].VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center; // KUNCI TENGAH

            // Cell DIPERIKSA
            row1.Cells[1].MergeRight = 1;
            row1.Cells[1].AddParagraph("DIPERIKSA").Format.Alignment = ParagraphAlignment.Center;
            row1.Cells[1].VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center; // KUNCI TENGAH

            // --- ROW 2: TEMPAT TANDA TANGAN ---
            Row row2 = table.AddRow();
            row2.Height = "1.2cm";

            // --- ROW 3: FOOTER (JABATAN) ---
            Row row3 = table.AddRow();
            row3.Height = "0.6cm";
            row3.Format.Font.Size = 6.5;

            string[] labels = { "Opt. PRODUKSI", "SPV. PRODUKSI", "SPV. QC" };
            for (int i = 0; i < 3; i++)
            {
                var p = row3.Cells[i].AddParagraph(labels[i]);
                p.Format.Alignment = ParagraphAlignment.Center;
                p.Format.Font.Bold = true;
                row3.Cells[i].VerticalAlignment = MigraDoc.DocumentObjectModel.Tables.VerticalAlignment.Center;
            }
        }
        #endregion
    }

    #region Helper Classes
    // Class helper untuk slot waktu
    public class TimeSlot
    {
        public string TimeRange { get; set; }
        public int Target { get; set; }
        public int Actual { get; set; }

        public TimeSlot(string timeRange)
        {
            TimeRange = timeRange;
            Target = 0;
            Actual = 0;
        }
    }

    public class ProductionReportData
    {
        public int No { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? TglUpdate { get; set; }
        public DateTime? Tanggal { get; set; }
        public string? Shift { get; set; }
        public string? ItemId { get; set; }
        public string? JamUpdate { get; set; }
        public string? Operator { get; set; }
        public string? KodeMesin { get; set; }
        public string? Lot { get; set; }
        public string? NoSeri { get; set; }
        public string? State { get; set; }
        public string? KeteranganErr { get; set; }
        public string? Supervisor { get; set; }
        public string? Kategori { get; set; }
        public string? PartName { get; set; }
        public string? JamMulai { get; set; }
        public string? JamSelesai { get; set; }
        public string? Lama { get; set; }
    }

    public class ScanHistoryItem
    {
        public string DisplayText { get; set; }
        public string StatusIcon { get; set; }
        public string StatusBackground { get; set; }

        public ScanHistoryItem(string barcode, string status)
        {
            StatusIcon = GetStatusIcon(status);
            StatusBackground = GetStatusBackground(status);
            DisplayText = FormatDisplayText(barcode, status);
        }

        private string GetStatusIcon(string status)
        {
            return status.ToUpper() switch
            {
                "INSERT" => "✓",
                "SUCCESS" => "✓",
                "TERPROSES" => "✓",
                "SKIP" => "↻",
                "DUPLICATE" => "↻",
                "SUDAH DISCAN" => "↻",
                _ => "✗"
            };
        }

        private string GetStatusBackground(string status)
        {
            return status.ToUpper() switch
            {
                "INSERT" => "#27AE60",    // Hijau
                "SUCCESS" => "#27AE60",   // Hijau
                "TERPROSES" => "#27AE60", // Hijau
                "SKIP" => "#F39C12",      // Orange
                "DUPLICATE" => "#F39C12", // Orange
                "SUDAH DISCAN" => "#F39C12", // Orange
                _ => "#E74C3C"            // Merah untuk error
            };
        }

        private string FormatDisplayText(string barcode, string status)
        {
            return status.ToUpper() switch
            {
                "INSERT" => $"{barcode} TERSCAN",
                "SUCCESS" => $"{barcode} TERSCAN",
                "TERPROSES" => $"{barcode} TERSCAN",
                "SKIP" => $"{barcode} Sudah TERSCAN",
                "DUPLICATE" => $"{barcode} Sudah TERSCAN",
                "SUDAH DISCAN" => $"{barcode} Sudah TERSCAN",
                _ => $"{barcode}"
            };
        }
    }
    #endregion
}