using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace Assy
{
    public partial class EditPartNumberWindow : Window
    {
        #region Fields & Properties
        private string connectionString = @"Server=YOUR_SERVER_NAME;Database=assy;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=true;";
        public string? SelectedPartNumber { get; private set; }
        #endregion

        #region Constructor & Initialization
        public EditPartNumberWindow()
        {
            InitializeComponent();
            Loaded += EditPartNumberWindow_Loaded;
            PreviewKeyDown += EditPartNumberWindow_PreviewKeyDown;
            MouseDown += Window_MouseDown;
        }
        #endregion

        #region Event Handlers
        private void EditPartNumberWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                btnCancel_Click(sender, e);
                e.Handled = true;
            }
        }

        private void EditPartNumberWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Update current part display
            txtCurrentPart.Text = App.PartNumber ?? "Not Set";

            LoadPartNumbersFromDatabase();

            // Select current part number if exists
            if (!string.IsNullOrEmpty(App.PartNumber))
            {
                // Tunggu sebentar agar Items sudah terload
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (ComboBoxItem item in cbPartNo.Items.OfType<ComboBoxItem>())
                    {
                        if (item.Content?.ToString() == App.PartNumber)
                        {
                            cbPartNo.SelectedItem = item;
                            break;
                        }
                    }

                    // Set focus to combobox
                    cbPartNo.Focus();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                cbPartNo.Focus();
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbPartNo.SelectedItem is ComboBoxItem selectedItem)
            {
                string newPartNumber = selectedItem.Content?.ToString() ?? "";

                if (string.IsNullOrEmpty(newPartNumber) ||
                    newPartNumber == "-- Select Part Number --" ||
                    selectedItem.IsEnabled == false)
                {
                    MessageBox.Show("Please select a valid part number!",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Confirm if changing to different part
                if (newPartNumber != App.PartNumber)
                {
                    var result = MessageBox.Show(
                        $"Change part number from '{App.PartNumber}' to '{newPartNumber}'?",
                        "Confirm Change",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                SelectedPartNumber = newPartNumber;
                DialogResult = true;
                Close();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        #endregion

        #region Database Operations
        private void LoadPartNumbersFromDatabase()
        {
            try
            {
                cbPartNo.Items.Clear();

                // Add placeholder as first item
                var placeholderItem = new ComboBoxItem
                {
                    Content = "-- Select Part Number --",
                    IsEnabled = false,
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
                cbPartNo.Items.Add(placeholderItem);

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT DISTINCT 
                            COALESCE(PartNameFG, '') AS PartNumber
                        FROM TRRPHMESIN 
                        WHERE PartNameFG IS NOT NULL 
                          AND PartNameFG != '' 
                          AND PartNameFG != 'Coba-JanganDipakai'
                        ORDER BY PartNumber";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                // Tambahkan ?. dan ?? ""
                                string partNumber = reader["PartNumber"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(partNumber))
                                {
                                    cbPartNo.Items.Add(new ComboBoxItem
                                    {
                                        Content = partNumber,
                                        Foreground = System.Windows.Media.Brushes.Black
                                    });
                                }
                            }
                        }
                    }
                }

                // Select placeholder by default
                cbPartNo.SelectedIndex = 0;

                // If no items loaded, add fallback
                if (cbPartNo.Items.Count == 1) // Only placeholder
                {
                    LoadFallbackPartNumbers();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading part numbers: {ex.Message}");
                LoadFallbackPartNumbers();

                // Tampilkan pesan error sederhana
                MessageBox.Show($"Database error: {ex.Message}\nUsing fallback data.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        #endregion

        #region Fallback & Helper Methods
        private void LoadFallbackPartNumbers()
        {
            var defaultParts = new List<string>
            {
                "PN-123456",
                "PN-789012",
                "PN-345678",
                "PN-901234"
            };

            foreach (var part in defaultParts)
            {
                cbPartNo.Items.Add(new ComboBoxItem
                {
                    Content = part,
                    Foreground = System.Windows.Media.Brushes.Black
                });
            }
        }
        #endregion
    }
}