using System;
using System.Windows;

namespace OpticalDataDiode
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TabMode_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewSender == null || ViewReceiver == null) return;

            if (TabSender.IsChecked == true)
            {
                ViewSender.Visibility = Visibility.Visible;
                ViewReceiver.Visibility = Visibility.Collapsed;
            }
            else
            {
                ViewSender.Visibility = Visibility.Collapsed;
                ViewReceiver.Visibility = Visibility.Visible;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ViewReceiver.Dispose();
            Application.Current.Shutdown();
        }
    }
}
