using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Assy.Views
{
    public partial class LoginWindow : Window
    {
        #region Fields & Properties
        private DispatcherTimer? timer;
        private string connectionString = @"Server=YOUR_SERVER_NAME;Database=assy;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=true;";
        #endregion

        #region Constructor & Initialization
        public LoginWindow()
        {
            InitializeComponent();

            // Set tanggal hari ini
            dpTanggal.SelectedDate = DateTime.Now;

            // Load data dari database
            LoadPartNumbersFromDatabase();

            // Set focus ke input No Register
            Loaded += (s, e) => txtNoRegister.Focus();

            // Setup timer untuk system time
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        #endregion

        #region Database Operations
        private void LoadPartNumbersFromDatabase()
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT DISTINCT PartNameFG AS tipartnumber 
                        FROM TRRPHMESIN 
                        WHERE PartNameFG IS NOT NULL 
                          AND PartNameFG != '' 
                          AND PartNameFG != 'Coba-JanganDipakai'
                        ORDER BY PartNameFG";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            cbPartNo.Items.Clear();

                            while (reader.Read())
                            {
                                string partNumber = reader["tipartnumber"]?.ToString() ?? "";
                                cbPartNo.Items.Add(new ComboBoxItem { Content = partNumber });
                            }

                            // Select first item jika ada
                            if (cbPartNo.Items.Count > 0)
                            {
                                cbPartNo.SelectedIndex = 0;
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                // Fallback data jika database error
                LoadFallbackPartNumbers();
                ShowCustomMessageBox($"Database connection error: {sqlEx.Message}\nUsing fallback data.",
                    "Warning", MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                LoadFallbackPartNumbers();
                ShowCustomMessageBox($"Error loading part numbers: {ex.Message}\nUsing fallback data.",
                    "Warning", MessageBoxImage.Warning);
            }
        }

        private (bool HasTarget, int Shift, int Target, DateTime CreatedAt)
        CheckExistingTarget(string noreg, string mesin)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"
DECLARE @date DATETIME = GETDATE();
DECLARE @noreg NVARCHAR(10) = @pnoreg;
DECLARE @mesin NVARCHAR(10) = @pmesin;

IF @date > CAST(CAST(@date AS DATE) AS NVARCHAR)+' 07:30'
   AND @date <= CAST(CAST(@date AS DATE) AS NVARCHAR)+' 19:30'
BEGIN
    SELECT TOP 1 shift, target, created_at
    FROM target_data
    WHERE created_at > CAST(CAST(@date AS DATE) AS NVARCHAR)+' 07:30'
      AND created_at <= CAST(CAST(@date AS DATE) AS NVARCHAR)+' 19:30'
      AND noreg = @noreg AND mesin = @mesin
    ORDER BY created_at DESC
END
ELSE
BEGIN
    SELECT TOP 1 shift, target, created_at
    FROM target_data
    WHERE (
        created_at > CAST(CAST(DATEADD(DAY,-1,@date) AS DATE) AS NVARCHAR)+' 19:30'
        OR
        created_at <= CAST(CAST(@date AS DATE) AS NVARCHAR)+' 07:30'
    )
    AND noreg = @noreg AND mesin = @mesin
    ORDER BY created_at DESC
END
";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@pnoreg", noreg);
                    cmd.Parameters.AddWithValue("@pmesin", mesin);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // ✅ Tambahkan ?. dan ?? agar tidak dianggap null
                            string shiftStr = reader["shift"]?.ToString() ?? "1";

                            if (!int.TryParse(shiftStr, out int shiftInt))
                            {
                                shiftInt = 1;
                            }

                            return (
                                true,
                                shiftInt,
                                reader["target"] != DBNull.Value ? Convert.ToInt32(reader["target"]) : 0,
                                reader["created_at"] != DBNull.Value ? Convert.ToDateTime(reader["created_at"]) : DateTime.MinValue
                            );
                        }
                    }
                }
            }

            return (false, 0, 0, DateTime.MinValue);
        }

        private string ValidateUserInDatabase(string noreg, string userType)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            CASE 
                                WHEN tgl_resign IS NOT NULL THEN 'RESIGN'
                                WHEN divisi != 'Plant 1' THEN 'DIVISI_BUKAN_PLANT1'
                                ELSE 'VALID'
                            END AS status,
                            nama,
                            divisi,
                            dept
                        FROM dbo.DB_PEGAWAI 
                        WHERE noreg = @noreg";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@noreg", noreg);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Gunakan ?. dan ?? "" untuk keamanan
                                string status = reader["status"]?.ToString() ?? "";

                                if (status == "RESIGN")
                                {
                                    return "Pegawai sudah resign";
                                }
                                else if (status == "DIVISI_BUKAN_PLANT1")
                                {
                                    // Sama di sini, jaga-jaga kalau kolom divisi di DB itu NULL
                                    string divisi = reader["divisi"]?.ToString() ?? "Tidak Diketahui";
                                    return $"Divisi bukan 'Plant 1' (Divisi saat ini: {divisi})";
                                }
                                else
                                {
                                    return "VALID";
                                }
                            }
                            else
                            {
                                return $"No Register tidak Sesuai!";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error database: {ex.Message}";
            }
        }

        private UserInfo GetUserInfoFromDatabase(string noreg)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT nama, divisi, dept
                        FROM dbo.DB_PEGAWAI 
                        WHERE noreg = @noreg";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@noreg", noreg);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new UserInfo
                                {
                                    // Jika null di DB, ganti jadi string kosong
                                    Nama = reader["nama"]?.ToString() ?? "",
                                    Divisi = reader["divisi"]?.ToString() ?? "",
                                    Dept = reader["dept"]?.ToString() ?? ""
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return default jika error
            }

            return new UserInfo
            {
                Nama = "Nama tidak ditemukan",
                Divisi = "-",
                Dept = "-"
            };
        }
        private string GetItemFGSByPartName(string partName)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"
