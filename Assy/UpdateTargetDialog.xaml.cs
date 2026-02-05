using System;
using System.Windows;
using System.Windows.Input;

namespace Assy
{
    public partial class UpdateTargetDialog : Window
    {
        #region Properties
        public int TargetValue { get; set; }
        #endregion

        #region Constructor & Initialization
        public UpdateTargetDialog()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                // Update tampilan setelah semua properti di-set
                txtCurrentTarget.Text = TargetValue.ToString("N0");
                txtNewTarget.Text = TargetValue.ToString();

                // Focus ke input
                txtNewTarget.Focus();
                txtNewTarget.SelectAll();
            };
        }
        #endregion

        #region Event Handlers
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtNewTarget.Text, out int newTarget) && newTarget > 0)
            {
                TargetValue = newTarget;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Masukkan angka yang valid (lebih dari 0)",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtNewTarget.Focus();
                txtNewTarget.SelectAll();
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        #endregion
    }
}