using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Assy
{
    public partial class TargetWindow : Window
    {
        private string supervisor;
        private string machine;
        private string partNo;
        private string selectedShift = "";
        private int targetPerShift = 0;

        public TargetWindow(string supervisor, string machine, string partNo)
        {
            InitializeComponent();

            this.supervisor = supervisor;
            this.machine = machine;
            this.partNo = partNo;

            // Tampilkan data user
            txtSupervisor.Text = supervisor;
            txtMachine.Text = machine;
            txtPartNo.Text = partNo;

            // Set default shift ke Shift 1
            SelectShift("Shift 1");
        }

        private void SelectShift(string shift)
        {
            selectedShift = shift;

            // Reset warna kedua border
            borderShift1.Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)); // #E0E0E0
            borderShift2.Background = new SolidColorBrush(Color.FromRgb(224, 224, 224));

            // Set warna border yang dipilih
            if (shift == "Shift 1")
            {
                borderShift1.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // #0078D4

                // Update teks Shift 1
                ((TextBlock)((StackPanel)borderShift1.Child).Children[0]).Foreground = Brushes.White;
                ((TextBlock)((StackPanel)borderShift1.Child).Children[1]).Foreground = Brushes.LightGray;

                // Update teks Shift 2
                ((TextBlock)((StackPanel)borderShift2.Child).Children[0]).Foreground = Brushes.Gray;
                ((TextBlock)((StackPanel)borderShift2.Child).Children[1]).Foreground = Brushes.DarkGray;
            }
            else if (shift == "Shift 2")
            {
                borderShift2.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // #0078D4

                // Update teks Shift 2
                ((TextBlock)((StackPanel)borderShift2.Child).Children[0]).Foreground = Brushes.White;
                ((TextBlock)((StackPanel)borderShift2.Child).Children[1]).Foreground = Brushes.LightGray;

                // Update teks Shift 1
                ((TextBlock)((StackPanel)borderShift1.Child).Children[0]).Foreground = Brushes.Gray;
                ((TextBlock)((StackPanel)borderShift1.Child).Children[1]).Foreground = Brushes.DarkGray;
            }

            // Check jika target sudah diisi untuk enable button
            CheckFormComplete();
        }

        private void Shift1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectShift("Shift 1");
        }

        private void Shift2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectShift("Shift 2");
        }

        private void txtTarget_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Hanya allow angka
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

        private void CheckFormComplete()
        {
            // Enable button hanya jika target > 0 dan shift sudah dipilih
            btnStart.IsEnabled = targetPerShift > 0 && !string.IsNullOrEmpty(selectedShift);
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (targetPerShift <= 0)
            {
                MessageBox.Show("Target harus lebih dari 0!", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrEmpty(selectedShift))
            {
                MessageBox.Show("Pilih shift terlebih dahulu!", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Simpan data ke App properties
            App.TargetPerShift = targetPerShift;
            App.SelectedShift = selectedShift;
            App.ProductionDate = DateTime.Now;

            // Konfirmasi
            var result = MessageBox.Show(
                $"Konfirmasi Target Produksi:\n\n" +
                $"🎯 Target/Shift: {targetPerShift:N0} units\n" +
                $"⏰ Shift: {selectedShift}\n" +
                $"📅 Tanggal: {DateTime.Now:dd/MM/yyyy}\n\n" +
                $"Apakah data sudah benar?",
                "Konfirmasi Target",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Buka MainWindow
                var mainWindow = new MainWindow();
                mainWindow.WindowState = WindowState.Maximized;
                mainWindow.WindowStyle = WindowStyle.None;
                mainWindow.Show();

                // Tutup TargetWindow
                this.Close();
            }
        }

        // Handle Enter key pada textbox target
        private void txtTarget_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckFormComplete();
            }
        }
    }
}