SELECT TOP 1 ItemFGS
FROM TRRPHMESIN
WHERE PartNameFG = @partName";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@partName", partName);

                    object result = cmd.ExecuteScalar();
                    // Tambahkan ?? "" di ujung
                    return result?.ToString() ?? "";
                }
            }
        }

        private void LoadFallbackPartNumbers()
        {
            // Fallback data jika database tidak bisa diakses
            cbPartNo.Items.Clear();
            string[] fallbackParts = { "PART-001", "PART-002", "PART-003", "PART-004", "PART-005" };

            foreach (var part in fallbackParts)
            {
                cbPartNo.Items.Add(new ComboBoxItem { Content = part });
            }

            if (cbPartNo.Items.Count > 0)
            {
                cbPartNo.SelectedIndex = 0;
            }
        }
        #endregion

        #region Timer & UI Updates
        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update system time di sidebar
            if (txtSidebarTime != null)
                txtSidebarTime.Text = DateTime.Now.ToString("dd MMM yyyy HH:mm:ss");
        }
        #endregion

        #region Event Handlers
        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            // Validasi input
            if (string.IsNullOrWhiteSpace(txtNoRegister.Text))
            {
                ShowCustomMessageBox("No Register (Operator) harus diisi!", "Validasi Error", MessageBoxImage.Warning);
                txtNoRegister.Focus();
                return;
            }

            if (cbMesin.SelectedItem == null)
            {
                ShowCustomMessageBox("Mesin harus dipilih!", "Validasi Error", MessageBoxImage.Warning);
                cbMesin.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtSupervisor.Text))
            {
                ShowCustomMessageBox("No Register Supervisor harus diisi!", "Validasi Error", MessageBoxImage.Warning);
                txtSupervisor.Focus();
                return;
            }

            if (cbPartNo.SelectedItem == null)
            {
                ShowCustomMessageBox("Part No harus dipilih!", "Validasi Error", MessageBoxImage.Warning);
                cbPartNo.Focus();
                return;
            }

            // Validasi No Register OPERATOR di database
            string operatorValidation = ValidateUserInDatabase(txtNoRegister.Text, "Operator");
            if (operatorValidation != "VALID")
            {
                ShowCustomMessageBox(
                    $"No Register Operator '{txtNoRegister.Text}' tidak valid:\n\n{operatorValidation}",
                    "Validasi Error", MessageBoxImage.Error);
                txtNoRegister.Focus();
                txtNoRegister.SelectAll();
                return;
            }

            // Validasi No Register SUPERVISOR di database
            string supervisorValidation = ValidateUserInDatabase(txtSupervisor.Text, "Supervisor");
            if (supervisorValidation != "VALID")
            {
                ShowCustomMessageBox(
                    $"No Register Supervisor '{txtSupervisor.Text}' tidak valid:\n\n{supervisorValidation}",
                    "Validasi Error", MessageBoxImage.Error);
                txtSupervisor.Focus();
                txtSupervisor.SelectAll();
                return;
            }

            // Ambil data nama dan informasi lengkap dari database
            var operatorInfo = GetUserInfoFromDatabase(txtNoRegister.Text);
            var supervisorInfo = GetUserInfoFromDatabase(txtSupervisor.Text);

            // Simpan data ke App properties
            App.SupervisorNoreg = txtSupervisor.Text;
            App.SupervisorNama = supervisorInfo.Nama;
            App.SupervisorDivisi = supervisorInfo.Divisi;
            App.SupervisorDept = supervisorInfo.Dept;

            App.LoggedInUserNoreg = txtNoRegister.Text;
            App.OperatorNama = operatorInfo.Nama;
            App.OperatorDivisi = operatorInfo.Divisi;
            App.OperatorDept = operatorInfo.Dept;

            // Ambil Machine Number dengan cadangan string kosong
            App.MachineNumber = (cbMesin.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            // Ambil Part Number dengan cadangan string kosong
            App.PartNumber = (cbPartNo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            // Sekarang memanggil fungsi ini tidak akan "merah" lagi
            App.ItemFGS = GetItemFGSByPartName(App.PartNumber);

            if (string.IsNullOrEmpty(App.ItemFGS))
            {
                ShowCustomMessageBox(
                    $"Part No '{App.PartNumber}' tidak ditemukan di master mesin (ItemFGS).",
                    "Validasi Error",
                    MessageBoxImage.Error
                );
                return;
            }
            App.ProductionDate = dpTanggal.SelectedDate ?? DateTime.Now;

            // Tampilkan konfirmasi custom yang bagus
            bool confirmed = ShowCustomConfirmationDialog(
                "KONFIRMASI LOGIN ASSEMBLY SYSTEM",
                operatorInfo, supervisorInfo,
                App.MachineNumber, App.PartNumber, App.ProductionDate);

            if (confirmed)
            {
                var shiftResult = CheckExistingTarget(
                    App.LoggedInUserNoreg,
                    App.MachineNumber
                );

                if (shiftResult.HasTarget)
                {
                    // === SAMA DENGAN dashboard/shift.php ===
                    App.SelectedShift = shiftResult.Shift;
                    App.TargetPerShift = shiftResult.Target;
                    App.ProductionDate = shiftResult.CreatedAt;

                    var mainWindow = new MainWindow();
                    mainWindow.WindowState = WindowState.Maximized;
                    mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    mainWindow.Show();
                }
                else
                {
                    // === SAMA DENGAN dashboard/set_target.php ===
                    var targetWindow = new TargetWindow();
                    targetWindow.WindowState = WindowState.Maximized;
                    targetWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                    targetWindow.Show();
                }

                this.Close();
            }

        }
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = ShowCustomMessageBoxConfirm1("Yakin ingin keluar dari aplikasi?", "Konfirmasi Keluar");
            if (confirmed)
            {
                Application.Current.Shutdown();
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            // Tampilkan konfirmasi reset
            bool confirmed = ShowCustomMessageBoxConfirm(
                "Reset semua inputan kecuali tanggal?\n\n" +
                "✅ No Register akan dikosongkan\n" +
                "✅ Mesin akan dikembalikan ke default\n" +
                "✅ Supervisor akan dikosongkan\n" +
                "✅ Part No akan dikembalikan ke default\n\n" +
                "Tanggal akan tetap sama dengan hari ini.",
                "Konfirmasi Reset");

            if (confirmed)
            {
                ResetFormInputs();
            }
        }
        #endregion

        #region UI Helper Methods
        private bool ShowCustomConfirmationDialog(string title, UserInfo operatorInfo, UserInfo supervisorInfo,
            string machineNumber, string partNumber, DateTime productionDate)
        {
            // Buat window konfirmasi custom
            Window confirmWindow = new Window
            {
                Title = title,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Width = 700,
                Height = 650,
                ResizeMode = ResizeMode.NoResize
            };

            // Konten utama
            Grid mainGrid = new Grid();

            // Shadow effect
            Border shadowBorder = new Border
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent,
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 0,
                    BlurRadius = 30,
                    Color = Colors.Black,
                    Opacity = 0.3
                }
            };

            // Konten container
            Border container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };

            // StackPanel untuk konten
            StackPanel contentPanel = new StackPanel
            {
                Margin = new Thickness(30)
            };

            // Header
            Border headerBorder = new Border
            {
                Background = new LinearGradientBrush(Colors.DodgerBlue, Colors.RoyalBlue, 45),
                CornerRadius = new CornerRadius(8, 8, 0, 0),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20)
            };

            StackPanel headerPanel = new StackPanel();
            TextBlock headerTitle = new TextBlock
            {
                Text = "CONFIRM LOGIN",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock headerSubtitle = new TextBlock
            {
                Text = "Assembly System - Production Login",
                FontSize = 14,
                Foreground = Brushes.WhiteSmoke,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0)
            };
            headerPanel.Children.Add(headerTitle);
            headerPanel.Children.Add(headerSubtitle);
            headerBorder.Child = headerPanel;

            // ========== PERBAIKAN DI SINI ==========
            // Info Grid - perlu 9 row (5 data + 4 separator)
            Grid infoGrid = new Grid();
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Tambah 9 row
            for (int i = 0; i < 9; i++)
            {
                infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            infoGrid.Margin = new Thickness(0, 0, 0, 30);

            // Production Date - row 0
            AddInfoRowImproved(infoGrid, 0, "📅", "PRODUCTION DATE", productionDate.ToString("dd/MM/yyyy"), true);

            // Operator Info - row 2
            AddInfoRowImproved(infoGrid, 2, "👨‍🔧", "OPERATOR",
                $"{txtNoRegister.Text} - {operatorInfo.Nama}\nDivisi: {operatorInfo.Divisi} | Dept: {operatorInfo.Dept}", true);

            // Machine Info - row 4
            AddInfoRowImproved(infoGrid, 4, "⚙️", "MACHINE", machineNumber, true);

            // Supervisor Info - row 6
            AddInfoRowImproved(infoGrid, 6, "👨‍💼", "SUPERVISOR",
                $"{txtSupervisor.Text} - {supervisorInfo.Nama}\nDivisi: {supervisorInfo.Divisi} | Dept: {supervisorInfo.Dept}", true);

            // Part Number - row 8
            AddInfoRowImproved(infoGrid, 8, "📦", "PART NUMBER", partNumber, false); // false karena tidak perlu separator di akhir

            // Tambah separator di row 1, 3, 5, 7
            AddSeparatorRow(infoGrid, 1);
            AddSeparatorRow(infoGrid, 3);
            AddSeparatorRow(infoGrid, 5);
            AddSeparatorRow(infoGrid, 7);
            // ========== END PERBAIKAN ==========

            // Buttons
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };

            Button btnConfirm = new Button
            {
                Content = "✅ CONFIRM LOGIN",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Background = new LinearGradientBrush(Colors.LimeGreen, Colors.ForestGreen, 90),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Width = 180,
                Height = 45,
                Margin = new Thickness(0, 0, 15, 0),
                Cursor = Cursors.Hand,
                Tag = true
            };
            btnConfirm.Click += (s, ev) => { confirmWindow.DialogResult = true; };

            Button btnCancel = new Button
            {
                Content = "❌ CANCEL",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Background = new LinearGradientBrush(Colors.OrangeRed, Colors.DarkRed, 90),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Width = 120,
                Height = 45,
                Cursor = Cursors.Hand,
                Tag = false
            };
            btnCancel.Click += (s, ev) => { confirmWindow.DialogResult = false; };

            ControlTemplate buttonTemplate = new ControlTemplate(typeof(Button));

            // Buat Factory untuk Border
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "border";
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(15, 10, 15, 10));

            // Buat Factory untuk ContentPresenter
            FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Tambah ContentPresenter ke Border
            borderFactory.AppendChild(contentPresenter);

            // Set VisualTree
            buttonTemplate.VisualTree = borderFactory;

            // Trigger untuk hover
            Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                new LinearGradientBrush(Colors.DarkOrange, Colors.DarkRed, 90), "border"));
            buttonTemplate.Triggers.Add(hoverTrigger);

            // Trigger untuk pressed
            Trigger pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                new LinearGradientBrush(Colors.DarkSlateGray, Colors.Black, 90), "border"));
            buttonTemplate.Triggers.Add(pressedTrigger);

            // Terapkan template ke tombol
            btnConfirm.Template = buttonTemplate;
            btnCancel.Template = buttonTemplate;

            buttonPanel.Children.Add(btnConfirm);
            buttonPanel.Children.Add(btnCancel);

            // Assembly semua komponen
            contentPanel.Children.Add(headerBorder);
            contentPanel.Children.Add(infoGrid);
            contentPanel.Children.Add(buttonPanel);

            container.Child = contentPanel;
            shadowBorder.Child = container;
            mainGrid.Children.Add(shadowBorder);
            confirmWindow.Content = mainGrid;

            // Tampilkan dialog
            bool? result = confirmWindow.ShowDialog();
            return result == true;
        }

        private void AddInfoRowImproved(Grid grid, int row, string icon, string label, string value, bool addMarginBottom)
        {
            // Icon
            TextBlock iconText = new TextBlock
            {
                Text = icon,
                FontSize = 20,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetRow(iconText, row);
            Grid.SetColumn(iconText, 0);

            // Label
            StackPanel labelPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 15, 0)
            };
            TextBlock labelText = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Color.FromRgb(30, 144, 255).ToBrush(),
                Margin = new Thickness(0, 0, 0, 2)
            };
            TextBlock valueText = new TextBlock
            {
                Text = value,
                FontSize = 14,
                Foreground = System.Windows.Media.Color.FromRgb(105, 105, 105).ToBrush(),
                TextWrapping = TextWrapping.Wrap,
                Margin = addMarginBottom ? new Thickness(0, 0, 0, 10) : new Thickness(0) // Margin hanya untuk yang atas
            };
            labelPanel.Children.Add(labelText);
            labelPanel.Children.Add(valueText);
            Grid.SetRow(labelPanel, row);
            Grid.SetColumn(labelPanel, 1);

            grid.Children.Add(iconText);
            grid.Children.Add(labelPanel);
        }

        private void AddSeparatorRow(Grid grid, int row)
        {
            Border separator = new Border
            {
                Height = 1,
                Background = Brushes.LightGray,
                Margin = new Thickness(0, 5, 0, 5)
            };
            Grid.SetRow(separator, row);
            Grid.SetColumn(separator, 0);
            Grid.SetColumnSpan(separator, 2);

            grid.Children.Add(separator);
        }

        private void ShowCustomMessageBox(string message, string title, MessageBoxImage icon)
        {
            Window messageBox = new Window
            {
                Title = title,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Width = 400,
                Height = 300,
                ResizeMode = ResizeMode.NoResize
            };

            Grid grid = new Grid();
            Border shadowBorder = new Border
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent,
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 0,
                    BlurRadius = 20,
                    Color = Colors.Black,
                    Opacity = 0.2
                }
            };

            Border container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.LightGray
            };

            StackPanel contentPanel = new StackPanel
            {
                Margin = new Thickness(25)
            };

            // Icon berdasarkan tipe pesan
            string iconText = icon == MessageBoxImage.Error ? "❌" :
                            icon == MessageBoxImage.Warning ? "⚠️" :
                            icon == MessageBoxImage.Information ? "ℹ️" : "❓";

            TextBlock iconBlock = new TextBlock
            {
                Text = iconText,
                FontSize = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = Colors.DimGray.ToBrush(),
                Margin = new Thickness(0, 0, 0, 20)
            };

            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 35,
                Background = Colors.DodgerBlue.ToBrush(),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            okButton.Click += (s, ev) => messageBox.DialogResult = true;

            contentPanel.Children.Add(iconBlock);
            contentPanel.Children.Add(messageBlock);
            contentPanel.Children.Add(okButton);
            container.Child = contentPanel;
            shadowBorder.Child = container;
            grid.Children.Add(shadowBorder);
            messageBox.Content = grid;

            messageBox.ShowDialog();
        }

        private bool ShowCustomMessageBoxConfirm(string message, string title)
        {
            Window messageBox = new Window
            {
                Title = title,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Width = 300,
                Height = 500,
                ResizeMode = ResizeMode.NoResize
            };

            Grid grid = new Grid();
            Border shadowBorder = new Border
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent,
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 0,
                    BlurRadius = 20,
                    Color = Colors.Black,
                    Opacity = 0.2
                }
            };

            Border container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.LightGray
            };

            StackPanel contentPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            TextBlock iconBlock = new TextBlock
            {
                Text = "❓",
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = Colors.DimGray.ToBrush(),
                Margin = new Thickness(0, 0, 0, 20)
            };

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button yesButton = new Button
            {
                Content = "Ya",
                Width = 70,
                Height = 30,
                Background = Colors.DodgerBlue.ToBrush(),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Tag = true
            };
            yesButton.Click += (s, ev) => messageBox.DialogResult = true;

            Button noButton = new Button
            {
                Content = "Tidak",
                Width = 70,
                Height = 30,
                Background = Colors.LightGray.ToBrush(),
                Foreground = Colors.DimGray.ToBrush(),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = false
            };
            noButton.Click += (s, ev) => messageBox.DialogResult = false;

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            contentPanel.Children.Add(iconBlock);
            contentPanel.Children.Add(messageBlock);
            contentPanel.Children.Add(buttonPanel);
            container.Child = contentPanel;
            shadowBorder.Child = container;
            grid.Children.Add(shadowBorder);
            messageBox.Content = grid;

            bool? result = messageBox.ShowDialog();
            return result == true;
        }
        private bool ShowCustomMessageBoxConfirm1(string message, string title)
        {
            Window messageBox = new Window
            {
                Title = title,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Width = 250,
                Height = 250,
                ResizeMode = ResizeMode.NoResize
            };

            Grid grid = new Grid();
            Border shadowBorder = new Border
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent,
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 0,
                    BlurRadius = 20,
                    Color = Colors.Black,
                    Opacity = 0.2
                }
            };

            Border container = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.LightGray
            };

            StackPanel contentPanel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            TextBlock iconBlock = new TextBlock
            {
                Text = "❓",
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBlock messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = Colors.DimGray.ToBrush(),
                Margin = new Thickness(0, 0, 0, 20)
            };

            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button yesButton = new Button
            {
                Content = "Ya",
                Width = 70,
                Height = 30,
                Background = Colors.DodgerBlue.ToBrush(),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Tag = true
            };
            yesButton.Click += (s, ev) => messageBox.DialogResult = true;

            Button noButton = new Button
            {
                Content = "Tidak",
                Width = 70,
                Height = 30,
                Background = Colors.LightGray.ToBrush(),
                Foreground = Colors.DimGray.ToBrush(),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = false
            };
            noButton.Click += (s, ev) => messageBox.DialogResult = false;

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);

            contentPanel.Children.Add(iconBlock);
            contentPanel.Children.Add(messageBlock);
            contentPanel.Children.Add(buttonPanel);
            container.Child = contentPanel;
            shadowBorder.Child = container;
            grid.Children.Add(shadowBorder);
            messageBox.Content = grid;

            bool? result = messageBox.ShowDialog();
            return result == true;
        }
        #endregion

        #region Form Reset Methods
        private void ResetFormInputs()
        {
            try
            {
                // 1. Reset No Register (kosongkan)
                txtNoRegister.Text = string.Empty;
                txtNoRegister.Focus(); // Set focus ke No Register setelah reset

                // 2. Reset Mesin (pilih item pertama)
                if (cbMesin.Items.Count > 0)
                {
                    cbMesin.SelectedIndex = 0;
                }

                // 3. Reset Supervisor (kosongkan)
                txtSupervisor.Text = string.Empty;

                // 4. Reset Part No (pilih item pertama atau reload dari database)
                if (cbPartNo.Items.Count > 0)
                {
                    cbPartNo.SelectedIndex = 0;
                }

                // 5. Tampilkan pesan sukses (opsional)
                ShowCustomMessageBox(
                    "✅ Form telah direset!\n\n" +
                    "Silakan isi data login baru.\n" +
                    "No Register telah difokuskan.",
                    "Reset Berhasil",
                    MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                ShowCustomMessageBox(
                    $"Error saat reset form: {ex.Message}",
                    "Reset Error",
                    MessageBoxImage.Error);
            }
        }
        #endregion

        #region Window Lifecycle
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
        #endregion

        #region Supporting Classes
        // Class untuk menyimpan informasi user
        private class UserInfo
        {
            public string? Nama { get; set; }
            public string? Divisi { get; set; }
            public string? Dept { get; set; }
        }
        #endregion
    }

    // Extension method untuk warna
    public static class ColorExtensions
    {
        public static System.Windows.Media.Brush ToBrush(this System.Windows.Media.Color color)
        {
            return new System.Windows.Media.SolidColorBrush(color);
        }
    }
}