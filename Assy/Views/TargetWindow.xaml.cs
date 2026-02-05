using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Data.SqlClient;

namespace Assy.Views
{
    public partial class TargetWindow : Window
    {
        #region Fields & Properties
        private string? operatorName;
        private string? supervisorName;
        private string? machine;
        private string? partNo;
        private int selectedShift = 1; // ✅ INTEGER
        private int targetPerShift = 0;

        private string connectionString = @"Server=YOUR_SERVER_NAME;Database=assy;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=true;";
        #endregion

        #region Constructor & Initialization
        public TargetWindow()
        {
            InitializeComponent();
            LoadUserData();
            InitializeShiftButtons();
        }
        #endregion

        #region Data Loading Methods
        private void LoadUserData()
        {
            try
            {
                this.operatorName = App.OperatorNama;
                this.supervisorName = App.SupervisorNama;
                this.machine = App.MachineNumber;
                this.partNo = App.PartNumber;

                txtOperator.Text = $"{App.LoggedInUserNoreg} - {operatorName}";
                txtSupervisor.Text = $"{App.SupervisorNoreg} - {supervisorName}";
                txtMachine.Text = machine;
                txtPartNo.Text = partNo;

                // ✅ Tidak perlu panggil SelectShift di sini karena sudah di InitializeShiftButtons
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading user data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Shift Management
        private void InitializeShiftButtons()
        {
            // Default ke Shift 1
            SelectShift(1); // ✅ PARAMETER INT
        }

        private void SelectShift(int shiftValue)
        {
            selectedShift = shiftValue;

            // Update UI
            if (shiftValue == 1)
            {
                btnShift1.Background = new SolidColorBrush(Color.FromRgb(30, 64, 175));
                if (btnShift1.Content is StackPanel stack1 && stack1.Children.Count > 0)
                {
                    if (stack1.Children[0] is TextBlock tb) tb.Foreground = Brushes.White;
                }

                btnShift2.Background = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                if (btnShift2.Content is StackPanel stack2 && stack2.Children.Count > 0)
                {
                    if (stack2.Children[0] is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                }
            }
            else if (shiftValue == 2)
            {
                btnShift2.Background = new SolidColorBrush(Color.FromRgb(30, 64, 175));
                if (btnShift2.Content is StackPanel stack2 && stack2.Children.Count > 0)
                {
                    if (stack2.Children[0] is TextBlock tb) tb.Foreground = Brushes.White;
                }

                btnShift1.Background = new SolidColorBrush(Color.FromRgb(229, 231, 235));
                if (btnShift1.Content is StackPanel stack1 && stack1.Children.Count > 0)
                {
                    if (stack1.Children[0] is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                }
            }

            CheckFormComplete();
        }
        #endregion

        #region Event Handlers
        // ✅ GANTI PARAMETER KE INT
        private void ShiftButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.Tag != null)
            {
                if (int.TryParse(button.Tag.ToString(), out int shiftValue))
                {
                    SelectShift(shiftValue);
                }
            }
        }

        // ✅ GANTI KE OVERLOAD INT
        private void Shift1_Click(object sender, RoutedEventArgs e)
        {
            SelectShift(1);
        }

        private void Shift2_Click(object sender, RoutedEventArgs e)
        {
            SelectShift(2);
        }

        private void txtTarget_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void txtTarget_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtTarget.Text, out int target))
            {
                targetPerShift = target;
            }
            else
            {
                targetPerShift = 0;
            }

            CheckFormComplete();
        }

        private void txtTarget_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckFormComplete();
            }
        }

        private void txtTarget_LostFocus(object sender, RoutedEventArgs e)
        {
            if (targetInputBorder != null)
            {
                targetInputBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                targetInputBorder.Effect = null;
            }
        }
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (targetPerShift <= 0)
            {
                MessageBox.Show("Target harus lebih dari 0!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (selectedShift <= 0) // ✅ INT COMPARISON
            {
                MessageBox.Show("Pilih shift terlebih dahulu!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ✅ SIMPAN KE APP SEBAGAI INT
            App.TargetPerShift = targetPerShift;
            App.SelectedShift = selectedShift; // ✅ INTEGER
            App.ProductionDate = DateTime.Now;

            // ✅ SIMPAN KE DATABASE
            SaveTargetToDatabase();

            var mainWindow = new MainWindow();
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            mainWindow.Show();

            this.Close();
        }
        #endregion

        #region Validation & Form Logic
        // ✅ PERBAIKI CHECK FORM
        private void CheckFormComplete()
        {
            // selectedShift adalah INT, jadi cek > 0
            btnStart.IsEnabled = targetPerShift > 0 && selectedShift > 0;
        }
        #endregion

        #region Database Operations
        private void SaveTargetToDatabase()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
INSERT INTO target_data 
(noreg, mesin, shift, target, created_at, supervisor, partno)
VALUES 
(@noreg, @mesin, @shift, @target, GETDATE(), @supervisor, @partno)";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@noreg", App.LoggedInUserNoreg);
                        cmd.Parameters.AddWithValue("@mesin", App.MachineNumber);

                        // ⚠️ PENTING: Cek tipe data kolom shift di database!
                        // Jika kolom shift VARCHAR, gunakan:
                        // cmd.Parameters.AddWithValue("@shift", selectedShift.ToString());
                        // Jika kolom shift INT, gunakan:
                        cmd.Parameters.AddWithValue("@shift", selectedShift);

                        cmd.Parameters.AddWithValue("@target", targetPerShift);
                        cmd.Parameters.AddWithValue("@supervisor", App.SupervisorNoreg);
                        cmd.Parameters.AddWithValue("@partno", App.PartNumber);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            Console.WriteLine($"Target berhasil disimpan: Shift {selectedShift}, Target {targetPerShift}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error SaveTargetToDatabase: {ex.Message}");
                MessageBox.Show($"Gagal menyimpan target ke database: {ex.Message}",
                               "Database Error",
                               MessageBoxButton.OK,
                               MessageBoxImage.Error);
                throw;
            }
        }
        #endregion
    }
}