using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Assy
{
    public partial class CustomMessageBox : Window
    {
        #region Enums
        public enum MessageBoxType
        {
            Info,
            Success,
            Warning,
            Error,
            Question
        }

        public enum MessageBoxButtons
        {
            OK,
            OKCancel,
            YesNo,
            YesNoCancel
        }
        #endregion

        #region Properties
        public MessageBoxResult Result { get; private set; }
        #endregion

        #region Constructor & Initialization
        private CustomMessageBox(string title, string message, MessageBoxType type, MessageBoxButtons buttons)
        {
            InitializeComponent();
            Owner = Application.Current.MainWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Title = title;
            txtMessage.Text = message;

            SetMessageType(type);
            SetButtons(buttons);

            // Animate window
            Loaded += OnLoaded;
        }
        #endregion

        #region Animation Methods
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Fade in animation untuk window
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            BeginAnimation(OpacityProperty, fadeIn);

            // Scale animation untuk content border (bukan window)
            var scaleTransform = new ScaleTransform(0.9, 0.9);
            contentBorder.RenderTransform = scaleTransform;
            contentBorder.RenderTransformOrigin = new Point(0.5, 0.5);

            var scaleAnimation = new DoubleAnimation(0.9, 1, TimeSpan.FromSeconds(0.3));
            scaleAnimation.EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 3 };
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }
        private void CloseWithAnimation()
        {
            // Scale out animation untuk content border
            var scaleTransform = contentBorder.RenderTransform as ScaleTransform;
            if (scaleTransform != null)
            {
                var scaleOut = new DoubleAnimation(1, 0.9, TimeSpan.FromSeconds(0.2));
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            }

            // Fade out animation untuk window
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
            fadeOut.Completed += (s, ev) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }
        #endregion

        #region UI Configuration Methods
        private void SetMessageType(MessageBoxType type)
        {
            string icon = "";
            Color color = Colors.Gray;

            switch (type)
            {
                case MessageBoxType.Success:
                    icon = "✅";
                    color = Color.FromRgb(46, 204, 113); // Green
                    break;
                case MessageBoxType.Info:
                    icon = "ℹ️";
                    color = Color.FromRgb(52, 152, 219); // Blue
                    break;
                case MessageBoxType.Warning:
                    icon = "⚠️";
                    color = Color.FromRgb(241, 196, 15); // Yellow
                    break;
                case MessageBoxType.Error:
                    icon = "❌";
                    color = Color.FromRgb(231, 76, 60); // Red
                    break;
                case MessageBoxType.Question:
                    icon = "❓";
                    color = Color.FromRgb(155, 89, 182); // Purple
                    break;
            }

            txtIcon.Text = icon;
            iconBorder.Background = new SolidColorBrush(color);
        }

        private void SetButtons(MessageBoxButtons buttons)
        {
            // Hide all buttons first
            btnOK.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Collapsed;
            btnYes.Visibility = Visibility.Collapsed;
            btnNo.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButtons.OK:
                    btnOK.Visibility = Visibility.Visible;
                    btnOK.Focus();
                    break;
                case MessageBoxButtons.OKCancel:
                    btnOK.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnCancel.Focus();
                    break;
                case MessageBoxButtons.YesNo:
                    btnYes.Visibility = Visibility.Visible;
                    btnNo.Visibility = Visibility.Visible;
                    btnYes.Focus();
                    break;
                case MessageBoxButtons.YesNoCancel:
                    btnYes.Visibility = Visibility.Visible;
                    btnNo.Visibility = Visibility.Visible;
                    btnCancel.Visibility = Visibility.Visible;
                    btnYes.Focus();
                    break;
            }
        }
        #endregion

        #region Event Handlers
        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            CloseWithAnimation();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            CloseWithAnimation();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            CloseWithAnimation();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            CloseWithAnimation();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
        #endregion

        #region Static Factory Methods
        // Static methods for easy use (matching standard MessageBox)
        public static MessageBoxResult Show(string message, string title = "",
            MessageBoxType type = MessageBoxType.Info,
            MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            var dialog = new CustomMessageBox(title, message, type, buttons);
            dialog.ShowDialog();
            return dialog.Result;
        }

        public static MessageBoxResult Show(string message)
        {
            return Show(message, "", MessageBoxType.Info, MessageBoxButtons.OK);
        }

        public static MessageBoxResult Show(string message, string title)
        {
            return Show(message, title, MessageBoxType.Info, MessageBoxButtons.OK);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxType type)
        {
            return Show(message, title, type, MessageBoxButtons.OK);
        }
        #endregion
    }
}